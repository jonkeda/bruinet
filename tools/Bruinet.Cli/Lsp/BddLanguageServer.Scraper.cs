using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;
using StreamJsonRpc;

namespace Bruinet.Bdd.LanguageServer;

// The Scraper: recording an app's pages into a UI test project, and reading them back, for the IDE's
// Scraper view. The work is ScraperService's; this is its JSON-RPC face. A change is sent to the
// client as bruinet/scraper/changed.
public sealed partial class BddLanguageServer
{
    private ScraperService? _scraper;

    /// <summary>The Scraper over the solution's scraped projects; the MCP server shares it.</summary>
    internal ScraperService Scraper
    {
        get
        {
            lock (_gate)
            {
                if (_scraper is null)
                {
                    _scraper = new ScraperService(_workspace?.Root ?? Environment.CurrentDirectory, templates: new Bruinet.Cli.Generate.ControlTemplates());
                    _scraper.Changed += (_, change) => _ = _rpc?.NotifyWithParameterObjectAsync("bruinet/scraper/changed", change);
                }

                return _scraper;
            }
        }
    }

    /// <summary><c>bruinet/scraper/host</c>: the Scraper view's page, which an IDE loads once and relays for.</summary>
    [JsonRpcMethod("bruinet/scraper/host")]
    public PreviewHostResult ScraperHost() => new(ScraperPage.Html);

    /// <summary><c>bruinet/scraper/similar</c>: the nodes of the same kind as a node, on every page's newest capture.</summary>
    [JsonRpcMethod("bruinet/scraper/similar")]
    public ScraperResult<IReadOnlyList<SimilarNode>> ScraperSimilar(ScraperPageNodeParams request)
        => Answer(() => Scraper.Similar(request.Project, request.Page, request.Node ?? 0));

    /// <summary><c>bruinet/scraper/suggest</c>: the control and locator for a node, or for each member-worthy node of a page.</summary>
    [JsonRpcMethod("bruinet/scraper/suggest")]
    public ScraperResult<IReadOnlyList<Bruinet.Scraper.Html.ControlSuggestion>> ScraperSuggest(ScraperPageNodeParams request)
        => Answer(() => Scraper.Suggest(request.Project, request.Page, request.Node));

    /// <summary><c>bruinet/scraper/header</c>: the first line of a file made from a page, or from one of its nodes.</summary>
    [JsonRpcMethod("bruinet/scraper/header")]
    public ScraperResult<string> ScraperHeader(ScraperPageNodeParams request)
        => Answer(() => Scraper.Header(request.Project, request.Page, request.Node));

    /// <summary><c>bruinet/scraper/projects</c>: the scraped projects and what each has recorded.</summary>
    [JsonRpcMethod("bruinet/scraper/projects")]
    public IReadOnlyList<ScraperProjectInfo> ScraperProjects()
    {
        var all = Scraper.Projects();
        return all.Select(project => new ScraperProjectInfo(
            project.Name,
            Scraper.Label(project, all),
            project.Folder,
            project.Config.Platform,
            project.Config.StartUrl,
            project.Sessions().Select(session => session.Id).ToList())).ToList();
    }

    /// <summary><c>bruinet/scraper/state</c>: the recording in progress, or null.</summary>
    [JsonRpcMethod("bruinet/scraper/state")]
    public RecordingState? ScraperState() => Scraper.Active;

    /// <summary><c>bruinet/scraper/start</c>: opens the recording browser and starts a session.</summary>
    [JsonRpcMethod("bruinet/scraper/start")]
    public Task<ScraperResult<RecordingState>> ScraperStart(ScraperStartParams request)
        => Answer(() => Scraper.StartRecordingAsync(request.Project, request.Session ?? "recording", request.Url, request.AutoCapture ?? true));

    /// <summary><c>bruinet/scraper/capture</c>: captures the page the browser shows; null when it did not change.</summary>
    [JsonRpcMethod("bruinet/scraper/capture")]
    public Task<ScraperResult<RecordingEvent?>> ScraperCapture(ScraperCaptureParams request)
        => Answer(() => Scraper.CapturePageAsync(request.Name));

