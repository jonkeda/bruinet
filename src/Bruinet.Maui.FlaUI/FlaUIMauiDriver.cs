using System.Diagnostics;
using System.Runtime.InteropServices;
using Bruinet.Core;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Utilities;
using Bruinet.Maui.Configuration;
using Bruinet.Maui.FlaUI.Bridge;
using Bruinet.FlaUI;
using Bruinet.Uia;
using Bruinet.Maui.Enums;
using FlaUI.Core.Definitions;
using System.Globalization;

namespace Bruinet.Maui.FlaUI;

/// <summary>
/// FlaUI-based implementation of <see cref="IMauiDriver"/> for Windows platform.
/// Provides native Windows UI Automation support for MAUI desktop apps.
/// </summary>
internal sealed class FlaUIMauiDriver : IMauiDriver, IDisposable
{
    private readonly DesktopApp _app;
    private bool _disposed;

    /// <summary>
    /// Creates a new FlaUIMauiDriver for an existing window.
    /// </summary>
    /// <param name="windowHandle">The window handle to attach to.</param>
    public FlaUIMauiDriver(IntPtr windowHandle)
    {
        _app = DesktopApp.Attach(windowHandle);
    }

    /// <summary>
    /// Creates a new FlaUIMauiDriver by launching an application.
    /// </summary>
    /// <param name="executablePath">Path to the application executable.</param>
    /// <param name="arguments">Optional command line arguments.</param>
    public FlaUIMauiDriver(string executablePath, string? arguments = null)
        : this(executablePath, arguments, environment: null)
    {
    }

    /// <summary>
    /// Creates a new FlaUIMauiDriver by launching an application with extra environment variables.
    /// </summary>
    /// <param name="executablePath">Path to the application executable.</param>
    /// <param name="arguments">Optional command line arguments.</param>
    /// <param name="environment">
    /// Variables set on the launched process only (<c>MauiDriverOptions.LaunchSettings</c>): the
    /// test processs own environment is not changed, so each launch carries its own settings.
    /// </param>
    public FlaUIMauiDriver(string executablePath, string? arguments, IReadOnlyDictionary<string, string>? environment)
    {
        // Ask the app to turn its gesture bridge on. Asking is all this is: an app that was not
        // built with a bridge has nothing to turn on - see BruinetBridgeGate. The callers
        // variables come after, so a launch setting may still turn it off.
        var launchEnvironment = new Dictionary<string, string> { [BruinetBridgeGate.EnableVariable] = "1" };
        foreach (var (name, value) in environment ?? new Dictionary<string, string>())
        {
            launchEnvironment[name] = value;
        }

        _app = DesktopApp.Launch(executablePath, arguments, launchEnvironment);
    }

    /// <summary>
    /// Creates a new FlaUIMauiDriver by attaching to a running process.
    /// </summary>
    /// <param name="process">The process to attach to.</param>
    public FlaUIMauiDriver(Process process)
    {
        _app = DesktopApp.Attach(process);
    }

    #region Platform

    /// <inheritdoc />
    public MauiPlatform Platform => MauiPlatform.Windows;

    #endregion

    #region The app

    /// <inheritdoc />
    public IMauiElement AppElement => FlaUIMauiElement.ForApp(this);

    /// <inheritdoc />
    /// <inheritdoc />
    public bool ActivatesRowsThroughContainers => true;

    public ShellChromeLocators ShellChrome { get; } = new(
        TabHost: Locator.ByAutomationId("TopNavMenuItemsHost"),
        Tab: Locator.ByControlType("TabItem"),
        FlyoutHost: Locator.ByAutomationId("MenuItemsHost"),
        FlyoutItem: Locator.ByControlType("ListItem"));

    #endregion

    #region Internal

    /// <summary>
    /// Gets the condition factory for building search conditions.
    /// </summary>
    internal ConditionFactory ConditionFactory => _app.ConditionFactory;

