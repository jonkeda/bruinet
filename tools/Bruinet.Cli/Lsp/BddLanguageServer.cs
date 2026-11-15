using System.Reflection;
using Bruinet.Bdd.Preview;
using Bruinet.Bdd.Workspace;
using StreamJsonRpc;

namespace Bruinet.Bdd.LanguageServer;

/// <summary>
/// The server's methods (P2): LSP's lifecycle and document sync, and Bruinet's own requests. A file
/// belongs to the suites whose project holds or links it (AD-014); the client picks one when there
/// are several. Until a suite is built, its files are parsed only, and the preview says why.
/// </summary>
public sealed partial class BddLanguageServer : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, (int Version, string Text)> _documents = new(StringComparer.OrdinalIgnoreCase);
    private BddWorkspace? _workspace;
    private BddRunResults? _results;
    private readonly List<FileSystemWatcher> _resultsWatchers = [];
    private Timer? _resultsChanged;
    private JsonRpc? _rpc;

    /// <summary>The virtual host a webview maps to the results folder, so it can show screenshots.</summary>
    public const string ResultsHost = "bruinet-results.local";

    /// <summary>Screenshots as <c>file:</c> URLs, for a page opened on its own (<c>bruinet scenario preview</c>); the IDE maps <see cref="ResultsHost"/>.</summary>
    public bool FileImageUrls { get; init; }

    /// <summary>Raised when the client asks the server to exit.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Starts serving on a stream pair with LSP's header framing, until the connection closes.</summary>
    /// <param name="sending">Where the server writes: the client reads it.</param>
    /// <param name="receiving">What the server reads: the client writes it.</param>
    public JsonRpc Attach(Stream sending, Stream receiving)
    {
        var formatter = new SystemTextJsonFormatter { JsonSerializerOptions = Json.Options };
        var rpc = new JsonRpc(new HeaderDelimitedMessageHandler(sending, receiving, formatter));
        rpc.AddLocalRpcTarget(this, new JsonRpcTargetOptions { UseSingleObjectParameterDeserialization = true });
        rpc.StartListening();
        _rpc = rpc;
        return rpc;
    }

    /// <summary>LSP <c>initialize</c>: opens the suites under the solution folder.</summary>
    [JsonRpcMethod("initialize")]
    public InitializeResult Initialize(InitializeParams request)
    {
        var root = request.InitializationOptions?.SolutionDir
            ?? (request.RootUri is { } uri ? new Uri(uri).LocalPath : null)
            ?? Environment.CurrentDirectory;
        lock (_gate)
        {
            _workspace?.Dispose();
            _workspace = BddWorkspace.Open(root);
            _results = new BddRunResults();
            WatchResults(_workspace.Suites.Select(suite => BddRunResults.RootFor(suite.ProjectDirectory)).Append(BddRunResults.RootFor(_workspace.Root)));
            _publishes = ClientTakesDiagnostics(request.Capabilities);
            WarmSettings(request.InitializationOptions);
            _renamesFiles = ClientRenamesFiles(request.Capabilities);
            return new InitializeResult(
                Capabilities(),
                new ServerInfo("Bruinet.Bdd.LanguageServer", typeof(BddLanguageServer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0"),
                _workspace.Suites.Select(Info).ToList());
        }
    }

    /// <summary>LSP <c>initialized</c>: nothing to do.</summary>
    [JsonRpcMethod("initialized")]
    public void Initialized()
    {
    }

    /// <summary>LSP <c>shutdown</c>: releases the suites.</summary>
    [JsonRpcMethod("shutdown")]
    public object? Shutdown()
    {
        Dispose();
        return null;
    }

    /// <summary>LSP <c>exit</c>.</summary>
    [JsonRpcMethod("exit")]
    public void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>LSP <c>textDocument/didOpen</c>.</summary>
    [JsonRpcMethod("textDocument/didOpen")]
    public void DidOpen(DidOpenTextDocumentParams request)
    {
        lock (_gate)
        {
            _documents[Key(request.TextDocument.Uri)] = (request.TextDocument.Version, request.TextDocument.Text);
        }

        ScheduleDiagnostics(Key(request.TextDocument.Uri));
        WarmOnOpen(Key(request.TextDocument.Uri));
    }

    /// <summary>LSP <c>textDocument/didChange</c>, full sync.</summary>
    [JsonRpcMethod("textDocument/didChange")]
    public void DidChange(DidChangeTextDocumentParams request)
    {
        if (request.ContentChanges.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            _documents[Key(request.TextDocument.Uri)] = (request.TextDocument.Version, request.ContentChanges[^1].Text);
        }

        ScheduleDiagnostics(Key(request.TextDocument.Uri));
    }

    /// <summary>LSP <c>textDocument/didClose</c>.</summary>
    [JsonRpcMethod("textDocument/didClose")]
    public void DidClose(DidCloseTextDocumentParams request)
    {
        lock (_gate)
        {
            _documents.Remove(Key(request.TextDocument.Uri));
        }

        // The saved text is what counts now, for the Error List.
        ScheduleDiagnostics(Key(request.TextDocument.Uri));
        WarmOnClose(Key(request.TextDocument.Uri));
    }

    /// <summary>LSP <c>textDocument/didSave</c>: the file's suites are checked whole, since a saved step file changes what every feature file binds to.</summary>
    [JsonRpcMethod("textDocument/didSave")]
    public void DidSave(DidSaveTextDocumentParams request) => PublishSuitesOf([Key(request.TextDocument.Uri)]);

    /// <summary><c>bruinet/buildDone</c>: the solution was built, so every suite loads its new output on next use.</summary>
    [JsonRpcMethod("bruinet/buildDone")]
    public void BuildDone()
    {
        lock (_gate)
        {
            _workspace?.Reload();
        }

        List<string> open;
        lock (_gate)
        {
            open = [.. _documents.Keys];
        }

        PublishSuitesOf(open);
        WarmAfterBuild(open);
    }

    /// <summary><c>bruinet/previewHost</c>: the page the client's webview loads once; <c>bruinet/preview</c> fills it.</summary>
    [JsonRpcMethod("bruinet/previewHost")]
    public PreviewHostResult PreviewHost() => new(BddPreview.HostPage);

    /// <summary><c>bruinet/preview</c>: the file rendered, bound in its suite when it is built.</summary>
    [JsonRpcMethod("bruinet/preview")]
    public PreviewResult Preview(PreviewParams request)
    {
        var file = Context(Key(request.Uri), request.Suite);
        var resultsRoot = BddRunResults.RootFor(file.Suite?.ProjectDirectory ?? _workspace?.Root ?? Environment.CurrentDirectory);
        var result = BddPreview.Render(file.Text, file.Path, new BddPreviewOptions
        {
            Catalogs = file.Catalogs,
            ParseOnlyReason = file.Reason,
            Runs = RunsOf(file),
            ImageUrl = image => ImageUrl(image, resultsRoot),
            Run = FileImageUrls ? null : RunState(file.Suite, file.Path),
            Record = FileImageUrls ? null : RecordState(file),
            Recordings = PreviewRecordings(file, request.RecordingsFolder),
        });
        return new PreviewResult(
            result.Html,
            file.Version,
            file.Suites.Select(candidate => candidate.Name).ToList(),
            file.Suite?.Name,
            file.Catalogs is null ? "parse-only" : "bound",
            file.Reason)
        {
            ResultsRoot = resultsRoot,
            ResultsHost = ResultsHost,
        };
    }

    /// <summary>
    /// <c>bruinet/runTarget</c>: the project and filter that run the scenario at a heading's line
    /// (every row of an outline), or the whole file. Asked after the build, so the suite's test class
    /// is known to use <c>[BddScenarioTheory]</c>.
    /// </summary>
    [JsonRpcMethod("bruinet/runTarget")]
    public RunTargetResult RunTarget(RunTargetParams request)
    {
        var path = Key(request.Uri);
        string text;
        BddSuite? suite;
        lock (_gate)
        {
            text = _documents.TryGetValue(path, out var open) ? open.Text : File.ReadAllText(path);
            var suites = Order(path, _workspace?.SuitesFor(path) ?? []);
            suite = suites.FirstOrDefault(candidate => string.Equals(candidate.Name, request.Suite, StringComparison.OrdinalIgnoreCase)) ?? suites.FirstOrDefault();
        }

        if (suite is null || !BddFileFormat.IsScenarioFile(path))
        {
            return new RunTargetResult(false, "No suite holds or links this feature file.", null, null, [], null);
        }

        // The project is named even when the scenario cannot run yet: a client builds it first,
        // and before the first build there are no catalogs to say more.
        if (suite.TryGetCatalogs(out var reason) is null)
        {
            return new RunTargetResult(false, reason, suite.ProjectFile, null, [], suite.Name);
        }

        if (RunState(suite, path) is { CanRun: false } || suite.ProjectFile is not { } project)
        {
            return new RunTargetResult(false, RunState(suite, path)?.Reason ?? $"{suite.Name} has no project file.", suite.ProjectFile, null, [], suite.Name);
        }

        var file = suite.RelativeToConfig(path);
        if (request.Line is not { } line)
        {
            return new RunTargetResult(true, null, project, $"{BddScenarioAddress.Trait}~{file}#", [], suite.Name);
        }

        // The scenarios under the heading at that line: one, or an outline's rows.
        var scenarios = BddMarkdownParser.Parse(text, path).Document?.Scenarios.Where(scenario => scenario.Source.LineNumber == line).Select(scenario => scenario.Name).ToList() ?? [];
        return scenarios.Count == 0
            ? new RunTargetResult(false, $"There is no scenario heading at line {line}.", null, null, [], suite.Name)
            : new RunTargetResult(true, null, project, string.Join('|', scenarios.Select(name => $"{BddScenarioAddress.Trait}={BddScenarioAddress.Of(file, name)}")), scenarios, suite.Name);
    }

    /// <summary>
    /// <c>bruinet/deleteResults</c>: deletes the old results of a scenario, a feature file or a
    /// suite from every run, the ones the preview and the explorer read; a dry run only counts.
    /// </summary>
    [JsonRpcMethod("bruinet/deleteResults")]
    public DeleteResultsResult DeleteResults(DeleteResultsParams request)
    {
        BddSuite? suite;
        string? path = null;
        IReadOnlyCollection<string>? scenarios = null;
        string what;
        if (request.Scope == "suite")
        {
            lock (_gate)
            {
                suite = _workspace?.Suites.FirstOrDefault(candidate => string.Equals(candidate.Name, request.Suite, StringComparison.OrdinalIgnoreCase));
            }

            what = suite?.Name ?? request.Suite ?? "the suite";
        }
        else if (request.Uri is { } uri && request.Scope is "file" or "scenario")
        {
            path = Key(uri);
            var file = Context(path, request.Suite);
            suite = file.Suite;
            what = Path.GetFileName(path);
            if (request.Scope == "scenario")
            {
                var found = BddMarkdownParser.Parse(file.Text, path).Document?.Scenarios.Where(scenario => scenario.Source.LineNumber == request.Line).ToList() ?? [];
                if (found.Count == 0)
                {
                    return new DeleteResultsResult(false, $"There is no scenario heading at line {request.Line}.", null, suite?.Name, 0, 0, 0, []);
                }

                scenarios = found.Select(scenario => scenario.Name).ToList();
                what = found[0].OutlineName ?? found[0].Name;
            }
        }
        else
        {
            return new DeleteResultsResult(false, $"'{request.Scope}' is not a scope: scenario, file or suite (a scenario and a file need the file's uri).", null, null, 0, 0, 0, []);
        }

        if (suite is null || _results is not { } results)
        {
            return new DeleteResultsResult(false, "No suite holds this file, so it has no results to delete.", what, null, 0, 0, 0, []);
        }

        BddDeleteResult deleted;
        lock (results)
        {
            deleted = results.Delete(BddRunResults.RootFor(suite.ProjectDirectory), suite.Name, path, scenarios, request.DryRun);
        }

        if (!request.DryRun && deleted.RunsChanged + deleted.RunsDeleted > 0)
        {
            NotifyResultsChanged();
        }

        return new DeleteResultsResult(
            true,
            null,
            what,
            suite.Name,
            deleted.RunsChanged,
            deleted.RunsDeleted,
            deleted.FilesDeleted,
            deleted.Skipped.Select(skipped => new SkippedRun(skipped.Run, skipped.Reason)).ToList());
    }

    // A suite runs by address when its test class uses [BddScenarioTheory]; before it is loaded, the build that comes first tells.
    private static Bruinet.Bdd.Preview.BddPreviewRunState? RunState(BddSuite? suite, string path)
    {
        if (suite is null || !BddFileFormat.IsScenarioFile(path))
        {
            return null;
        }

        return !suite.IsLoaded || suite.HasScenarioTheory
            ? new Bruinet.Bdd.Preview.BddPreviewRunState(true)
            : new Bruinet.Bdd.Preview.BddPreviewRunState(false, $"{suite.Name} runs its scenarios with [Theory], so one has no address to run by. Change it to [BddScenarioTheory]; Test Explorer still runs them.");
    }

    // A screenshot under the results folder, through the virtual host the client maps to it.
    private string? ImageUrl(string path, string? root)
    {
        if (FileImageUrls)
        {
            return new Uri(path).AbsoluteUri;
        }

        if (root is null || !Path.GetFullPath(path).StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relative = Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar).Select(Uri.EscapeDataString);
        return $"https://{ResultsHost}/{string.Join('/', relative)}";
    }

    // A run writes its manifest after each scenario: the open files' previews are told to refresh,
    // once the writes pause. Each suite's results folder is watched; most suites share one.
    private void WatchResults(IEnumerable<string> roots)
    {
        foreach (var watcher in _resultsWatchers)
        {
            watcher.Dispose();
        }

        _resultsWatchers.Clear();
        _resultsChanged ??= new Timer(_ => NotifyResultsChanged());
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            // A folder the first run creates: watched from its parent, which exists.
            var parent = Path.GetDirectoryName(root);
            if (!Directory.Exists(root) && (parent is null || !Directory.Exists(parent)))
            {
                continue;
            }

            var watcher = Directory.Exists(root)
                ? new FileSystemWatcher(root, "manifest.json") { IncludeSubdirectories = true }
                : new FileSystemWatcher(parent!, "manifest.json") { IncludeSubdirectories = true };
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            watcher.Changed += (_, _) => _resultsChanged.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
            watcher.Created += (_, _) => _resultsChanged.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
            watcher.EnableRaisingEvents = true;
            _resultsWatchers.Add(watcher);
        }
    }

    private void NotifyResultsChanged()
    {
        List<string> open;
        lock (_gate)
        {
            open = _documents.Keys.Where(path => BddFileFormat.IsScenarioFile(path)).ToList();
        }

        foreach (var path in open)
        {
            _ = _rpc?.NotifyWithParameterObjectAsync("bruinet/resultsChanged", new { uri = new Uri(path).AbsoluteUri });
            ScheduleDiagnostics(path);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeScraper();
        DisposeRun();
        DisposeWarm();
        DisposeStep();
        lock (_gate)
        {
            foreach (var watcher in _resultsWatchers)
            {
                watcher.Dispose();
            }

            _resultsWatchers.Clear();
            _resultsChanged?.Dispose();
            _resultsChanged = null;
            foreach (var timer in _pending.Values)
            {
                timer.Dispose();
            }

            _pending.Clear();
            _workspace?.Dispose();
            _workspace = null;
        }
    }

    // The suite whose own project holds the file first, then the ones that link it.
    private static IReadOnlyList<BddSuite> Order(string path, IReadOnlyList<BddSuite> suites)
        => suites
            .OrderBy(suite => path.StartsWith(suite.ProjectDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(suite => suite.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static SuiteInfo Info(BddSuite suite) => new(suite.Name, suite.ConfigPath, TryHead(suite));

    private static string? TryHead(BddSuite suite)
    {
        try
        {
            return suite.Head;
        }
        catch (BddWorkspaceException)
        {
            return null;
        }
    }

    private static string Key(string uri) => Path.GetFullPath(Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile ? LocalPath(parsed) : uri);

    // VS Code escapes the drive's colon (file:///e%3A/...), which .NET reads as /e:/...
    private static string LocalPath(Uri uri)
    {
        var local = uri.LocalPath;
        return local.Length > 2 && local[0] == '/' && char.IsAsciiLetter(local[1]) && local[2] == ':' ? local[1..] : local;
    }
}
