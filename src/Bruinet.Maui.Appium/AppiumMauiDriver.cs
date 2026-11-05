using Bruinet.Core;
using Bruinet.Core.Exceptions;
using Bruinet.Maui.Enums;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace Bruinet.Maui.Appium;

/// <summary>
/// Appium-based implementation of <see cref="IMauiDriver"/>.
/// Delegates all operations to the underlying AppiumDriver.
/// </summary>
public sealed class AppiumMauiDriver : IMauiDriver, IDisposable, Bruinet.Core.Recording.IRecordable
{
    private readonly AppiumDriver _driver;

    // Facts of a native element that never change for its element id, read once per session:
    // .my/android-performance/design-fewer-reads.md, D1.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string Element, string Name), string?> _elementFacts = new();
    private readonly MauiPlatform _platform;
    private bool _disposed;

    /// <summary>
    /// Creates a new AppiumMauiDriver wrapper.
    /// </summary>
    /// <param name="driver">The AppiumDriver to wrap.</param>
    /// <param name="platform">The platform this driver is connected to.</param>
    /// <exception cref="ArgumentNullException">Thrown when driver is null.</exception>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown for any platform but Android and iOS. Windows is driven by <c>Bruinet.Maui.FlaUI</c>,
    /// and the element code here has no desktop routes to fall back on.
    /// </exception>
    public AppiumMauiDriver(AppiumDriver driver, MauiPlatform platform)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));

        if (platform is not (MauiPlatform.Android or MauiPlatform.iOS))
        {
            throw new PlatformNotSupportedException(
                $"{nameof(AppiumMauiDriver)} drives Android and iOS only, not {platform}. "
                + "MAUI on Windows is driven by Bruinet.Maui.FlaUI - MauiDriverFactory chooses it.");
        }

        _platform = platform;
        _appium = AppiumPlatform.For(platform);
    }

    /// <summary>What this session does differently on its platform.</summary>
    internal AppiumPlatform Appium => _appium;

    private readonly AppiumPlatform _appium;

    #region Platform

    /// <inheritdoc />
    public MauiPlatform Platform => _platform;

    /// <summary>
    /// The Android device the session drives, by the serial UiAutomator2 reports (<c>deviceUDID</c>,
    /// else the <c>udid</c> asked for); null on iOS, where a simulator and a device cannot be told apart here.
    /// </summary>
    public Bruinet.Core.Recording.RecordingTarget? RecordingTarget
        => _platform == MauiPlatform.Android && !_disposed
           && (_driver.Capabilities.GetCapability("deviceUDID") ?? _driver.Capabilities.GetCapability("udid"))?.ToString() is { Length: > 0 } serial
            ? new Bruinet.Core.Recording.RecordingTarget.AndroidDevice(serial)
            : null;

    #endregion

    #region Element Finding

    /// <inheritdoc />
    public IReadOnlyList<IMauiElement> FindElements(Locator locator)
    {
        var by = _appium.ToBy(locator);

        return AppiumErrors.Live(() => _driver.FindElements(by).Select(e => new AppiumMauiElement(e, this)).ToList());
    }

    /// <summary>
    /// Finds a piece of app chrome the driver acts on itself, waiting for it to appear.
    /// </summary>
    /// <param name="locator">The chrome element's locator.</param>
    /// <param name="timeoutMs">How long to wait for it to appear.</param>
    /// <returns>The element.</returns>
    /// <exception cref="ElementNotFoundException">It did not appear in time.</exception>
    internal IMauiElement FindChrome(Locator locator, int timeoutMs)
    {
        // A short wait inside an action (a flyout's item, a toolbar's button), within what the
        // running call has left; not Selenium's WebDriverWait, which kept a clock of its own.
        IMauiElement? found = null;
        return Bruinet.Core.Calls.Settle.Until(() => (found = FindElements(locator).FirstOrDefault()) is not null, timeoutMs)
            ? found!
            : throw new ElementNotFoundException(locator);
    }

    /// <summary>
    /// Finds every match for a piece of app chrome, waiting for at least one to appear.
    /// </summary>
    /// <param name="locator">The chrome elements' locator.</param>
    /// <param name="timeoutMs">How long to wait for the first to appear.</param>
    /// <returns>The matches; empty when none appeared in time.</returns>
    internal IReadOnlyList<IMauiElement> FindAllChrome(Locator locator, int timeoutMs)
    {
        IReadOnlyList<IMauiElement> found = [];
        return Bruinet.Core.Calls.Settle.Until(() => (found = FindElements(locator)).Count > 0, timeoutMs)
            ? found
            : [];
    }

    #endregion

    #region The app

    /// <inheritdoc />
    /// <remarks>
    /// Stands for the whole screen. It finds a root node only when a member needs one; the
    /// app-level members address the screen and do not.
    /// </remarks>
    public IMauiElement AppElement => AppiumMauiElement.ForApp(this);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Android renders Shell's tabs as a bottom navigation bar whose items are frame layouts
    /// carrying the tab's title as their content description - the only frame layouts on the page
    /// that carry one, which is what makes the locator exact rather than merely plausible.
    /// </para>
    /// <para>
    /// <b>The hosts are found beneath the Shell, never above it.</b> They are looked up in the
    /// page's scope, whose root is the Shell's own view (the <c>DrawerLayout</c>, once the app
    /// publishes its id), and a scope only searches down. They used to be <c>android:id/content</c>,
    /// the frame <i>above</i> the Shell, which no scoped lookup can reach: every tab and flyout
    /// collection reported zero items. Each host is named by what it holds, as measured on the
    /// device.
    /// </para>
    /// <para>
    /// The drawer's items are view groups carrying their title as a content description, inside
    /// the drawer's recycler view, and they leave the tree entirely while the drawer is shut. iOS
    /// has not been mapped and throws.
    /// </para>
    /// </remarks>
    /// <inheritdoc />
    public bool OmitsOffscreenElements => true;

    /// <inheritdoc />
    public bool FoundMeansVisible => _appium.FoundMeansVisible;

    /// <inheritdoc />
    public Locator ChildrenPath => _appium.ChildrenPath;

    /// <inheritdoc />
    public IReadOnlyList<Locator> AlertButtons => _appium.AlertButtons;

    /// <inheritdoc />
    public bool DrawsRatingStars => _appium.DrawsRatingStars;

    /// <inheritdoc />
    public Locator? IndicatorDots => _appium.IndicatorDots;

    /// <inheritdoc />
    public (Locator Within, string Type)? PageControl => _appium.PageControl;

    public ShellChromeLocators ShellChrome => _appium.ShellChrome;

    /// <summary>The hierarchy's root node, for the app element's members that need a node.</summary>
    internal AppiumElement FindRootNode() => (AppiumElement)_driver.FindElement(_appium.RootNode);

    #endregion

    #region Window Management

    /// <inheritdoc />
    public string CurrentWindowHandle => _driver.CurrentWindowHandle;

    /// <inheritdoc />
    public IReadOnlyCollection<string> WindowHandles => _driver.WindowHandles;

    #endregion

    #region Session Management

    /// <inheritdoc />
    public void Quit() => _driver.Quit();

    /// <inheritdoc />
    public void Close() => _driver.Close();

    #endregion

    #region Screenshots

    /// <inheritdoc />
    public byte[] GetScreenshot() => AppiumErrors.Live(() => _driver.GetScreenshot().AsByteArray);

    #endregion

    #region Context Switching

    /// <inheritdoc />
    public string Context
    {
        get => _driver.Context;
        set => _driver.Context = value;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Contexts => _driver.Contexts;

    #endregion

    #region Script Execution

    /// <inheritdoc />
    public object? ExecuteScript(string script, params object[] args)
    {
        return AppiumErrors.Live(() => _driver.ExecuteScript(script, args));
    }

    #endregion

    #region IDiagnosticDriver

    /// <inheritdoc />
    public string GetPageSource() => AppiumErrors.Live(() => _driver.PageSource);

    /// <inheritdoc />
    public string GetAutomationTree() => AppiumErrors.Live(() => _driver.PageSource);

    #endregion

    #region Navigation

    /// <inheritdoc />
    public void NavigateTo(string destination) => AppiumErrors.Live(() => _driver.Navigate().GoToUrl(destination));

    /// <inheritdoc />
    /// <remarks>
    /// <b>Sends back whatever <see cref="IsAtNavigationRoot"/> thinks.</b> That answer reads the
    /// toolbar, and not everything above the first page has one: an Android modal page - a
    /// CommunityToolkit popup, say - covers the app and reports no way back. Refusing here on that
    /// reading was tried and blocked the one press that dismisses such a page, so the judgement
    /// stays with the caller, which can see what it expected to be on screen.
    /// </remarks>
    public void NavigateBack()
    {
        using var mark = Bruinet.Core.Recording.Interactions.Report(Bruinet.Core.Recording.InteractionKind.Key, () => System.Drawing.Rectangle.Empty, key: "Back");
        AppiumErrors.Live(GoBack);
    }

    private void GoBack()
    {
        // iOS: a popup lives in a window of its own and neither the back button nor the edge
        // swipe reaches it. A failed test that left one open made recovery swipe the page under
        // it, and every later test in the class failed "Tab 'Projects' was not found".
        if (_platform == MauiPlatform.iOS && TryDismissIosPopup())
            return;

        // iOS: XCUITest's back taps the navigation bar's back button and silently does nothing
        // when the app hides that bar behind its own title bar. The system's left-edge swipe is
        // the platform's other way back and does not depend on the bar.
        if (_platform == MauiPlatform.iOS && _driver.FindElements(IosNavigationBar).Count == 0)
        {
            var size = _driver.Manage().Window.Size;
            _driver.ExecuteScript("mobile: dragFromToForDuration", new Dictionary<string, object>
            {
                { "duration", 0.1 },
                { "fromX", 1 },
                { "fromY", size.Height / 2 },
                { "toX", size.Width * 3 / 4 },
                { "toY", size.Height / 2 },
            });
            return;
        }

        // Android: with the soft keyboard up, the system's back only hides the keyboard. A prompt
        // left open by a failed test showed it - one back hid the keyboard, the dialog stayed, and
        // every later test in the class failed "could not get back to the hub". Hiding it first
        // makes this one back mean what the caller asked for.
        if (_platform == MauiPlatform.Android && IsKeyboardShown())
        {
            _driver.ExecuteScript("mobile: hideKeyboard", new Dictionary<string, object>());
        }

        _driver.Navigate().Back();
    }

    private bool IsKeyboardShown()
        => _driver.ExecuteScript("mobile: isKeyboardShown", new Dictionary<string, object>()) is true;

    /// <summary>Closes the soft keyboard when it is up.</summary>
    /// <returns>Whether a keyboard was shown and a close was attempted.</returns>
    internal bool HideKeyboardIfShown()
    {
        try
        {
            if (!IsKeyboardShown())
                return false;

            // iOS: XCUITest taps the first of these keys the keyboard has; a MAUI Entry's Return
            // key resigns the field. A numeric pad has no such key: MAUI gives it a toolbar above
            // the keyboard, DoneAccessory, whose Done button does it instead.
            if (_platform == MauiPlatform.iOS)
            {
                var done = _driver.FindElements(MobileBy.AccessibilityId("Done")).FirstOrDefault(button => button.Displayed);
                if (done != null)
                {
                    done.Click();
                    return true;
                }
            }

            var arguments = _platform == MauiPlatform.iOS
                ? new Dictionary<string, object> { { "keys", new[] { "return", "Return", "done", "Done" } } }
                : new Dictionary<string, object>();
            _driver.ExecuteScript("mobile: hideKeyboard", arguments);
            return true;
        }
        catch (WebDriverException)
        {
            return false;
        }
    }

    private static readonly By IosNavigationBar = MobileBy.ClassName("XCUIElementTypeNavigationBar");

    /// <summary>
    /// The toolbar's up button, which a <c>NavigationPage</c> shows on every page above its root.
    /// </summary>
    private static readonly By AndroidNavigateUp =
        By.XPath("//android.widget.ImageButton[@content-desc='Navigate up']");

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Android: true when the toolbar shows no "Navigate up" button. Without this override the
    /// interface default answered true on every page, so <c>MauiFixture</c> never went back and every
    /// test after the first failed to reach the hub.
    /// </para>
    /// <para>
    /// iOS: true when no navigation bar starts with a back button (<see cref="IsIosBackButton"/>).
    /// UIKit names that button after the previous page's title or "Back", so it is known by its
    /// place, not its name. Not yet run on a simulator.
    /// </para>
    /// </remarks>
    public bool IsAtNavigationRoot()
        => AppiumErrors.Live(() => _platform switch
        {
            MauiPlatform.Android => _driver.FindElements(AndroidNavigateUp).Count == 0,
            MauiPlatform.iOS => !IsIosBackButtonShown(),
            _ => true,
        });

    private bool IsIosBackButtonShown()
    {
        foreach (var bar in _driver.FindElements(IosNavigationBar))
        {
            var first = bar.FindElements(IosBarButton).FirstOrDefault();
            if (first is not null
                && IsIosBackButton(first.GetAttribute("name"), first.Location.X - bar.Location.X, bar.Size.Width))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly By IosBarButton = By.XPath("./XCUIElementTypeButton");

    /// <summary>
    /// The names MAUI's flyout opener has carried in the navigation bar. It sits where a back button
    /// would, on a root page. Not measured yet.
    /// </summary>
    internal static readonly IReadOnlyList<string> IosFlyoutOpenerNames = ["OK", "Menu", "FlyoutIcon"];

    /// <summary>
    /// Whether a navigation bar's first button is UIKit's back button: in the bar's leading third
    /// (toolbar items sit trailing) and not the flyout opener.
    /// </summary>
    /// <param name="name">The button's <c>name</c>.</param>
    /// <param name="offsetInBar">The button's x, from the bar's left edge.</param>
    /// <param name="barWidth">The bar's width.</param>
    internal static bool IsIosBackButton(string? name, int offsetInBar, int barWidth)
        => offsetInBar < barWidth / 3
           && !(name is not null && IosFlyoutOpenerNames.Contains(name, StringComparer.Ordinal));

    /// <inheritdoc />
    /// <remarks>
    /// A lower bound on Android: 1 at the root, otherwise 2. The toolbar shows whether there is a
    /// page to go back to, not how many; to unwind several, loop on <see cref="IsAtNavigationRoot"/>.
    /// </remarks>
    public int NavigationDepth() => IsAtNavigationRoot() ? 1 : 2;

    /// <inheritdoc />
    public void Refresh() => AppiumErrors.Live(() => _driver.Navigate().Refresh());

    /// <inheritdoc />
    public byte[] TakeScreenshot() => GetScreenshot();

    /// <inheritdoc />
    public void ResetAppState() => AppiumErrors.Live(ResetApp);

    private void ResetApp()
    {
        var bundleId = _driver.Capabilities.GetCapability("appPackage")?.ToString()
                    ?? _driver.Capabilities.GetCapability("bundleId")?.ToString();

        if (!string.IsNullOrEmpty(bundleId))
        {
            _driver.TerminateApp(bundleId);
            _driver.ActivateApp(bundleId);
        }
    }

    #endregion

    #region Dialogs

    /// <summary>The dialog on screen, or null. Answers <c>AppElement.TryFindActiveDialog</c>.</summary>
    internal IMauiElement? TryFindActiveDialogRoot()
    {
        var by = _platform switch
        {
            // Any package: the framework AlertDialog's root is android:id/parentPanel, but MAUI's
            // DisplayAlert is AndroidX AppCompat's, whose ids carry the app's package
            // (probed 2026-09-19 in the Todo sample: com.bruinet.samples.todo:id/parentPanel).
            // Looking for the framework id only never found a MAUI alert at all.
            MauiPlatform.Android => MobileBy.AndroidUIAutomator("new UiSelector().resourceIdMatches(\".*:id/parentPanel\")"),
            // DisplayActionSheet is a UIAlertController too, but with the sheet style.
            MauiPlatform.iOS => MobileBy.IosNSPredicate("type == 'XCUIElementTypeAlert' OR type == 'XCUIElementTypeSheet'"),
            _ => null
        };

        if (by == null)
            return null;

        var root = _driver.FindElements(by).LastOrDefault();
        if (root == null && _platform == MauiPlatform.iOS)
            root = TryFindIosPopupWindow();

        return root == null ? null : new AppiumMauiElement(root, this);
    }

    /// <summary>
    /// A popup the app draws itself: Mopups (MAUI's common popup library) puts each popup in a
    /// UIWindow of its own above the app's. iOS lists the keyboard and its input accessory as
    /// windows too, so those are skipped. Probed 2026-11-04 on iOS 27: a Core.Apps drawer was
    /// the second window, the main window read visible=false under it.
    /// </summary>
    /// <summary>
    /// Closes the alert, sheet or popup window on top of an iOS app, the way a user would.
    /// </summary>
    /// <returns>Whether there was one to close.</returns>
    /// <remarks>
    /// A system alert takes its cancel action. A popup window takes its own cancel or close
    /// control when it has one, and a tap on the dimmed backdrop above it otherwise, which a
    /// Mopups popup closes on by default.
    /// </remarks>
    private bool TryDismissIosPopup()
    {
        if (_driver.FindElements(IosAlertOrSheet).Count > 0)
        {
            _driver.ExecuteScript("mobile: alert", new Dictionary<string, object> { { "action", "dismiss" } });
            return true;
        }

        if (TryFindIosPopupWindow() is not { } popup)
            return false;

        var close = popup.FindElements(IosCloseControl).FirstOrDefault(element => element.Displayed);
        if (close is not null)
        {
            close.Click();
            return true;
        }

        var size = _driver.Manage().Window.Size;
        _driver.ExecuteScript("mobile: tap", new Dictionary<string, object>
        {
            { "x", size.Width / 2 },
            { "y", size.Height / 10 },
        });
        return true;
    }

    private static readonly By IosAlertOrSheet = MobileBy.IosNSPredicate(
        "type == 'XCUIElementTypeAlert' OR type == 'XCUIElementTypeSheet'");

    private static readonly By IosCloseControl = MobileBy.IosNSPredicate(
        "name ENDSWITH '_Cancel' OR name ENDSWITH '_Close' OR name == 'Cancel' OR name == 'Close'");

    private AppiumElement? TryFindIosPopupWindow()
    {
        var windows = _driver.FindElements(MobileBy.IosClassChain("XCUIElementTypeWindow"));
        for (var i = windows.Count - 1; i > 0; i--)
        {
            var window = windows[i];
            if (window.FindElements(MobileBy.ClassName("XCUIElementTypeKeyboard")).Count > 0
                || window.FindElements(MobileBy.AccessibilityId("DoneAccessory")).Count > 0
                || window.FindElements(MobileBy.ClassName("XCUIElementTypeOther")).Count == 0)
            {
                continue;
            }

            if (window.Displayed)
                return window;
        }

        return null;
    }

    /// <inheritdoc />
    /// <remarks>Every gesture is touch input on Android and iOS; see <c>AppiumMauiElement.PerformGesture</c>.</remarks>
    public bool SupportsGesture(string automationId, MauiGesture gesture)
        => _platform is MauiPlatform.Android or MauiPlatform.iOS;

    /// <inheritdoc />
    public void PerformGesture(string automationId, MauiGesture gesture)
        => PerformGesture(automationId, gesture, 0, 0);

    /// <inheritdoc />
    /// <remarks>
    /// The element as <c>AppElement.TryFindDeclared</c> finds it - by resource id, then accessibility
    /// id, which is what MAUI maps an <c>AutomationId</c> to - and the gesture as real touch input on
    /// it. A gesture's target is often a container with no text of its own: a RefreshView, a SwipeView.
    /// </remarks>
    /// <exception cref="ElementNotFoundException">No element carries the id now.</exception>
    public void PerformGesture(string automationId, MauiGesture gesture, int arg1, int arg2)
    {
        // Below the fold it is not in the tree at all - UiAutomator2 leaves out what the user
        // cannot see - so the page is scrolled to it, as a user would before swiping it.
        var target = (AppElement.TryFindDeclared(automationId)
                      ?? TryFindByScrollingWithin(null, Locator.ByAutomationId(automationId))) as AppiumMauiElement
            ?? throw new ElementNotFoundException(Locator.ByAutomationId(automationId));

        target.PerformGesture(gesture, arg1, arg2);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The device's locale as <c>mobile: deviceInfo</c> reports it (<c>en_US</c>), as the culture's
    /// standard data: a device knows nothing of this machine's custom formats. Null when the
    /// device does not say.
    /// </remarks>
    public System.Globalization.CultureInfo? DeviceCulture
    {
        get
        {
            try
            {
                if (_driver.ExecuteScript("mobile: deviceInfo", new Dictionary<string, object>())
                        is IDictionary<string, object> info
                    && DeviceLocaleName(info) is { } name)
                {
                    return System.Globalization.CultureInfo.GetCultureInfo(name);
                }
            }
            catch (System.Globalization.CultureNotFoundException)
            {
                // A locale .NET does not know: the machine's culture it is, as before.
            }

            return null;
        }
    }

    /// <summary>
    /// The culture name in a <c>mobile: deviceInfo</c> answer: UiAutomator2 says <c>locale</c>
    /// (<c>en_US</c>), XCUITest says <c>currentLocale</c> with any region override after an
    /// <c>@</c> (<c>en_US@rg=nlzzzz</c>). The override is dropped: an app that honours it formats
    /// with Apple's regional data, which this machine's culture data does not match, so a test
    /// device should run without one.
    /// </summary>
    internal static string? DeviceLocaleName(IDictionary<string, object> info)
    {
        foreach (var key in new[] { "locale", "currentLocale" })
        {
            if (info.TryGetValue(key, out var value) && value is string raw)
            {
                var name = raw.Split('@')[0].Replace('_', '-');
                if (name.Length > 0)
                {
                    return name;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// An element's position and size in one round trip. The Appium client's <c>Rect</c> reads
    /// <c>Location</c> and then <c>Size</c>, two requests for the same W3C rect, and a row check
    /// reads it often: on iOS each request costs about 100 ms on a list page.
    /// </summary>
    internal System.Drawing.Rectangle ElementRect(string elementId)
    {
        if (!_rectCommandRegistered)
        {
            _driver.RegisterCustomDriverCommand(
                ElementRectCommand,
                new HttpCommandInfo(HttpCommandInfo.GetCommand, "/session/{sessionId}/element/{id}/rect"));
            _rectCommandRegistered = true;
        }

        var rect = (Dictionary<string, object>)_driver.ExecuteCustomDriverCommand(
            ElementRectCommand, new Dictionary<string, object> { ["id"] = elementId });
        return new System.Drawing.Rectangle(Whole(rect["x"]), Whole(rect["y"]), Whole(rect["width"]), Whole(rect["height"]));

        static int Whole(object value) => (int)Math.Round(Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture));
    }

    private const string ElementRectCommand = "bruinetElementRect";
    private bool _rectCommandRegistered;

    /// <summary>
    /// Whether an element this session finds is necessarily on screen: Android, where UiAutomator2
    /// leaves invisible elements out of its tree unless <c>allowInvisibleElements</c> is set.
    /// </summary>
    /// <summary>
    /// A fact of a native element that never changes for its element id, such as its
    /// <c>resource-id</c> or widget class on Android: read once, then answered from memory. A read
    /// that throws (a stale element) is not kept.
    /// </summary>
    /// <remarks>
    /// A RecyclerView that rebinds a view gives it new content, not a new id or class: both come
    /// from the item template. A view replaced by another has another element id.
    /// </remarks>
    internal string? ElementFact(string elementId, string name, Func<string?> read)
        => _elementFacts.TryGetValue((elementId, name), out var known)
            ? known
            : _elementFacts[(elementId, name)] = read();

    internal bool FindsOnlyVisibleElements => _findsOnlyVisible ??=
        _platform == MauiPlatform.Android
        && !string.Equals(
            Convert.ToString(_driver.Capabilities.GetCapability("settings[allowInvisibleElements]"),
                System.Globalization.CultureInfo.InvariantCulture),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private bool? _findsOnlyVisible;

    /// <summary>Screen pixels per device-independent unit, read once per session.</summary>
    internal double PixelsPerUnit()
    {
        if (_pixelsPerUnit is { } known)
        {
            return known;
        }

        var scale = 1.0;
        if (_platform == MauiPlatform.Android
            && _driver.ExecuteScript("mobile: deviceInfo", new Dictionary<string, object>())
                is IDictionary<string, object> info
            && info.TryGetValue("displayDensity", out var dpi)
            && double.TryParse(
                Convert.ToString(dpi, System.Globalization.CultureInfo.InvariantCulture),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var density)
            && density > 0)
        {
            scale = density / 160.0;
        }

        _pixelsPerUnit = scale;
        return scale;
    }

    private double? _pixelsPerUnit;

    /// <summary>The alert on screen as MAUI was asked for it, or null. Answers <c>AppElement.ReadAlert</c>.</summary>
    /// <remarks>
    /// Android and iOS. AppCompat's alert keeps its title, message and both buttons inside
    /// <c>parentPanel</c>, by ids that carry the app's package (see <see cref="TryFindActiveDialogRoot"/>).
    /// MAUI sets the accept as the positive button (<c>button1</c>) and the cancel as the negative one
    /// (<c>button2</c>), so an alert with one button has only <c>button2</c> and reads with an empty
    /// accept, as on Windows.
    /// </remarks>
    internal AlertContents? ReadActiveAlert()
    {
        if (_platform == MauiPlatform.iOS)
        {
            return ReadIosAlert();
        }

        if (_platform != MauiPlatform.Android)
        {
            return null;
        }

        var root = _driver.FindElements(AndroidDialogRoot).LastOrDefault();
        if (root == null)
        {
            return null;
        }

        string Part(string id)
            => root.FindElements(MobileBy.AndroidUIAutomator(
                    $"new UiSelector().resourceIdMatches(\".*:id/{id}\")"))
                .FirstOrDefault()?.Text ?? string.Empty;

        return new AlertContents(Part("alertTitle"), Part("message"), Part("button1"), Part("button2"));
    }

    /// <summary>iOS: a <c>UIAlertController</c>, its title and message as static texts.</summary>
    /// <remarks>
    /// MAUI gives the cancel the cancel style, which UIKit puts first (left of two), so one button
    /// reads as the cancel with an empty accept, as on Android. Not yet run on a simulator.
    /// </remarks>
    private AlertContents? ReadIosAlert()
    {
        var alert = _driver.FindElements(By.XPath("//XCUIElementTypeAlert")).LastOrDefault();
        if (alert == null)
        {
            return null;
        }

        var texts = alert.FindElements(By.XPath(".//XCUIElementTypeStaticText"))
            .Select(t => t.GetAttribute("label") ?? t.Text ?? string.Empty).ToList();
        var buttons = alert.FindElements(By.XPath(".//XCUIElementTypeButton"))
            .Select(b => b.GetAttribute("label") ?? b.Text ?? string.Empty).ToList();

        return new AlertContents(
            texts.ElementAtOrDefault(0) ?? string.Empty,
            texts.ElementAtOrDefault(1) ?? string.Empty,
            buttons.Count > 1 ? buttons[1] : string.Empty,
            buttons.ElementAtOrDefault(0) ?? string.Empty);
    }

    private static readonly By AndroidDialogRoot =
        MobileBy.AndroidUIAutomator("new UiSelector().resourceIdMatches(\".*:id/parentPanel\")");

    #endregion

    #region Scrolling

    /// <summary>
    /// Finds an element by scrolling a container until it enters the tree. Answers
    /// <c>IMauiElement.TryFindByScrolling</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A lookup, a swipe, a lookup</b>: down from where the scroller is, then up, each swipe one
    /// <c>mobile: scrollGesture</c>, which also says whether the scroller can move further. It
    /// replaced <c>UiScrollable.scrollIntoView</c>, which flings to the top before every search -
    /// even when the page is there already - and cost 3.6 s a hit and 11 s a miss per matcher, two
    /// matchers a miss (measured over a night's runs; 60 % of the scroll-heavy classes' time).
    /// </para>
    /// <para>
    /// An <c>AutomationId</c> is looked for as MAUI maps it: the resource id, then the content
    /// description.
    /// </para>
    /// </remarks>
    /// <param name="container">The container to scroll, or null for the first scrollable on screen.</param>
    /// <param name="locator">The locator for the element.</param>
    internal IMauiElement? TryFindByScrollingWithin(IMauiElement? container, Locator locator)
        => AppiumErrors.Live(() => FindByScrolling(container, locator));

    private IMauiElement? FindByScrolling(IMauiElement? container, Locator locator)
    {
        if (_platform == MauiPlatform.iOS)
        {
            return FindByScrollingIos(container, locator);
        }

        if (_platform != MauiPlatform.Android)
        {
            return null;
        }

        if (FindOnScreen(locator) is { } already)
        {
            return already;
        }

        var scroller = _driver.FindElements(MobileBy.AndroidUIAutomator(ScrollableSelector(container))).FirstOrDefault();
        if (scroller is null)
        {
            return null;
        }

        // Inside a call, what the call has left: a miss used to cost 24 swipes and 25 lookups,
        // near nine seconds, inside one attempt of a 5 s call. Outside a call, the swipes' cap alone.
        var budgetMs = Bruinet.Core.Calls.CallBudget.RemainingOr(int.MaxValue);
        var gesture = new Dictionary<string, object> { { "elementId", ((AppiumElement)scroller).Id } };

        return ScrollUntil(gesture, ScrollDirections, () => FindOnScreen(locator) is not null, budgetMs, atLeastOneSwipe: false)
            ? ReResolveAfterScrolling(locator) ?? FindOnScreen(locator)
            : null;
    }

    /// <summary>
    /// The one scroll routine (scroll-to-find and an element's scroll into view): swipes one
    /// direction, then the next, until <paramref name="found"/> holds, the scroller cannot move,
    /// or <paramref name="budgetMs"/> is spent. One <c>mobile: scrollGesture</c> a swipe, which also
    /// says whether the scroller can move further.
    /// </summary>
    /// <param name="gesture">The gesture's target: an <c>elementId</c>, or a region.</param>
    /// <param name="directions">The directions, in order.</param>
    /// <param name="found">One look after each swipe.</param>
    /// <param name="budgetMs">How long the swipes may take in all.</param>
    /// <param name="atLeastOneSwipe">Swipe once even on a spent budget: the caller's poll decides whether to try again, and a zero budget must not mean "never scrolls".</param>
    /// <returns>Whether <paramref name="found"/> held after a swipe.</returns>
    internal bool ScrollUntil(IReadOnlyDictionary<string, object> gesture, IReadOnlyList<string> directions,
        Func<bool> found, int budgetMs, bool atLeastOneSwipe)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var swiped = false;
        foreach (var direction in directions)
        {
            for (var swipe = 0; swipe < MaxScrollSwipes; swipe++)
            {
                if ((swiped || !atLeastOneSwipe) && clock.ElapsedMilliseconds >= budgetMs)
                {
                    return false;
                }

                bool more;
                try
                {
                    var parameters = new Dictionary<string, object>(gesture)
                    {
                        ["direction"] = direction,
                        ["percent"] = ScrollPercent,
                    };
                    more = _driver.ExecuteScript("mobile: scrollGesture", parameters) is true;
                }
                catch (Exception error) when (!AppiumErrors.IsGone(error))
                {
                    return false;
                }

                swiped = true;
                if (found())
                {
                    return true;
                }

                if (!more)
                {
                    break;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// iOS scroll-to-find. A UICollectionView only has cells for its visible rows, so a row below
    /// the fold is not in the XCUITest tree and XCUITest's own scroll-to-element cannot reach it.
    /// The scroller is scrolled a page at a time, down then up, with a lookup after each page.
    /// </summary>
    /// <remarks>
    /// XCUITest's <c>mobile: scroll</c> does not say whether the scroller moved, so the visible
    /// rows' names and positions are compared before and after: unchanged means that end is
    /// reached. Without a container, the tallest scroller on screen is taken, not the first: a
    /// project page's first is its horizontal tab strip.
    /// </remarks>
    private IMauiElement? FindByScrollingIos(IMauiElement? container, Locator locator)
    {
        if (FindOnScreenIos(locator) is { } already)
        {
            return already;
        }

        var scroller = IosScroller(container);
        if (scroller is null)
        {
            return null;
        }

        var budgetMs = Bruinet.Core.Calls.CallBudget.RemainingOr(int.MaxValue);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        foreach (var direction in ScrollDirections)
        {
            var before = IosScrollSignature(scroller);
            for (var swipe = 0; swipe < MaxScrollSwipes && clock.ElapsedMilliseconds < budgetMs; swipe++)
            {
                try
                {
                    _driver.ExecuteScript("mobile: scroll", new Dictionary<string, object>
                    {
                        ["elementId"] = scroller.Id,
                        ["direction"] = direction,
                    });
                }
                catch (Exception error) when (!AppiumErrors.IsGone(error))
                {
                    return null;
                }

                if (FindOnScreenIos(locator) is { } found)
                {
                    return found;
                }

                var after = IosScrollSignature(scroller);
                if (after == before)
                {
                    break;
                }

                before = after;
            }
        }

        return null;
    }

    /// <summary>The element if it is in the tree now, by the locator or an AutomationId's accessibility id.</summary>
    private IMauiElement? FindOnScreenIos(Locator locator)
    {
        var found = FindElements(locator).FirstOrDefault();
        if (found is null && locator.Strategy == LocatorStrategy.AutomationId)
        {
            found = FindElements(Locator.ByAccessibilityId(locator.Value)).FirstOrDefault();
        }

        return found;
    }

    private const string IosScrollerTypes =
        "type IN {'XCUIElementTypeCollectionView', 'XCUIElementTypeScrollView', 'XCUIElementTypeTable'}";

    /// <summary>The scroller in or at <paramref name="container"/>, or the tallest visible one on screen.</summary>
    private AppiumElement? IosScroller(IMauiElement? container)
    {
        try
        {
            if (container is AppiumMauiElement scope)
            {
                if (scope.Element.TagName is "XCUIElementTypeCollectionView" or "XCUIElementTypeScrollView" or "XCUIElementTypeTable")
                {
                    return scope.Element;
                }

                return scope.Element.FindElements(MobileBy.IosNSPredicate(IosScrollerTypes)).FirstOrDefault() as AppiumElement;
            }

            return _driver.FindElements(MobileBy.IosNSPredicate(IosScrollerTypes + " AND visible == 1"))
                .OrderByDescending(element => element.Size.Height)
                .FirstOrDefault();
        }
        catch (Exception error) when (!AppiumErrors.IsGone(error))
        {
            return null;
        }
    }

    /// <summary>What the scroller shows now: its children's names and positions, in one lookup.</summary>
    private static string IosScrollSignature(AppiumElement scroller)
    {
        try
        {
            return string.Join(";", scroller.FindElements(MobileBy.XPath("./*"))
                .Take(3)
                .Select(child => $"{child.GetAttribute("name")}@{child.Location.Y}"));
        }
        catch (Exception error) when (!AppiumErrors.IsGone(error))
        {
            return string.Empty;
        }
    }

    /// <summary>Down first: a page opens at its top, and most lookups are for what follows.</summary>
    private static readonly string[] ScrollDirections = ["down", "up"];

    /// <summary>Swipes each way before giving up; the sample's longest page is a few screens.</summary>
    private const int MaxScrollSwipes = 12;

    /// <summary>
    /// How much of the scroller one swipe moves: most of it, since a search only needs the element
    /// to enter the viewport, and a swipe shorter than the scroller skips nothing.
    /// </summary>
    private const double ScrollPercent = 0.9;

    /// <summary>An AutomationId a <c>UiSelector</c> can carry as it is.</summary>
    private static readonly System.Text.RegularExpressions.Regex PlainId = new("^[A-Za-z0-9_]+$");

    /// <summary>The element if it is in the tree now, by the locator, or an AutomationId's description.</summary>
    private IMauiElement? FindOnScreen(Locator locator)
    {
        // One round trip for both of MAUI's mappings, the resource id and the content description:
        // UiAutomator2 answers a query of several selectors with their union. A scroll search looks
        // after every swipe, so two lookups a miss doubled its cost.
        if (locator.Strategy == LocatorStrategy.AutomationId && PlainId.IsMatch(locator.Value))
        {
            return _driver.FindElements(MobileBy.AndroidUIAutomator(
                    $"new UiSelector().resourceIdMatches(\".*:id/{locator.Value}\");new UiSelector().description(\"{locator.Value}\")"))
                .Select(element => new AppiumMauiElement(element, this))
                .FirstOrDefault();
        }

        var found = FindElements(locator).FirstOrDefault();
        if (found is null && locator.Strategy == LocatorStrategy.AutomationId)
        {
            found = FindElements(Locator.ByAccessibilityId(locator.Value)).FirstOrDefault();
        }

        return found;
    }

    /// <summary>
    /// The <c>UiSelector</c> naming the container to scroll.
    /// </summary>
    private static string ScrollableSelector(IMauiElement? container)
    {
        var resourceId = TryGetResourceId(container);

        return resourceId == null
            ? "new UiSelector().scrollable(true).instance(0)"
            : $"new UiSelector().resourceIdMatches(\".*{resourceId}\")";
    }

    private static string? TryGetResourceId(IMauiElement? container)
    {
        if (container == null)
        {
            return null;
        }

        try
        {
            var resourceId = container.GetAttribute("resource-id");
            return string.IsNullOrEmpty(resourceId) ? null : resourceId;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Looks the element up again by its own locator once <c>UiScrollable</c> has brought it
    /// into view.
    /// </summary>
    /// <param name="locator">The caller's locator.</param>
    /// <returns>The freshly resolved element, or null to fall back to the scroll result.</returns>
    private IMauiElement? ReResolveAfterScrolling(Locator locator)
    {
        try
        {
            var elements = FindElements(locator);
            if (elements.Count == 0)
            {
                return null;
            }

            var element = elements[0];
            if (element is AppiumMauiElement appiumElement)
            {
                appiumElement.WaitUntilPositionSettles();
                appiumElement.NudgeClearOfBottomEdge();
            }

            return element;
        }
        catch
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
            _driver.Quit();
            _disposed = true;
        }
    }

    #endregion

    #region Internal (for AppiumMauiElement access)

    /// <summary>
    /// Gets the underlying AppiumDriver for internal use.
    /// </summary>
    internal AppiumDriver Driver => _driver;

    #endregion
}
