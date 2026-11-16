using System.Net;
using System.Text.Json;
using Microsoft.Maui.DevFlow.Driver;

namespace Bruinet.Maui.DevFlow;

/// <summary>
/// What the runner asks of a DevFlow agent, synchronous as Bruinet's driver interfaces are. The real
/// one is <see cref="DevFlowAgent"/> over <see cref="AgentClient"/>; tests give a fake.
/// </summary>
internal interface IDevFlowAgent : IDisposable
{
    /// <summary>The agent's status, or null when it does not answer.</summary>
    AgentStatus? Status();

    /// <summary>The whole visual tree, every window; with <paramref name="includeNative"/>, native dialogs too (four times the cost).</summary>
    IReadOnlyList<ElementInfo> Tree(bool includeNative);

    /// <summary>The ids the agent's CSS selector engine names.</summary>
    IReadOnlyList<string> QueryCss(string selector);

    bool Tap(ElementInfo element);

    bool Fill(ElementInfo element, string text);

    bool Clear(ElementInfo element);

    bool Focus(ElementInfo element);

    bool Gesture(ElementInfo element, string type, string? direction = null, double? distance = null, int? durationMs = null);

    /// <summary>Scrolls by an amount, an element into view, or a list to an item (<paramref name="position"/>: Start, Center, End or MakeVisible).</summary>
    bool Scroll(ElementInfo? element, double deltaX, double deltaY, int? itemIndex = null, string? position = null);

    bool Navigate(string route);

    bool Back();

    string? GetProperty(string elementId, string name);

    bool SetProperty(ElementInfo element, string name, string value);

    byte[] Screenshot(string? elementId = null);

    /// <summary>Lets go of the mutation lease, so a person's DevFlow session can drive the app.</summary>
    void ReleaseLease();

    /// <summary>
    /// Sends one of Bruinet's verbs (by its name, as <c>BruinetVerb</c> spells it) through the app's
    /// bridge extension, to the element with <paramref name="automationId"/> or, without one, to the
    /// first element that performs it. Not declared when the app carries no bridge.
    /// </summary>
    BridgeAnswer BridgeInvoke(string? automationId, string verb, int arg1 = 0, int arg2 = 0);

    /// <summary>As <see cref="BridgeInvoke"/>, for a verb that carries a text and answers one.</summary>
    BridgeAnswer BridgeExchange(string? automationId, string verb, string argument = "");

    /// <summary>Whether an element with <paramref name="automationId"/> (or any, without one) declares the verb.</summary>
    bool BridgeDeclares(string? automationId, string verb);

    /// <summary>Plays, pauses, stops or seeks (<c>SeekTo</c>, in seconds) a media element, through the app's bridge.</summary>
    BridgeAnswer Media(string automationId, string action, double seconds = 0);

    /// <summary>
    /// The item index of each row the list has realized, by the row's <c>Element.Id</c> (32 hex digits),
    /// through the app's bridge; null when the app carries none.
    /// </summary>
    IReadOnlyList<(string ElementId, string? AutomationId, int Index)>? Rows(string listAutomationId);

    /// <summary>
    /// The iOS alert on screen as the app's bridge describes it (JSON, <c>open</c> false when none is),
    /// or with <paramref name="request"/> a button pressed or its field filled; null when the app has no such route.
    /// </summary>
    string? Alert(object? request = null) => null;

    /// <summary>
    /// Selects the list's item at <paramref name="index"/> as a tap on its row does (the item becomes
    /// the selection, or in a multiple selection is turned over), through the app's bridge: on iOS the
    /// agent's tap reaches no row's selection.
    /// </summary>
    BridgeAnswer SelectRow(string listAutomationId, int index) => BridgeAnswer.None("the app has no row selection route");

    /// <summary>
    /// Taps a view whose tap is a platform behavior's command (the toolkit's TouchBehavior), through the
    /// app's bridge: the agent's tap makes no platform touch, so such a behavior never hears it. The view
    /// has the AutomationId and an <c>Element.Id</c> starting with <paramref name="idPrefix"/>, or, with
    /// none, starting with none of <paramref name="exclude"/>. Not declared when it has no such command.
    /// </summary>
    BridgeAnswer TapBehavior(string? automationId, string? idPrefix, IReadOnlyList<string> exclude) => BridgeAnswer.None("the app has no behavior tap route");