    /// <summary><c>bruinet/scraper/stop</c>: closes the recording browser.</summary>
    [JsonRpcMethod("bruinet/scraper/stop")]
    public Task<ScraperResult<RecordingState?>> ScraperStop()
        => Answer(() => Scraper.StopRecordingAsync());

    /// <summary><c>bruinet/scraper/pages</c>: a project's recorded pages, by key.</summary>
    [JsonRpcMethod("bruinet/scraper/pages")]
    public ScraperResult<IReadOnlyList<ScraperPageInfo>> ScraperPages(ScraperProjectParams request)
        => Answer(() =>
        {
            var folder = Scraper.Project(request.Project).RecordingsFolder;
            var made = Scraper.PageObjects(request.Project);
            return (IReadOnlyList<ScraperPageInfo>)Scraper.Pages(request.Project).Select(page => new ScraperPageInfo(
                page.Key,
                page.Latest.Event.DisplayName,
                page.Latest.Event.Location,
                made.TryGetValue(page.Key, out var code) ? code.PageObject : null,
                made.TryGetValue(page.Key, out code) ? code.Changed : null,
                page.Captures.Select(capture => new ScraperCaptureInfo(capture.Session, capture.Event.Number, capture.Event.At, capture.Event.Nodes, Path.Combine(folder, capture.Session, capture.Event.Snapshot ?? ""))).ToList())).ToList();
        });

    /// <summary><c>bruinet/scraper/diff</c>: what changed on a page between two captures.</summary>
    [JsonRpcMethod("bruinet/scraper/diff")]
    public ScraperResult<PageDiff> ScraperDiff(ScraperDiffParams request)
        => Answer(() => Scraper.Diff(request.Project, request.Page, request.FromSession, request.FromNumber, request.ToSession, request.ToNumber));

    /// <summary><c>bruinet/scraper/outline</c>: a capture as an outline, or a node's subtree of it.</summary>
    [JsonRpcMethod("bruinet/scraper/outline")]
    public ScraperResult<string> ScraperOutline(ScraperOutlineParams request)
        => Answer(() => SnapshotOutline.Render(Scraper.Snapshot(request.Project, request.Page, request.Session, request.Number).Snapshot, request.Node ?? 0, request.MaxLines ?? 400, request.Focus ?? 0));

    /// <summary><c>bruinet/scraper/highlight</c>: outlines a node of the last capture in the browser, or removes the outline.</summary>
    [JsonRpcMethod("bruinet/scraper/highlight")]
    public Task<ScraperResult<bool>> ScraperHighlight(ScraperNodeParams request)
        => Answer(async () =>
        {
            await Scraper.HighlightAsync(request.Node).ConfigureAwait(false);
            return true;
        });

    /// <summary><c>bruinet/scraper/pick</c>: captures, then lets the user click a node in the browser.</summary>
    [JsonRpcMethod("bruinet/scraper/pick")]
    public Task<ScraperResult<ScraperPickResult>> ScraperPick()
        => Answer(async () =>
        {
            var (capture, node) = await Scraper.PickAsync().ConfigureAwait(false);
            return new ScraperPickResult(capture, node);
        });

    /// <summary><c>bruinet/scraper/patterns</c>: what the recorded pages are built from.</summary>
    [JsonRpcMethod("bruinet/scraper/patterns")]
    public ScraperResult<IReadOnlyList<Bruinet.Scraper.Code.Pattern>> ScraperPatterns(ScraperProjectParams request)
        => Answer(() => Scraper.Patterns(request.Project));

    /// <summary><c>bruinet/scraper/controlBrief</c>: what the agent gets to make a control from a pattern.</summary>
    [JsonRpcMethod("bruinet/scraper/controlBrief")]
    public ScraperResult<Bruinet.Scraper.Code.ControlBriefText> ScraperControlBrief(ScraperControlBriefParams request)
        => Answer(() => Scraper.ControlBrief(request.Project, request.Signature, request.Name, request.Variant, request.Lifted));

