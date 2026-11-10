using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Bruinet.Scraper.Snapshots;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Recordings;

namespace Bruinet.Scraper.Explore;

/// <summary>
/// Goes through an app by itself, breadth first: each page and each state a safe click shows is captured
/// once, within the budget and the guards of <see cref="ExploreSettings"/>. A state is reached again by
/// opening its page and replaying the clicks that led there, so a dialog is left by reloading, never by
/// its action.
/// </summary>
public sealed class Explorer(ScrapeProject project, IExploreSource source, ExploreSettings settings, ExploreOptions options, ExploreIo io)
{
    private readonly List<ExploredCapture> _captures = [];
    private readonly List<SkippedElement> _skipped = [];
    private readonly HashSet<(string, string, string)> _skippedOnce = [];
    private readonly List<WouldClick> _wouldClick = [];
    private readonly List<string> _notes = [];
    private readonly List<string> _downloads = [];
    private readonly List<string> _dialogs = [];
    private readonly Stopwatch _clock = new();
    private RequestGuard _guard = new(settings);
    private IReadOnlyList<string>? _startCookies;
    private ClickPolicy _policy = settings.ClickPolicy;
    private int _clicks;
    private int _deeper;
    private bool _websocketSeen;

    /// <summary>Explores from <paramref name="start"/>; the source is started (on a blank page) and is left open.</summary>
    public async Task<ExploreResult> RunAsync(string start, CancellationToken cancellationToken = default)
    {
        _clock.Start();
        _guard = new RequestGuard(settings);
        if (!Uri.TryCreate(start, UriKind.Absolute, out var startUri))
            return Result(ExploreStop.Error, $"'{start}' is not an address.");
        if (RequestGuard.RefuseHost(startUri, settings) is { } refused)
            return Result(ExploreStop.HostRefused, refused);

        source.Downloaded += OnDownloaded;
        source.DialogDismissed += OnDialog;
        await source.GuardAsync(_guard).ConfigureAwait(false);
        try
        {
            var (stop, detail) = await LoopAsync(start, cancellationToken).ConfigureAwait(false);
            return Result(stop, detail);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result(ExploreStop.Stopped, "Stopped by the person.");
        }
        finally
        {
            source.Downloaded -= OnDownloaded;
            source.DialogDismissed -= OnDialog;
            await source.UnguardAsync().ConfigureAwait(false);
        }
    }

    private async Task<(ExploreStop, string)> LoopAsync(string start, CancellationToken cancellationToken)
    {
        var queue = new Queue<Item>();
        queue.Enqueue(new Item(start, [], 0, "the start URL", new HashSet<string>()));
        var queued = new HashSet<string>(StringComparer.Ordinal) { PageKey.Of(start) };
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (queue.TryDequeue(out var item))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_clock.Elapsed >= TimeSpan.FromMinutes(settings.MaxMinutes))
                return (ExploreStop.MaxMinutes, $"MaxMinutes ({settings.MaxMinutes}) reached with {queue.Count + 1} left to explore.");
            if (item.Clicks.Count > 0 && _policy == ClickPolicy.Links)
                continue; // the app turned out to talk over a websocket after this click was queued

            var reached = await ReachAsync(item, cancellationToken).ConfigureAwait(false);
            if (reached.Stop is { } stop)
            {
                if (stop.Kind != ExploreStop.SignIn || options.WaitForSignIn is null)
                    return stop;
                Report($"Waiting at a sign-in page ({source.Location}) for the person to sign in and continue.");
                await options.WaitForSignIn(cancellationToken).ConfigureAwait(false);
                _startCookies = null;
                reached = await ReachAsync(item, cancellationToken).ConfigureAwait(false);
                if (reached.Stop is { } again)
                    return again;
            }
            if (reached.Snapshot is not { } snapshot)
                continue;

            var key = PageKey.Of(snapshot.Location);
            if (reached.Navigated)
            {
                // A click that went to another page: that page is explored as if a link led there.
                if (queued.Add(key))
                    queue.Enqueue(item with { Url = snapshot.Location, Clicks = [], Known = new HashSet<string>() });
                continue;
            }
            if (!seen.Add(StateKey(snapshot)))
                continue;

