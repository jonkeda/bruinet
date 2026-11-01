using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;

namespace Bruinet.Bdd;

public static partial class BddMarkdownParser
{
    private const string DocStringDelimiter = "\"\"\"";

    private const string TableMarker = "<table>";
    private const string DocStringMarker = "<doc string>";

    public static BddParseResult Parse(string markdown, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var parser = new Parser(markdown, filePath);
        return parser.Parse();
    }

    /// <summary>
    /// Parses a feature file: once while it stays as it is (its last write and length), since a suite reads each file once
    /// per scenario and its lanes and skips again (next-plan item 7). The result is immutable, so it is shared; a file that
    /// changed is parsed anew, which keeps an IDE's and a warm runner's reads current.
    /// </summary>
    public static BddParseResult ParseFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var full = Path.GetFullPath(filePath);
        var info = new FileInfo(full);
        var stamp = (info.LastWriteTimeUtc.Ticks, info.Length);
        if (Parsed.TryGetValue(full, out var kept) && kept.Stamp == stamp && kept.Path == filePath)
        {
            return kept.Result;
        }

        var result = Parse(File.ReadAllText(filePath), filePath);
        Parsed[full] = (stamp, filePath, result);
        return result;
    }

    // One parse per file, by its full path; the path as asked, since a result's locations carry it.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ((long Ticks, long Length) Stamp, string Path, BddParseResult Result)> Parsed =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Parses a <c>*.steps.md</c> file: one <c># Steps:</c> heading and only step sections.</summary>
    /// <param name="markdown">The file's text.</param>
    /// <param name="filePath">The file, for locations.</param>
    public static BddPhrasesParseResult ParsePhrases(string markdown, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return new Parser(markdown, filePath).ParsePhrases();
    }

    private sealed partial class Parser
    {
        private readonly List<BddDiagnostic> _diagnostics = [];
        private readonly string? _filePath;
        private readonly Line[] _lines;
        private readonly HashSet<int> _stepLikeProse = [];
        private int _index;

        public Parser(string markdown, string? filePath)
        {
            _filePath = filePath;
            _lines = ReadBlocks(markdown);
        }

        [SuppressMessage("Maintainability", "CA1502", Justification = "Known debt (AD-033): split when it next changes.")]
        public BddParseResult Parse()
        {
            string? title = null;
            BddSourceLocation? titleSource = null;
            Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase);
            List<BddStep> background = [];
            List<BddNamedDataTable> dataTables = [];
            List<BddScenario> scenarios = [];
            List<BddCallable> callables = [];
            List<string> pendingTags = [];

            while (!End)
            {
                SkipBlankLines();
                if (End)
                {
                    break;
                }

                var line = Current;
                var trimmed = line.Text.Trim();

                if (line.Kind == LineKind.Tags)
                {
                    if (line.Detached)
                    {
                        AddError("UAT020", "Tag lines must be immediately followed by a scenario heading.", line);
                    }
                    else if (TryParseTags(trimmed, out var tags))
                    {
                        pendingTags.AddRange(tags);
                    }

                    _index++;
                    continue;
                }

                if (pendingTags.Count > 0 && !(line.Kind == LineKind.Heading && IsScenarioHeading(trimmed)))
                {
                    AddError("UAT020", "Tag lines must be immediately followed by a scenario heading.", line);
                    pendingTags.Clear();
                }

                if (SkipOutsideSection(line))
                {
                    continue;
                }

                if (!TryParseHeading(ReadHeading(line), out var level, out var headingText))
                {
                    _index++;
                    continue;
                }

                if (level == 1 && headingText.StartsWith("Feature:", StringComparison.Ordinal))
                {
                    if (title is not null)
                    {
                        AddError("UAT001", "Document must contain exactly one '# Feature:' heading.", line);
                    }

                    title = headingText["Feature:".Length..].Trim();
                    titleSource = Location(line);
                    if (title.Length == 0)
                    {
                        AddError("UAT002", "The '# Feature:' heading must include a title.", line);
                    }

                    _index++;
                    continue;
                }

                if (level == 1)
                {
                    AddError("UAT022", $"A feature file has one level-1 heading, '# Feature:'; found '{trimmed}'.", line);
                    _index++;
                    continue;
                }

                if (level != 2 || !IsFeatureSection(headingText))
                {
                    ReadProseHeading(level, headingText, line);
                    continue;
                }

                if (title is null)
                {
                    AddError("UAT003", "Document must start with a '# Feature:' heading before other sections.", line);
                }

                if (level == 2 && headingText.Equals("Metadata", StringComparison.Ordinal))
                {
                    _index++;
                    metadata = ParseMetadataTable();
                    continue;
                }

                if (level == 2 && headingText.Equals("Background", StringComparison.Ordinal))
                {
                    _index++;
                    background = ParseStepBlock(stopAtExamples: false);
                    if (background.Count == 0)
                    {
                        AddError("UAT004", NoSteps("Background section must contain at least one step.", line), line);
                    }

                    continue;
                }

                if (level == 2 && headingText.StartsWith("Data:", StringComparison.Ordinal))
                {
                    var dataName = headingText["Data:".Length..].Trim();
                    if (dataName.Length == 0)
                    {
                        AddError("UAT005", "Data section must include a name.", line);
                    }

                    _index++;
                    var table = ParseRequiredTable("Data section must contain a Markdown table.");
                    if (table is not null)
                    {
                        dataTables.Add(new BddNamedDataTable(dataName, table, Location(line)));
                    }

                    continue;
                }

                if (level == 2 && TryParsePhraseHeading(headingText, out var kind, out var phrase))
                {
                    _index++;
                    if (ParsePhraseSection(kind, phrase, line) is { } callable)
                    {
                        callables.Add(callable);
                    }

                    continue;
                }

                if (level == 2 && headingText.StartsWith("Scenario Outline:", StringComparison.Ordinal))
                {
                    var scenarioName = headingText["Scenario Outline:".Length..].Trim();
                    _index++;
                    var outlineSteps = ParseStepBlock(stopAtExamples: true);
                    var examples = ParseExamplesBlocks();
                    scenarios.AddRange(ExpandOutline(scenarioName, pendingTags, outlineSteps, examples, line));
                    pendingTags.Clear();
                    continue;
                }

                if (level == 2 && headingText.StartsWith("Scenario:", StringComparison.Ordinal))
                {
                    var scenarioName = headingText["Scenario:".Length..].Trim();
                    if (scenarioName.Length == 0)
                    {
                        AddError("UAT006", "Scenario heading must include a name.", line);
                    }

                    _index++;
                    var steps = ParseStepBlock(stopAtExamples: false);
                    if (steps.Count == 0)
                    {
                        AddError("UAT007", NoSteps("Scenario section must contain at least one step.", line), line);
                    }

                    scenarios.Add(new BddScenario(scenarioName, [.. pendingTags], steps, Location(line)));
                    pendingTags.Clear();
                    continue;
                }
            }

            if (pendingTags.Count > 0)
            {
                var location = _lines.Length == 0 ? new BddSourceLocation(_filePath, 1) : Location(_lines[^1]);
                _diagnostics.Add(new BddDiagnostic(
                    BddDiagnosticSeverity.Error,
                    "UAT020",
                    "Tag lines must be immediately followed by a scenario heading.",
                    location));
            }

            if (title is null)
            {
                _diagnostics.Add(new BddDiagnostic(
                    BddDiagnosticSeverity.Error,
                    "UAT000",
                    "Document must contain exactly one '# Feature:' heading.",
                    new BddSourceLocation(_filePath, 1)));
            }

            if (scenarios.Count == 0)
            {
                _diagnostics.Add(new BddDiagnostic(
                    BddDiagnosticSeverity.Error,
                    "UAT008",
                    "Document must contain at least one scenario.",
                    titleSource ?? new BddSourceLocation(_filePath, 1)));
            }

            var document = title is null
                ? null
                : new BddDocument(
                    title,
                    metadata,
                    background,
                    dataTables,
                    scenarios,
                    titleSource ?? new BddSourceLocation(_filePath, 1))
                {
                    Callables = callables,
                };

            return new BddParseResult(document, _diagnostics);
        }

        public BddPhrasesParseResult ParsePhrases()
        {
            string? title = null;
            BddSourceLocation? titleSource = null;
            List<BddCallable> callables = [];

            while (!End)
            {
                SkipBlankLines();
                if (End)
                {
                    break;
                }

                var line = Current;
                var trimmed = line.Text.Trim();
                if (line.Kind is LineKind.Tags or LineKind.Fence)
                {
                    AddError("UAT028", $"A steps file holds only '## Step:' sections; found '{trimmed}'.", line);
                    _index++;
                    SkipBlockLines();
                    continue;
                }

                if (!TryParseHeading(ReadHeading(line), out var level, out var headingText))
                {
                    _index++;
                    continue;
                }

                if (level == 1 && headingText.StartsWith("Steps:", StringComparison.Ordinal))
                {
                    if (title is not null)
                    {
                        AddError("UAT028", "A steps file has exactly one '# Steps:' heading.", line);
                    }

                    title = headingText["Steps:".Length..].Trim();
                    titleSource = Location(line);
                    _index++;
                    continue;
                }

                if (level == 1)
                {
                    AddError("UAT028", $"A steps file has one level-1 heading, '# Steps:'; found '{trimmed}'.", line);
                    _index++;
                    continue;
                }

                if (level != 2 || !IsFeatureSection(headingText))
                {
                    ReadProseHeading(level, headingText, line);
                    continue;
                }

                if (title is null)
                {
                    AddError("UAT028", "A steps file starts with a '# Steps:' heading.", line);
                    title = string.Empty;
                }

                if (level == 2 && TryParsePhraseHeading(headingText, out var kind, out var phrase))
                {
                    _index++;
                    if (ParsePhraseSection(kind, phrase, line) is { } callable)
                    {
                        callables.Add(callable);
                    }

                    continue;
                }

                AddError("UAT028", $"A steps file holds only step sections; '{trimmed}' belongs in a .feature.md file.", line);
                _index++;
                SkipToNextSection();
            }

            if (title is null)
            {
                AddError("UAT028", "A steps file starts with a '# Steps:' heading.", new BddSourceLocation(_filePath, 1));
            }

            var document = new BddPhrasesDocument(title ?? string.Empty, callables, titleSource ?? new BddSourceLocation(_filePath, 1));
            return new BddPhrasesParseResult(document, _diagnostics);
        }

        // A heading in the form before feature files is one error with its fix, then read as the
        // new form, so the rest of the file is still checked.
        private string ReadHeading(Line line)
        {
            var trimmed = line.Text.Trim();
            if (BddFileFormat.NewHeading(trimmed) is not { } heading)
            {
                return trimmed;
            }

            AddError(BddFileFormat.OldFormCode, $"'{trimmed}' is the old form of '{heading}'. Run 'bruinet data validate --fix' to rewrite it.", line);
            return heading;
        }

        private void SkipToNextSection()
        {
            while (!End && !IsSectionBoundary(Current))
            {
                _index++;
            }
        }

        // A level-2 heading, or tags that stand above one, ends a section.
        private static bool IsSectionBoundary(Line line)
            => (line.Kind == LineKind.Heading && TryParseHeading(line.Text.Trim(), out var level, out _) && level <= 2) ||
               (line.Kind == LineKind.Tags && !line.Detached);

        // Between the sections: a table is prose, and a gherkin block belongs in a section.
        private bool SkipOutsideSection(Line line)
        {
            switch (line.Kind)
            {
                case LineKind.Table:
                case LineKind.Gherkin:
                    _index++;
                    return true;
                case LineKind.Fence:
                    AddError("UAT021", "A ```gherkin block belongs in a Background, Scenario or Step section.", line);
                    _index++;
                    SkipBlockLines();
                    return true;
                default:
                    return false;
            }
        }

        private void SkipBlockLines()
        {
            while (!End && Current.Kind == LineKind.Gherkin)
            {
                _index++;
            }
        }

        // A heading the engine does not read is prose; a level-2 one opens a prose section. One that
        // looks like a section is a warning, so a typo is not silently prose (AD-023).
        private void ReadProseHeading(int level, string headingText, Line line)
        {
            WarnIfLooksLikeSection(headingText, line);
            _index++;
            if (level == 2)
            {
                SkipToNextSection();
            }
        }

        private void WarnIfLooksLikeSection(string headingText, Line line)
        {
            if (LooksLikeSection().IsMatch(headingText))
            {
                _diagnostics.Add(new BddDiagnostic(
                    BddDiagnosticSeverity.Warning,
                    "UAT031",
                    $"'{line.Text.Trim()}' looks like a section heading but is not one, so it is read as prose. Check its level, spelling and colon.",
                    Location(line)));
            }
        }

        private static bool IsFeatureSection(string headingText)
            => headingText is "Metadata" or "Background" ||
               headingText.StartsWith("Data:", StringComparison.Ordinal) ||
               headingText.StartsWith("Scenario:", StringComparison.Ordinal) ||
               headingText.StartsWith("Scenario Outline:", StringComparison.Ordinal) ||
               TryParsePhraseHeading(headingText, out _, out _);

        // A section with no steps that holds text reading like steps is most likely in the form
        // before gherkin blocks: say where the steps go.
        private string NoSteps(string message, Line heading)
        {
            var end = End ? int.MaxValue : Current.Number;
            return _stepLikeProse.Any(number => number > heading.Number && number < end)
                ? $"{message} {BddFileFormat.FenceHint}"
                : message;
        }

        private static bool TryParsePhraseHeading(string headingText, out BddCallableKind kind, out string phrase)
        {
            foreach (var (prefix, candidate) in new[] { ("Step for each row:", BddCallableKind.EachRow), ("Step:", BddCallableKind.Once) })
            {
                if (headingText.StartsWith(prefix, StringComparison.Ordinal))
                {
                    kind = candidate;
                    phrase = headingText[prefix.Length..].Trim();
                    return true;
                }
            }

            kind = default;
            phrase = string.Empty;
            return false;
        }

        // An optional Parameters table, then the body. What needs the catalog is checked later, as
        // the callable's problems, so a wrong callable does not stop the file (AD-016 A10).
        private BddCallable? ParsePhraseSection(BddCallableKind kind, string phrase, Line heading)
        {
            if (phrase.Length == 0)
            {
                AddError("UAT025", "A '## Step:' heading names the step: '## Step: a project {name}'.", heading);
            }

            SkipBlankLines();
            IReadOnlyList<BddCallableParameterDeclaration> declared = [];
            if (!End && Current.Kind == LineKind.Table && SplitTableRow(Current.Text) is { Count: > 0 } header &&
                header[0].Equals("Parameter", StringComparison.OrdinalIgnoreCase))
            {
                var table = TryParseCurrentTable(LineKind.Table);
                if (table is null)
                {
                    AddError("UAT027", "A Parameters table needs a separator row under its header.", Current);
                    _index++;
                }
                else
                {
                    declared = ReadParameters(table);
                }
            }

            var steps = ParseStepBlock(stopAtExamples: false, inPhrase: true);
            if (steps.Count == 0)
            {
                AddError("UAT026", NoSteps($"The phrase '{phrase}' has no steps.", heading), heading);
            }

            return phrase.Length == 0 ? null : new BddCallable(kind, phrase, declared, steps, Location(heading));
        }

        private List<BddCallableParameterDeclaration> ReadParameters(BddTable table)
        {
            string? Column(string name) => table.Columns.FirstOrDefault(column => column.Equals(name, StringComparison.OrdinalIgnoreCase));

            List<BddCallableParameterDeclaration> declared = [];
            if (Column("Parameter") is not { } parameter || Column("Type") is not { } type)
            {
                AddError("UAT027", "A Parameters table has the columns 'Parameter' and 'Type', and optionally 'Example'.", table.Source);
                return declared;
            }

            var example = Column("Example");
            foreach (var row in table.Rows)
            {
                var name = row.Cells[parameter];
                if (name.Length == 0 || row.Cells[type].Length == 0)
                {
                    AddError("UAT027", "A Parameters row names a parameter and its type.", row.Source);
                    continue;
                }

                if (declared.Any(existing => existing.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    AddError("UAT027", $"The Parameters table lists '{name}' twice.", row.Source);
                    continue;
                }

                declared.Add(new BddCallableParameterDeclaration(name, row.Cells[type], example is null ? null : row.Cells[example], row.Source));
            }

            return declared;
        }

        private Dictionary<string, string> ParseMetadataTable()
        {
            var table = ParseRequiredTable("Metadata section must contain a Markdown table.");
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (table is null)
            {
                return metadata;
            }

            if (table.Columns.Count != 2 ||
                !table.Columns[0].Equals("Field", StringComparison.OrdinalIgnoreCase) ||
                !table.Columns[1].Equals("Value", StringComparison.OrdinalIgnoreCase))
            {
                AddError("UAT009", "Metadata table must have columns 'Field' and 'Value'.", table.Source);
                return metadata;
            }

            foreach (var row in table.Rows)
            {
                var field = row.Cells["Field"];
                if (field.Length == 0)
                {
                    AddError("UAT010", "Metadata field name cannot be empty.", row.Source);
                    continue;
                }

                metadata[field] = row.Cells["Value"];
            }

            return metadata;
        }

        private List<BddStep> ParseStepBlock(bool stopAtExamples, bool inPhrase = false)
        {
            List<BddStep> steps = [];
            BddEffectiveStepKeyword? currentEffectiveKeyword = null;

            while (!End)
            {
                SkipBlankLines();
                if (End)
                {
                    break;
                }

                var line = Current;
                var trimmed = line.Text.Trim();

                if (line.Kind == LineKind.Heading && TryParseHeading(trimmed, out var level, out var headingText))
                {
                    if (level <= 2 || (stopAtExamples && level == 3 && TryExamplesName(headingText, out _)))
                    {
                        break;
                    }

                    WarnIfLooksLikeSection(headingText, line);
                    _index++;
                    continue;
                }

                if (line.Kind == LineKind.Tags)
                {
                    break;
                }

                // Prose tables and the fence lines between blocks; a '#' in a block is a comment.
                if (line.Kind != LineKind.Gherkin || trimmed.StartsWith('#'))
                {
                    _index++;
                    continue;
                }

                if (trimmed is TableMarker or DocStringMarker)
                {
                    AddError("UAT024", $"'{trimmed}' goes directly under a step in a '## Step:' section.", line);
                    _index++;
                    continue;
                }

                if (!TryParseStep(trimmed, out var keyword, out var text))
                {
                    AddError("UAT011", $"Expected a step but found '{trimmed}'.", line);
                    _index++;
                    continue;
                }

                var effectiveKeyword = ResolveEffectiveKeyword(keyword, currentEffectiveKeyword, line);
                currentEffectiveKeyword = effectiveKeyword;
                _index++;

                var table = TryParseCurrentTable(LineKind.Gherkin);
                var docString = TryParseDocString();
                var passes = BddPassedBlock.None;
                if (table is null && docString is null && !End && Current.Kind == LineKind.Gherkin && Current.Text.Trim() is TableMarker or DocStringMarker)
                {
                    if (inPhrase)
                    {
                        passes = Current.Text.Trim() == TableMarker ? BddPassedBlock.Table : BddPassedBlock.DocString;
                    }
                    else
                    {
                        AddError("UAT024", $"'{Current.Text.Trim()}' passes a call's block, so it is only allowed in a '## Step:' section.", Current);
                    }

                    _index++;
                }

                steps.Add(new BddStep(keyword, effectiveKeyword, text, table, Location(line), docString) { Passes = passes });
            }

            return steps;
        }

        // An outline's '### Examples' sections, each with the tag line above it and its table.
        private List<ExamplesBlock> ParseExamplesBlocks()
        {
            List<ExamplesBlock> blocks = [];
            while (TryReadExamplesHeading(out var tags, out var name, out var heading))
            {
                blocks.Add(new ExamplesBlock(name, tags, ParseRequiredTable("Examples section must contain a Markdown table."), heading));
            }

            if (blocks.Count == 0)
            {
                SkipBlankLines();
                var location = End ? new BddSourceLocation(_filePath, _lines.Length + 1) : Location(Current);
                _diagnostics.Add(new BddDiagnostic(
                    BddDiagnosticSeverity.Error,
                    "UAT012",
                    "Scenario Outline must contain a '### Examples' section.",
                    location));
            }

            return blocks;
        }

        // Past the tag lines to the next '### Examples' heading; where it was when none comes next.
        private bool TryReadExamplesHeading(out IReadOnlyList<string> tags, out string? name, out Line heading)
        {
            var start = _index;
            List<string> found = [];
            SkipBlankLines();
            while (!End && Current.Kind == LineKind.Tags && TryParseTags(Current.Text.Trim(), out var lineTags))
            {
                found.AddRange(lineTags);
                _index++;
                SkipBlankLines();
            }

            if (!End && Current.Kind == LineKind.Heading && TryParseHeading(Current.Text.Trim(), out var level, out var headingText) &&
                level == 3 && TryExamplesName(headingText, out name))
            {
                tags = found;
                heading = Current;
                _index++;
                return true;
            }

            _index = start;
            tags = [];
            name = null;
            heading = null!;
            return false;
        }

        // 'Examples', or 'Examples: <name>'; an empty name is none.
        private static bool TryExamplesName(string headingText, out string? name)
        {
            name = null;
            if (headingText.Equals("Examples", StringComparison.Ordinal))
            {
                return true;
            }

            if (!headingText.StartsWith("Examples:", StringComparison.Ordinal))
            {
                return false;
            }

            var rest = headingText["Examples:".Length..].Trim();
            name = rest.Length == 0 ? null : rest;
            return true;
        }

        private IReadOnlyList<BddScenario> ExpandOutline(
            string scenarioName,
            IReadOnlyList<string> tags,
            IReadOnlyList<BddStep> outlineSteps,
            IReadOnlyList<ExamplesBlock> examples,
            Line headingLine)
        {
            if (scenarioName.Length == 0)
            {
                AddError("UAT013", "Scenario Outline heading must include a name.", headingLine);
            }

            if (outlineSteps.Count == 0)
            {
                AddError("UAT014", NoSteps("Scenario Outline section must contain at least one step.", headingLine), headingLine);
            }

            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (var block in examples.Where(block => block.Name is not null))
            {
                if (!names.Add(block.Name!))
                {
                    AddError("UAT032", $"Two Examples sections of this outline are named '{block.Name}'; give each its own name.", block.Heading);
                }
            }

            // Rows are numbered across the blocks, so a row's name does not depend on its block.
            List<BddScenario> scenarios = [];
            foreach (var block in examples)
            {
                if (block.Table is not { } table)
                {
                    continue;
                }

                var columns = new HashSet<string>(table.Columns, StringComparer.Ordinal);
                foreach (var step in outlineSteps)
                {
                    foreach (var parameter in BddOutline.Placeholders(step))
                    {
                        if (!columns.Contains(parameter))
                        {
                            _diagnostics.Add(new BddDiagnostic(
                                BddDiagnosticSeverity.Error,
                                "UAT015",
                                block.Name is null
                                    ? $"Scenario Outline parameter '<{parameter}>' has no matching Examples column."
                                    : $"Scenario Outline parameter '<{parameter}>' has no matching column in Examples '{block.Name}'.",
                                step.Source));
                        }
                    }
                }

                string[] rowTags = [.. tags.Concat(block.Tags).Distinct(StringComparer.Ordinal)];
                foreach (var row in table.Rows)
                {
                    var index = scenarios.Count + 1;
                    scenarios.Add(new BddScenario(
                        $"{scenarioName} [{index}]",
                        rowTags,
                        [.. outlineSteps.Select(step => BddOutline.Substitute(step, row.Cells))],
                        Location(headingLine),
                        scenarioName,
                        index,
                        block.Name));
                }
            }

            return scenarios;
        }

        private BddEffectiveStepKeyword ResolveEffectiveKeyword(
            BddStepKeyword keyword,
            BddEffectiveStepKeyword? currentEffectiveKeyword,
            Line line)
        {
            return keyword switch
            {
                BddStepKeyword.Given => BddEffectiveStepKeyword.Given,
                BddStepKeyword.When => BddEffectiveStepKeyword.When,
                BddStepKeyword.Then => BddEffectiveStepKeyword.Then,
                BddStepKeyword.And or BddStepKeyword.But when currentEffectiveKeyword.HasValue => currentEffectiveKeyword.Value,
                _ => AddAndReturnStepKeywordError(line)
            };
        }

        private BddEffectiveStepKeyword AddAndReturnStepKeywordError(Line line)
        {
            AddError("UAT016", "'And' and 'But' steps must follow Given, When, or Then.", line);
            return BddEffectiveStepKeyword.Given;
        }

        // The first table of the section; a lower heading before it is prose.
        private BddTable? ParseRequiredTable(string message)
        {
            SkipBlankLines();
            while (!End && Current.Kind == LineKind.Heading && TryParseHeading(Current.Text.Trim(), out var level, out var headingText) &&
                   level >= 3 && !TryExamplesName(headingText, out _))
            {
                WarnIfLooksLikeSection(headingText, Current);
                _index++;
            }

            var table = TryParseCurrentTable(LineKind.Table);
            if (table is null)
            {
                var location = End ? new BddSourceLocation(_filePath, _lines.Length + 1) : Location(Current);
                _diagnostics.Add(new BddDiagnostic(BddDiagnosticSeverity.Error, "UAT017", message, location));
            }

            return table;
        }

        // A table is read from lines of one kind: a step's table from its gherkin block, a section's
        // table from the Markdown around the blocks.
        private BddTable? TryParseCurrentTable(LineKind kind)
        {
            if (End || _index + 1 >= _lines.Length || Current.Kind != kind || _lines[_index + 1].Kind != kind ||
                !IsTableRow(Current.Text) || !IsSeparatorRow(_lines[_index + 1].Text))
            {
                return null;
            }

            var source = Location(Current);
            var columns = SplitTableRow(Current.Text);
            _index += 2;

            List<BddTableRow> rows = [];
            while (!End && Current.Kind == kind && IsTableRow(Current.Text))
            {
                var rowLine = Current;
                var cells = SplitTableRow(rowLine.Text);
                if (cells.Count != columns.Count)
                {
                    AddError("UAT018", "Markdown table row must have the same number of cells as the header.", rowLine);
                    _index++;
                    continue;
                }

                var row = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var i = 0; i < columns.Count; i++)
                {
                    row[columns[i]] = cells[i];
                }

                rows.Add(new BddTableRow(row, Location(rowLine)));
                _index++;
            }

            if (rows.Count == 0)
            {
                AddError("UAT019", "Markdown table must contain at least one data row.", source);
            }

            return new BddTable(columns, rows, source);
        }

        // A """ block under a step: its lines lose the opening delimiter's indentation.
        private string? TryParseDocString()
        {
            if (End || Current.Kind != LineKind.Gherkin || Current.Text.Trim() != DocStringDelimiter)
            {
                return null;
            }

            var opening = Current;
            var indent = opening.Text.Length - opening.Text.TrimStart().Length;
            _index++;
            List<string> lines = [];
            while (!End && Current.Kind == LineKind.Gherkin && Current.Text.Trim() != DocStringDelimiter)
            {
                var text = Current.Text;
                lines.Add(text.Length >= indent && string.IsNullOrWhiteSpace(text[..indent]) ? text[indent..] : text.TrimStart());
                _index++;
            }

            if (End || Current.Kind != LineKind.Gherkin)
            {
                AddError("UAT023", "A doc string (\"\"\") is not closed.", opening);
            }
            else
            {
                _index++;
            }

            return string.Join('\n', lines);
        }

        private bool End => _index >= _lines.Length;

        private Line Current => _lines[_index];

        private void SkipBlankLines()
        {
            while (!End && string.IsNullOrWhiteSpace(Current.Text))
            {
                _index++;
            }
        }

        private BddSourceLocation Location(Line line)
        {
            return new BddSourceLocation(_filePath, line.Number);
        }

        private void AddError(string code, string message, Line line)
        {
            _diagnostics.Add(new BddDiagnostic(BddDiagnosticSeverity.Error, code, message, Location(line)));
        }

        private void AddError(string code, string message, BddSourceLocation location)
        {
            _diagnostics.Add(new BddDiagnostic(BddDiagnosticSeverity.Error, code, message, location));
        }

        private static bool IsScenarioHeading(string line)
        {
            return line.StartsWith("## Scenario:", StringComparison.Ordinal) ||
                   line.StartsWith("## Scenario Outline:", StringComparison.Ordinal);
        }

        private static bool TryParseHeading(string line, out int level, out string text)
        {
            level = 0;
            text = string.Empty;

            while (level < line.Length && line[level] == '#')
            {
                level++;
            }

            if (level == 0 || level >= line.Length || line[level] != ' ')
            {
                return false;
            }

            text = line[(level + 1)..].Trim();
            return text.Length > 0;
        }

        private static bool TryParseTags(string line, out IReadOnlyList<string> tags)
        {
            tags = [];
            if (!line.StartsWith('@'))
            {
                return false;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || parts.Any(x => x.Length < 2 || x[0] != '@'))
            {
                return false;
            }

            tags = parts.Select(x => x[1..]).ToArray();
            return true;
        }

        private static bool TryParseStep(string line, out BddStepKeyword keyword, out string text)
        {
            foreach (var name in Enum.GetNames<BddStepKeyword>())
            {
                if (line.StartsWith(name + " ", StringComparison.Ordinal))
                {
                    keyword = Enum.Parse<BddStepKeyword>(name);
                    text = line[(name.Length + 1)..].Trim();
                    return text.Length > 0;
                }
            }

            keyword = default;
            text = string.Empty;
            return false;
        }

        private static bool IsTableRow(string line)
        {
            var trimmed = line.Trim();
            return trimmed.StartsWith('|') && trimmed.EndsWith('|') && trimmed.Count(x => x == '|') >= 2;
        }

        private static bool IsSeparatorRow(string line)
        {
            if (!IsTableRow(line))
            {
                return false;
            }

            var cells = SplitTableRow(line);
            return cells.Count > 0 && cells.All(cell =>
            {
                var trimmed = cell.Replace(" ", string.Empty, StringComparison.Ordinal);
                return trimmed.Length >= 3 && trimmed.All(x => x is '-' or ':');
            });
        }

        private static IReadOnlyList<string> SplitTableRow(string line)
        {
            var trimmed = line.Trim();
            var inner = trimmed[1..^1];
            return inner.Split('|').Select(x => x.Trim()).ToArray();
        }

        // The parts the engine reads, found in Markdig's block tree at the top level of the document:
        // ATX headings, the lines of ```gherkin blocks, tables and tag lines. Every other block is
        // prose and is left out, so the rest of the parser never sees it (AD-023). Tables are read
        // from the raw lines, so one without a separator row still reaches its section's check.
        private Line[] ReadBlocks(string markdown)
        {
            var text = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var raw = text.Split('\n');
            var starts = new int[raw.Length];
            for (var i = 1; i < raw.Length; i++)
            {
                starts[i] = starts[i - 1] + raw[i - 1].Length + 1;
            }

            var document = Markdown.Parse(text, Pipeline);
            List<Line> lines = [];
            for (var b = 0; b < document.Count; b++)
            {
                switch (document[b])
                {
                    case HeadingBlock { IsSetext: false } heading:
                        lines.Add(new Line(raw[heading.Line], heading.Line + 1, LineKind.Heading));
                        break;
                    case HeadingBlock setext:
                        if (LooksLikeSection().IsMatch(raw[setext.Line].Trim()))
                        {
                            _diagnostics.Add(new BddDiagnostic(
                                BddDiagnosticSeverity.Warning,
                                "UAT031",
                                $"'{raw[setext.Line].Trim()}' is underlined, so it is read as prose; a section heading starts with '##'.",
                                new BddSourceLocation(_filePath, setext.Line + 1)));
                        }

                        break;
                    case FencedCodeBlock fence when string.Equals(fence.Info, "gherkin", StringComparison.OrdinalIgnoreCase):
                        lines.Add(new Line(raw[fence.Line], fence.Line + 1, LineKind.Fence));
                        for (var i = 0; i < fence.Lines.Count; i++)
                        {
                            lines.Add(new Line(fence.Lines.Lines[i].Slice.ToString(), fence.Line + i + 2, LineKind.Gherkin));
                        }

                        break;
                    case ParagraphBlock paragraph when LinesOf(paragraph) is var texts && texts.All(line => TryParseTags(line.Text.Trim(), out _)):
                        var detached = b + 1 >= document.Count || document[b + 1] is not HeadingBlock { IsSetext: false };
                        lines.AddRange(texts.Select(line => line with { Kind = LineKind.Tags, Detached = detached }));
                        break;
                    case ParagraphBlock paragraph when LinesOf(paragraph) is var texts && texts.All(line => IsTableRow(line.Text)):
                        lines.AddRange(texts.Select(line => line with { Kind = LineKind.Table }));
                        break;
                    case LeafBlock prose:
                        _stepLikeProse.UnionWith(LinesOf(prose).Where(line => TryParseStep(line.Text.Trim(), out _, out _)).Select(line => line.Number));
                        break;
                }
            }

            return [.. lines];

            // A top-level block's lines run from its first to the line its span ends on.
            List<Line> LinesOf(Block block)
            {
                var last = Array.BinarySearch(starts, Math.Max(block.Span.End, block.Span.Start));
                last = last >= 0 ? last : ~last - 1;
                return [.. Enumerable.Range(block.Line, Math.Max(1, last - block.Line + 1)).Select(number => new Line(raw[number], number + 1, LineKind.Heading))];
            }
        }

        [GeneratedRegex(@"^(feature|steps?|phrases?|background|scenario|examples|data|metadata)\b", RegexOptions.IgnoreCase)]
        private static partial Regex LooksLikeSection();
    }

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().Build();

    private enum LineKind
    {
        Heading,
        Fence,
        Gherkin,
        Table,
        Tags,
    }

    private sealed record Line(string Text, int Number, LineKind Kind, bool Detached = false);

    // One '### Examples' section of an outline: its name, the tags above it, its table (null when missing).
    private sealed record ExamplesBlock(string? Name, IReadOnlyList<string> Tags, BddTable? Table, Line Heading);
}
