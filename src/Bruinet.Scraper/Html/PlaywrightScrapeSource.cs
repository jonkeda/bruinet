using System.Text.Json;
using System.Threading.Channels;
using Bruinet.Core.Scraping;
using Bruinet.Scraper.Explore;
using Microsoft.Playwright;

namespace Bruinet.Scraper.Html;

/// <summary>
/// The Html source: a Chromium started through Playwright, with the project's own profile so a login
/// lasts. Each frame, cross-origin ones too, is captured on its own and hung under its <c>iframe</c>.
/// </summary>
public sealed class PlaywrightScrapeSource : IExploreSource
{
    /// <summary>How long the page must be unchanged to count as settled.</summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(500);

    /// <summary>How long a capture waits for the page to settle before it captures anyway.</summary>
    public static readonly TimeSpan SettleLimit = TimeSpan.FromSeconds(15);

    /// <summary>The longest text kept for a node's text or an attribute value.</summary>
    public const int MaxText = 200;

    private static readonly string CaptureScript = Script("capture.js");
    private static readonly string SettleScript = Script("settle.js");
    private static readonly string HighlightScript = Script("highlight.js");
    private static readonly string PickScript = Script("pick.js");
    private static readonly string RecorderScript = Script("recorder.js");
    private static readonly string ToolbarScript = Script("toolbar.js");

    // What an element shows, for the checks an assert offers: its text, a field's value (never a
    // password's), and a checkbox's or switch's state.
    private const string FactsScript = """
        n => {
            const el = (window.__bruinetScrapeNodes || [])[n - 1];
            if (!el || !el.getAttribute) return null;
            const text = (el.innerText || el.textContent || '').replace(/\s+/g, ' ').trim().substring(0, 200);
            const toggle = el instanceof HTMLInputElement && (el.type === 'checkbox' || el.type === 'radio');
            let value = null;
            if (el instanceof HTMLSelectElement) value = el.selectedOptions.length ? el.selectedOptions[0].text.trim() : '';
            else if (el instanceof HTMLTextAreaElement || (el instanceof HTMLInputElement && !toggle && el.type !== 'password')) value = el.value;
            const checked = toggle ? el.checked : el.hasAttribute('aria-checked') ? el.getAttribute('aria-checked') === 'true' : null;
            return JSON.stringify({ text, value, checked });
        }
        """;

    private IPlaywright? _playwright;
    private IBrowserContext? _context;
    private IPage? _page;
    private string? _lastLocation;
    private RequestGuard? _guard;

    // Global node number of the last capture -> the frame and its local number there.
    private readonly List<(IFrame Frame, int Local)> _numbers = [];

    // One capture at a time: an action is placed in the capture it was taken against.
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private int _generation;
    private DateTimeOffset _capturedAt;

    // Actions from every frame, placed one at a time in the order the person took them.
    private readonly Channel<Act> _acts = Channel.CreateUnbounded<Act>(new() { SingleReader = true });
    private Task? _pump;
    private volatile bool _recordingActions;

    /// <inheritdoc/>
    public string Platform => "html";

    /// <inheritdoc/>
    public event EventHandler<ScrapeTransition>? Transitioned;

    /// <inheritdoc/>
    public event EventHandler<ScrapeAction>? Acted;

    /// <inheritdoc/>
    public event EventHandler<string>? Commanded;

    /// <inheritdoc/>
    public event EventHandler<string>? Downloaded;

    /// <inheritdoc/>
    public event EventHandler<string>? DialogDismissed;

    /// <inheritdoc/>
    public string Location => _page?.Url ?? "";

    /// <summary>The page being recorded; null before <see cref="StartAsync"/>.</summary>
    public IPage? Page => _page;