            WatchWebSockets();
            var saved = await io.Save(snapshot, item.Clicks.Count == 0 ? null : $"{Title(snapshot)}: {item.Clicks[^1].Label}", cancellationToken).ConfigureAwait(false);
            _captures.Add(new ExploredCapture(saved.Number, key, snapshot.Location, item.Via, item.Depth));
            Progress($"captured {key}");
            if (_captures.Count >= settings.MaxPages)
                return (ExploreStop.MaxPages, $"MaxPages ({settings.MaxPages}) reached with {queue.Count} left to explore.");

            await FollowAsync(item, snapshot, key, queue, queued, cancellationToken).ConfigureAwait(false);
        }
        return (ExploreStop.Done, _deeper > 0
            ? $"Everything within reach was explored; {_deeper} more lay deeper than MaxDepth ({settings.MaxDepth})."
            : "Everything within reach was explored.");
    }

    // Judges what the state shows that its parent did not, and queues what may be followed.
    private async Task FollowAsync(Item item, ScrapeSnapshot snapshot, string key, Queue<Item> queue, HashSet<string> queued, CancellationToken cancellationToken)
    {
        var context = new JudgeContext(settings, uri => project.IsOwnPage(uri.AbsoluteUri))
        {
            NeverNodes = await MatchAsync(settings.NeverClick, cancellationToken).ConfigureAwait(false),
            AlwaysNodes = await MatchAsync(settings.AlwaysClick, cancellationToken).ConfigureAwait(false),
        };
        var refs = NodeRef.Of(snapshot);
        // What a person sees here; a hidden element is judged in the state that shows it.
        var known = refs.Values.Where(r => snapshot[r.Number].Bounds is not null).Select(r => r.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var judgement in ClickJudge.Judge(snapshot, context))
        {
            var target = refs[judgement.Node];
            if (item.Known.Contains(target.Key))
                continue; // its parent state showed it: judged there

            if (judgement.Action == JudgedAction.Visit)
            {
                if (judgement.Verdict != Verdict.Safe)
                { Skip(key, judgement); continue; }
                if (!queued.Contains(PageKey.Of(judgement.Url!)))
                {
                    if (item.Depth + 1 > settings.MaxDepth)
                    { _deeper++; continue; }
                    queued.Add(PageKey.Of(judgement.Url!));
                    queue.Enqueue(new Item(judgement.Url!, [], item.Depth + 1, $"link '{judgement.Label}' on {key}", new HashSet<string>()));
                }
                continue;
            }

            var node = snapshot[judgement.Node];
            if (node.Bounds is null)
                continue; // hidden: a person cannot click it here
            if (node.Attributes.ContainsKey("disabled") || node.Attribute("aria-disabled") == "true")
                continue; // nor when it is off
            if (!ClickJudge.Allows(settings.ClickPolicy, judgement))
            {
                Skip(key, judgement.Verdict == Verdict.Unsafe ? judgement : judgement with { Reason = $"{judgement.Reason}; ClickPolicy {Policy(settings.ClickPolicy)} does not click it" });
                continue;
            }
            if (options.DryRun)
            { _wouldClick.Add(new WouldClick(key, judgement.Label, judgement.Reason)); continue; }
            if (_policy == ClickPolicy.Links)
            { Skip(key, judgement with { Reason = "the app talks over a websocket: links only (WebsocketApps)" }); continue; }
            if (item.Depth + 1 > settings.MaxDepth)
            { _deeper++; continue; }
            queue.Enqueue(new Item(snapshot.Location, [.. item.Clicks, target with { Label = judgement.Label }], item.Depth + 1, $"click '{judgement.Label}' on {key}", known));
        }
    }

    // Opens the item's page and replays its clicks; the snapshot of where that led, or why to stop.
    private async Task<Reached> ReachAsync(Item item, CancellationToken cancellationToken)
    {
        var requested = PageKey.Of(item.Url);
        if (!await source.GoToAsync(item.Url, cancellationToken).ConfigureAwait(false))
        {
            Report($"Could not open {item.Url} ({item.Via}).");
            return default;
        }
        await PauseAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await io.Capture(cancellationToken).ConfigureAwait(false);
        if (await CheckAsync(snapshot, requested, "opening " + item.Url, cancellationToken).ConfigureAwait(false) is { } stop)
            return new Reached(null, false, stop);

        foreach (var click in item.Clicks)
        {
            var node = NodeRef.Of(snapshot).Values.FirstOrDefault(r => r.Key == click.Key);
            var failed = node is null ? "it is not on the page any more" : await source.ClickAsync(node.Number, cancellationToken).ConfigureAwait(false);
            if (failed is not null)
            {
                Report($"Could not click '{click.Label}' on {requested} ({item.Via}): {failed}");
                return default;
            }
            _clicks++;
            await PauseAsync(cancellationToken).ConfigureAwait(false);
            snapshot = await io.Capture(cancellationToken).ConfigureAwait(false);
            if (await CheckAsync(snapshot, requested, $"clicking '{click.Label}' on {requested}", cancellationToken).ConfigureAwait(false) is { } after)
                return new Reached(null, false, after);
            if (PageKey.Of(snapshot.Location) != requested)
                return new Reached(snapshot, true, null);
        }
        return new Reached(snapshot, false, null);
    }

    // What is checked after every step: a download, the app's host, a sign-in page, the start's cookies.
    private async Task<(ExploreStop Kind, string Detail)?> CheckAsync(ScrapeSnapshot snapshot, string requested, string after, CancellationToken cancellationToken)
    {
        if (_downloads.Count > 0)
            return (ExploreStop.Download, $"A download ({_downloads[0]}) started after {after}.");
        var hasPassword = snapshot.Nodes.Any(n => n.Type == "input" && n.Attribute("type") == "password" && n.Bounds is not null);
        if (!project.IsOwnPage(snapshot.Location))
            return hasPassword
                ? (ExploreStop.SignIn, $"A sign-in page ({snapshot.Location}) after {after}.")
                : (ExploreStop.LeftApp, $"The page left the app for {snapshot.Location} after {after}.");
        if (IsLoginUrl(snapshot.Location) || (hasPassword && PageKey.Of(snapshot.Location) != requested))
            return (ExploreStop.SignIn, $"A sign-in page ({snapshot.Location}) after {after}.");
        return await CheckCookiesAsync(after, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(ExploreStop, string)?> CheckCookiesAsync(string after, CancellationToken cancellationToken)
    {
        var now = await source.CookieNamesAsync(cancellationToken).ConfigureAwait(false);
        if (_startCookies is null)
        {
            _startCookies = now;
            return null;
        }
        var gone = _startCookies.Except(now, StringComparer.Ordinal).ToArray();
        return gone.Length == 0 ? null : (ExploreStop.SignedOut, $"The cookie {string.Join(", ", gone)} went after {after}: signed out.");
    }

    private bool IsLoginUrl(string location)
    {
        if (settings.LoginUrl.Length == 0)
            return false;
        var login = Uri.TryCreate(settings.LoginUrl, UriKind.Absolute, out var absolute) ? PageKey.Of(absolute.AbsoluteUri) : PageKey.Of("http://x" + (settings.LoginUrl.StartsWith('/') ? "" : "/") + settings.LoginUrl);
        return PageKey.Of(location) == login;
    }

    // Guard 3: a websocket carries clicks the request guard cannot see.
    private void WatchWebSockets()
    {
        if (_guard.WebSockets.Count == 0 || _websocketSeen)
            return;
        _websocketSeen = true;
        if (settings.WebsocketApps == WebsocketApps.Links && _policy != ClickPolicy.Links)
        {
            _policy = ClickPolicy.Links;
            Report($"The app opened a websocket ({_guard.WebSockets[0]}): the request guard does not see what its clicks send, so only links are followed (WebsocketApps: links).");
        }
        else if (settings.WebsocketApps == WebsocketApps.Warn)
        {
            Report($"The app opened a websocket ({_guard.WebSockets[0]}): clicks go on (WebsocketApps: warn), and the request guard does not see what they send.");
        }
    }

    private async Task<IReadOnlySet<int>> MatchAsync(IReadOnlyList<ElementMatch> matches, CancellationToken cancellationToken)
    {
        var nodes = new HashSet<int>();
        foreach (var match in matches.Where(m => m.Kind == "selector"))
            nodes.UnionWith(await source.MatchAsync(match.Value, cancellationToken).ConfigureAwait(false));
        return nodes;
    }

    private void Skip(string page, Judgement judgement)
    {
        if (_skippedOnce.Add((page, judgement.Label, judgement.Reason)))
            _skipped.Add(new SkippedElement(page, judgement.Label, judgement.Verdict, judgement.Reason));
    }

    /// <summary>A policy as the config writes it.</summary>
    public static string Policy(ClickPolicy policy) => policy switch
    {
        ClickPolicy.Links => "links",
        ClickPolicy.Safe => "safe",
        ClickPolicy.NotUnsafe => "not-unsafe",
        _ => "all",
    };

    private Task PauseAsync(CancellationToken cancellationToken) => Task.Delay(settings.DelayMs, cancellationToken);

    private void Report(string note)
    {
        _notes.Add(note);
        Progress(note);
    }

    private void Progress(string doing) =>
        options.Progress?.Report(new ExploreProgress(_captures.Count, _clicks, _skipped.Count, _guard.Blocked.Count, doing));

    private void OnDownloaded(object? sender, string file) => _downloads.Add(file);

    private void OnDialog(object? sender, string message) => _dialogs.Add(message);

    private ExploreResult Result(ExploreStop stop, string detail)
    {
        var notes = _notes.Concat(_dialogs.Select(d => $"A page dialog was dismissed: \"{d}\".")).ToArray();
        return new(stop, detail, options.DryRun, settings, _captures.ToArray(), _skipped.ToArray(), _guard.Blocked, _guard.WebSockets, _wouldClick.ToArray(), notes, _clicks, _clock.Elapsed);
    }

    private static string Title(ScrapeSnapshot snapshot) => snapshot.Title.Length > 0 ? snapshot.Title : PageKey.Of(snapshot.Location);

    // A state: the page, what it holds (without boxes, so the window size does not count) and which
    // nodes show (a dialog or expander opening changes no attribute the capture keeps).
    private static string StateKey(ScrapeSnapshot snapshot)
    {
        var shown = string.Join(',', snapshot.Nodes.Where(n => n.Bounds is not null).Select(n => n.Number));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(shown)))[..8];
        return $"{PageKey.Of(snapshot.Location)} {SnapshotFile.ContentHash(snapshot)} {hash}";
    }

    private sealed record Item(string Url, IReadOnlyList<NodeRef> Clicks, int Depth, string Via, IReadOnlySet<string> Known);

    private readonly record struct Reached(ScrapeSnapshot? Snapshot, bool Navigated, (ExploreStop Kind, string Detail)? Stop);
}

