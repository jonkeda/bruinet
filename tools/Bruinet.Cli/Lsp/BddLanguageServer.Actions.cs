using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bruinet.Bdd.Workspace;
using StreamJsonRpc;
using System.Globalization;

namespace Bruinet.Bdd.LanguageServer;

// Quick fixes and refactorings (phase 4): the value fixes bruinet data validate --fix applies, the
// old file forms, Create step definition for a step that binds to nothing, Extract and Inline a
// callable step, and Rename a callable step with every call of it.
public sealed partial class BddLanguageServer
{
    private const string NotBound = "UATB001";
    private const string ExtractedPhrase = "the extracted steps";

    private bool _renamesFiles;

    // Whether the client applies a file rename in a workspace edit.
    private static bool ClientRenamesFiles(JsonElement? capabilities)
        => capabilities is { ValueKind: JsonValueKind.Object } client &&
           client.TryGetProperty("workspace", out var workspace) && workspace.ValueKind == JsonValueKind.Object &&
           workspace.TryGetProperty("workspaceEdit", out var edit) && edit.ValueKind == JsonValueKind.Object &&
           edit.TryGetProperty("resourceOperations", out var operations) && operations.ValueKind == JsonValueKind.Array &&
           operations.EnumerateArray().Any(operation => operation.GetString() == "rename");

    /// <summary>LSP <c>textDocument/codeAction</c>.</summary>
    [JsonRpcMethod("textDocument/codeAction")]
    public IReadOnlyList<CodeAction> CodeActions(CodeActionParams request)
    {
        var file = Context(Key(request.TextDocument.Uri));
        var uri = new Uri(file.Path).AbsoluteUri;
        var first = request.Range.Start.Line + 1;
        var last = Math.Max(first, request.Range.End.Line + (request.Range.End.Character == 0 && request.Range.End.Line > request.Range.Start.Line ? 0 : 1));
        List<CodeAction> actions = [];
        if (IsConfig(file.Path))
        {
            RegenerateSchema(file, actions);
            return actions;
        }

        var analysis = Analyze(file);

        // A value with one right answer, as bruinet data validate --fix rewrites it.
        foreach (var diagnostic in analysis.Diagnostics.Where(diagnostic => diagnostic.Fix is not null && analysis.IsHere(diagnostic) && diagnostic.Location.LineNumber >= first && diagnostic.Location.LineNumber <= last))
        {
            var fix = diagnostic.Fix!;
            var line = file.Line(diagnostic.Location.LineNumber);
            var column = line.IndexOf(fix.Original, StringComparison.Ordinal);
            if (column >= 0)
            {
                var range = new Range(new Position(diagnostic.Location.LineNumber - 1, column), new Position(diagnostic.Location.LineNumber - 1, column + fix.Original.Length));
                actions.Add(new CodeAction($"Write '{fix.Replacement}'", "quickfix", Edit(uri, new TextEdit(range, fix.Replacement)))
                {
                    Diagnostics = [ToLsp(file, analysis, diagnostic)],
                    IsPreferred = true,
                });
            }
        }

        OldForms(file, uri, actions);

        // Steps written bare, before AD-023: wrap them, as bruinet data validate --fix does.
        if (analysis.Diagnostics.FirstOrDefault(diagnostic => diagnostic.Message.EndsWith(BddFileFormat.FenceHint, StringComparison.Ordinal) && analysis.IsHere(diagnostic) &&
                diagnostic.Location.LineNumber >= first && diagnostic.Location.LineNumber <= last) is { } bare &&
            BddFileFormat.FenceSteps(file.Text) is ({ } fenced, > 0))
        {
            actions.Add(new CodeAction("Put the steps in ```gherkin blocks", "quickfix", Edit(uri, new TextEdit(Whole(file), fenced)))
            {
                Diagnostics = [ToLsp(file, analysis, bare)],
                IsPreferred = true,
            });
        }

        foreach (var line in Enumerable.Range(first, last - first + 1))
        {
            if (analysis.Diagnostics.FirstOrDefault(diagnostic => diagnostic.Code == NotBound && analysis.IsHere(diagnostic) && diagnostic.Location.LineNumber == line) is { } unbound &&
                CreateStepDefinition(file, line) is { } create)
            {
                actions.Add(create with { Diagnostics = [ToLsp(file, analysis, unbound)] });
            }
        }

        if (Extract(file, first, last) is { } extract)
        {
            actions.Add(extract);
        }

        if (Inline(file, first) is { } inline)
        {
            actions.Add(inline);
        }

        return actions;
    }