    /// <summary>
    /// Gets the underlying automation instance.
    /// </summary>
    internal UIA3Automation Automation => _app.Automation;

    /// <summary>The app's top-level window. Where a bridge lookup starts.</summary>
    internal AutomationElement RootElement => _app.RootElement;

    /// <summary>
    /// Whether the app this driver launched has exited.
    /// </summary>
    internal bool AppHasExited => _app.HasExited;

    /// <summary>The desktop app session the driver reads.</summary>
    internal DesktopApp App => _app;

    /// <summary>How many times the root element had gone stale and was attached again.</summary>
    public int RootReattachments => _app.RootReattachments;

    /// <summary>
    /// How many times the app under test took the foreground and had to be put back.
    /// </summary>
    public int ForegroundGrabs => _app.ForegroundGrabs;

    #endregion

    #region Element Finding

    /// <summary>
    /// Asks the app's bridge: the first published target that answers <paramref name="verb"/>.
    /// </summary>
    private Bridge.BridgeVerbResult Exchange(BruinetVerb verb, string argument = "")
    {
        var answer = Bridge.BridgeVerbRunner.ExchangeAnywhere(RootElement, Automation, verb, argument);
        if (!answer.Delivered && AppHasExited)
        {
            throw new AppUnavailableException("the application process has exited.");
        }

        return answer;
    }

    /// <inheritdoc />
    public IReadOnlyList<IMauiElement> FindElements(Locator locator)
    {
        var condition = locator.ToCondition(ConditionFactory);

        try
        {
            return RootElement.FindAllDescendants(condition)
                .Select(e => new FlaUIMauiElement(e, this))
                .ToList();
        }
        catch (Exception error) when (FlaUIErrors.IsElementGone(error) && AppHasExited)
        {
            throw new AppUnavailableException("the application process has exited.", error);
        }
    }

    /// <inheritdoc />
    /// <remarks>Stops at the first match rather than collecting the whole tree.</remarks>
    public IMauiElement? TryFindElement(Locator locator)
    {
        var condition = locator.ToCondition(ConditionFactory);

        try
        {
            var found = RootElement.FindFirstDescendant(condition);
            return found == null ? null : new FlaUIMauiElement(found, this);
        }
        catch (Exception error) when (FlaUIErrors.IsElementGone(error) && AppHasExited)
        {
            throw new AppUnavailableException("the application process has exited.", error);
        }
    }

    /// <summary>
    /// Finds a piece of window chrome the driver acts on itself, waiting for it to appear.
    /// </summary>
    /// <param name="locator">The chrome element's locator.</param>
    /// <param name="timeoutMs">How long to wait for it to appear.</param>
    /// <returns>The element.</returns>
    /// <exception cref="ElementNotFoundException">It did not appear in time.</exception>
    internal IMauiElement FindChrome(Locator locator, int timeoutMs)
    {
        var condition = locator.ToCondition(ConditionFactory);
        timeoutMs = Bruinet.Core.Calls.CallBudget.Within(timeoutMs);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        do
        {
            var found = RootElement.FindFirstDescendant(condition);
            if (found != null)
            {
                return new FlaUIMauiElement(found, this);
            }

            Thread.Sleep(100);
        }
        while (stopwatch.ElapsedMilliseconds < timeoutMs);

        throw new ElementNotFoundException(locator);
    }

    #endregion

    #region Window Management

    /// <inheritdoc />
    public string CurrentWindowHandle => RootElement.Properties.NativeWindowHandle.Value.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public IReadOnlyCollection<string> WindowHandles => _app.WindowHandles();

    #endregion

    #region Session Management

    /// <inheritdoc />
    public void Quit()
    {
        _app.CloseApp();
    }

    /// <inheritdoc />
    public void Close() => _app.CloseWindow();

    #endregion

    #region Screenshots

    /// <inheritdoc />
    public byte[] GetScreenshot() => _app.Screenshot();

    #endregion

