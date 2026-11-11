using Bruinet.Scraper.Snapshots;
using Bruinet.Scraper.Code;
using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Recordings;
using Bruinet.Scraper.Testing;

namespace Bruinet.Scraper.Service;

/// <summary>
/// What the IDE view and an agent ask of the Scraper, over the scraped projects under a solution: one
/// recording at a time, and reading what is recorded. The language server and the MCP server are thin
/// adapters over it. It calls no model.
/// </summary>
public sealed partial class ScraperService : IAsyncDisposable
{
    private readonly string _solutionDir;
    private readonly Func<string, IScrapeSource> _sourceFor;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Recording? _recording;
    private CancellationTokenSource? _exploring;
    private TaskCompletionSource? _signIn;
    private readonly ITemplateGenerator? _templates;

    /// <summary>A service for the projects under <paramref name="solutionDir"/>.</summary>
    /// <param name="solutionDir">The solution's folder.</param>
    /// <param name="sourceFor">Makes the source for a platform; the Html source when null.</param>
    /// <param name="templates">Generates a project's control templates before it builds; null to build only.</param>
    public ScraperService(string solutionDir, Func<string, IScrapeSource>? sourceFor = null, ITemplateGenerator? templates = null)
    {
        _templates = templates;
        // A solution file names its folder: the IDE may pass either.
        var full = Path.GetFullPath(solutionDir);
        _solutionDir = File.Exists(full) ? Path.GetDirectoryName(full)! : full;
        _sourceFor = sourceFor ?? DefaultSource;
    }

    /// <summary>Raised after a page is captured or the recording starts or stops.</summary>
    public event EventHandler<ScraperChange>? Changed;

    /// <summary>The recording in progress, or null.</summary>
    public RecordingState? Active => _recording?.State;

    /// <summary>The scraped projects: those with a <c>scraper.config.md</c>.</summary>
    public IReadOnlyList<ScrapeProject> Projects() => ScrapeProject.FindAll(_solutionDir);

