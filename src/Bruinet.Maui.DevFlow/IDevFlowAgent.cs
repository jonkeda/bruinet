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
        // The agent refuses a read while the UI changes under it and asks for the read again.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return Run(_client.GetTreeAsync(0, null, includeNative));
            }
            catch (InvalidOperationException changing) when (attempt < 4 && changing.Message.Contains("kept changing", StringComparison.Ordinal))
            {
                Thread.Sleep(50 * attempt);
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