/// <summary>An element found again after a reload: what it is and which of its kind, in document order.</summary>
internal sealed record NodeRef(string Key, int Number, string Label)
{
    public static IReadOnlyDictionary<int, NodeRef> Of(ScrapeSnapshot snapshot)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var refs = new Dictionary<int, NodeRef>();
        foreach (var node in snapshot.Nodes)
        {
            var signature = string.Join('|', node.Type, node.Role, node.Name, node.Text, node.Attribute("data-testid"), node.Attribute("id"), node.Attribute("href"));
            var ordinal = counts[signature] = counts.GetValueOrDefault(signature) + 1;
            refs[node.Number] = new NodeRef($"{signature}#{ordinal}", node.Number, node.Name ?? node.Text ?? node.Type);
        }
        return refs;
    }
}

/// <summary>What Explore reads and writes through the service: a capture, and saving one into the session.</summary>
/// <param name="Capture">Waits for the page to settle and captures it.</param>
/// <param name="Save">Adds a capture to the session, with a name for a state a click showed.</param>
public sealed record ExploreIo(
    Func<CancellationToken, Task<ScrapeSnapshot>> Capture,
    Func<ScrapeSnapshot, string?, CancellationToken, Task<Projects.RecordingEvent>> Save);

/// <summary>How one run of Explore goes, besides the project's settings.</summary>
public sealed record ExploreOptions
{
    /// <summary>Only links are visited; what would be clicked is listed.</summary>
    public bool DryRun { get; init; }

