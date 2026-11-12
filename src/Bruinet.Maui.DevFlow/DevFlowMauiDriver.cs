using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using Bruinet.Maui.Configuration;
using Bruinet.Maui.Enums;
using Bruinet.Maui.Interfaces;

namespace Bruinet.Maui.DevFlow;

/// <summary>
/// Bruinet's MAUI driver over the MauiDevFlow agent the app carries (.my/devflow-runner/design.md):
/// lookup in the app's own visual tree, actions as the agent performs them (a tap invokes, a fill
/// sets the text), navigation through Shell and the navigation stack. The same page objects and
/// controls that drive the app through FlaUI and Appium drive it here.
/// </summary>
/// <remarks>
/// <para>
/// It reports the platform the app runs on and otherwise answers as Windows' UI Automation does,
/// since the agent's tree is like that one: off-screen elements are in it, finding is not seeing.
/// </para>
/// <para>
/// Every lookup reads the whole tree in one call and matches it here, so a page another page covers
/// is left out, as a platform's tree leaves it out. A read is kept for <see cref="SnapshotAge"/> and
/// dropped at every action.
/// </para>
/// </remarks>
public sealed class DevFlowMauiDriver : IMauiDriver
{
    /// <summary>The agent's port when neither the options nor DevFlow's broker give one.</summary>
    public const int DefaultPort = 9223;

    private const int BrokerPort = 19223;
    private static readonly TimeSpan SnapshotAge = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(90);

    private readonly MauiDriverOptions? _options;
    private IDevFlowAgent _agent;
    private ILaunchedApp? _app;
    private DevFlowTree? _snapshot;
    private bool _snapshotNative;
    private long _snapshotAt;
    private bool _dialogOpen;
    private bool _disposed;

    internal DevFlowMauiDriver(IDevFlowAgent agent, MauiPlatform platform, ILaunchedApp? app = null, MauiDriverOptions? options = null)
    {
        _agent = agent;
        Platform = platform;
        _app = app;
        _options = options;
    }

    /// <summary>
    /// Launches the app at <see cref="MauiDriverOptions.AppPath"/> (Windows) and waits for its agent,
    /// or, without an app path, attaches to the app whose agent answers on <see cref="MauiDriverOptions.DevFlowPort"/>.
    /// <c>MauiDriverFactory</c> calls it for <see cref="MauiRunner.DevFlow"/>.
    /// </summary>
    /// <param name="options">The driver options.</param>
    /// <returns>The driver, its agent answering.</returns>
    public static IMauiDriver Start(MauiDriverOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (CanLaunch(options))
        {
            var (agent, app) = Launch(options);
            return new DevFlowMauiDriver(agent, PlatformOf(agent, options.Platform), app, options);
        }

        if (string.IsNullOrWhiteSpace(options.AppPath) || options.DevFlowPort is not null)
            return Attach(options.DevFlowPort ?? DefaultPort, options);

        throw new PlatformNotSupportedException(
            $"The DevFlow runner launches only a Windows app so far. For {options.Platform}, start the app built "
            + "with its DevFlow agent, forward its port (adb forward tcp:9223 tcp:9223 on Android), and set "
            + "BRUINET_DEVFLOW_PORT, so the runner attaches to it.");
    }

    private static bool CanLaunch(MauiDriverOptions options)
        => OperatingSystem.IsWindows() && options.Platform == MauiPlatform.Windows
           && options.AppPath?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true;

    private static DevFlowMauiDriver Attach(int port, MauiDriverOptions options)
    {
        var agent = new DevFlowAgent(port);
        if (agent.Status() is not { Running: true })
        {
            agent.Dispose();
            throw new BruinetException(
                $"No DevFlow agent answers on port {port}. Start the app built with its agent "
                + "(builder.AddMauiDevFlowAgent()), or set APPIUM_APP_PATH so the runner launches it.");
        }

        return new DevFlowMauiDriver(agent, PlatformOf(agent, options.Platform), app: null, options);
    }

