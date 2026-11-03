using System.Text.Json;
using Bruinet.Bdd.Preview;
using Bruinet.Bdd.Workspace;

namespace Bruinet.Bdd.LanguageServer;

/// <summary>A file as the server sees it: its text (the editor's when open), its suite and whether it binds.</summary>
/// <param name="Path">The file's full path.</param>
/// <param name="Text">Its text: the open document's, or the file on disk.</param>
/// <param name="Version">The open document's version; 0 for a file read from disk.</param>
/// <param name="Suites">The suites that hold or link it, its own first.</param>
/// <param name="Suite">The suite it is checked in; null when none holds it.</param>
/// <param name="Catalogs">The suite's catalogs; null when it is only parsed.</param>
/// <param name="Reason">Why it is only parsed; null when bound.</param>
internal sealed record BddServerFile(string Path, string Text, int Version, IReadOnlyList<BddSuite> Suites, BddSuite? Suite, BddCatalogSet? Catalogs, string? Reason)
{
    private string[]? _lines;

    /// <summary>The text's lines, without their line breaks.</summary>
    public string[] Lines => _lines ??= Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private BddDocumentStructure? _structure;

    /// <summary>The file's headings, fences, tables and step lines.</summary>
    public BddDocumentStructure Structure => _structure ??= BddDocumentStructure.Read(Lines);

    /// <summary>A 1-based line's text; empty past the end.</summary>
    public string Line(int lineNumber) => lineNumber >= 1 && lineNumber <= Lines.Length ? Lines[lineNumber - 1] : string.Empty;
}

// The file context every feature shares, and the diagnostics (3.1): the gate's words at their
// lines, published after a pause in typing, for the whole suite on save and after a build, and a
// step that failed in the last run as an error at its line.
public sealed partial class BddLanguageServer
{
    private const string DiagnosticSource = "Bruinet BDD";

    // How long typing must pause before a file is checked again.
    private static readonly TimeSpan DiagnosticsPause = TimeSpan.FromMilliseconds(200);

    private readonly Dictionary<string, Timer> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (string Text, BddCatalogSet? Catalogs, BddFileAnalysis Analysis)> _analyses = new(StringComparer.OrdinalIgnoreCase);
    private bool _publishes;

    private static ServerCapabilities Capabilities() => new(new TextDocumentSyncOptions(OpenClose: true, Change: 1, new SaveOptions(IncludeText: false)))
    {
        DocumentSymbolProvider = true,
        FoldingRangeProvider = true,
        SemanticTokensProvider = new SemanticTokensOptions(new SemanticTokensLegend(TokenTypes, []), Full: true),
        CompletionProvider = new CompletionOptions([" ", "\"", "<"]),
        HoverProvider = true,
        DefinitionProvider = true,
        ReferencesProvider = true,
        CodeActionProvider = new CodeActionOptions(["quickfix", "refactor.extract", "refactor.inline"]),
        RenameProvider = new RenameOptions(PrepareProvider: true),
    };

    // Only an editor's LSP client takes diagnostics; the preview's client has no use for them, so none are computed for it.
    private static bool ClientTakesDiagnostics(JsonElement? capabilities)
        => capabilities is { ValueKind: JsonValueKind.Object } client &&
           client.TryGetProperty("textDocument", out var document) && document.ValueKind == JsonValueKind.Object &&
           document.TryGetProperty("publishDiagnostics", out _);

    /// <summary>A file, with its text, its suite (the one picked, else its own) and its catalogs.</summary>
    internal BddServerFile Context(string path, string? suiteName = null)
    {
        (int Version, string Text) document;
        IReadOnlyList<BddSuite> suites;
        BddSuite? suite;
        string root;
        lock (_gate)
        {
            document = _documents.TryGetValue(path, out var open) ? open : (0, File.Exists(path) ? File.ReadAllText(path) : string.Empty);
            suites = Order(path, _workspace?.SuitesFor(path) ?? []);
            suite = suites.FirstOrDefault(candidate => string.Equals(candidate.Name, suiteName, StringComparison.OrdinalIgnoreCase)) ?? suites.FirstOrDefault();
            root = _workspace?.Root ?? "the solution";
        }

        string? reason = null;
        var catalogs = suite?.TryGetCatalogs(out reason);
        reason = catalogs is not null
            ? null
            : suite is null
                ? $"No suite holds or links this file: no project with a {BddFileFormat.UiConfigFileName} or {BddFileFormat.ServiceConfigFileName} in {root} (under it, or listed by its solution) has it."
                : reason;
        return new BddServerFile(path, document.Text, document.Version, suites, suite, catalogs, reason);
    }