    /// <summary>A project by name, or the only one when <paramref name="name"/> is null.</summary>
    /// <exception cref="ScraperProblem">No such project, or several and no name.</exception>
    public ScrapeProject Project(string? name)
    {
        var projects = Projects();
        if (projects.Count == 0)
            throw new ScraperProblem($"No scraped project under {_solutionDir}: add a {ScraperConfig.FileName} to a UI test project.");
        if (name is null)
            return projects.Count == 1 ? projects[0] : throw new ScraperProblem($"Say which project: {string.Join(", ", projects.Select(p => p.Name))}.");
        // A name, or a label as the picker shows it, or a folder relative to the solution.
        var matches = projects.Where(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Label(p, projects), name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Relative(p), name.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)).ToList();
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new ScraperProblem($"No scraped project '{name}'. They are {string.Join(", ", projects.Select(p => Label(p, projects)))}."),
            _ => throw new ScraperProblem($"Several projects are named {name}: {string.Join(", ", matches.Select(Relative))}. Say which by its folder."),
        };
    }

    /// <summary>How a project is shown: its name, with its folder when another project has the same name.</summary>
    public string Label(ScrapeProject project, IReadOnlyList<ScrapeProject> all) =>
        all.Count(p => string.Equals(p.Name, project.Name, StringComparison.OrdinalIgnoreCase)) > 1 ? $"{project.Name} ({Relative(project)})" : project.Name;

    /// <summary>A project's folder relative to the solution, with forward slashes.</summary>
    public string Relative(ScrapeProject project) => Path.GetRelativePath(_solutionDir, project.Folder).Replace('\\', '/');

    /// <summary>
    /// Opens the recording browser on a project's start URL (or <paramref name="url"/>) and starts a
    /// session. With <paramref name="autoCapture"/>, each page the app moves to is captured once it settles.
    /// With <paramref name="actions"/>, it is a test recording: what the person does is recorded too, in order.
    /// </summary>
    public async Task<RecordingState> StartRecordingAsync(string? projectName, string sessionName, string? url = null, bool autoCapture = true, bool visible = true, bool actions = false, CancellationToken cancellationToken = default)
    {
        var project = Project(projectName);
        var start = url ?? project.Config.StartUrl;
        if (start.Length == 0)
            throw new ScraperProblem($"{project.Name} has no StartUrl in its {ScraperConfig.FileName}; give a url.");

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_recording is not null)
                throw new ScraperProblem($"A recording is in progress ({_recording.State.Session} in {_recording.State.Project}); stop it first.");
            var source = _sourceFor(project.Config.Platform);
            try
            {
                await source.StartAsync(new ScrapeStart(start, project.ProfileFolder, visible), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await source.DisposeAsync().ConfigureAwait(false);
                throw;
            }
            var session = project.StartSession(sessionName, DateTimeOffset.Now);
            _recording = new Recording(project, session, source, autoCapture, actions);
            if (actions)
            {
                // A transition is a navigate action then: its capture follows it in the same order.
                source.Acted += OnActed;
                source.Commanded += OnCommanded;
                await source.StartActionsAsync(new ScrapeActionOptions(project.Config.TestIds, project.Config.Secrets), cancellationToken).ConfigureAwait(false);
            }
            else if (autoCapture)
            {
                source.Transitioned += OnTransitioned;
            }
        }
        finally
        {
            _gate.Release();
        }
        Changed?.Invoke(this, new ScraperChange(ScraperChangeKind.Started, _recording.State, null));
        if (autoCapture)
            await CapturePageAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return _recording.State;
    }

    /// <summary>
    /// Captures the page the recording browser shows now into the session. Returns null when nothing
    /// changed since the last capture of the same page.
    /// </summary>
    public async Task<RecordingEvent?> CapturePageAsync(string? pageName = null, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        RecordingEvent? added;
        Recording recording;
        try
        {
            recording = _recording ?? throw new ScraperProblem("No recording is in progress: start one first.");
            var snapshot = await recording.Source.CaptureAsync(cancellationToken).ConfigureAwait(false);
            if (pageName is null && recording.IsUnchanged(snapshot))
                return null;
            var screenshot = recording.Project.Config.Screenshots ? await recording.Source.ScreenshotAsync(cancellationToken).ConfigureAwait(false) : null;
            added = recording.Session.AddPage(snapshot, screenshot, pageName);
            recording.Remember(snapshot);
        }
        finally
        {
            _gate.Release();
        }
        Changed?.Invoke(this, new ScraperChange(ScraperChangeKind.Captured, recording.State, added));
        return added;
    }

    /// <summary>
    /// Explores a project's app by itself (see <see cref="Explorer"/>): a new session, guarded by the
    /// project's Explore settings and this run's overrides, with its report as <c>explore.md</c> in the
    /// session. The recording is stopped at the end.
    /// </summary>
    public async Task<ExploreResult> ExploreAsync(string? projectName, ExploreOptions options, CancellationToken cancellationToken = default)
    {
        var project = Project(projectName);
        var settings = options.Apply(project.Config.Explore);
        var start = options.Url ?? project.Config.StartUrl;
        if (start.Length == 0)
            throw new ScraperProblem($"{project.Name} has no StartUrl in its {ScraperConfig.FileName}; give a url.");
        if (Uri.TryCreate(start, UriKind.Absolute, out var startUri) && RequestGuard.RefuseHost(startUri, settings) is { } refused)
            return new ExploreResult(ExploreStop.HostRefused, refused, options.DryRun, settings, [], [], [], [], [], [], 0, TimeSpan.Zero);

        // The browser opens blank, so nothing of the app loads before the guards are on.
        await StartRecordingAsync(project.Name, options.DryRun ? "explore-dry-run" : "explore", "about:blank", autoCapture: false, visible: options.Visible, cancellationToken: cancellationToken).ConfigureAwait(false);
        var recording = _recording!;
        if (recording.Source is not IExploreSource source)
        {
            await StopRecordingAsync(cancellationToken).ConfigureAwait(false);
            throw new ScraperProblem($"{project.Config.Platform} apps cannot be explored yet: html and blazor can.");
        }

        using var exploring = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _exploring = exploring;
        var run = options with
        {
            WaitForSignIn = options.StopAtSignIn ? null : options.WaitForSignIn ?? (options.Visible ? WaitForSignInAsync : null),
            // The view follows along through Changed; the caller's own progress too.
            Progress = new Relay(p =>
            {
                options.Progress?.Report(p);
                Changed?.Invoke(this, new ScraperChange(ScraperChangeKind.Exploring, _recording?.State, null, p.Doing) { Progress = p });
            }),
        };
        ExploreResult result;
        try
        {
            result = await new Explorer(project, source, settings, run, new ExploreIo(ExploreCaptureAsync, ExploreSaveAsync))
                .RunAsync(start, exploring.Token).ConfigureAwait(false);
        }
        finally
        {
            _exploring = null;
            _signIn?.TrySetCanceled(cancellationToken);
            _signIn = null;
        }
        var report = Path.Combine(recording.Session.Folder, ExploreReport.FileName);
        await File.WriteAllTextAsync(report, ExploreReport.Markdown(result, project.Name, start), CancellationToken.None).ConfigureAwait(false);
        await StopRecordingAsync(CancellationToken.None).ConfigureAwait(false);
        result = result with { Folder = recording.Session.Folder, Report = report };
        Changed?.Invoke(this, new ScraperChange(ScraperChangeKind.Explored, null, null, $"{result.Stop}: {result.StopDetail}") { Explored = result });
        return result;
    }

    /// <summary>
    /// Makes one change to a project's <c>scraper.config.md</c> (see <see cref="ScraperConfigWriter.Apply"/>),
    /// keeping the rest of the file; written only when <paramref name="write"/> and the result still reads.
    /// </summary>
    /// <exception cref="ScraperProblem">The change is not one, or the config would not read after it.</exception>
    public ConfigEdit EditConfig(string? projectName, ConfigChange change, bool write = true)
    {
        var project = Project(projectName);
        var path = Path.Combine(project.Folder, ScraperConfig.FileName);
        var text = File.ReadAllText(path);
        ConfigEdit edit;
        try
        {
            edit = ScraperConfigWriter.Apply(text, change);
            ScraperConfig.Parse(edit.Text);
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            throw new ScraperProblem($"{project.Name}'s config was not changed: {e.Message}");
        }
        if (write && edit.Changed)
            File.WriteAllText(path, edit.Text);
        return edit;
    }

    /// <summary>The UI test projects under the solution with no <c>scraper.config.md</c> yet: where Create config can write one.</summary>
    public IReadOnlyList<string> ConfigCandidates()
    {
        var scraped = Projects().Select(p => p.Folder).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var found = new List<string>();
        var pending = new Stack<string>([_solutionDir]);
        while (pending.Count > 0)
        {
            var folder = pending.Pop();
            if (!scraped.Contains(folder) && Directory.EnumerateFiles(folder, "*.csproj").Any(f => Path.GetFileNameWithoutExtension(f).EndsWith("Tests", StringComparison.OrdinalIgnoreCase)))
                found.Add(folder);
            foreach (var child in Directory.EnumerateDirectories(folder))
            {
                var name = Path.GetFileName(child);
                if (name is "bin" or "obj" or "node_modules" or ScrapeProject.RecordingsFolderName || name.StartsWith('.'))
                    continue;
                pending.Push(child);
            }
        }
        return found.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Writes a <c>scraper.config.md</c> into a UI test project with its start URL; the rest keeps the defaults.</summary>
    /// <exception cref="ScraperProblem">The folder is not under the solution, has no project, or has a config already.</exception>
    public ScrapeProject CreateConfig(string folder, string startUrl)
    {
        var full = Path.GetFullPath(folder);
        if (!full.StartsWith(_solutionDir, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(full) || !Directory.EnumerateFiles(full, "*.csproj").Any())
            throw new ScraperProblem($"{folder} is not a project folder under {_solutionDir}.");
        var path = Path.Combine(full, ScraperConfig.FileName);
        if (File.Exists(path))
            throw new ScraperProblem($"{path} is there already.");
        if (!Uri.TryCreate(startUrl, UriKind.Absolute, out _))
            throw new ScraperProblem($"'{startUrl}' is not an address to start at.");
        File.WriteAllText(path, ScraperConfigWriter.New(startUrl, target: ScrapeProject.DefaultTestTarget(full)));
        return ScrapeProject.Open(full);
    }

    /// <summary>What the new project form proposes for a project named <paramref name="name"/>.</summary>
    public NewProjectDefaults NewProjectDefaults(string name) => ProjectMaker.Defaults(_solutionDir, name);

    /// <summary>What is wrong with a new project, by field; empty when it can be made.</summary>
    public IReadOnlyDictionary<string, string> NewProjectProblems(NewProject request) => ProjectMaker.Problems(_solutionDir, request);

    /// <summary>Makes a scraped project: its <c>.csproj</c>, <c>scraper.config.md</c> and <c>Pages/</c>.</summary>
    /// <exception cref="ScraperProblem">The request has problems (<see cref="NewProjectProblems"/>).</exception>
    public ScrapeProject NewProject(NewProject request)
    {
        try
        {
            return ScrapeProject.Open(ProjectMaker.Create(_solutionDir, request));
        }
        catch (ArgumentException e)
        {
            throw new ScraperProblem(e.Message);
        }
    }

    /// <summary>What Explore would run with: the project, where it starts, and its budget and guards, to show a person before it starts.</summary>
    public string ExploreSummary(string? projectName, ExploreOptions options)
    {
        var project = Project(projectName);
        return ExploreReport.Summary(project.Name, options.Url ?? project.Config.StartUrl, options.Apply(project.Config.Explore), options.DryRun);
    }

    /// <summary>Where the recording browser is, or null when nothing is recorded.</summary>
    public string? CurrentLocation => _recording?.Source is IExploreSource source ? source.Location : null;

    /// <summary>
    /// How the config names a node of a page's newest capture for <c>Never click</c> or <c>Always click</c>:
    /// by a selector when it has a test id or a stable id, else by its text.
    /// </summary>
    public string ElementMatchFor(string? projectName, string page, int node)
    {
        var project = Project(projectName);
        var (_, snapshot) = Snapshot(project.Name, page);
        if (node < 1 || node > snapshot.Nodes.Count)
            throw new ScraperProblem($"Page '{page}' has nodes 1 to {snapshot.Nodes.Count}, not {node}.");
        var n = snapshot[node];
        foreach (var testId in project.Config.TestIds)
            if (n.Attribute(testId) is { Length: > 0 } value)
                return $"selector: [{testId}='{value}']";
        var text = n.Name ?? n.Text ?? snapshot.ChildrenOf(node).Select(c => c.Text).FirstOrDefault(t => t is { Length: > 0 });
        if (text is { Length: > 0 })
            return $"text: {text}";
        if (n.Attribute("id") is { Length: > 0 } id)
            return $"selector: #{id}";
        throw new ScraperProblem($"Node #{node} has no test id, text or id to name it by: name it in the config by hand.");
    }

    /// <summary>True while Explore runs.</summary>
    public bool IsExploring => _exploring is not null;

    /// <summary>True while Explore waits at a sign-in page for <see cref="ContinueExploring"/>.</summary>
    public bool IsWaitingForSignIn => _signIn is { Task.IsCompleted: false };

    /// <summary>Stops Explore; what it recorded stays.</summary>
    public void StopExploring() => _exploring?.Cancel();

    /// <summary>Goes on after the person signed in, where Explore stopped at a sign-in page.</summary>
    public void ContinueExploring() => _signIn?.TrySetResult();

    private Task WaitForSignInAsync(CancellationToken cancellationToken)
    {
        _signIn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Changed?.Invoke(this, new ScraperChange(ScraperChangeKind.SignIn, _recording?.State, null, "Sign in in the recording browser, then Continue."));
        return _signIn.Task.WaitAsync(cancellationToken);
    }

    private async Task<ScrapeSnapshot> ExploreCaptureAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var recording = _recording ?? throw new ScraperProblem("The recording was stopped.");
            return await recording.Source.CaptureAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<RecordingEvent> ExploreSaveAsync(ScrapeSnapshot snapshot, string? name, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        RecordingEvent added;
        Recording recording;
        try
        {
            recording = _recording ?? throw new ScraperProblem("The recording was stopped.");
            var screenshot = recording.Project.Config.Screenshots ? await recording.Source.ScreenshotAsync(cancellationToken).ConfigureAwait(false) : null;
            added = recording.Session.AddPage(snapshot, screenshot, name);
            recording.Remember(snapshot);
        }
        finally
        {
            _gate.Release();
        }
        Changed?.Invoke(this, new ScraperChange(ScraperChangeKind.Captured, recording.State, added));
        return added;
    }

    /// <summary>Closes the recording browser; the session stays as it is on disk.</summary>
    public async Task<RecordingState?> StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        Recording? stopped;
        try
        {
            _exploring?.Cancel();
            stopped = _recording;
            _recording = null;
            if (stopped is null)
                return null;
            stopped.Source.Transitioned -= OnTransitioned;
            stopped.Source.Acted -= OnActed;
            stopped.Source.Commanded -= OnCommanded;
            stopped.Source.StopActions();
            await stopped.Source.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
        Changed?.Invoke(this, new ScraperChange(ScraperChangeKind.Stopped, stopped.State, null));
        return stopped.State;
    }

    /// <summary>Outlines a node of the last capture in the recording browser, or removes the outline.</summary>
    public Task HighlightAsync(int? node, CancellationToken cancellationToken = default) =>
        (_recording ?? throw new ScraperProblem("No recording is in progress.")).Source.HighlightAsync(node, cancellationToken);

    /// <summary>Captures the page, then lets the user click a node in the recording browser; its number in that capture, or null.</summary>
    public async Task<(RecordingEvent? Capture, int? Node)> PickAsync(CancellationToken cancellationToken = default)
    {
        var capture = await CapturePageAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var recording = _recording ?? throw new ScraperProblem("No recording is in progress.");
        return (capture ?? recording.Session.Events.LastOrDefault(), await recording.Source.PickAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>A project's recorded pages: key, newest title, how many captures, and the newest capture's session and event.</summary>
    public IReadOnlyList<RecordedPage> Pages(string? projectName) => Project(projectName).Index().Pages;

    /// <summary>A capture's snapshot: the newest of a page, or a given session's event.</summary>
    /// <exception cref="ScraperProblem">No such page or capture.</exception>
    public (PageCapture Capture, ScrapeSnapshot Snapshot) Snapshot(string? projectName, string page, string? session = null, int? number = null)
    {
        var index = Project(projectName).Index();
        var recorded = index.Page(page) ?? throw new ScraperProblem($"No recorded page '{page}'. The pages are {string.Join(", ", index.Pages.Select(p => p.Key))}.");
        var capture = session is null
            ? recorded.Latest
            : recorded.Captures.FirstOrDefault(c => c.Session == session && (number is null || c.Event.Number == number))
              ?? throw new ScraperProblem($"Page '{page}' has no capture in session '{session}'{(number is null ? "" : $" numbered {number}")}.");
        return (capture, index.Read(capture));
    }

    /// <summary>The nodes on each page's newest capture with the same signature as a node (<see cref="NodeSignature"/>).</summary>
    public IReadOnlyList<SimilarNode> Similar(string? projectName, string page, int node)
    {
        var project = Project(projectName);
        var index = project.Index();
        var (_, snapshot) = Snapshot(project.Name, page);
        if (node < 1 || node > snapshot.Nodes.Count)
            throw new ScraperProblem($"Page '{page}' has nodes 1 to {snapshot.Nodes.Count}, not {node}.");
        var signature = NodeSignature.Of(snapshot, snapshot[node]);
        var found = new List<SimilarNode>();
        foreach (var recorded in index.Pages)
        {
            var other = index.Read(recorded.Latest);
            found.AddRange(other.Nodes.Where(n => NodeSignature.Of(other, n) == signature).Select(n => new SimilarNode(recorded.Key, n.Number, SnapshotOutline.Line(n))));
        }
        return found;
    }

    /// <summary>The nodes whose text, name or an attribute holds <paramref name="text"/>, on each page's newest capture.</summary>
    public IReadOnlyList<SimilarNode> Search(string? projectName, string text, int max = 100) =>
        Project(projectName).Index().Search(text).Take(max).Select(f => new SimilarNode(f.Page.Key, f.Node.Number, SnapshotOutline.Line(f.Node))).ToArray();

    /// <summary>The page objects and controls a project has, and those of the scraped projects it references.</summary>
    public IReadOnlyList<CodeClass> Controls(string? projectName) => ProjectCode.Read(Project(projectName));

    /// <summary>What the recorded pages are built from: repeated parts and structure, most widespread first.</summary>
    public IReadOnlyList<Pattern> Patterns(string? projectName, int max = 30)
    {
        var project = Project(projectName);
        var index = project.Index();
        var pages = index.Pages.Select(p => (p.Key, index.Read(p.Latest))).ToArray();
        return PatternDetector.Detect(pages, ProjectCode.Read(project), max, project.Config.TestIds, project.Config.Components);
    }

    /// <summary>
    /// The brief for making a control from a pattern: where to write it, what to read, the facts of
    /// its occurrences and the hints.
    /// </summary>
    /// <param name="projectName">The project.</param>
    /// <param name="signature">The pattern's signature.</param>
    /// <param name="name">The control's name; the pattern's own when null.</param>
    /// <param name="variant">Which variant of the pattern, when it has several (<see cref="Pattern.Variant"/>).</param>
    /// <param name="lifted">Whether the pattern is lifted to its roots (<see cref="Pattern.Lifted"/>).</param>
    public ControlBriefText ControlBrief(string? projectName, string signature, string? name = null, string? variant = null, bool lifted = false)
    {
        var project = Project(projectName);
        return Code.ControlBrief.Write(project, signature, variant, lifted, name, (page, node) => Header(project.Name, page, node, variant, lifted), BruinetRoot(project.Folder));
    }

    // The Bruinet repository holding the skills and the base controls, when the project is inside it
    // or the solution is.
    private string? BruinetRoot(string from)
    {
        foreach (var start in new[] { from, _solutionDir })
        {
            for (var folder = new DirectoryInfo(start); folder is not null; folder = folder.Parent)
            {
                if (File.Exists(Path.Combine(folder.FullName, ".github", "skills", "html-control", "SKILL.md")))
                    return folder.FullName;
            }
        }
        return null;
    }

    /// <summary>The header line a file made from something recorded starts with.</summary>
    /// <param name="projectName">The project.</param>
    /// <param name="page">The page key.</param>
    /// <param name="node">A node on its newest capture, for a control; null for the page object.</param>
    /// <param name="variant">For a control made from one variant of a pattern: the variant.</param>
    /// <param name="lifted">For a control made from a lifted pattern (<see cref="Pattern.Lifted"/>): its root is the node lifted.</param>
    public string Header(string? projectName, string page, int? node = null, string? variant = null, bool lifted = false)
    {
        var (capture, snapshot) = Snapshot(projectName, page);
        var from = $"{capture.Session}/{capture.Event.Snapshot}";
        if (node is null)
            return new ScrapedHeader { Page = page, From = from, Hash = SnapshotFile.ContentHash(snapshot) }.ToString();
        if (node < 1 || node > snapshot.Nodes.Count)
            throw new ScraperProblem($"Page '{page}' has nodes 1 to {snapshot.Nodes.Count}, not {node}.");
        return new ScrapedHeader { Control = NodeSignature.Of(snapshot, snapshot[node.Value]), Variant = variant, Lifted = lifted, From = $"{from}#{node}" }.ToString();
    }

    /// <summary>
    /// Which of the project's controls occur on a page's newest capture (by their header's signature),
    /// and the page object made for the page, if there is one.
    /// </summary>
    public ControlMatches Match(string? projectName, string page)
    {
        var classes = Controls(projectName);
        var (_, snapshot) = Snapshot(projectName, page);
        var bySignature = classes.Where(c => c.Header?.Control is not null).ToLookup(c => c.Header!.Control!, StringComparer.Ordinal);
        var matches = snapshot.Nodes
            .Select(n => (Node: n, Controls: bySignature[NodeSignature.Of(snapshot, n)].ToArray()))
            .Where(m => m.Controls.Length > 0)
            .Select(m => new ControlMatch(m.Node.Number, SnapshotOutline.Line(m.Node), m.Controls.Select(c => c.Name).ToArray()))
            .ToArray();
        var pageObject = classes.FirstOrDefault(c => c.Header?.Page == page);
        return new ControlMatches(page, pageObject?.Name, pageObject?.Header?.Hash is { } hash ? hash != SnapshotFile.ContentHash(snapshot) : null, matches);
    }

    /// <summary>
    /// The members a page object would have for a page's newest capture (or a node's subtree of it): a
    /// name, the control that fits, and the locator, in the preference order, with whether it is stable.
    /// </summary>
    public IReadOnlyList<ControlSuggestion> Suggest(string? projectName, string page, int? node = null)
    {
        var project = Project(projectName);
        var (_, snapshot) = Snapshot(project.Name, page);
        if (node is { } n && (n < 1 || n > snapshot.Nodes.Count))
            throw new ScraperProblem($"Page '{page}' has nodes 1 to {snapshot.Nodes.Count}, not {n}.");
        return ControlAdvisor.Suggest(snapshot, project.Config.TestIds, project.Config.Platform == "blazor", node ?? 0);
    }

    /// <summary>
    /// What changed on a page between two captures: by default from the capture its page object was
    /// made from (its header) or else the one before the newest, to the newest.
    /// </summary>
    public PageDiff Diff(string? projectName, string page, string? fromSession = null, int? fromNumber = null, string? toSession = null, int? toNumber = null)
    {
        var project = Project(projectName);
        var index = project.Index();
        var recorded = index.Page(page) ?? throw new ScraperProblem($"No recorded page '{page}'.");
        PageCapture Pick(string session, int? number) =>
            recorded.Captures.FirstOrDefault(c => c.Session == session && (number is null || c.Event.Number == number))
            ?? throw new ScraperProblem($"Page '{page}' has no capture in session '{session}'{(number is null ? "" : $" numbered {number}")}.");
        var to = toSession is null ? recorded.Latest : Pick(toSession, toNumber);
        PageCapture from;
        if (fromSession is not null)
        {
            from = Pick(fromSession, fromNumber);
        }
        else if (ProjectCode.Read(project).FirstOrDefault(c => c.Header?.Page == page)?.Header?.From is { } made
                 && recorded.Captures.FirstOrDefault(c => $"{c.Session}/{c.Event.Snapshot}" == made) is { } source)
        {
            from = source;
        }
        else
        {
            from = recorded.Captures.Count > 1 ? recorded.Captures[1] : throw new ScraperProblem($"Page '{page}' has one capture: record it again to compare.");
        }
        return SnapshotDiff.Compare(index.Read(from), index.Read(to));
    }

    /// <summary>For each recorded page: the page object made for it, and whether the page changed since (null: no page object, or no hash).</summary>
    public IReadOnlyDictionary<string, (string? PageObject, bool? Changed)> PageObjects(string? projectName)
    {
        var project = Project(projectName);
        var index = project.Index();
        var made = ProjectCode.Read(project).Where(c => c.Header?.Page is not null).GroupBy(c => c.Header!.Page!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        return index.Pages.ToDictionary(
            p => p.Key,
            p => made.TryGetValue(p.Key, out var code)
                ? ((string?)code.Name, code.Header!.Hash is { } hash ? hash != SnapshotFile.ContentHash(index.Read(PageDrafter.Basis(p))) : (bool?)null)
                : ((string?)null, (bool?)null),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Drafts a page object for every recorded page, or one (see <see cref="DraftRun"/>), from rules and no
    /// model; a draft the project does not build with is put back. Also names the repeated patterns that
    /// have no control yet: controls need a judgement, so they are the agent's or a person's.
    /// </summary>
    public async Task<DraftResult> DraftAsync(string? projectName, string? page = null, CancellationToken cancellationToken = default)
    {
        var project = Project(projectName);
        DraftResult result;
        try
        {
            result = await DraftRun.RunAsync(project, page is null ? null : [page], _templates, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException e)
        {
            throw new ScraperProblem(e.Message);
        }
        return result with { MakeControls = DraftRun.ControlsToMake(Patterns(project.Name)) };
    }

    /// <summary>
    /// Checks the project's page objects against the running app (see <see cref="PageVerifier"/>), in a
    /// browser of its own with the project's profile, under the Explore guards.
    /// </summary>
    public async Task<VerifyResult> VerifyAsync(string? projectName, bool visible = false, CancellationToken cancellationToken = default)
    {
        var project = Project(projectName);
        if (_recording is not null)
            throw new ScraperProblem("A recording is in progress, and it holds the project's browser profile: stop it first.");
        if (_sourceFor(project.Config.Platform) is not PlaywrightScrapeSource source)
            throw new ScraperProblem($"{project.Config.Platform} page objects cannot be verified yet: html and blazor can.");
        await using (source.ConfigureAwait(false))
        {
            await source.StartAsync(new ScrapeStart("about:blank", project.ProfileFolder, visible), cancellationToken).ConfigureAwait(false);
            await source.GuardAsync(new RequestGuard(project.Config.Explore)).ConfigureAwait(false);
            var result = await PageVerifier.RunAsync(project, source.Page!, () => source.CaptureAsync(cancellationToken)).ConfigureAwait(false);
            Remember(project, result);
            return result;
        }
    }

    /// <summary>Resolves a test recording's actions to page-object members, verbs and phrases, and its gaps (<see cref="TestResolver"/>).</summary>
    /// <exception cref="ScraperProblem">No such session.</exception>
    public ResolvedTest ResolveActions(string? projectName, string session)
    {
        var project = Project(projectName);
        return TestResolver.Resolve(project, project.Session(session) ?? throw new ScraperProblem($"{project.Name} has no recording session '{session}'."));
    }

    /// <summary>
    /// Drafts a test recording as an xUnit test, a scenario or both (<see cref="TestDrafter"/>), into the
    /// project, and gives what it wrote and its gaps. The target is the project's <c>## Tests</c> setting
    /// unless one is given.
    /// </summary>
    /// <exception cref="ScraperProblem">No such session.</exception>
    public DraftTestResult DraftTest(string? projectName, string session, TestTarget? target = null, string? name = null)
    {
        var project = Project(projectName);
        var recorded = project.Session(session) ?? throw new ScraperProblem($"{project.Name} has no recording session '{session}'.");
        var test = TestResolver.Resolve(project, recorded);
        var draft = TestDrafter.Draft(project, test, new RecordedHeader(recorded.Id, recorded.Hash()), target ?? project.TestTarget, name);
        return new DraftTestResult(target ?? project.TestTarget, TestDrafter.Write(project, draft), draft.Problems, test.Gaps);
    }

    /// <summary>Writes a C# file into the project, when its syntax is valid.</summary>
    public WriteResult WriteCode(string? projectName, string path, string code) => CodeGate.Write(Project(projectName), path, code);

    /// <summary>Builds the project: the gate every generated file must pass.</summary>
    public Task<BuildResult> CheckCodeAsync(string? projectName, CancellationToken cancellationToken = default) =>
        CodeGate.BuildAsync(Project(projectName), _templates, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopRecordingAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private static IScrapeSource DefaultSource(string platform) => platform switch
    {
        "html" or "blazor" => new PlaywrightScrapeSource(),
        _ => throw new ScraperProblem($"No source records '{platform}' yet: html and blazor are recorded."),
    };

    // A transition captures once the page settles; a page elsewhere (a sign-in provider) is not captured.
    private void OnTransitioned(object? sender, ScrapeTransition transition)
    {
        if (_recording is not { } recording || !recording.Project.IsOwnPage(transition.Location))
            return;
        _ = CaptureOnTransitionAsync();
    }

    /// <summary>
    /// In a test recording: lets the person click an element in the recording browser and choose a check
    /// (R5), after the page is captured, and records it in turn with the actions. Null when they cancel.
    /// </summary>
    /// <exception cref="ScraperProblem">No test recording is in progress.</exception>
    public Task<RecordingEvent?> AssertAsync(CancellationToken cancellationToken = default)
    {
        var recording = _recording ?? throw new ScraperProblem("No recording is in progress: start a test recording first.");
        if (!recording.Actions)
            throw new ScraperProblem($"{recording.State.Session} is not a test recording: an assert goes with recorded actions.");
        var done = new TaskCompletionSource<RecordingEvent?>(TaskCreationOptions.RunContinuationsAsynchronously);
        recording.Then(async () =>
        {
            try
            {
                done.TrySetResult(await AssertNowAsync(recording, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception e)
            {
                done.TrySetException(e);
            }
        });
        return done.Task;
    }

    private async Task<RecordingEvent?> AssertNowAsync(Recording recording, CancellationToken cancellationToken)
    {
        var capture = await CapturePageAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (await recording.Source.PickAssertAsync(cancellationToken).ConfigureAwait(false) is not { } picked)
            return null;
        RecordingEvent added;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_recording != recording)
                return null;
            added = recording.Session.AddAction(new RecordingEvent
            {
                Kind = RecordingEvent.AssertKind,
                At = DateTimeOffset.UtcNow,
                Location = picked.Location,
                Page = PageKey.Of(picked.Location),
                Node = picked.Node,
                Capture = picked.Node is null ? null : capture?.Number,
                Check = picked.Check,
                Value = picked.Value,
                Checked = picked.Checked,
            });
        }
        finally
        {
            _gate.Release();
        }
        Changed?.Invoke(this, new ScraperChange(ScraperChangeKind.Acted, recording.State, added));
        await recording.Source.ShowCountAsync(recording.Session.Events.Count(e => e.IsAction), CancellationToken.None).ConfigureAwait(false);
        return added;
    }

    // The toolbar: Assert records a check in turn; Stop ends the recording as the view's Stop does.
    private void OnCommanded(object? sender, string command)
    {
        if (_recording is not { } recording)
            return;
        if (command == "assert")
            recording.Then(() => AssertNowSafeAsync(recording));
        else if (command == "stop")
            _ = StopRecordingAsync();
    }

    private async Task AssertNowSafeAsync(Recording recording)
    {
        try
        {
            await AssertNowAsync(recording, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e) when (e is ScraperProblem or InvalidOperationException or ArgumentException or Microsoft.Playwright.PlaywrightException)
        {
            Changed?.Invoke(this, new ScraperChange(ScraperChangeKind.Failed, recording.State, null, e.Message));
        }
    }

    private void OnActed(object? sender, ScrapeAction action)
    {
        if (_recording is { } recording)
            recording.Then(() => SaveActionReportedAsync(recording, action));
    }

    // The top of the save queue: nothing awaits it, so a save that fails is reported here or not at all.
    private async Task SaveActionReportedAsync(Recording recording, ScrapeAction action)
    {
        try
        {
            await SaveActionAsync(recording, action).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Changed?.Invoke(this, new ScraperChange(ScraperChangeKind.Failed, recording.State, null, $"The {action.Kind} action was not saved: {e.Message}"));
        }
    }

    // The capture taken for the action first (unless the page is unchanged since its last capture), then
    // the action in the capture its node is in. A navigation on an auto-capture recording captures after.
    private async Task SaveActionAsync(Recording recording, ScrapeAction action)
    {
        RecordingEvent added;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_recording != recording)
                return;
            RecordingEvent? capture = null;
            if (action.Capture is { } snapshot && !recording.IsUnchanged(snapshot))
            {
                capture = recording.Session.AddPage(snapshot);
                recording.Remember(snapshot);
            }
            if (capture is null && action.Node is not null && action.CapturedAt is { } at)
                capture = recording.Session.Events.LastOrDefault(e => e.Kind == RecordingEvent.PageKind && e.At == at);
            added = recording.Session.AddAction(new RecordingEvent
            {
                Kind = action.Kind,
                At = action.At,
                Location = action.Location,
                Page = PageKey.Of(action.Location),
                Node = action.Node,
                Capture = action.Node is null ? null : capture?.Number,
                Path = action.Path,
                Value = action.Secret is null ? action.Value : null,
                Placeholder = action.Secret,
                Checked = action.Checked,
                Accepted = action.Accepted,
            });
        }
        finally
        {
            _gate.Release();
        }
        Changed?.Invoke(this, new ScraperChange(ScraperChangeKind.Acted, recording.State, added));
        await recording.Source.ShowCountAsync(recording.Session.Events.Count(e => e.IsAction), CancellationToken.None).ConfigureAwait(false);
        if (action.Kind == ScrapeActionKind.Navigate && recording.AutoCapture && recording.Project.IsOwnPage(action.Location))
            await CaptureOnTransitionAsync().ConfigureAwait(false);
    }

    private async Task CaptureOnTransitionAsync()
    {
        try
        {
            await CapturePageAsync().ConfigureAwait(false);
        }
        catch (Exception e) when (e is ScraperProblem or InvalidOperationException or Microsoft.Playwright.PlaywrightException)
        {
            // The browser was closed or navigated on while capturing: the next transition captures again.
            Changed?.Invoke(this, new ScraperChange(ScraperChangeKind.Failed, _recording?.State, null, e.Message));
        }
    }

    private sealed class Recording(ScrapeProject project, RecordingSession session, IScrapeSource source, bool autoCapture, bool actions)
    {
        private readonly Dictionary<string, string> _lastBody = new(StringComparer.Ordinal);
        private readonly Lock _chainLock = new();
        private Task _chain = Task.CompletedTask;

        public ScrapeProject Project { get; } = project;

        public RecordingSession Session { get; } = session;

        public IScrapeSource Source { get; } = source;

        public bool AutoCapture => autoCapture;

        public bool Actions => actions;

        public RecordingState State => new(Project.Name, Session.Id, Session.Folder, autoCapture, Session.Events.Count, actions);

        // Runs after what was queued before: actions are saved in the order the source raised them.
        public void Then(Func<Task> next)
        {
            lock (_chainLock)
                _chain = _chain.ContinueWith(_ => next(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
        }

        public bool IsUnchanged(ScrapeSnapshot snapshot) =>
            _lastBody.TryGetValue(PageKey.Of(snapshot.Location), out var body) && body == Body(snapshot);

        public void Remember(ScrapeSnapshot snapshot) => _lastBody[PageKey.Of(snapshot.Location)] = Body(snapshot);

        // The nodes without the header: the same page captured twice, unchanged, has the same body.
        private static string Body(ScrapeSnapshot snapshot)
        {
            using var writer = new StringWriter();
            SnapshotFile.Write(snapshot with { CapturedAt = default }, writer);
            return writer.ToString();
        }
    }
}

/// <summary>The controls found on a page, the page object made for it, and whether the page changed since.</summary>
public sealed record ControlMatches(string Page, string? PageObject, bool? ChangedSinceGenerated, IReadOnlyList<ControlMatch> Matches);

/// <summary>A node that one or more of the project's controls stand for.</summary>
public sealed record ControlMatch(int Node, string Line, IReadOnlyList<string> Controls);

/// <summary>A recording in progress.</summary>
/// <param name="Project">The project's name.</param>
/// <param name="Session">The session's id.</param>
/// <param name="Folder">The session's folder.</param>
/// <param name="AutoCapture">Whether each page the app moves to is captured.</param>
/// <param name="Captures">How many events the session has: pages, and actions in a test recording.</param>
/// <param name="Actions">Whether it is a test recording, with the person's actions.</param>
public sealed record RecordingState(string Project, string Session, string Folder, bool AutoCapture, int Captures, bool Actions = false);

/// <summary>A drafted test: the target, each file written or why not, why a target was not drafted, and the gaps marked in it.</summary>
public sealed record DraftTestResult(TestTarget Target, IReadOnlyList<DraftWrite> Files, IReadOnlyList<string> Problems, IReadOnlyList<ResolvedStep> Gaps);

/// <summary>A node found on a page: its page key, its number on the page's newest capture, and its outline line.</summary>
public sealed record SimilarNode(string Page, int Node, string Line);

/// <summary>What changed in the service.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ScraperChangeKind>))]
public enum ScraperChangeKind
{
    /// <summary>A recording started.</summary>
    Started,

    /// <summary>A page was captured.</summary>
    Captured,

    /// <summary>The person took an action in a test recording: the change's event is the action.</summary>
    Acted,

    /// <summary>A recording stopped.</summary>
    Stopped,

    /// <summary>A capture on a transition, or the save of a recorded action, failed: the message says which.</summary>
    Failed,

    /// <summary>Explore reached a sign-in page and waits for the person to sign in and continue.</summary>
    SignIn,

    /// <summary>Explore took a step: the progress says where it is.</summary>
    Exploring,

    /// <summary>Explore ended: the result says why and what it did.</summary>
    Explored,
}

/// <summary>A change: its kind, the recording, the captured page if any, and a message for a failure.</summary>
public sealed record ScraperChange(ScraperChangeKind Kind, RecordingState? Recording, RecordingEvent? Captured, string? Message = null)
{
    /// <summary>For <see cref="ScraperChangeKind.Exploring"/>: where Explore is.</summary>
    public ExploreProgress? Progress { get; init; }

    /// <summary>For <see cref="ScraperChangeKind.Explored"/>: what it did.</summary>
    public ExploreResult? Explored { get; init; }
}

// Reports on the thread that reports: a view's progress line keeps up with the run.
internal sealed class Relay(Action<ExploreProgress> report) : IProgress<ExploreProgress>
{
    public void Report(ExploreProgress value) => report(value);
}

/// <summary>A request the Scraper cannot answer; the message says why and what to do.</summary>
public sealed class ScraperProblem(string message) : Exception(message);
