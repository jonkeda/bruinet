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

    // BruinetBridgeGate.EnableVariable: the bridge's run-time switch in the app.
    private const string BridgeVariable = "BRUINET_UIA_BRIDGE";
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
            $"The DevFlow runner launches a Windows app and an Android APK so far. For {options.Platform}, start the app "
            + "built with its DevFlow agent and set BRUINET_DEVFLOW_PORT, so the runner attaches to it.");
    }

    private static bool CanLaunch(MauiDriverOptions options)
        => options.Platform switch
        {
            MauiPlatform.Windows => OperatingSystem.IsWindows() && options.AppPath?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true,
            MauiPlatform.Android => options.AppPath?.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) == true,
            _ => false,
        };

    /// <summary>
    /// Installs and starts the APK on the device or emulator the options name (as for Appium: <c>udid</c>),
    /// forwards the agent's port, and waits for the agent of that process. The package is the one the
    /// Appium options wait for (<c>appPackage</c>, <c>appWaitPackage</c>).
    /// </summary>
    private static (IDevFlowAgent Agent, ILaunchedApp App) LaunchOnAndroid(MauiDriverOptions options)
    {
        var package = (options.AdditionalCapabilities.GetValueOrDefault("appPackage")
                       ?? options.AdditionalCapabilities.GetValueOrDefault("appWaitPackage")) as string
                      ?? throw new ArgumentException(
                          "The DevFlow runner needs the app's package to start it on Android: set appPackage or appWaitPackage in the options.");
        var serial = options.Udid ?? options.AdditionalCapabilities.GetValueOrDefault("udid") as string;

        var app = AndroidLaunch.Start(options.AppPath!, package, serial, options.DevFlowPort ?? DefaultPort, options.LaunchSettings);
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < LaunchTimeout)
        {
            var agent = new DevFlowAgent(app.AgentPort);
            var status = agent.Status();
            if (status is { Running: true } && (status.App?.PackageId == package || status.App?.ProcessId == app.ProcessId))
                return (agent, app);
            agent.Dispose();
            Thread.Sleep(500);
        }

        app.Dispose();
        throw new BruinetException(
            $"'{package}' started, and its DevFlow agent did not answer within {LaunchTimeout.TotalSeconds:0} s on the forwarded port. "
            + "Was the APK built with -p:BruinetDevFlow=true (builder.AddMauiDevFlowAgent())?");
    }

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
        if (options.Platform == MauiPlatform.Android)
            return LaunchOnAndroid(options);

        var asked = options.DevFlowPort ?? LaunchedApp.FreePort();
        var environment = new Dictionary<string, string>(options.LaunchSettings, StringComparer.Ordinal)
        {
            [LaunchedApp.PortVariable] = asked.ToString(CultureInfo.InvariantCulture),
        };

        // Turns the app's Bruinet bridge on, as the FlaUI runner does; a launch setting may turn it off.
        environment.TryAdd(BridgeVariable, "1");
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
            var windows = _agent.Tree(native);
            if (native && AndroidAlert() is { } alert)
                windows = [.. windows, alert];
            _snapshot = new DevFlowTree(windows, modalsCover: Platform != MauiPlatform.Windows);
            _snapshotNative = native;
            _snapshotAt = Stopwatch.GetTimestamp();
            if (native)
                _dialogOpen = _snapshot.ActiveDialog() is not null;
        }

        return _snapshot;
    }

    /// <summary>
    /// The alert on an Android screen, which the agent's tree does not show, read from a UiAutomator
    /// dump (<see cref="AndroidDialogs"/>). A dump takes a second or two, so it is taken only when the
    /// app's bridge says an alert is open, or when the app cannot say.
    /// </summary>
    private Microsoft.Maui.DevFlow.Driver.ElementInfo? AndroidAlert()
    {
        if (_app is not AndroidLaunch android)
            return null;

        var asked = _agent.BridgeExchange(null, "CurrentAlert");
        if (asked.Declared && asked.Delivered && asked.Value.Length == 0)
            return null;

        return AndroidDialogs.Read(android.DumpScreen());
    }

    /// <summary>Whether lists here scroll by touch: on Android, where the agent's scroll moves no list.</summary>
    internal bool SwipesLists => _app is AndroidLaunch;

    /// <summary>
    /// A swipe over a list on Android, over adb: forward moves the content on (the finger goes up). The
    /// agent's jump to an index does not count as the user's scroll, so a paging list's
    /// RemainingItemsThreshold fires only on this, and its scroll by an amount moves no Android list.
    /// </summary>
    internal void SwipeList(Microsoft.Maui.DevFlow.Driver.ElementInfo list, bool forward)
    {
        if (_app is not AndroidLaunch android || list.WindowBounds is not { Width: > 0, Height: > 0 } bounds)
            return;

        // The agent's bounds are in device-independent units; adb's input is in pixels.
        _density ??= _agent.Status()?.Device?.DisplayDensity is > 0 and var density ? density : 1;
        var scale = _density.Value;
        var x = (int)((bounds.X + bounds.Width / 2) * scale);
        var low = (int)((bounds.Y + bounds.Height * 0.8) * scale);
        var high = (int)((bounds.Y + bounds.Height * 0.2) * scale);
        android.Swipe(x, forward ? low : high, x, forward ? high : low);
        Changed();
        Settle();
    }

    private double? _density;

    /// <summary>A tap on a node of an Android alert, over adb.</summary>
    internal void TapAndroidDialog(Microsoft.Maui.DevFlow.Driver.ElementInfo node)
    {
        var (x, y) = AndroidDialogs.Centre(node);
        ((AndroidLaunch)_app!).Tap(x, y);
        Changed();
        Settle();
    }

    /// <summary>Types into an Android alert's field over adb: tapped first, its text replaced or appended to.</summary>
    internal void TypeIntoAndroidDialog(Microsoft.Maui.DevFlow.Driver.ElementInfo field, string text, bool replace)
    {
        var android = (AndroidLaunch)_app!;
        var (x, y) = AndroidDialogs.Centre(field);
        android.Tap(x, y);
        if (replace)
            android.Delete(field.Text?.Length ?? 0);
        android.Type(text);
        Changed();
    }

    /// <summary>
    /// A list row's item index, as UI Automation's <c>PositionInSet</c> gives it on Windows: asked of the
    /// app's bridge once per tree read and list, and matched to the agent's ids, which are made from
    /// each element's <c>Element.Id</c> (its AutomationId, with the id's first 8 hex digits where the
    /// AutomationId repeats; the first 12 without one). Null without a bridge, or for a row it cannot place.
    /// </summary>
    internal int? RowIndex(DevFlowNode row)
    {
        if (row.Parent is not { AutomationId: { Length: > 0 } listId } list || !DevFlowLocators.IsItemsView(list))
            return null;

        var tree = Snapshot();
        if (!_rowIndexes.TryGetValue(tree, out var byList))
        {
            byList = [];
            _rowIndexes.Clear();
            _rowIndexes[tree] = byList;
        }

        if (!byList.TryGetValue(list.Id, out var indexes))
            byList[list.Id] = indexes = MatchRows(list, _agent.Rows(listId));
        return indexes.TryGetValue(row.Id, out var index) && index >= 0 ? index : null;
    }

    /// <summary>
    /// Whether a list row is a container still showing an item the list no longer has, as the app's
    /// bridge says: left out of lookups, as a platform tree drops the removed item's row.
    /// </summary>
    internal bool IsRemovedRow(DevFlowNode row)
    {
        if (row.Parent is not { AutomationId: { Length: > 0 } } list || !DevFlowLocators.IsItemsView(list))
            return false;
        _ = RowIndex(row);
        // Also a row the app no longer lists, though it answered for the list: on Android a removed
        // item's row stays in the tree while it animates out, after the count has changed.
        return _rowIndexes.TryGetValue(Snapshot(), out var byList) && byList.TryGetValue(list.Id, out var indexes)
               && indexes.ContainsKey(Answered)
               && (!indexes.TryGetValue(row.Id, out var index) || index == -2);
    }

    // In a list's matched rows: the app's bridge answered for that list.
    private const string Answered = "\u0000answered";

    private readonly Dictionary<DevFlowTree, Dictionary<string, Dictionary<string, int>>> _rowIndexes = [];

    private static Dictionary<string, int> MatchRows(DevFlowNode list, IReadOnlyList<(string ElementId, string? AutomationId, int Index)>? rows)
    {
        var matched = new Dictionary<string, int>(StringComparer.Ordinal);
        if (rows is null)
            return matched;
        matched[Answered] = 0;

        var left = rows.ToList();
        var plain = new List<DevFlowNode>();
        foreach (var row in list.Children)
        {
            var tail = row.Id[(row.Id.LastIndexOf('_') + 1)..];
            var prefix = row.Id.Length == 12 && row.AutomationId is null ? row.Id
                : tail.Length == 8 && tail.All(char.IsAsciiHexDigitLower) && row.Id.Contains('_', StringComparison.Ordinal) ? tail
                : null;
            if (prefix is null)
            {
                plain.Add(row);
                continue;
            }

            var found = left.FindIndex(candidate => candidate.ElementId.StartsWith(prefix, StringComparison.Ordinal));
            if (found >= 0)
            {
                matched[row.Id] = left[found].Index;
                left.RemoveAt(found);
            }
        }

        // A row whose id is its bare AutomationId (the first the agent met with it) takes the one row
        // left with that AutomationId.
        foreach (var row in plain)
        {
            var same = left.Where(candidate => candidate.AutomationId == row.AutomationId).ToList();
            if (same.Count == 1)
            {
                matched[row.Id] = same[0].Index;
                left.Remove(same[0]);
            }
        }

        return matched;
    }

    /// <summary>Drops the kept tree: the app changes after an action.</summary>
    internal void Changed() => _snapshot = null;

    public IReadOnlyList<IMauiElement> FindElements(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        var tree = Snapshot();
        return tree.Windows.SelectMany(window => Find(window, locator)).ToList();
    }

    internal IReadOnlyList<IMauiElement> Find(DevFlowNode root, Locator locator, bool includeHidden = false, string? rowKey = null)
    {
        var found = DevFlowLocators.Match(root, locator, _agent.QueryCss)
            .Where(node => includeHidden || node.Shown && !IsRemovedRow(node))
            .Select(node => (IMauiElement)new DevFlowMauiElement(this, node.Id, rowKey))
            .ToList();

        // A media element's transport controls, which the platform draws outside the agent's tree,
        // stand in under WinUI's ids for the media elements on screen (DevFlowMediaPart). See AD-037.
        if (found.Count == 0 && locator is { Strategy: LocatorStrategy.AutomationId, Parent: null }
            && DevFlowMediaPart.Parts.Contains(locator.Value))
        {
            return root.DescendantsAndSelf()
                .Where(node => node.Type == "MediaElement" && (includeHidden || node.Shown))
                .Select(node => (IMauiElement)new DevFlowMediaPart(this, node.Id, locator.Value))
                .ToList();
        }

        return found;
    }

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

    /// <summary>
    /// The device's culture on Android, from its locale: the app formats dates and times with it, and
    /// the fixture reads them with it (as for Appium). Null on Windows, where the app runs on this
    /// machine with its culture, and where the device does not say.
    /// </summary>
    public CultureInfo? DeviceCulture
    {
        get
        {
            if (_app is not AndroidLaunch android || android.Locale() is not { } locale)
                return null;
            try
            {
                return CultureInfo.GetCultureInfo(locale.Replace('_', '-'));
            }
            catch (CultureNotFoundException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Shell's tabs and flyout items, as the agent draws them: its own <c>Tab</c> and <c>FlyoutItem</c>
    /// nodes, directly under the Shell (the scope itself), named by the agent's type so Shell's own
    /// FlyoutItem and Tab elements, which describe the structure, are not taken for them.
    /// </summary>
    public ShellChromeLocators ShellChrome { get; } = new(
        TabHost: Locator.ByXPath("."),
        Tab: Locator.ByClassName("Microsoft.Maui.DevFlow.Agent.Core.Tab"),
        FlyoutHost: Locator.ByXPath("."),
        FlyoutItem: Locator.ByClassName("Microsoft.Maui.DevFlow.Agent.Core.FlyoutItem"));

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
            if (AndroidDialogs.IsDialogNode(dismiss.Id))
                TapAndroidDialog(dismiss.Info);
            else
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
    /// A menu bar or context menu item by its <c>AutomationId</c>, raised by the app's bridge without
    /// opening a menu, as the Windows bridge raises it. The agent alone names menu bar items by their
    /// title path and shows no context menus, so without the bridge there is no route.
    /// </summary>
    public void InvokeMenuItem(string automationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(automationId);
        var answer = _agent.BridgeExchange(null, "InvokeMenuItem", automationId);
        Changed();
        if (!answer.Declared)
        {
            throw new RouteUnavailableException(
                $"The DevFlow runner cannot raise the menu item '{automationId}': the app carries no Bruinet bridge "
                + "(AddBruinetBridge, BRUINET_UIA_BRIDGE=1), and the agent names menu items by title path, not AutomationId.");
        }

        // As on Windows: the outcome travels in the answer, and "disabled" is a refusal a user would meet too.
        if (answer.Delivered)
        {
            if (answer.Value != "disabled")
                return;
            throw new ElementNotReadyException(Locator.ByAutomationId(automationId), NotReadyReason.Disabled,
                "the menu item is disabled, so a user could not have picked it either");
        }

        var reason = answer.HResult switch
        {
            unchecked((int)0x80070057) => "no AutomationId was given",
            unchecked((int)0x80040201) => "no menu item on any open page carries that AutomationId. Menu items are matched "
                                          + "by the id in the app's markup, not by their text",
            _ => answer.Reason,
        };
        throw new BruinetException($"Could not invoke menu item '{automationId}': {reason}.");
    }

    #endregion

    #region Gestures

    // What the app's bridge declares on the element, else what the agent can do semantically.
    public bool SupportsGesture(string automationId, MauiGesture gesture)
        => _agent.BridgeDeclares(automationId, gesture.ToString())
           || gesture is not MauiGesture.Pinch and not MauiGesture.Pan && Declared(automationId) is not null;

    public void PerformGesture(string automationId, MauiGesture gesture, int arg1, int arg2)
        => (Declared(automationId) ?? throw new BruinetException($"No element carries the AutomationId '{automationId}'."))
            .PerformGesture(gesture, arg1, arg2);

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
        Settle();
    }

    /// <summary>
    /// Whether the app has finished its queued work, as its bridge's <c>IsIdle</c> verb says (it waits
    /// for a post to the UI thread to run). True where the app carries no bridge: there is nothing to ask.
    /// </summary>
    public bool IsIdle(int timeoutMs = 2000)
    {
        var answer = _agent.BridgeExchange(null, "IsIdle", Math.Max(0, timeoutMs).ToString(CultureInfo.InvariantCulture));
        return !answer.Declared || answer.Delivered && bool.TryParse(answer.Value, out var idle) && idle;
    }

    /// <summary>
    /// Lets the app finish what an action queued before the next read, as the FlaUI runner does after
    /// a write: a row deleted left its count changed and its container still showing it a moment
    /// longer. An app that never answers is asked a few times, then not again.
    /// </summary>
    internal void Settle()
    {
        if (_settleUnanswered >= 3)
            return;

        var answer = _agent.BridgeExchange(null, "IsIdle", "1000");
        _settleUnanswered = answer.Delivered ? 0 : _settleUnanswered + 1;
        Changed();
    }

    private int _settleUnanswered;

    public void Dispose() => Quit();

    public override string ToString() => $"DevFlow ({Platform}): {Snapshot().TopPage()?.Type}";
}