    /// <inheritdoc/>
    public async Task StartAsync(ScrapeStart start, CancellationToken cancellationToken = default)
    {
        if (_page is not null)
            throw new InvalidOperationException("The source is started already.");
        Directory.CreateDirectory(start.ProfileFolder);
        _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        try
        {
            _context = await _playwright.Chromium.LaunchPersistentContextAsync(start.ProfileFolder, new()
            {
                Headless = !start.Visible,
                ViewportSize = start.Visible ? ViewportSize.NoViewport : new ViewportSize { Width = 1280, Height = 800 },
                Args = [.. start.Visible ? ["--start-maximized"] : Array.Empty<string>(), .. DebugPortArguments()],
            }).ConfigureAwait(false);
        }
        catch (PlaywrightException e) when (e.Message.Contains("Executable doesn't exist", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Playwright's Chromium is not installed. Install it once with: pwsh <any Bruinet Html test output>\\playwright.ps1 install chromium", e);
        }
        _page = _context.Pages.Count > 0 ? _context.Pages[0] : await _context.NewPageAsync().ConfigureAwait(false);
        // A link that opens a new tab is followed in the recorded page: one page is recorded at a time.
        _context.Page += (_, page) => _ = FollowInRecordedPageAsync(page);
        _page.FrameNavigated += OnFrameNavigated;
        _page.Download += (_, download) => Downloaded?.Invoke(this, download.SuggestedFilename);
        // With a handler, Playwright no longer dismisses a dialog itself: a confirm is always answered no.
        _page.Dialog += (_, dialog) =>
        {
            Enqueue(_page.MainFrame, JsonSerializer.Serialize(new { kind = ScrapeActionKind.Dialog, value = dialog.Message, accepted = false }));
            DialogDismissed?.Invoke(this, dialog.Message);
            _ = dialog.DismissAsync();
        };
        await _page.GotoAsync(start.Location, Opening).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ScrapeSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        await _captureLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await CaptureLockedAsync(settle: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _captureLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StartActionsAsync(ScrapeActionOptions options, CancellationToken cancellationToken = default)
    {
        var page = Started();
        if (_pump is null)
        {
            _pump = Task.Run(PumpAsync, CancellationToken.None);
            await _context!.ExposeBindingAsync<string>("__bruinetAct", (source, json) => Enqueue(source.Frame, json)).ConfigureAwait(false);
            var argument = new { testIds = options.TestIds, secrets = options.Secrets.Select(s => new { field = s.Field, placeholder = s.Placeholder }) };
            await _context.AddInitScriptAsync($"({RecorderScript})({JsonSerializer.Serialize(argument)});").ConfigureAwait(false);
            foreach (var frame in page.Frames)
                await TryEvaluateAsync<bool>(frame, RecorderScript, argument).ConfigureAwait(false);
            // The toolbar's buttons: on a thread of their own, so Stop can close the browser that raised it.
            await _context.ExposeBindingAsync<string>("__bruinetCommand", (source, command) => { _ = Task.Run(() => Commanded?.Invoke(this, command)); }).ConfigureAwait(false);
            await _context.AddInitScriptAsync($"({ToolbarScript})();").ConfigureAwait(false);
            await TryEvaluateAsync<bool>(page.MainFrame, ToolbarScript, null).ConfigureAwait(false);
        }
        _recordingActions = true;
    }

    /// <inheritdoc/>
    public void StopActions() => _recordingActions = false;

    /// <inheritdoc/>
    public async Task<ScrapeAssert?> PickAssertAsync(CancellationToken cancellationToken = default)
    {
        var page = Started();
        if (await PickAsync(cancellationToken).ConfigureAwait(false) is not { } node)
            return null;
        var (frame, local) = _numbers[node - 1];
        var json = await TryEvaluateAsync<string?>(frame, FactsScript, local).ConfigureAwait(false);
        var facts = json is null ? null : JsonSerializer.Deserialize<Facts>(json, FrameCapture.Json);
        if (facts is null)
            return null; // the element went away while it was picked
        string? check;
        using (cancellationToken.Register(() => _ = TryEvaluateAsync<bool>(page.MainFrame, "() => { if (window.__bruinetChooseCancel) window.__bruinetChooseCancel(); return true; }", null)))
            check = await TryEvaluateAsync<string?>(page.MainFrame, "f => window.__bruinetChooseCheck(f)", new { text = facts.Text, value = facts.Value, @checked = facts.Checked }).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return check switch
        {
            ScrapeCheck.Page => new ScrapeAssert(page.Url, null, check, null, null),
            ScrapeCheck.Visible => new ScrapeAssert(page.Url, node, check, null, null),
            ScrapeCheck.Text => new ScrapeAssert(page.Url, node, check, facts.Text, null),
            ScrapeCheck.Value => new ScrapeAssert(page.Url, node, check, facts.Value, null),
            ScrapeCheck.Checked => new ScrapeAssert(page.Url, node, check, null, facts.Checked),
            _ => null,
        };
    }

    /// <inheritdoc/>
    public Task ShowCountAsync(int actions, CancellationToken cancellationToken = default) =>
        _page is null ? Task.CompletedTask : TryEvaluateAsync<bool>(_page.MainFrame, "n => window.__bruinetToolbarCount ? window.__bruinetToolbarCount(n) : true", actions);

    private async Task<ScrapeSnapshot> CaptureLockedAsync(bool settle, CancellationToken cancellationToken)
    {
        var page = Started();
        if (settle)
            await SettleAsync(page, cancellationToken).ConfigureAwait(false);
        var generation = ++_generation;

        var frames = new Dictionary<IFrame, FrameCapture>();
        foreach (var frame in page.Frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await TryCaptureFrameAsync(frame, generation).ConfigureAwait(false) is { } capture)
                frames[frame] = capture;
        }
        if (!frames.TryGetValue(page.MainFrame, out var main))
            throw new InvalidOperationException($"The page at {page.Url} could not be captured: it navigated away while it was read. Capture again.");

        // Which iframe node (local number in its parent frame) holds which frame.
        var hosts = new Dictionary<(IFrame, int), IFrame>();
        foreach (var frame in frames.Keys.Where(f => f.ParentFrame is not null && frames.ContainsKey(f.ParentFrame)))
        {
            if (await TryHostNumberAsync(frame).ConfigureAwait(false) is { } local)
                hosts[(frame.ParentFrame!, local)] = frame;
        }

        _numbers.Clear();
        var nodes = new List<ScrapeNode>();
        Append(page.MainFrame, main, 1, 0, frames, hosts, nodes);
        // Two captures never share a time: an action names its capture by it.
        var at = DateTimeOffset.UtcNow;
        _capturedAt = at > _capturedAt ? at : _capturedAt.AddTicks(1);
        return new ScrapeSnapshot
        {
            Platform = Platform,
            Location = main.Location,
            Title = main.Title,
            CapturedAt = _capturedAt,
            Nodes = nodes,
        };
    }

    private void Enqueue(IFrame frame, string json)
    {
        if (_recordingActions)
            _acts.Writer.TryWrite(new Act(frame, json, _page?.Url ?? frame.Url, DateTimeOffset.UtcNow));
    }

    private async Task PumpAsync()
    {
        await foreach (var act in _acts.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (_recordingActions && await TryPlaceAsync(act).ConfigureAwait(false) is { } action)
                Acted?.Invoke(this, action);
        }
    }

    // The element's number in the capture the page had when the person acted; else in the last capture,
    // if the element is still in it; else in a capture taken now (R2). Null when the page sent something
    // else.
    private async Task<ScrapeAction?> TryPlaceAsync(Act act)
    {
        RawAction? raw;
        try
        {
            raw = JsonSerializer.Deserialize<RawAction>(act.Json, FrameCapture.Json);
        }
        catch (JsonException)
        {
            return null; // not the recorder's message
        }
        if (raw?.Kind is null)
            return null;
        var action = new ScrapeAction
        {
            Kind = raw.Kind,
            At = act.At,
            Location = act.Location,
            Path = raw.Path,
            Value = raw.Secret is null ? raw.Value : null,
            Secret = raw.Secret,
            Checked = raw.Checked,
            Accepted = raw.Accepted,
        };
        if (raw.Id is not { } id)
            return action;

        await _captureLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var node = raw.Gen == _generation && raw.Local is { } local ? Global(act.Frame, local) : null;
            node ??= await TryPendingNodeAsync(act.Frame, id).ConfigureAwait(false);
            ScrapeSnapshot? capture = null;
            if (node is null && _page is not null)
            {
                // Not settled: the person acted on the page as it was. One that moved on since is not its page.
                capture = await TryCaptureLockedAsync().ConfigureAwait(false);
                if (capture is not null && Projects.PageKey.Of(capture.Location) != Projects.PageKey.Of(act.Location))
                    capture = null;
                if (capture is not null)
                    node = await TryPendingNodeAsync(act.Frame, id).ConfigureAwait(false);
            }
            await TryEvaluateAsync<bool>(act.Frame, "id => { delete (window.__bruinetPending || {})[id]; return true; }", id).ConfigureAwait(false);
            return action with { Node = node, CapturedAt = node is null ? null : _capturedAt, Capture = capture };
        }
        finally
        {
            _captureLock.Release();
        }
    }

    private async Task<int?> TryPendingNodeAsync(IFrame frame, int id) =>
        Global(frame, await TryEvaluateAsync<int>(frame, "id => (window.__bruinetScrapeNodes || []).indexOf((window.__bruinetPending || {})[id]) + 1", id).ConfigureAwait(false));

    private async Task<ScrapeSnapshot?> TryCaptureLockedAsync()
    {
        try
        {
            return await CaptureLockedAsync(settle: false, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e) when (e is InvalidOperationException or PlaywrightException)
        {
            return null; // navigated away while it was read, or closed: the action keeps its path
        }
    }

    private int? Global(IFrame frame, int local)
    {
        if (local <= 0)
            return null;
        var index = _numbers.FindIndex(n => n.Frame == frame && n.Local == local);
        return index >= 0 ? index + 1 : null;
    }

    /// <inheritdoc/>
    public async Task<byte[]?> ScreenshotAsync(CancellationToken cancellationToken = default) =>
        await Started().ScreenshotAsync(new() { Type = ScreenshotType.Png }).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task HighlightAsync(int? number, CancellationToken cancellationToken = default)
    {
        var page = Started();
        var target = number is { } n && n >= 1 && n <= _numbers.Count ? _numbers[n - 1] : default;
        foreach (var frame in page.Frames)
        {
            var local = frame == target.Frame ? target.Local : 0;
            await TryEvaluateAsync<bool>(frame, HighlightScript, local).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<int?> PickAsync(CancellationToken cancellationToken = default)
    {
        var page = Started();
        await HighlightAsync(null, cancellationToken).ConfigureAwait(false);
        var frames = page.Frames.ToArray();
        var picks = frames.Select(f => (Frame: f, Task: f.EvaluateAsync<int>(PickScript))).ToList();
        using var registration = cancellationToken.Register(() => _ = CancelPicksAsync(frames));
        try
        {
            while (picks.Count > 0)
            {
                var done = await Task.WhenAny(picks.Select(p => p.Task)).ConfigureAwait(false);
                var pick = picks.First(p => p.Task == done);
                picks.Remove(pick);
                if (!done.IsCompletedSuccessfully)
                    continue; // the frame went away while picking
                if (done.Result == 0)
                    return null;
                var index = _numbers.FindIndex(x => x.Frame == pick.Frame && x.Local == done.Result);
                return index >= 0 ? index + 1 : null;
            }
            return null;
        }
        finally
        {
            await CancelPicksAsync(frames).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// Puts Explore's request and host guards on every request of the browser: a blocked one is aborted.
    /// Refused when the page is not on an Explore host.
    /// </summary>
    public async Task GuardAsync(RequestGuard guard)
    {
        var page = Started();
        if (_guard is not null)
            throw new InvalidOperationException("The source is guarded already.");
        if (Uri.TryCreate(page.Url, UriKind.Absolute, out var location) && location.Scheme is "http" or "https" && RequestGuard.RefuseHost(location, guard.Settings) is { } refused)
            throw new InvalidOperationException(refused);
        _guard = guard;
        page.WebSocket += OnWebSocket;
        await _context!.RouteAsync("**/*", RouteAsync).ConfigureAwait(false);
    }

    /// <summary>Takes the guards of <see cref="GuardAsync"/> off.</summary>
    public async Task UnguardAsync()
    {
        if (_guard is null)
            return;
        _guard = null;
        if (_page is not null)
            _page.WebSocket -= OnWebSocket;
        if (_context is not null)
            await _context.UnrouteAsync("**/*", RouteAsync).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> GoToAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            await Started().GotoAsync(url, Opening).ConfigureAwait(false);
            return true;
        }
        catch (Exception e) when (e is PlaywrightException or TimeoutException)
        {
            return false; // blocked by a guard, or the site did not answer
        }
    }

    /// <inheritdoc/>
    public async Task<string?> ClickAsync(int node, CancellationToken cancellationToken = default)
    {
        if (node < 1 || node > _numbers.Count)
            return $"node {node} is not in the last capture";
        var (frame, local) = _numbers[node - 1];
        try
        {
            var handle = await frame.EvaluateHandleAsync("n => window.__bruinetScrapeNodes[n - 1]", local).ConfigureAwait(false);
            if (handle.AsElement() is not { } element)
                return "the element is gone";
            await element.ClickAsync(new() { Timeout = (float)ClickLimit.TotalMilliseconds }).ConfigureAwait(false);
            return null;
        }
        catch (Exception e) when (e is PlaywrightException or TimeoutException)
        {
            return e.Message.ReplaceLineEndings(" ").Split(" Call log:")[0]; // hidden, detached or covered
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlySet<int>> MatchAsync(string selector, CancellationToken cancellationToken = default)
    {
        var matched = new HashSet<int>();
        foreach (var frame in _numbers.Select(n => n.Frame).Distinct())
        {
            var locals = await TryEvaluateAsync<int[]>(frame,
                "s => { try { return [...document.querySelectorAll(s)].map(e => (window.__bruinetScrapeNodes || []).indexOf(e) + 1).filter(n => n > 0); } catch { return []; } }",
                selector).ConfigureAwait(false) ?? [];
            foreach (var local in locals)
            {
                var index = _numbers.FindIndex(n => n.Frame == frame && n.Local == local);
                if (index >= 0)
                    matched.Add(index + 1);
            }
        }
        return matched;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> CookieNamesAsync(CancellationToken cancellationToken = default)
    {
        var page = Started();
        var cookies = await _context!.CookiesAsync([page.Url]).ConfigureAwait(false);
        return cookies.Where(c => c.HttpOnly).Select(c => c.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>How long a click waits for its element to be visible and still.</summary>
    public static readonly TimeSpan ClickLimit = TimeSpan.FromSeconds(3);

    private async Task RouteAsync(IRoute route)
    {
        var request = route.Request;
        if (_guard?.Check(request.Method, request.Url, request.IsNavigationRequest && IsMainFrame(request)) is not null)
            await route.AbortAsync("blockedbyclient").ConfigureAwait(false);
        else
            await route.ContinueAsync().ConfigureAwait(false);
    }

    private bool IsMainFrame(IRequest request)
    {
        try
        {
            return request.Frame == _page?.MainFrame;
        }
        catch (PlaywrightException)
        {
            return false; // a service worker's request has no frame
        }
    }

    private void OnWebSocket(object? sender, IWebSocket socket) => _guard?.WebSocketOpened(socket.Url);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _recordingActions = false;
        _acts.Writer.TryComplete();
        if (_pump is not null)
            await _pump.ConfigureAwait(false);
        if (_context is not null)
            await _context.CloseAsync().ConfigureAwait(false);
        _playwright?.Dispose();
        _context = null;
        _page = null;
    }

    /// <summary>
    /// Set to a port, the recording browser also listens for DevTools there, so a scripted check can act
    /// in it as a person would (the manual test plan's Claude part). Unset in normal use.
    /// </summary>
    public const string DebugPortVariable = "BRUINET_SCRAPER_DEBUG_PORT";

    private static string[] DebugPortArguments() =>
        int.TryParse(Environment.GetEnvironmentVariable(DebugPortVariable), out var port) && port > 0
            ? [$"--remote-debugging-port={port}"]
            : [];

    private IPage Started() => _page ?? throw new InvalidOperationException("The source is not started.");

    private void OnFrameNavigated(object? sender, IFrame frame)
    {
        // Also raised for pushState and hash changes: a single-page app's page transitions.
        if (frame != _page?.MainFrame || frame.Url == _lastLocation)
            return;
        _lastLocation = frame.Url;
        Enqueue(frame, JsonSerializer.Serialize(new { kind = ScrapeActionKind.Navigate }));
        Transitioned?.Invoke(this, new ScrapeTransition(frame.Url));
    }

    private async Task FollowInRecordedPageAsync(IPage opened)
    {
        if (_page is null || opened == _page)
            return;
        var url = await TryWaitForUrlAsync(opened).ConfigureAwait(false);
        await opened.CloseAsync().ConfigureAwait(false);
        if (url is not null)
            await _page.GotoAsync(url, Opening).ConfigureAwait(false);
    }

    private static async Task<string?> TryWaitForUrlAsync(IPage page)
    {
        try
        {
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            return page.Url == "about:blank" ? null : page.Url;
        }
        catch (PlaywrightException)
        {
            return null; // closed before it had an address
        }
    }

    private static async Task SettleAsync(IPage page, CancellationToken cancellationToken)
    {
        // An app still fetching its script bundle changes nothing in the page, so the page can look
        // quiet before it renders (react-admin's demo does): the network first.
        await TryWaitForLoadedAsync(page).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        // Not settled within the limit (an animation that never stops): capture what is there.
        await TryEvaluateAsync<bool>(page.MainFrame, SettleScript, new { quiet = (int)Quiet.TotalMilliseconds, max = (int)SettleLimit.TotalMilliseconds })
            .ConfigureAwait(false);
    }

    // A page is open once its document is: a sub-resource that never finishes (an ad, a font) would
    // keep "load" from firing. The capture waits for the network and the DOM to settle after.
    internal static readonly PageGotoOptions Opening = new() { WaitUntil = WaitUntilState.DOMContentLoaded };

    // "load", then the network idle, within one SettleLimit together: a page with a script that never
    // finishes (a slow analytics script) or that polls reaches neither, and the quiet DOM decides alone.
    private static async Task TryWaitForLoadedAsync(IPage page)
    {
        var deadline = DateTime.UtcNow + SettleLimit;
        foreach (var state in new[] { LoadState.Load, LoadState.NetworkIdle })
        {
            var left = deadline - DateTime.UtcNow;
            if (left <= TimeSpan.Zero)
                return;
            try
            {
                await page.WaitForLoadStateAsync(state, new() { Timeout = (float)left.TotalMilliseconds }).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return;
            }
        }
    }

    private static async Task<FrameCapture?> TryCaptureFrameAsync(IFrame frame, int generation)
    {
        var json = await TryEvaluateAsync<string>(frame, CaptureScript, new { kept = HtmlAttributes.Kept, maxText = MaxText, gen = generation }).ConfigureAwait(false);
        return json is null ? null : JsonSerializer.Deserialize<FrameCapture>(json, FrameCapture.Json);
    }

    private static async Task<int?> TryHostNumberAsync(IFrame frame)
    {
        try
        {
            var element = await frame.FrameElementAsync().ConfigureAwait(false);
            var number = await element.EvaluateAsync<int?>("el => window.__bruinetScrapeFrames ? window.__bruinetScrapeFrames.get(el) ?? null : null").ConfigureAwait(false);
            return number;
        }
        catch (PlaywrightException)
        {
            return null; // the frame was detached after its capture
        }
    }

    private static async Task<T?> TryEvaluateAsync<T>(IFrame frame, string script, object? argument)
    {
        try
        {
            return await frame.EvaluateAsync<T>(script, argument).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            return default; // the frame navigated or was detached meanwhile
        }
    }

    private static async Task CancelPicksAsync(IEnumerable<IFrame> frames)
    {
        foreach (var frame in frames)
            await TryEvaluateAsync<bool>(frame, "() => { if (window.__bruinetPickCancel) window.__bruinetPickCancel(); return true; }", null).ConfigureAwait(false);
    }

    // Adds a frame's nodes in document order, numbering them globally, and a child frame's nodes right
    // after its iframe node. Returns the next free number.
    private int Append(IFrame frame, FrameCapture capture, int next, int parent, Dictionary<IFrame, FrameCapture> frames, Dictionary<(IFrame, int), IFrame> hosts, List<ScrapeNode> nodes)
    {
        var children = capture.Nodes.ToLookup(n => n.P);
        var global = new Dictionary<int, int>();

        void Visit(FrameNode local, int globalParent)
        {
            var number = next++;
            global[local.N] = number;
            hosts.TryGetValue((frame, local.N), out var child);
            nodes.Add(new ScrapeNode
            {
                Number = number,
                Parent = globalParent,
                Kind = local.Kind == "shadow-root" ? ScrapeNodeKind.ShadowRoot : ScrapeNodeKind.Element,
                Type = local.Type ?? "",
                Role = local.Role,
                Name = local.Name,
                Text = local.Text,
                Frame = child?.Url,
                Bounds = local.Box is { Length: 4 } b ? new ScrapeBounds(b[0], b[1], b[2], b[3]) : null,
                Attributes = local.Attr ?? new Dictionary<string, string>(),
            });
            _numbers.Add((frame, local.N));
            if (child is not null && frames.TryGetValue(child, out var inner))
                next = Append(child, inner, next, number, frames, hosts, nodes);
            foreach (var c in children[local.N])
                Visit(c, number);
        }

        foreach (var root in children[0])
            Visit(root, parent);
        return next;
    }

    private static string Script(string name)
    {
        using var stream = typeof(PlaywrightScrapeSource).Assembly.GetManifestResourceStream($"Bruinet.Scraper.Html.Scripts.{name}")
            ?? throw new InvalidOperationException($"The script {name} is not in {typeof(PlaywrightScrapeSource).Assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed record FrameCapture(string Location, string Title, FrameNode[] Nodes)
    {
        public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    }

    private sealed record Act(IFrame Frame, string Json, string Location, DateTimeOffset At);

    private sealed record Facts(string? Text, string? Value, bool? Checked);

    // What recorder.js sends: the element's number in the frame's capture `gen`, and its id in __bruinetPending.
    private sealed record RawAction(string? Kind, int? Id, int? Local, int? Gen, ScrapePathStep[]? Path, string? Value, string? Secret, bool? Checked, bool? Accepted);

    private sealed record FrameNode(int N, int P, string? Kind, string? Type, string? Role, string? Name, string? Text, int[]? Box, Dictionary<string, string>? Attr);
}