    /// <summary>LSP <c>textDocument/prepareRename</c>: a <c>## Step:</c> heading's phrase, or a call of it.</summary>
    [JsonRpcMethod("textDocument/prepareRename")]
    public PrepareRenameResult? PrepareRename(PositionParams request)
    {
        var file = Context(Key(request.TextDocument.Uri));
        return RenameTarget(file, request.Position.Line + 1) is { } target
            ? new PrepareRenameResult(target.Range, target.Callable.Phrase)
            : null;
    }

    /// <summary>
    /// LSP <c>textDocument/rename</c>: the callable step's heading and every call of it, each call
    /// keeping its values. The new phrase must have the same <c>{x}</c> placeholders.
    /// </summary>
    [JsonRpcMethod("textDocument/rename")]
    public WorkspaceEdit? Rename(RenameParams request)
    {
        var file = Context(Key(request.TextDocument.Uri));
        if (RenameTarget(file, request.Position.Line + 1) is not { } target)
        {
            return null;
        }

        var callable = target.Callable;
        var newPhrase = request.NewName.Trim();
        if (!BddCommandPattern.ParameterNames(newPhrase).Order(StringComparer.Ordinal).SequenceEqual(callable.HeadingParameters.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new LocalRpcException($"'{newPhrase}' must keep the placeholders {string.Join(", ", callable.HeadingParameters.Select(name => $"{{{name}}}"))}, so each call keeps its values.") { ErrorCode = -32803 };
        }

        Dictionary<string, List<TextEdit>> edits = new(StringComparer.Ordinal);
        var heading = Context(Path.GetFullPath(callable.Source.FilePath!));
        var headingText = heading.Line(callable.Source.LineNumber);
        var phraseAt = headingText.IndexOf(callable.Phrase, StringComparison.Ordinal);
        if (phraseAt >= 0)
        {
            Add(heading.Path, new TextEdit(new Range(new Position(callable.Source.LineNumber - 1, phraseAt), new Position(callable.Source.LineNumber - 1, phraseAt + callable.Phrase.Length)), newPhrase));
        }

        var pattern = PhraseRegex(callable.Phrase);
        foreach (var call in CallsOf(file.Path, callable.Source))
        {
            var caller = Context(call.File);
            if (caller.Structure.StepAt(call.Line) is not { } step || pattern.Match(step.Text) is not { Success: true } match)
            {
                continue;
            }

            var renamed = Regex.Replace(newPhrase, @"\{([A-Za-z][A-Za-z0-9_-]*)\}", placeholder => match.Groups[placeholder.Groups[1].Value.Replace('-', '_')].Value);
            Add(call.File, new TextEdit(new Range(new Position(call.Line - 1, step.TextColumn), new Position(call.Line - 1, step.TextColumn + step.Text.Length)), renamed));
        }

        return new WorkspaceEdit { Changes = edits.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<TextEdit>)pair.Value) };