    /// <summary>The start address; the config's <c>StartUrl</c> when null.</summary>
    public string? Url { get; init; }

    /// <summary>Overrides <see cref="ExploreSettings.MaxPages"/>.</summary>
    public int? MaxPages { get; init; }

    /// <summary>Overrides <see cref="ExploreSettings.MaxDepth"/>.</summary>
    public int? MaxDepth { get; init; }

    /// <summary>Overrides <see cref="ExploreSettings.MaxMinutes"/>.</summary>
    public int? MaxMinutes { get; init; }

    /// <summary>Overrides <see cref="ExploreSettings.DelayMs"/>.</summary>
    public int? DelayMs { get; init; }

    /// <summary>Shows the browser; headless when false (our own apps in CI: a sign-in needs the window).</summary>
    public bool Visible { get; init; } = true;

    /// <summary>Stops at a sign-in page, whoever could sign in: an unattended run.</summary>
    public bool StopAtSignIn { get; init; }

    /// <summary>At a sign-in page, waits for the person to sign in and continue; null: the service's Continue when the browser shows.</summary>
    public Func<CancellationToken, Task>? WaitForSignIn { get; init; }

    /// <summary>Told what Explore is doing, after each step.</summary>
    public IProgress<ExploreProgress>? Progress { get; init; }

