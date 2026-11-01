using System.Reflection;
using System.Text;
using Bruinet.Bdd.Workspace;
using StreamJsonRpc;
using System.Globalization;

namespace Bruinet.Bdd.LanguageServer;

// Hover, go to definition and find references (3.4), from the bound step at a line: the method
// and page it binds to, its summary and its arguments as read; its definition through the
// suite's portable PDB; and every step across the suite that binds to the same thing.
public sealed partial class BddLanguageServer
{
    /// <summary>LSP <c>textDocument/hover</c>: what the step at the line binds to.</summary>
    [JsonRpcMethod("textDocument/hover")]
    public Hover? HoverAt(PositionParams request)
    {
        var file = Context(Key(request.TextDocument.Uri));
        var line = request.Position.Line + 1;
        if (IsConfig(file.Path))
        {
            return ConfigHover(file, line);
        }

        var invocations = InvocationsAt(file, line);
        if (invocations.Count == 0)
        {
            return null;
        }

        var first = invocations[0];
        StringBuilder text = new();
        if (first.Call is { } call)
        {
            var callable = Outermost(call).Callable;
            text.Append(CultureInfo.InvariantCulture, $"**calls** `{callable}` ({Where(callable.Source)})");
            text.Append("\n\n");
            foreach (var step in invocations)
            {
                text.Append(CultureInfo.InvariantCulture, $"- {new string(' ', (step.Call!.Depth - 1) * 2)}{step.Step.Keyword} {step.Step.Text} · `{step.CommandId}`\n");
            }
        }
        else
        {
            var page = first.Command.ControlVerb is not null ? PageAt(file, line) : null;
            text.Append(CultureInfo.InvariantCulture, $"**binds to** `{first.CommandId}`");
            if (first.Scope.Length > 0)
            {
                text.Append(CultureInfo.InvariantCulture, $" · {first.Scope}");
            }
            else if (page is not null)
            {
                text.Append(CultureInfo.InvariantCulture, $" · page {PageName(page)}");
            }

            if (first.App is { } app)
            {
                text.Append(CultureInfo.InvariantCulture, $" · on {app}");
            }

            if (Method(file, first.CommandId) is { } method && BddSourceLocator.Summary(method) is { Length: > 0 } summary)
            {
                text.Append("\n\n").Append(summary);
            }

            Arguments(text, first);
        }

        return new Hover(new MarkupContent("markdown", text.ToString()), LineRange(file, line));
    }

    /// <summary>LSP <c>textDocument/definition</c>: the step definition, the page, the control or the callable step.</summary>
    [JsonRpcMethod("textDocument/definition")]
    public IReadOnlyList<Location> Definition(PositionParams request)
    {
        var file = Context(Key(request.TextDocument.Uri));
        var line = request.Position.Line + 1;
        return InvocationsAt(file, line).FirstOrDefault() is { } invocation && DefinitionOf(file, line, invocation) is { FilePath: { } path } found
            ? [new Location(new Uri(path).AbsoluteUri, LocationRange(path, found.LineNumber))]
            : [];
    }