    /// <summary>The gate's check of a file, kept while its text and its suite's build stay the same.</summary>
    internal BddFileAnalysis Analyze(BddServerFile file)
    {
        lock (_analyses)
        {
            if (_analyses.TryGetValue(file.Path, out var cached) && ReferenceEquals(cached.Text, file.Text) && ReferenceEquals(cached.Catalogs, file.Catalogs))
            {
                return cached.Analysis;
            }
        }

        var analysis = BddFileAnalysis.Analyze(file.Text, file.Path, file.Catalogs);
        lock (_analyses)
        {
            _analyses[file.Path] = (file.Text, file.Catalogs, analysis);
        }

        return analysis;
    }

    // The newest run of each of a feature file's scenarios; of the ones accept takes, when given.
    private IReadOnlyDictionary<string, BddScenarioRun> RunsOf(BddServerFile file, Func<BddScenarioRun, bool>? accept = null)
    {
        if (file.Suite is not { } suite || _results is not { } results || !file.Path.EndsWith(BddFileFormat.FeatureExtension, StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, BddScenarioRun>();
        }

        lock (results)
        {
            return results.For(BddRunResults.RootFor(suite.ProjectDirectory), suite.Name, file.Path, accept);
        }
    }

    private void ScheduleDiagnostics(string path)
    {
        if (!_publishes)
        {
            return;
        }

        lock (_gate)
        {
            if (!_pending.TryGetValue(path, out var timer))
            {
                _pending[path] = timer = new Timer(_ => PublishFile(path));
            }

            timer.Change(DiagnosticsPause, Timeout.InfiniteTimeSpan);
        }
    }

    // Every feature and step file of the suites that hold these files, so the Error List shows the
    // suite's problems, not only the open files'.
    private void PublishSuitesOf(IReadOnlyCollection<string> paths)
    {
        if (!_publishes)
        {
            return;
        }

        List<string> files;
        lock (_gate)
        {
            files = paths
                .SelectMany(path => _workspace?.SuitesFor(path) ?? [])
                .DistinctBy(suite => suite.ConfigPath, StringComparer.OrdinalIgnoreCase)
                .SelectMany(suite => suite.FeatureFiles.Concat(suite.StepFiles).Concat(suite.LinkedFiles).Append(suite.ConfigPath))
                .Concat(paths)
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        _ = Task.Run(() =>
        {
            foreach (var file in files)
            {
                PublishFile(file);
            }
        });
    }

    private void PublishFile(string path)
    {
        if (_rpc is not { } rpc)
        {
            return;
        }

        try
        {
            var file = Context(path);
            _ = rpc.NotifyWithParameterObjectAsync(
                "textDocument/publishDiagnostics",
                new PublishDiagnosticsParams(new Uri(path).AbsoluteUri, file.Version == 0 ? null : file.Version, DiagnosticsOf(file)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            // A file deleted or locked between the event and the check: the next change checks it again.
        }
    }

    internal IReadOnlyList<Diagnostic> DiagnosticsOf(BddServerFile file)
    {
        if (IsConfig(file.Path))
        {
            return ConfigDiagnostics(file);
        }

        var analysis = Analyze(file);
        List<Diagnostic> diagnostics = [];
        foreach (var diagnostic in analysis.Diagnostics)
        {
            var here = analysis.IsHere(diagnostic);
            var message = diagnostic.Message
                + (diagnostic.Within.Count == 0 ? string.Empty : $" (in {string.Join(" → ", diagnostic.Within.Select(Where))})")
                + (here ? string.Empty : $" ({Where(diagnostic.Location)})");
            diagnostics.Add(new Diagnostic(
                LineRange(file, here ? diagnostic.Location.LineNumber : 1),
                diagnostic.Severity == BddDiagnosticSeverity.Error ? 1 : 2,
                diagnostic.Code,
                DiagnosticSource,
                message));
        }

        // A step that failed in the last run, where it is written: a callable's step at its call.
        foreach (var run in RunsOf(file).Values)
        {
            foreach (var step in run.Steps.Where(step => string.Equals(step.Status, "Failed", StringComparison.OrdinalIgnoreCase)))
            {
                diagnostics.Add(new Diagnostic(
                    LineRange(file, step.WrittenLine),
                    1,
                    "UATRUN",
                    DiagnosticSource,
                    $"Failed in the last run of '{run.Scenario}'{(step.Message is { Length: > 0 } why ? $": {why}" : ".")}"));
            }
        }

        return diagnostics;
    }

    // A line's text, without its indent.
    internal static Range LineRange(BddServerFile file, int lineNumber)
    {
        var line = Math.Clamp(lineNumber, 1, file.Lines.Length);
        var text = file.Line(line).TrimEnd('\r');
        var start = text.Length - text.TrimStart().Length;
        return new Range(new Position(line - 1, start), new Position(line - 1, text.Length));
    }

    private static string Where(BddSourceLocation location)
        => location.FilePath is { } file ? $"{Path.GetFileName(file)}:{location.LineNumber}" : $"line {location.LineNumber}";
}