    /// <summary>
    /// Opens the SwipeView whose <c>Element.Id</c> starts with <paramref name="idPrefix"/> on
    /// <paramref name="side"/> (Left, Right, Top, Bottom items), as a finger dragging it does: the
    /// agent's swipe reaches SwipeGestureRecognizers only. Not declared when it has no items there.
    /// </summary>
    BridgeAnswer OpenSwipe(string idPrefix, string side) => BridgeAnswer.None("the app has no swipe route");
}

/// <summary>
/// What the app's bridge answered (<c>Bruinet.Maui.AppSupport</c>'s <c>BruinetDevFlowBridge</c>):
/// whether any element declared the verb, whether one performed it, its HRESULT and its answer.
/// </summary>
internal sealed record BridgeAnswer(bool Declared, bool Delivered, int HResult, string Value, int Declined, string Reason)
{
    /// <summary>UI Automation's "not supported", which the bridge answers for a verb nothing declares.</summary>
    public const int NotSupported = unchecked((int)0x80040204);

    /// <summary>The answer when the app carries no bridge, or it is off.</summary>
    public static BridgeAnswer None(string reason) => new(false, false, NotSupported, string.Empty, 0, reason);
}

/// <summary>
/// <see cref="IDevFlowAgent"/> over DevFlow's own client. The client claims the mutation lease
/// before each change, under one id per driver, so a claim is also the lease's heartbeat.
/// </summary>
internal sealed class DevFlowAgent : IDevFlowAgent
{
    private readonly AgentClient _client;

    public DevFlowAgent(int port)
    {
        _client = new AgentClient("localhost", port)
        {
            MutationLeaseHolderKind = "driver",
            MutationLeaseLabel = "Bruinet",
            // A lost response must not tap twice.
            RetryMutatingRequests = false,
        };
        Port = port;
    }

    public int Port { get; }