        void Add(string path, TextEdit edit)
        {
            var key = new Uri(path).AbsoluteUri;
            if (!edits.TryGetValue(key, out var list))
            {
                edits[key] = list = [];
            }

            list.Add(edit);
        }
    }

    // The old file name (UAT029) and the old headings: rename the file and rewrite its headings.
    private void OldForms(BddServerFile file, string uri, List<CodeAction> actions)
    {
        var (rewritten, count) = BddFileFormat.RewriteHeadings(file.Text);
        var newName = BddFileFormat.NewFileName(file.Path);
        if (count > 0 && newName is null)
        {
            actions.Add(new CodeAction($"Rewrite {count} old heading(s)", "quickfix", Edit(uri, new TextEdit(Whole(file), rewritten))) { IsPreferred = true });
        }

        if (newName is not null && _renamesFiles)
        {
            List<object> changes = [];
            if (count > 0)
            {
                changes.Add(new TextDocumentEdit(new VersionedTextDocumentIdentifierOrNull(uri, file.Version == 0 ? null : file.Version), [new TextEdit(Whole(file), rewritten)]));
            }

            changes.Add(new RenameFile(uri, new Uri(newName).AbsoluteUri));
            actions.Add(new CodeAction($"Rename to {Path.GetFileName(newName)}{(count > 0 ? " and rewrite its headings" : string.Empty)}", "quickfix", new WorkspaceEdit { DocumentChanges = changes }) { IsPreferred = true });
        }
    }

    // A step that binds to nothing: a method for it in the suite's step-definition class.
    private CodeAction? CreateStepDefinition(BddServerFile file, int line)
    {
        if (file.Suite is not { } suite || file.Structure.StepAt(line) is not { } step)
        {
            return null;
        }

        var target = suite.StepDefinitionTypes()
            .Select(type => (Type: type, At: BddSourceLocator.Of(type)))
            .Where(candidate => candidate.At?.FilePath is { } path && File.Exists(path))
            .OrderBy(candidate => candidate.At!.FilePath!.StartsWith(suite.ProjectDirectory, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .FirstOrDefault();
        if (target.At?.FilePath is not { } source || ClosingBrace(source, target.At.LineNumber) is not { } closing)
        {
            return null;
        }

        var (phrase, parameters) = StepDefinitionPhrase(step.Text);
        var name = MethodName(phrase);
        var indent = new string(' ', LeadingSpaces(File.ReadAllLines(source)[closing - 1]) + 4);
        var method = new StringBuilder()
            .Append('\n')
            .Append(CultureInfo.InvariantCulture, $"{indent}[Bdd{step.Effective}(\"{phrase.Replace("\"", "\\\"", StringComparison.Ordinal)}\")]\n")
            .Append(CultureInfo.InvariantCulture, $"{indent}public void {name}({string.Join(", ", parameters)})\n")
            .Append(CultureInfo.InvariantCulture, $"{indent}{{\n")
            .Append(CultureInfo.InvariantCulture, $"{indent}    throw new NotImplementedException();\n")
            .Append(CultureInfo.InvariantCulture, $"{indent}}}\n")
            .ToString();
        var at = new Position(closing - 1, 0);
        return new CodeAction($"Create step definition in {target.Type.Name}", "quickfix", Edit(new Uri(source).AbsoluteUri, new TextEdit(new Range(at, at), method)));
    }

    // The step's text as a phrase: each quoted value and each number a placeholder.
    internal static (string Phrase, IReadOnlyList<string> Parameters) StepDefinitionPhrase(string text)
    {
        List<string> parameters = [];
        var phrase = Regex.Replace(text, "\"[^\"]*\"|(?<![\\w-])\\d+(?![\\w-])", value =>
        {
            var number = value.Value[0] != '"';
            var name = (number ? "number" : "text") + (parameters.Count(p => p.StartsWith(number ? "int " : "string ", StringComparison.Ordinal)) is > 0 and var n ? (n + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty);
            parameters.Add($"{(number ? "int" : "string")} {name}");
            return $"{{{name}}}";
        });
        return (phrase, parameters);
    }

    private static string MethodName(string phrase)
    {
        var words = Regex.Replace(phrase, @"\{[^}]*\}", " ").Split([' ', '-', '\'', ',', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => new string(word.Where(char.IsLetterOrDigit).ToArray()))
            .Where(word => word.Length > 0)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]);
        var name = string.Concat(words);
        return name.Length == 0 || char.IsDigit(name[0]) ? "Step" + name : name;
    }

    // The line of the brace that closes the type declared at a line: braces counted, strings and comments skipped.
    private static int? ClosingBrace(string source, int declaration)
    {
        var lines = File.ReadAllLines(source);
        var depth = 0;
        var opened = false;
        for (var i = declaration - 1; i < lines.Length; i++)
        {
            var text = Regex.Replace(lines[i], "//.*$|\"(\\\\.|[^\"\\\\])*\"|'(\\\\.|[^'\\\\])'", string.Empty);
            foreach (var character in text)
            {
                if (character == '{')
                {
                    depth++;
                    opened = true;
                }
                else if (character == '}' && --depth == 0 && opened)
                {
                    return i + 1;
                }
            }
        }

        return null;
    }

    // Selected step lines of one scenario: into a ## Step: of the file, and a call in their place.
    private CodeAction? Extract(BddServerFile file, int first, int last)
    {
        var structure = file.Structure;
        var steps = structure.Steps.Where(step => step.Line >= first && step.Line <= last).ToList();
        if (steps.Count == 0 || Bruinet.Bdd.Preview.BddFileAnalysis.IsStepFile(file.Path) ||
            structure.Blocks.FirstOrDefault(block => block.Kind == BddBlockKind.Gherkin && first > block.Start && first < block.End) is not { } fence ||
            steps[^1].Line >= fence.End)
        {
            return null;
        }

        // A step's table goes with it; a step file's steps are bodies already, so it is not offered there.
        var end = steps[^1].Line;
        while (end + 1 < fence.End && file.Line(end + 1).TrimStart().StartsWith('|'))
        {
            end++;
        }

        var existing = VisibleCallables(file).Select(callable => callable.Phrase).ToHashSet(StringComparer.Ordinal);
        var phrase = ExtractedPhrase;
        for (var n = 2; existing.Contains(phrase); n++)
        {
            phrase = $"{ExtractedPhrase} {n}";
        }

        var indent = file.Line(steps[0].Line)[..steps[0].KeywordColumn];
        var body = string.Join('\n', Enumerable.Range(steps[0].Line, end - steps[0].Line + 1).Select(line => file.Line(line)[Math.Min(indent.Length, LeadingSpaces(file.Line(line)))..]));
        var uri = new Uri(file.Path).AbsoluteUri;
        var endOfFile = new Position(file.Lines.Length - 1, file.Lines[^1].Length);
        return new CodeAction($"Extract to '## Step: {phrase}'", "refactor.extract", new WorkspaceEdit
        {
            Changes = new Dictionary<string, IReadOnlyList<TextEdit>>
            {
                [uri] =
                [
                    new TextEdit(new Range(new Position(steps[0].Line - 1, 0), new Position(end - 1, file.Line(end).Length)), $"{indent}{steps[0].Keyword} {phrase}"),
                    new TextEdit(new Range(endOfFile, endOfFile), $"{(file.Lines[^1].Length == 0 ? string.Empty : "\n")}\n## Step: {phrase}\n\n```gherkin\n{body}\n```\n"),
                ],
            },
        });
    }

    // A call of a once-callable: its body in its place, with the call's values filled in.
    private CodeAction? Inline(BddServerFile file, int line)
    {
        var invocations = InvocationsAt(file, line);
        if (invocations.Count == 0 || invocations[0].Call is not { } call || Outermost(call).Callable.Kind != BddCallableKind.Once ||
            file.Structure.StepAt(line) is not { } written)
        {
            return null;
        }

        // Each body step once: a step of the body itself, or a call in the body as expanded.
        List<BddStep> body = [];
        foreach (var invocation in invocations)
        {
            var frame = invocation.Call!;
            BddStep step = invocation.Step;
            for (var inner = frame; inner.Parent is { } parent; inner = parent)
            {
                if (parent.Parent is null)
                {
                    step = inner.Call;
                }
            }

            if (body.Count == 0 || !ReferenceEquals(body[^1], step))
            {
                body.Add(step);
            }
        }

        var end = line;
        while (file.Line(end + 1).TrimStart().StartsWith('|'))
        {
            end++;
        }

        var indent = file.Line(line)[..written.KeywordColumn];
        StringBuilder text = new();
        foreach (var step in body)
        {
            text.Append(text.Length == 0 ? string.Empty : "\n").Append(CultureInfo.InvariantCulture, $"{indent}{step.Keyword} {step.Text}");
            if (step.Table is { } table)
            {
                text.Append('\n').Append(TableText(table, indent + "  "));
            }
        }

        var uri = new Uri(file.Path).AbsoluteUri;
        return new CodeAction($"Inline '{Outermost(call).Callable}'", "refactor.inline", Edit(uri, new TextEdit(new Range(new Position(line - 1, 0), new Position(end - 1, file.Line(end).Length)), text.ToString())));
    }

    private static string TableText(BddTable table, string indent)
    {
        IEnumerable<IEnumerable<string>> rows = [table.Columns, .. table.Rows.Select(row => table.Columns.Select(column => row.Cells.GetValueOrDefault(column) ?? string.Empty))];
        var cells = rows.Select(row => row.ToList()).ToList();
        var widths = table.Columns.Select((_, i) => cells.Max(row => row[i].Length)).ToList();
        var lines = cells.Select(row => $"{indent}| {string.Join(" | ", row.Select((cell, i) => cell.PadRight(widths[i])))} |").ToList();
        lines.Insert(1, $"{indent}| {string.Join(" | ", widths.Select(width => new string('-', Math.Max(3, width))))} |");
        return string.Join('\n', lines);
    }

    // What a rename at a line renames: the callable of a ## Step: heading, or of the call on the line.
    private (BddCallable Callable, Range Range)? RenameTarget(BddServerFile file, int line)
    {
        if (file.Structure.HeadingAt(line) is { Kind: "Step" or "Step for each row" } heading && heading.Line == line &&
            UatFileAnalysisCallables(file).FirstOrDefault(callable => callable.Source.LineNumber == line) is { } own)
        {
            var text = file.Line(line);
            var at = text.IndexOf(own.Phrase, StringComparison.Ordinal);
            return at < 0 ? null : (own with { Source = own.Source with { FilePath = file.Path } }, new Range(new Position(line - 1, at), new Position(line - 1, at + own.Phrase.Length)));
        }

        if (InvocationsAt(file, line).FirstOrDefault()?.Call is { } call && file.Structure.StepAt(line) is { } step)
        {
            return (Outermost(call).Callable, new Range(new Position(line - 1, step.TextColumn), new Position(line - 1, step.TextColumn + step.Text.Length)));
        }

        return null;
    }

    // Every call of a callable across the suites that hold a file.
    private IEnumerable<(string File, int Line)> CallsOf(string path, BddSourceLocation callable)
    {
        HashSet<(string, int)> found = [];
        foreach (var feature in SuiteFeatureFiles(path).Append(path).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var scenario in Analyze(Context(feature)).Scenarios)
            {
                foreach (var step in scenario.Invocations)
                {
                    for (var frame = step.Call; frame is not null; frame = frame.Parent)
                    {
                        if (Same(frame.Callable.Source, callable) && frame.Call.Source.FilePath is { } caller)
                        {
                            found.Add((Path.GetFullPath(caller), frame.Call.Source.LineNumber));
                        }
                    }
                }
            }
        }

        return found;
    }

    // A phrase as a regex over a step's text: each {x} a named group.
    private static Regex PhraseRegex(string phrase)
    {
        StringBuilder pattern = new("^");
        var last = 0;
        foreach (Match placeholder in Regex.Matches(phrase, @"\{([A-Za-z][A-Za-z0-9_-]*)\}"))
        {
            pattern.Append(Literal(phrase[last..placeholder.Index])).Append(CultureInfo.InvariantCulture, $"(?<{placeholder.Groups[1].Value.Replace('-', '_')}>.+?)");
            last = placeholder.Index + placeholder.Length;
        }

        return new Regex(pattern.Append(Literal(phrase[last..])).Append('$').ToString(), RegexOptions.CultureInvariant);
    }

    private static WorkspaceEdit Edit(string uri, TextEdit edit)
        => new() { Changes = new Dictionary<string, IReadOnlyList<TextEdit>> { [uri] = [edit] } };

    private static Range Whole(BddServerFile file)
        => new(new Position(0, 0), new Position(file.Lines.Length - 1, file.Lines[^1].Length));

    private static int LeadingSpaces(string text) => text.Length - text.TrimStart().Length;

    private Diagnostic ToLsp(BddServerFile file, Bruinet.Bdd.Preview.BddFileAnalysis analysis, BddDiagnostic diagnostic)
        => DiagnosticsOf(file).FirstOrDefault(lsp => lsp.Code == diagnostic.Code && lsp.Range.Start.Line == diagnostic.Location.LineNumber - 1)
           ?? new Diagnostic(LineRange(file, diagnostic.Location.LineNumber), 1, diagnostic.Code, DiagnosticSource, diagnostic.Message);
}