    /// <summary>
    /// Launches the app with a free port of its own in <c>BRUINET_DEVFLOW_PORT</c> (unless the options
    /// name one), so two apps of one run do not share 9223, and waits for the agent of that process:
    /// on that port, on the one DevFlow's broker gave it, or on 9223 for an app that does not read the variable.
    /// </summary>
    private static (IDevFlowAgent Agent, ILaunchedApp App) Launch(MauiDriverOptions options)
    {
        var appPath = options.AppPath!;
        if (!File.Exists(appPath))
            throw new FileNotFoundException($"The app to launch is not there. Build it with -p:BruinetDevFlow=true: {appPath}", appPath);

        var asked = options.DevFlowPort ?? LaunchedApp.FreePort();
        var environment = new Dictionary<string, string>(options.LaunchSettings, StringComparer.Ordinal)
        {
            [LaunchedApp.PortVariable] = asked.ToString(CultureInfo.InvariantCulture),
        };
        if (!string.IsNullOrWhiteSpace(options.Placement))
            environment["BRUINET_AUT_PLACE"] = options.Placement;

        var app = LaunchedApp.Start(appPath, environment, LaunchTimeout);
        var deadline = Stopwatch.StartNew();
        string? lastWrongApp = null;
        while (deadline.Elapsed < LaunchTimeout)
        {
            if (app.HasExited)
                throw new BruinetException($"{app.ExitDescription} before its DevFlow agent answered: {appPath}");

            foreach (var port in new[] { asked, BrokerPortOf(app.ProcessId) ?? asked, DefaultPort }.Distinct())
            {
                var agent = new DevFlowAgent(port);
                var status = agent.Status();
                if (status is { Running: true } && status.App?.ProcessId == app.ProcessId)
                    return (agent, app);
                if (status?.App?.ProcessId is { } other && other != app.ProcessId && port == asked)
                    lastWrongApp = $"port {port} answers for another app ({status.App.Name}, process {other})";
                agent.Dispose();
            }

            Thread.Sleep(250);
        }

        app.Dispose();
        throw new BruinetException(
            $"The app's DevFlow agent did not answer within {LaunchTimeout.TotalSeconds:0} s. "
            + (lastWrongApp is null
                ? "Was it built with -p:BruinetDevFlow=true (builder.AddMauiDevFlowAgent())?"
                : $"Its port is taken: {lastWrongApp}. Close that app, or give this one another port (BRUINET_DEVFLOW_PORT)."));
    }