    public AgentStatus? Status()
    {
        try
        {
            return Run(_client.GetStatusAsync());
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    // On Windows a dialog is a native window, absent from the tree without native nodes.
    public IReadOnlyList<ElementInfo> Tree(bool includeNative)
    {
        // The agent refuses a read while the UI changes under it (409) and asks for the read again.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var body = Run(_client.CallExtensionToolAsync("GET", "/api/v1/ui/tree",
                    includeNative ? null : JsonSerializer.SerializeToElement(new { native = "false" })));
                var tree = JsonSerializer.Deserialize<List<ElementInfo>>(body, TreeJson) ?? [];
                NameItems(tree);
                return tree;
            }
            catch (HttpRequestException changing) when (attempt < 8 && changing.StatusCode == HttpStatusCode.Conflict)
            {
                Thread.Sleep(25 * attempt);
            }
            catch (HttpRequestException changing) when (changing.StatusCode == HttpStatusCode.Conflict)
            {
                throw new InvalidOperationException("The UI kept changing while DevFlow was reading it.", changing);
            }
            // As the client's own read: an app that is not answering (starting, relaunching) has no tree yet.
            catch (HttpRequestException)
            {
                return [];
            }
            catch (TaskCanceledException)
            {
                return [];
            }
        }
    }

    // The client reads the tree with System.Text.Json's default depth of 64, two per element (the
    // element and its children), and answers an empty tree for a page deeper than 31 elements, such
    // as a form input in a PagePattern. The tree is read here with room; the app's bridge raises the
    // agent's own limit to match (AddBruinetBridge).
    private static readonly JsonSerializerOptions TreeJson = new() { MaxDepth = 256 };

    // The agent names and sizes only visual elements, so a swipe or menu item arrives without its
    // AutomationId and hidden. The app's bridge names them by Element.Id, whose first 12 hex digits are
    // the node's id (before a window scope or a collision suffix); one read per tree that has such a node.
    private void NameItems(List<ElementInfo> tree)
    {
        if (_noBridge)
            return;
        var nameless = new List<ElementInfo>();
        var pending = new Stack<ElementInfo>(tree);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node.AutomationId is null && node.Type.EndsWith("Item", StringComparison.Ordinal))
                nameless.Add(node);
            foreach (var child in node.Children ?? [])
                pending.Push(child);
        }
        if (nameless.Count == 0)
            return;

        Dictionary<string, (string AutomationId, bool Visible, bool Enabled)> names;
        try
        {
            var body = Run(_client.CallExtensionToolAsync("GET", BridgePath + "/names", null));
            using var answer = JsonDocument.Parse(body);
            if (!answer.RootElement.TryGetProperty("names", out var list))
                return;
            names = list.EnumerateArray()
                .Select(name => (Key: name.GetProperty("id").GetString() ?? string.Empty,
                    Value: (name.GetProperty("automationId").GetString() ?? string.Empty, name.GetProperty("visible").GetBoolean(),
                        !name.TryGetProperty("enabled", out var enabled) || enabled.GetBoolean())))
                .Where(name => name.Key.Length >= 12)
                .GroupBy(name => name.Key[..12])
                .ToDictionary(group => group.Key, group => group.First().Value);
        }
        catch (HttpRequestException missing) when (missing.StatusCode == HttpStatusCode.NotFound)
        {
            _noBridge = true;
            return;
        }
        catch (HttpRequestException)
        {
            return;
        }

        foreach (var node in nameless)
        {
            var id = node.Id.Split(':')[0].Split('_')[0];
            if (names.TryGetValue(id, out var name))
            {
                node.AutomationId = name.AutomationId;
                node.IsVisible = name.Visible;
                node.IsEnabled = name.Enabled;
            }
        }
    }

    public IReadOnlyList<string> QueryCss(string selector) => Run(_client.QueryCssAsync(selector)).Select(e => e.Id).ToList();

    public bool Tap(ElementInfo element) => Run(_client.TapAsync(element.Id, null, null));

    public bool Fill(ElementInfo element, string text) => Run(_client.FillAsync(element.Id, text, null, null));

    public bool Clear(ElementInfo element) => Run(_client.ClearAsync(element.Id, null, null));

    public bool Focus(ElementInfo element) => Run(_client.FocusAsync(element.Id, null, null));

    public bool Gesture(ElementInfo element, string type, string? direction = null, double? distance = null, int? durationMs = null)
        => Run(_client.GestureAsync(type, element.Id, direction, distance, durationMs, null, null));

    public bool Scroll(ElementInfo? element, double deltaX, double deltaY, int? itemIndex = null, string? position = null)
    {
        var done = Run(_client.ScrollAsync(element?.Id, deltaX, deltaY, animated: false, window: null, itemIndex, groupIndex: null,
            scrollToPosition: position, null, null));
        return done;
    }

    public bool Navigate(string route) => Run(_client.NavigateAsync(route));

    public bool Back() => Run(_client.BackAsync());

    public string? GetProperty(string elementId, string name) => Run(_client.GetPropertyAsync(elementId, name));

    public bool SetProperty(ElementInfo element, string name, string value)
        => Run(_client.SetPropertyAsync(element.Id, name, value, null, null));

    public byte[] Screenshot(string? elementId = null) => Run(_client.ScreenshotAsync(elementId: elementId)) ?? [];

    public void ReleaseLease()
    {
        try
        {
            Run(_client.ControlMutationLeaseAsync("release"));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            // The app has gone: there is no lease left to release.
        }
    }

    // The app's bridge extension (BruinetDevFlowBridge); its absence is remembered, so an app
    // without it costs one request, not one per read.
    private const string BridgePath = "/api/v1/ext/dev.bruinet.bridge";
    private bool _noBridge;

    public BridgeAnswer BridgeInvoke(string? automationId, string verb, int arg1 = 0, int arg2 = 0)
        => Bridge("/invoke", new { automationId, verb, arg1, arg2 });

    public BridgeAnswer BridgeExchange(string? automationId, string verb, string argument = "")
        => Bridge("/exchange", new { automationId, verb, argument });

    public BridgeAnswer Media(string automationId, string action, double seconds = 0)
        => Bridge("/media", new { automationId, action, seconds });

    public IReadOnlyList<(string ElementId, string? AutomationId, int Index)>? Rows(string listAutomationId)
    {
        if (_noBridge)
            return null;
        try
        {
            var body = Run(_client.CallExtensionToolAsync("GET", BridgePath + "/rows", JsonSerializer.SerializeToElement(new { automationId = listAutomationId })));
            using var answer = JsonDocument.Parse(body);
            if (!answer.RootElement.TryGetProperty("rows", out var rows))
                return null;
            return rows.EnumerateArray()
                .Select(row => (
                    row.GetProperty("id").GetString() ?? string.Empty,
                    row.TryGetProperty("automationId", out var automationId) ? automationId.GetString() : null,
                    row.GetProperty("index").GetInt32()))
                .ToList();
        }
        catch (HttpRequestException missing) when (missing.StatusCode == HttpStatusCode.NotFound)
        {
            _noBridge = true;
            return null;
        }
    }

    public string? Alert(object? request = null)
    {
        if (_noBridge)
            return null;
        try
        {
            if (request is not null)
                Run(_client.ControlMutationLeaseAsync("claim"));
            return Run(_client.CallExtensionToolAsync(request is null ? "GET" : "POST", BridgePath + "/alert",
                JsonSerializer.SerializeToElement(request ?? new { })));
        }
        catch (HttpRequestException missing) when (missing.StatusCode == HttpStatusCode.NotFound)
        {
            // An app without the route (not iOS, or an older bridge): no alert can be read.
            return null;
        }
    }

    public BridgeAnswer SelectRow(string listAutomationId, int index)
    {
        if (_noBridge)
            return BridgeAnswer.None("the app carries no Bruinet bridge (AddBruinetBridge)");
        try
        {
            return Bridge("/selectrow", new { automationId = listAutomationId, index });
        }
        finally
        {
            // An older bridge without the route answers 404: that is no route, not no bridge.
            _noBridge = false;
        }
    }

    public BridgeAnswer TapBehavior(string? automationId, string? idPrefix, IReadOnlyList<string> exclude)
    {
        if (_noBridge)
            return BridgeAnswer.None("the app carries no Bruinet bridge (AddBruinetBridge)");
        try
        {
            return Bridge("/tapbehavior", new { automationId, id = idPrefix, exclude });
        }
        finally
        {
            // An older bridge without the route answers 404: that is no route, not no bridge.
            _noBridge = false;
        }
    }

    public BridgeAnswer OpenSwipe(string idPrefix, string side)
    {
        if (_noBridge)
            return BridgeAnswer.None("the app carries no Bruinet bridge (AddBruinetBridge)");
        try
        {
            return Bridge("/openswipe", new { id = idPrefix, side });
        }
        finally
        {
            // An older bridge without the route answers 404: that is no route, not no bridge.
            _noBridge = false;
        }
    }

    public bool BridgeDeclares(string? automationId, string verb)
    {
        if (_noBridge)
            return false;
        try
        {
            var body = Run(_client.CallExtensionToolAsync("GET", BridgePath + "/declares",
                JsonSerializer.SerializeToElement(new { automationId = automationId ?? string.Empty, verb })));
            using var answer = JsonDocument.Parse(body);
            return answer.RootElement.TryGetProperty("declared", out var declared) && declared.GetBoolean();
        }
        catch (HttpRequestException missing) when (missing.StatusCode == HttpStatusCode.NotFound)
        {
            _noBridge = true;
            return false;
        }
    }

    private BridgeAnswer Bridge(string route, object request)
    {
        if (_noBridge)
            return BridgeAnswer.None("the app carries no Bruinet bridge (AddBruinetBridge)");
        try
        {
            // A verb changes the app, so it goes under this driver's mutation lease, as the agent's
            // own actions do.
            Run(_client.ControlMutationLeaseAsync("claim"));
            var body = Run(_client.CallExtensionToolAsync("POST", BridgePath + route, JsonSerializer.SerializeToElement(request)));
            using var answer = JsonDocument.Parse(body);
            var root = answer.RootElement;
            return new BridgeAnswer(
                root.GetProperty("declared").GetBoolean(),
                root.GetProperty("delivered").GetBoolean(),
                root.GetProperty("hresult").GetInt32(),
                root.GetProperty("value").GetString() ?? string.Empty,
                root.GetProperty("declined").GetInt32(),
                root.GetProperty("reason").GetString() ?? string.Empty);
        }
        catch (HttpRequestException missing) when (missing.StatusCode == HttpStatusCode.NotFound)
        {
            _noBridge = true;
            return BridgeAnswer.None("the app carries no Bruinet bridge (AddBruinetBridge)");
        }
    }

    public void Dispose() => _client.Dispose();

    // The client's calls are plain HTTP with no synchronization context to return to.
    private static T Run<T>(Task<T> call) => call.ConfigureAwait(false).GetAwaiter().GetResult();
}