    /// <summary>LSP <c>textDocument/references</c>: every step across the file's suites that binds to the same thing.</summary>
    [JsonRpcMethod("textDocument/references")]
    public IReadOnlyList<Location> References(ReferenceParams request)
    {
        var file = Context(Key(request.TextDocument.Uri));
        var line = request.Position.Line + 1;
        BddSourceLocation? callable = null;
        string? command = null;
        BddSourceLocation? declaration = null;
        if (InvocationsAt(file, line).FirstOrDefault() is { } invocation)
        {
            if (invocation.Call is { } call)
            {
                callable = Outermost(call).Callable.Source;
            }
            else
            {
                command = invocation.CommandId;
            }

            declaration = DefinitionOf(file, line, invocation);
        }
        else if (file.Structure.HeadingAt(line) is { Kind: "Step" or "Step for each row" } heading && heading.Line == line)
        {
            callable = new BddSourceLocation(file.Path, line);
            declaration = callable;
        }

        if (callable is null && command is null)
        {
            return [];
        }

        HashSet<(string File, int Line)> found = [];
        foreach (var path in SuiteFeatureFiles(file.Path))
        {
            foreach (var scenario in Analyze(Context(path)).Scenarios)
            {
                foreach (var step in scenario.Invocations)
                {
                    if (command is not null && step.CommandId == command && step.Step.Source.FilePath is { } written)
                    {
                        found.Add((Path.GetFullPath(written), step.Step.Source.LineNumber));
                    }

                    for (var frame = step.Call; callable is not null && frame is not null; frame = frame.Parent)
                    {
                        if (Same(frame.Callable.Source, callable) && frame.Call.Source.FilePath is { } caller)
                        {
                            found.Add((Path.GetFullPath(caller), frame.Call.Source.LineNumber));
                        }
                    }
                }
            }
        }

        if (request.Context?.IncludeDeclaration == true && declaration is { FilePath: { } declared })
        {
            found.Add((Path.GetFullPath(declared), declaration.LineNumber));
        }

        return found
            .OrderBy(location => location.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(location => location.Line)
            .Select(location => new Location(new Uri(location.File).AbsoluteUri, LocationRange(location.File, location.Line)))
            .ToList();
    }

    // The steps bound at a line: one, or a call's whole body; from the first scenario (row) that has them.
    private List<BddStepInvocation> InvocationsAt(BddServerFile file, int line)
    {
        foreach (var scenario in Analyze(file).Scenarios)
        {
            var here = scenario.Invocations.Where(invocation => WrittenLine(invocation) == line && WrittenIn(invocation, file.Path)).ToList();
            if (here.Count > 0)
            {
                return here;
            }
        }

        return [];
    }

    // A step is written in this file: its own line, or its outermost call's.
    private static bool WrittenIn(BddStepInvocation invocation, string path)
    {
        var source = invocation.Call is { } call ? Outermost(call).Call.Source : invocation.Step.Source;
        return source.FilePath is null || string.Equals(Path.GetFullPath(source.FilePath), path, StringComparison.OrdinalIgnoreCase);
    }

    private BddSourceLocation? DefinitionOf(BddServerFile file, int line, BddStepInvocation invocation)
    {
        if (invocation.Call is { } call)
        {
            return Outermost(call).Callable.Source;
        }

        if (file.Catalogs is not { } catalogs)
        {
            return null;
        }

        var pages = UatPageSet.Of(catalogs.Default);
        if (invocation.Command.ControlVerb is not null)
        {
            var page = PageAt(file, line) is { } key ? pages.Values.FirstOrDefault(model => model.Key == key || model.Name == PageName(key)) : null;
            if (page is not null)
            {
                return invocation.Arguments.GetValueOrDefault("control") is { } control
                    ? BddSourceLocator.Of(page.PageType, page.Controls.FirstOrDefault(model => model.Name == control)?.Name ?? control)
                    : BddSourceLocator.Of(page.PageType);
            }

            return null;
        }

        if (invocation.Command.NamesPage && invocation.Arguments.GetValueOrDefault("page") is { } named &&
            pages.Values.FirstOrDefault(model => model.Key == named || model.Name == PageName(named)) is { } namedPage)
        {
            return BddSourceLocator.Of(namedPage.PageType);
        }

        return Method(file, invocation.CommandId) is { } method ? BddSourceLocator.Of(method) : null;
    }

    // A C# step's method, from its command id: the declaring type's full name, a dot, the method's name.
    private static MethodInfo? Method(BddServerFile file, string commandId)
    {
        var dot = commandId.LastIndexOf('.');
        if (file.Suite is not { } suite || dot <= 0 || suite.FindType(commandId[..dot]) is not { } type)
        {
            return null;
        }

        var name = commandId[(dot + 1)..];
        return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .FirstOrDefault(method => method.Name == name);
    }

    // Each argument as written and, for a typed parameter, as the step definition will read it.
    private static void Arguments(StringBuilder text, BddStepInvocation invocation)
    {
        if (invocation.RawArguments.Count == 0)
        {
            return;
        }

        text.Append("\n\n| Argument | Written | As read |\n| --- | --- | --- |\n");
        foreach (var (name, written) in invocation.RawArguments)
        {
            var asRead = invocation.Arguments.GetValueOrDefault(name) ?? written;
            if (invocation.Command.Parameters.FirstOrDefault(parameter => parameter.Name == name) is { } parameter &&
                BddValues.TryRead(asRead, parameter.Type, BddValueContext.Default, out var value, out _, parameter.AllowsNull))
            {
                asRead = BddValues.Write(value);
            }

            text.Append(CultureInfo.InvariantCulture, $"| `{name}` | {Cell(written)} | {Cell(asRead)} |\n");
        }

        static string Cell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
    }

    // The feature files of the suites that hold a file: their own and the ones they link.
    private IReadOnlyList<string> SuiteFeatureFiles(string path)
    {
        lock (_gate)
        {
            return (_workspace?.SuitesFor(path) ?? [])
                .SelectMany(suite => suite.FeatureFiles.Concat(suite.LinkedFiles))
                .Where(file => file.EndsWith(BddFileFormat.FeatureExtension, StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private Range LocationRange(string path, int line) => LineRange(Context(Path.GetFullPath(path)), line);

    private static bool Same(BddSourceLocation a, BddSourceLocation b)
        => a.LineNumber == b.LineNumber && a.FilePath is { } x && b.FilePath is { } y && string.Equals(Path.GetFullPath(x), Path.GetFullPath(y), StringComparison.OrdinalIgnoreCase);

    private static BddCallFrame Outermost(BddCallFrame frame)
    {
        while (frame.Parent is { } parent)
        {
            frame = parent;
        }

        return frame;
    }
}
