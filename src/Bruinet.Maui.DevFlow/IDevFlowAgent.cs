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

    public void Dispose() => _client.Dispose();

    // The client's calls are plain HTTP with no synchronization context to return to.
    private static T Run<T>(Task<T> call) => call.ConfigureAwait(false).GetAwaiter().GetResult();
}