    /// <summary><c>bruinet/scraper/controls</c>: the page objects and controls a project has.</summary>
    [JsonRpcMethod("bruinet/scraper/controls")]
    public ScraperResult<IReadOnlyList<Bruinet.Scraper.Code.CodeClass>> ScraperControls(ScraperProjectParams request)
        => Answer(() => Scraper.Controls(request.Project));

    // A request the Scraper cannot answer comes back as a reason the view shows, not as an RPC error.
    private static ScraperResult<T> Answer<T>(Func<T> work)
    {
        try
        {
            return new ScraperResult<T>(work(), null);
        }
        catch (Exception e) when (e is ScraperProblem or FormatException or FileNotFoundException)
        {
            return new ScraperResult<T>(default, e.Message);
        }
    }

    private static async Task<ScraperResult<T>> Answer<T>(Func<Task<T>> work)
    {
        try
        {
            return new ScraperResult<T>(await work().ConfigureAwait(false), null);
        }
        catch (Exception e) when (e is ScraperProblem or FormatException or FileNotFoundException or InvalidOperationException or Microsoft.Playwright.PlaywrightException)
        {
            return new ScraperResult<T>(default, e.Message);
        }
    }

    private void DisposeScraper()
    {
        ScraperService? scraper;
        lock (_gate)
        {
            scraper = _scraper;
            _scraper = null;
        }

        scraper?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}

/// <summary>An answer, or the reason there is none. Both are always sent, null or not: the view tells a result by them.</summary>
public sealed record ScraperResult<T>(
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)] T? Value,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)] string? Problem);

/// <summary>A scraped project.</summary>
public sealed record ScraperProjectInfo(string Name, string Label, string Folder, string Platform, string StartUrl, IReadOnlyList<string> Sessions);

/// <summary>A recorded page, the page object made for it and whether the page changed since, and its captures, newest first.</summary>
public sealed record ScraperPageInfo(string Key, string Name, string Location, string? PageObject, bool? ChangedSinceGenerated, IReadOnlyList<ScraperCaptureInfo> Captures);

/// <summary><c>bruinet/scraper/diff</c>'s parameters: the page, and the captures to compare (the defaults are ScraperService.Diff's).</summary>
public sealed record ScraperDiffParams(string? Project, string Page, string? FromSession, int? FromNumber, string? ToSession, int? ToNumber);

/// <summary>One capture of a page.</summary>
public sealed record ScraperCaptureInfo(string Session, int Number, DateTimeOffset At, int Nodes, string File);

/// <summary>What the user picked: the capture it is in and its node.</summary>
public sealed record ScraperPickResult(RecordingEvent? Capture, int? Node);

/// <summary><c>bruinet/scraper/start</c>'s parameters.</summary>
public sealed record ScraperStartParams(string? Project, string? Session, string? Url, bool? AutoCapture);

/// <summary><c>bruinet/scraper/capture</c>'s parameters.</summary>
public sealed record ScraperCaptureParams(string? Name);

/// <summary>A project, by name; the only one when null.</summary>
public sealed record ScraperProjectParams(string? Project);

/// <summary><c>bruinet/scraper/controlBrief</c>'s parameters: the pattern, and the name the person gave the control.</summary>
public sealed record ScraperControlBriefParams(string? Project, string Signature, string? Name, string? Variant = null, bool Lifted = false);

/// <summary><c>bruinet/scraper/outline</c>'s parameters: the page's newest capture unless a session (and number) is given.</summary>
/// <param name="Focus">A node shown even when it is a wrapper.</param>
public sealed record ScraperOutlineParams(string? Project, string Page, string? Session, int? Number, int? Node, int? MaxLines, int? Focus = null);

/// <summary>A page's newest capture, and a node on it.</summary>
public sealed record ScraperPageNodeParams(string? Project, string Page, int? Node);

/// <summary>A node of the last capture; none to clear.</summary>
public sealed record ScraperNodeParams(int? Node);