    /// <summary>The port DevFlow's broker gave the agent of process <paramref name="processId"/>, when a broker runs.</summary>
    private static int? BrokerPortOf(int processId)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };
            var json = http.GetStringAsync(new Uri($"http://127.0.0.1:{BrokerPort}/api/agents")).GetAwaiter().GetResult();
            using var agents = JsonDocument.Parse(json);
            if (agents.RootElement.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var agent in agents.RootElement.EnumerateArray())
            {
                if (agent.TryGetProperty("processId", out var pid) && pid.TryGetInt32(out var id) && id == processId
                    && agent.TryGetProperty("port", out var port) && port.TryGetInt32(out var number))
                    return number;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            // No broker: the agent serves on its own port.
        }

        return null;
    }

    private static MauiPlatform PlatformOf(IDevFlowAgent agent, MauiPlatform asked)
        => agent.Status()?.Device?.Platform?.ToLowerInvariant() switch
        {
            "winui" or "windows" => MauiPlatform.Windows,
            "android" => MauiPlatform.Android,
            "ios" => MauiPlatform.iOS,
            _ => asked,
        };

    internal IDevFlowAgent Agent => _agent;

    #region Lookup

    /// <summary>
    /// The app's tree as it is now, read at most <see cref="SnapshotAge"/> ago and not since an action.
    /// Native nodes (a dialog, on Windows) cost four times a plain read, so they are read only when a
    /// dialog is asked for (<paramref name="dialogs"/>), and while one is open.
    /// </summary>
    internal DevFlowTree Snapshot(bool dialogs = false)
    {
        var native = dialogs || _dialogOpen;
        if (_snapshot is null || (native && !_snapshotNative) || Stopwatch.GetElapsedTime(_snapshotAt) > SnapshotAge)
        {
            _snapshot = new DevFlowTree(_agent.Tree(native), modalsCover: Platform != MauiPlatform.Windows);
            _snapshotNative = native;
            _snapshotAt = Stopwatch.GetTimestamp();
            if (native)
                _dialogOpen = _snapshot.ActiveDialog() is not null;
        }

        return _snapshot;
    }

    /// <summary>Drops the kept tree: the app changes after an action.</summary>
    internal void Changed() => _snapshot = null;

    public IReadOnlyList<IMauiElement> FindElements(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        var tree = Snapshot();
        return tree.Windows.SelectMany(window => Find(window, locator)).ToList();
    }

    internal IReadOnlyList<IMauiElement> Find(DevFlowNode root, Locator locator, bool includeHidden = false)
        => DevFlowLocators.Match(root, locator, _agent.QueryCss)
            .Where(node => includeHidden || node.Shown)
            .Select(node => (IMauiElement)new DevFlowMauiElement(this, node.Id))
            .ToList();

    #endregion

    #region Session, platform, the app

    public void Close() => Quit();

    public void Quit()
    {
        if (_disposed)
            return;

        _disposed = true;
        _agent.ReleaseLease();
        _agent.Dispose();
        _app?.Dispose();
        _app = null;
    }

    public byte[] GetScreenshot() => _agent.Screenshot();

    public byte[] TakeScreenshot() => _agent.Screenshot();

    public MauiPlatform Platform { get; }

    /// <summary>Shell's tabs and flyout items, as the agent's tree shows them: <c>Tab</c> and <c>FlyoutItem</c> nodes.</summary>
    public ShellChromeLocators ShellChrome { get; } = new(
        TabHost: Locator.ByClassName("Window"),
        Tab: Locator.ByClassName("Tab"),
        FlyoutHost: Locator.ByClassName("Window"),
        FlyoutItem: Locator.ByClassName("FlyoutItem"));

    public IMauiElement AppElement => new DevFlowMauiElement(this, id: null);

    public string Context { get; set; } = "NATIVE_APP";

    public IReadOnlyCollection<string> Contexts => ["NATIVE_APP"];

    public string CurrentWindowHandle => "devflow-" + (_app?.ProcessId.ToString(CultureInfo.InvariantCulture) ?? "attached");

    public IReadOnlyCollection<string> WindowHandles => [CurrentWindowHandle];

    public string GetPageSource() => Snapshot().Format();

    public string GetAutomationTree() => GetPageSource();

    public object? ExecuteScript(string script, params object[] args)
        => throw new RouteUnavailableException(
            "The DevFlow runner runs no scripts; a Blazor WebView's would go through DevFlow's CDP bridge, which Bruinet does not use yet.");

    #endregion

    #region Navigation

    public void NavigateTo(string destination)
    {
        ArgumentException.ThrowIfNullOrEmpty(destination);
        Act(_agent.Navigate(destination), $"navigate to '{destination}'");
    }

    /// <summary>
    /// Goes back as a device's back key does: an open dialog closes first, by its dismissing button,
    /// then the page. Without it a back under an alert went to the page behind and left the alert open.
    /// </summary>
    public void NavigateBack()
    {
        if (Snapshot(dialogs: true).ActiveDialog() is { } dialog
            && dialog.DescendantsAndSelf().LastOrDefault(node => node.Type == "Button") is { } dismiss)
        {
            Act(_agent.Tap(dismiss.Info), "close the dialog");
            return;
        }

        Act(_agent.Back(), "go back");
    }

    public int NavigationDepth() => Snapshot().NavigationDepth();

    // The Shell route where there is one, else the top page's AutomationId, else its type, as the
    // other heads answer (PageNavigation.WaitArrived looks a page up by it).
    public string CurrentRoute()
    {
        if (_agent.Status()?.Route is { Length: > 0 } route)
            return route;

        var top = Snapshot().TopPage();
        return top is null ? string.Empty : string.IsNullOrEmpty(top.AutomationId) ? top.Type : top.AutomationId;
    }

    public void Refresh() => Changed();

    /// <summary>
    /// A launched app is closed and launched again, as a device's reset relaunches it; an attached
    /// one is taken back to its first page.
    /// </summary>
    public void ResetAppState()
    {
        Changed();
        if (_app is not null && _options is not null)
        {
            _agent.ReleaseLease();
            _agent.Dispose();
            _app.Dispose();
            (_agent, _app) = Launch(_options);
            return;
        }

        for (var guard = 0; guard < 20 && NavigationDepth() > 1; guard++)
            NavigateBack();
    }

    /// <summary>
    /// No route: the agent names menu bar items by their title path (<c>File/New</c>), not by
    /// <c>AutomationId</c>, and shows no context menus, so no item can be raised by its id.
    /// </summary>
    public void InvokeMenuItem(string automationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(automationId);
        throw new RouteUnavailableException(
            $"The DevFlow runner cannot raise the menu item '{automationId}': the DevFlow agent names menu bar items by their "
            + "title path, not by AutomationId, and does not show context menus. Run this test on FlaUI.");
    }

    #endregion

    #region Gestures

    public bool SupportsGesture(string automationId, MauiGesture gesture)
        => gesture is not MauiGesture.Pinch and not MauiGesture.Pan && Declared(automationId) is not null;

    public void PerformGesture(string automationId, MauiGesture gesture, int arg1, int arg2)
        => (Declared(automationId) ?? throw new BruinetException($"No element carries the AutomationId '{automationId}'."))
            .PerformGesture(gesture);

    private DevFlowMauiElement? Declared(string automationId)
        => Snapshot().Windows.SelectMany(window => Find(window, Locator.ByAutomationId(automationId), includeHidden: true))
            .FirstOrDefault() as DevFlowMauiElement;

    #endregion

    /// <summary>Throws when the agent said no to an action; drops the kept tree either way.</summary>
    internal void Act(bool done, string what)
    {
        Changed();
        if (!done)
            throw new BruinetException($"The DevFlow agent could not {what}.");
    }

    public void Dispose() => Quit();

    public override string ToString() => $"DevFlow ({Platform}): {Snapshot().TopPage()?.Type}";
}