    /// <summary>The project's settings with this run's overrides.</summary>
    public ExploreSettings Apply(ExploreSettings settings) => settings with
    {
        MaxPages = MaxPages ?? settings.MaxPages,
        MaxDepth = MaxDepth ?? settings.MaxDepth,
        MaxMinutes = MaxMinutes ?? settings.MaxMinutes,
        DelayMs = DelayMs ?? settings.DelayMs,
    };
}

/// <summary>Where Explore is: captures, clicks, skipped, blocked, and what it just did.</summary>
public sealed record ExploreProgress(int Captures, int Clicks, int Skipped, int Blocked, string Doing);

/// <summary>Why Explore stopped.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ExploreStop>))]
public enum ExploreStop
{
    /// <summary>Everything within reach was explored.</summary>
    Done,

    /// <summary>The budget's captures were used.</summary>
    MaxPages,

    /// <summary>The budget's time was used.</summary>
    MaxMinutes,

    /// <summary>The person stopped it.</summary>
    Stopped,

    /// <summary>A sign-in page, and no one to sign in.</summary>
    SignIn,

    /// <summary>The page left the app's hosts.</summary>
    LeftApp,

    /// <summary>A cookie the start had is gone.</summary>
    SignedOut,

    /// <summary>A download started.</summary>
    Download,

    /// <summary>The start is not on an Explore host.</summary>
    HostRefused,

    /// <summary>It could not run.</summary>
    Error,
}

/// <summary>A capture Explore made: its event in the session, page key, address, what led there, and how many steps from the start.</summary>
public sealed record ExploredCapture(int Event, string Page, string Location, string Via, int Depth);

/// <summary>An element Explore did not act on, and why.</summary>
public sealed record SkippedElement(string Page, string Label, Verdict Verdict, string Reason);

/// <summary>In a dry run: an element a real run would click.</summary>
public sealed record WouldClick(string Page, string Label, string Reason);

/// <summary>What a run of Explore did.</summary>
public sealed record ExploreResult(
    ExploreStop Stop,
    string StopDetail,
    bool DryRun,
    ExploreSettings Settings,
    IReadOnlyList<ExploredCapture> Captures,
    IReadOnlyList<SkippedElement> Skipped,
    IReadOnlyList<BlockedRequest> Blocked,
    IReadOnlyList<string> WebSockets,
    IReadOnlyList<WouldClick> WouldClick,
    IReadOnlyList<string> Notes,
    int Clicks,
    TimeSpan Took)
{
    /// <summary>The session folder it recorded into; null when it did not start.</summary>
    public string? Folder { get; init; }

    /// <summary>The report's path, <c>explore.md</c> in the session; null when it did not start.</summary>
    public string? Report { get; init; }

    /// <summary>True when it ran within its guards: done, or stopped by the budget or the person.</summary>
    public bool WithinGuards => Stop is ExploreStop.Done or ExploreStop.MaxPages or ExploreStop.MaxMinutes or ExploreStop.Stopped;
}