    #region Context Switching (Not applicable for FlaUI)

    /// <inheritdoc />
    public string Context
    {
        get => "NATIVE_APP";
        set { } // No-op for FlaUI
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Contexts => new[] { "NATIVE_APP" };

    #endregion

    #region Script Execution (Not applicable for FlaUI)

    /// <inheritdoc />
    public object? ExecuteScript(string script, params object[] args)
    {
        // FlaUI doesn't support script execution
        throw new NotSupportedException("Script execution is not supported by FlaUI driver");
    }

    #endregion

    #region IDiagnosticDriver

    /// <inheritdoc />
    public string GetPageSource() => _app.AutomationTree();

    /// <inheritdoc />
    public string GetAutomationTree() => _app.AutomationTree();

    #endregion

    #region Gestures (Bruinet UI Automation bridge)

    /// <inheritdoc />
    public bool SupportsGesture(string automationId, MauiGesture gesture)
        => GestureRunner.Supports(RootElement, Automation, automationId, gesture);

    /// <inheritdoc />
    /// <exception cref="Bridge.GestureUnavailableException">
    /// The app publishes no bridge, the element was not declared, or the verb was refused.
    /// </exception>
    public void PerformGesture(string automationId, MauiGesture gesture)
    {
        using var mark = ReportGesture(automationId, gesture);
        WhileTheAppRuns(() => GestureRunner.Perform(RootElement, Automation, automationId, gesture));
    }

    /// <inheritdoc />
    /// <exception cref="Bridge.GestureUnavailableException">
    /// The app publishes no bridge, the element was not declared, or the verb was refused.
    /// </exception>
    public void PerformGesture(string automationId, MauiGesture gesture, int arg1, int arg2)
    {
        using var mark = ReportGesture(automationId, gesture);
        WhileTheAppRuns(() => GestureRunner.Perform(RootElement, Automation, automationId, gesture, arg1, arg2));
    }

    // A recorded video's touch mark (AD-024): a control the tree cannot show (a RefreshView, a
    // SwipeView) has its bounds from the bridge.
    private Bruinet.Core.Recording.Interactions.Scope ReportGesture(string automationId, MauiGesture gesture)
        => MauiInteractions.Report(gesture, () => BruinetBridgeLookup.TargetBounds(RootElement, Automation, automationId), () => automationId);

    /// <summary>
    /// Runs a bridge gesture; a gesture that found no target because the app has exited is
    /// <see cref="AppUnavailableException"/>, not "unavailable" (see <see cref="Exchange"/>).
    /// </summary>
    private void WhileTheAppRuns(Action gesture)
    {
        try
        {
            gesture();
        }
        catch (Bridge.GestureUnavailableException error) when (AppHasExited)
        {
            throw new AppUnavailableException("the application process has exited.", error);
        }
    }

    /// <summary>Reads app-published state for a bridge target, which may have no node in the tree.</summary>
    /// <remarks>
    /// The bridge is the only route, so a target a page has not published yet is waited for
    /// briefly (<see cref="BridgeVerbRunner.Send"/>): a Stepper's <c>SetValue</c> read its
    /// <c>Minimum</c> in the moment after a navigation and failed as "not on the bridge".
    /// </remarks>
    internal string ReadState(string automationId, string property)
    {
        var answer = BridgeVerbRunner.Send(
            RootElement, Automation, automationId, BruinetVerb.GetState, property, onlyRoute: true);

        if (!answer.Delivered && AppHasExited)
        {
            throw new AppUnavailableException("the application process has exited.");
        }

        if (!answer.Delivered)
        {
            throw new NotSupportedException(
                $"'{automationId}' does not answer GetState('{property}'). The bridge said: "
                + answer.Reason);
        }

        return answer.Value;
    }

    /// <summary>
    /// Whether the app under test publishes a Bruinet bridge at all.
    /// </summary>
    /// <returns>Whether a bridge window is present.</returns>
    public bool HasGestureBridge()
        => Bridge.BruinetBridgeLookup.HasBridge(RootElement, Automation);

    /// <summary>
    /// Describes the raw automation tree just below the app window.
    /// </summary>
    /// <param name="maxDepth">How far below the window to walk.</param>
    /// <returns>One line per element, indented by depth.</returns>
    public string DescribeGestureBridge(int maxDepth = 3)
        => Bridge.BruinetBridgeLookup.Describe(RootElement, Automation, maxDepth);

    /// <summary>
    /// Reports what each instrumented element offers somebody who is not using a pointer.
    /// </summary>
    /// <param name="page">Where the app is, so a merged report says where each element was seen.</param>
    /// <returns>One finding per published element.</returns>
    public Bridge.AccessibilityAuditReport AuditGestureAccessibility(string page = "")
        => Bridge.AccessibilityAudit.Run(RootElement, Automation, page);

    /// <inheritdoc />
    public bool IsAtNavigationRoot() => NavigationDepth() <= 1;

    /// <inheritdoc />
    public bool IsIdle(int timeoutMs = 2000)
    {
        var answer = Exchange(BruinetVerb.IsIdle,
            timeoutMs.ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (!answer.Delivered)
        {
            throw new NotSupportedException(
                "The app under test cannot say whether it is idle, so there is nothing to wait on "
                + "and every such wait has to go back to being a sleep. Declare IsIdle on its "
                + $"pages - see GestureAutomation.Verbs. The bridge said: {answer.Reason}");
        }

        return bool.TryParse(answer.Value, out var idle) && idle;
    }

    /// <summary>
    /// Lets the app finish what a UI Automation write queued, where the app can say so.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A Value pattern write lands in the WinUI <c>TextBox</c> at once, but WinUI raises
    /// <c>TextChanged</c> later, and only that event carries the text to MAUI's <c>Entry.Text</c>
    /// and its binding. A Save invoked straight after the write ran before it: the box showed the
    /// new comment and the view model still had the old one (Construction's garbage editor). A
    /// person typing never meets this, because each keystroke is followed by human time.
    /// </para>
    /// <para>
    /// The app's <c>IsIdle</c> verb posts to the dispatcher and waits for that post to run, which
    /// is "everything queued before now has finished". An app that never answers it is asked a
    /// few times - a page between publications answers nothing either - and then not again.
    /// </para>
    /// </remarks>
    internal void SettleAfterWrite()
    {
        if (!_settleAnswered && _settleUnanswered >= SettleAttemptsWithoutAnswer)
            return;

        var answer = Exchange(BruinetVerb.IsIdle,
            Bruinet.Core.Calls.CallBudget.Within(2_000).ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (answer.Delivered)
            _settleAnswered = true;
        else
            _settleUnanswered++;
    }

    private const int SettleAttemptsWithoutAnswer = 3;
    private bool _settleAnswered;
    private int _settleUnanswered;

    /// <inheritdoc />
    public int NavigationDepth()
    {
        var transitionBudgetMs = Bruinet.Core.Calls.CallBudget.Within(2_000);
        const int pollMs = 25;

        var clock = System.Diagnostics.Stopwatch.StartNew();
        Bridge.BridgeVerbResult answer;
        int depth;

        while (true)
        {
            answer = Exchange(BruinetVerb.GetState, "NavigationDepth");

            if (answer.Delivered
                && int.TryParse(
                    answer.Value, System.Globalization.CultureInfo.InvariantCulture, out depth))
            {
                break;
            }

            // Any non-delivery is retried, including "nothing published answers GetState at all".
            //
            // That case looks like a configuration error and usually is one - but measured, it is
            // also what a page transition produces: the outgoing page has withdrawn and the
            // incoming one has not published yet, so for a few milliseconds the bridge is empty
            // and there is nobody to ask. Distinguishing them by the count was tried and is
            // wrong. An app that really declares no GetState pays the budget once and then gets
            // the message below, which names the fix.
            if (clock.ElapsedMilliseconds >= transitionBudgetMs)
            {
                break;
            }

            Thread.Sleep(pollMs);
        }

        if (!answer.Delivered
            || !int.TryParse(answer.Value, System.Globalization.CultureInfo.InvariantCulture, out depth))
        {
            throw new NotSupportedException(
                "The app under test cannot say how deep its navigation stack is, so whether it is "
                + "at the root is not knowable. Declare GetState on the app's pages - see "
                + "GestureAutomation.Verbs - or drive the back affordance as a control instead. "
                + $"The bridge said: {answer.Reason}" + Environment.NewLine
                + "What is below the app window, so an empty bridge can be told from no bridge:"
                + Environment.NewLine
                + DescribeBridgeSafely());
        }

        return depth;
    }

    /// <summary>The raw tree under the window, or why it could not be read.</summary>
    private string DescribeBridgeSafely()
    {
        try
        {
            return DescribeGestureBridge(maxDepth: 2);
        }
        catch (Exception describing)
        {
            return $"(the tree could not be read: {describing.Message})";
        }
    }

    /// <inheritdoc />
    public void NavigateBack()
    {
        using var mark = Bruinet.Core.Recording.Interactions.Report(Bruinet.Core.Recording.InteractionKind.Key, () => System.Drawing.Rectangle.Empty, key: "Back");
        var publishWindowMs = Bruinet.Core.Calls.CallBudget.Within(1_000);
        const int pollMs = 25;

        var clock = System.Diagnostics.Stopwatch.StartNew();
        Bridge.BridgeVerbResult result;

        while (true)
        {
            result = Bridge.BridgeVerbRunner.InvokeAnywhere(
                RootElement, Automation, BruinetVerb.NavigateBack);

            if (result.Delivered)
            {
                return;
            }

            // Something was asked and refused: the app has answered, and the answer is no.
            // Nothing was asked at all means no page has published yet, which a page transition
            // produces for a few milliseconds and a misconfigured app produces forever - the
            // budget tells them apart without needing to know which.
            if (result.Declined > 0 || clock.ElapsedMilliseconds >= publishWindowMs)
            {
                break;
            }

            Thread.Sleep(pollMs);
        }

        throw new BruinetException(
            "The app under test did not go back. This is a navigation failure with a specific "
            + $"cause, not something to retry: {result.Reason}");
    }


    #endregion

    #region Navigation

    /// <inheritdoc />
    public void NavigateTo(string destination)
    {
        var result = Exchange(BruinetVerb.NavigateTo, destination);

        if (!result.Delivered)
        {
            throw new BruinetException(
                $"The app under test did not navigate to '{destination}'. Routes are a Shell "
                + "concept, so an app using a NavigationPage has none to go to. "
                + $"The bridge said: {result.Reason}");
        }
    }

    /// <inheritdoc />
    public string CurrentRoute()
    {
        var result = Exchange(BruinetVerb.CurrentRoute);

        if (!result.Delivered)
        {
            throw new NotSupportedException(
                "The app under test cannot say where it is. Declare CurrentRoute on its pages - "
                + $"see GestureAutomation.Verbs. The bridge said: {result.Reason}");
        }

        return result.Value;
    }

    /// <inheritdoc />
    public void Refresh() => NavigateTo(CurrentRoute());

    /// <inheritdoc />
    public byte[] TakeScreenshot() => GetScreenshot();

    /// <inheritdoc />
    public void ResetAppState()
    {
        // For desktop apps, close and relaunch.
        // Note: This doesn't fully reset - caller may need to recreate the driver.
        _app.CloseApp();
    }

    #endregion

    #region Dialogs

    /// <inheritdoc />
    public void InvokeMenuItem(string automationId)
    {
        var answer = Exchange(BruinetVerb.InvokeMenuItem, automationId);

        // The outcome is in the payload, not in the HRESULT, and that is not a style choice.
        // A success HRESULT does not survive UI Automation's custom-pattern marshalling: the app
        // returning S_FALSE - "I found it and did nothing" - reaches this side as S_OK, measured
        // both ways round. Anything a caller must be able to tell apart from success therefore
        // has to travel as a value or as a failure HRESULT.
        if (answer.Delivered)
        {
            if (answer.Value != MenuItemDisabled)
            {
                return;
            }

            // Nothing was picked: waited for within the call's budget, as for toolbar items.
            throw new ElementNotReadyException(
                Locator.ByAutomationId(automationId),
                NotReadyReason.Disabled,
                "the menu item is disabled, so a user could not have picked it either");
        }

        if (answer.Declined == 0)
        {
            // Nothing on the app's bridge answered, so nothing was picked: as for toolbar items.
            throw new ElementNotReadyException(
                Locator.ByAutomationId(automationId),
                NotReadyReason.Other,
                answer.Reason);
        }

        var reason = answer.HResult switch
        {
            HResults.E_INVALIDARG => "no AutomationId was given",

            HResults.UIA_E_ELEMENTNOTAVAILABLE =>
                "no menu item on any open page carries that AutomationId. Menu items are matched "
                + "by the id in the app's markup, not by their text",

            _ => answer.Reason,
        };

        throw new BruinetException($"Could not invoke menu item '{automationId}': {reason}.");
    }

    /// <summary>Invokes the toolbar item that carries this AutomationId.</summary>
    internal void InvokeToolbarItem(string automationId)
    {
        var answer = Exchange(BruinetVerb.InvokeToolbarItem, automationId);

        // In the payload, as for menu items: a success HRESULT cannot say "found it, declined".
        if (answer.Delivered)
        {
            if (answer.Value != MenuItemDisabled)
            {
                return;
            }

            // Nothing was pressed: a command enabled a moment late is waited for within the call's
            // budget, and one that never re-enables fails as "disabled" when it runs out.
            throw new ElementNotReadyException(
                Locator.ByAutomationId(automationId),
                NotReadyReason.Disabled,
                "the toolbar item is disabled, so a user could not have pressed it either");
        }

        if (answer.Declined == 0)
        {
            // Nothing on the app's bridge answered, so nothing was pressed: the page is still
            // publishing its bridge (seen right after navigating back to it). Waited for like any
            // other not-ready element; a bridge that never answers fails when the budget runs out.
            throw new ElementNotReadyException(
                Locator.ByAutomationId(automationId),
                NotReadyReason.Other,
                answer.Reason);
        }

        var reason = answer.HResult switch
        {
            HResults.E_INVALIDARG => "no AutomationId was given",

            HResults.UIA_E_ELEMENTNOTAVAILABLE =>
                "no toolbar item on the page on screen carries that AutomationId. Toolbar items "
                + "are matched by the id in the app's markup, not by their text",

            _ => answer.Reason,
        };

        throw new BruinetException($"Could not invoke toolbar item '{automationId}': {reason}.");
    }

    /// <summary>
    /// What <c>InvokeMenuItem</c> and <c>InvokeToolbarItem</c> answer with when they declined to
    /// raise the item.
    /// </summary>
    private const string MenuItemDisabled = "disabled";

    /// <summary>Opens the Shell's flyout through the app's verb.</summary>
    internal void OpenFlyout() => PresentFlyout(BruinetVerb.OpenFlyout, "open");

    /// <summary>Whether the app declares the flyout verbs.</summary>
    internal bool SupportsFlyoutVerbs
        => Bridge.BruinetBridgeLookup.Targets(RootElement, Automation).Any(target =>
        {
            var verbs = target.SupportedVerbs();
            return verbs.Contains(BruinetVerb.OpenFlyout)
                   && verbs.Contains(BruinetVerb.CloseFlyout)
                   && verbs.Contains(BruinetVerb.GetState);
        });

    /// <summary>Closes the Shell's flyout through the app's verb.</summary>
    internal void CloseFlyout() => PresentFlyout(BruinetVerb.CloseFlyout, "close");

    /// <summary>
    /// Sends one of the two flyout verbs, and reports what came back.
    /// </summary>
    private void PresentFlyout(BruinetVerb verb, string what)
    {
        var answer = Bridge.BridgeVerbRunner.InvokeAnywhere(RootElement, Automation, verb);

        // A flyout already in the state asked for is success: the caller asked for a state, not
        // for a transition. The app does answer S_FALSE for it, which shows in the bridge log,
        // but that never reaches here - UI Automation reports every success HRESULT as S_OK - so
        // this deliberately does not pretend to tell the two apart.
        if (answer.Delivered)
        {
            return;
        }

        throw new BruinetException(
            $"Could not {what} the app's flyout. Either the app has no Shell - only a Shell has a "
            + "flyout - or no element declares the verb. The bridge said: " + answer.Reason);
    }

    /// <summary>Whether the Shell's flyout is showing, as the app reports it.</summary>
    internal bool IsFlyoutOpen()
    {
        var answer = Exchange(BruinetVerb.GetState, "FlyoutIsPresented");

        if (!answer.Delivered)
        {
            throw new NotSupportedException(
                "The app under test cannot say whether its flyout is open. Declare GetState on "
                + "its Shell - see GestureAutomation.Verbs - or ask the flyout's own elements, "
                + "remembering that Windows leaves them in the tree once it has been opened. "
                + $"The bridge said: {answer.Reason}");
        }

        return bool.TryParse(answer.Value, out var presented) && presented;
    }

    /// <summary>What the alert on screen asks, or null.</summary>
    internal AlertContents? CurrentAlert()
    {
        // The screen decides whether there is an alert; the app only says what it asks.
        //
        // Without this the two could disagree, and did: the app clears its record when
        // DisplayAlert's await resumes, which is a continuation queued on the UI thread and
        // therefore some moments after the dialog has already left the tree. A test that
        // dismissed a prompt and immediately asked was told about the prompt it had just closed.
        // Each end answering only what it can see removes the window rather than narrowing it.
        if (TryFindActiveDialogRoot() is null)
        {
            return null;
        }

        var answer = Exchange(BruinetVerb.CurrentAlert);

        if (!answer.Delivered
            || !AlertPayload.TryParse(
                answer.Value, out var title, out var message, out var accept, out var cancel))
        {
            return null;
        }

        return new AlertContents(title, message, accept, cancel);
    }

    /// <summary>
    /// Answers the app's open-file dialog with one file, for <see cref="FlaUIMauiElement.AnswerFileDialog"/>.
    /// </summary>
    /// <remarks>
    /// The Windows open dialog (class <c>#32770</c>) is owned by the app's window, so UI
    /// Automation lists it as that window's child. Its file-name box (id 1148) takes the path
    /// through the Value pattern and its Open button (id 1) the Invoke pattern: no keyboard,
    /// mouse or foreground is needed.
    /// </remarks>
    internal void AnswerFileDialog(string path, int timeoutMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("The file to answer the dialog with does not exist.", path);

        timeoutMs = Bruinet.Core.Calls.CallBudget.Within(timeoutMs);
        var stopwatch = Stopwatch.StartNew();
        AutomationElement? dialog;
        while ((dialog = TryFindFileDialog(stopwatch.ElapsedMilliseconds > FileDialogDesktopSearchAfterMs)) is null)
        {
            if (stopwatch.ElapsedMilliseconds >= timeoutMs)
                throw new ElementNotFoundException(Locator.ByClassName(FileDialogClass), timeoutMs);
            Thread.Sleep(100);
        }

        var nameBox = dialog.FindFirstDescendant(ConditionFactory.ByControlType(ControlType.Edit)
                          .And(ConditionFactory.ByAutomationId(FileNameBoxId)))
                      ?? throw new ElementNotFoundException(Locator.ByAutomationId(FileNameBoxId));
        if (!nameBox.Patterns.Value.TryGetPattern(out var value))
            throw new RouteUnavailableException("The file dialog's name box offers no Value pattern.");
        value.SetValue(path);

        var open = dialog.FindFirstChild(ConditionFactory.ByControlType(ControlType.Button)
                       .And(ConditionFactory.ByAutomationId(OpenButtonId)))
                   ?? throw new ElementNotFoundException(Locator.ByAutomationId(OpenButtonId));
        if (!open.Patterns.Invoke.TryGetPattern(out var invoke))
            throw new RouteUnavailableException("The file dialog's Open button offers no Invoke pattern.");
        invoke.Invoke();

        var closeBy = Math.Max(timeoutMs, stopwatch.ElapsedMilliseconds + 2_000);
        while (TryFindFileDialog(searchDesktop: false) is not null)
        {
            if (stopwatch.ElapsedMilliseconds >= closeBy)
                throw new BruinetException($"The file dialog did not close after choosing '{Path.GetFileName(path)}'.");
            Thread.Sleep(100);
        }
    }

    private const string FileDialogClass = "#32770";
    private const string FileNameBoxId = "1148";
    private const string OpenButtonId = "1";

    // Searching the desktop walks every top-level window (seconds), so it only backs up the
    // child search when the dialog is slow to show up there.
    private const int FileDialogDesktopSearchAfterMs = 3_000;

    private AutomationElement? TryFindFileDialog(bool searchDesktop)
    {
        var condition = ConditionFactory.ByClassName(FileDialogClass);
        var dialog = RootElement.FindFirstChild(condition);
        if (dialog is not null || !searchDesktop || !_app.HasProcess)
            return dialog;
        return _app.TopLevelWindows()
            .FirstOrDefault(window => window.Properties.ClassName.ValueOrDefault == FileDialogClass);
    }

    /// <summary>The dialog on screen, or null.</summary>
    internal IMauiElement? TryFindActiveDialogRoot()
    {
        var popupCondition = ConditionFactory.ByControlType(ControlType.Window)
            .And(ConditionFactory.ByClassName("Popup"));
        var contentDialogCondition = ConditionFactory.ByClassName("ContentDialog");
        var buttonCondition = ConditionFactory.ByControlType(ControlType.Button);

        // Searched inside the app's own window: a WinUI ContentDialog renders as a Popup
        // descendant of it, not as the sibling top-level window one might expect. Enumerating
        // top-level windows instead costs about 8 s a call — it walks the desktop and filters by
        // process, so it grows with whatever else the machine has open — against about 15 ms
        // here.
        var inRootWindow = TryFindDialogRoot(
            RootElement, popupCondition, contentDialogCondition, buttonCondition);
        if (inRootWindow != null && !ReferenceEquals(inRootWindow, RootElement))
        {
            return new FlaUIMauiElement(inRootWindow, this);
        }

        return null;
    }

    private AutomationElement? TryFindDialogRoot(
        AutomationElement window,
        ConditionBase popupCondition,
        ConditionBase contentDialogCondition,
        ConditionBase buttonCondition)
    {
        try
        {
            var popup = window.FindAllDescendants(popupCondition)
                .LastOrDefault(candidate =>
                    !candidate.Properties.IsOffscreen.ValueOrDefault
                    && candidate.FindFirstDescendant(buttonCondition) != null);

            var dialog = popup
                ?? window.FindFirst(TreeScope.Element, contentDialogCondition)
                ?? window.FindFirstDescendant(contentDialogCondition);
            if (dialog != null)
                return dialog;

            return window.Properties.NativeWindowHandle.ValueOrDefault == _app.WindowHandle
                ? null
                : window;
        }
        catch (COMException)
        {
            return null;
        }
    }

    #endregion

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _app.Dispose();
            _disposed = true;
        }
    }

    #endregion
}
