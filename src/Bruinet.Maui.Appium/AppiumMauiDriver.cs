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
internal sealed class AppiumMauiDriver : IMauiDriver, IDisposable, Bruinet.Core.Recording.IRecordable
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
        // A new session starts with the app in front, so the front app is ours when no capability names it.
        _appId = _appium.AppId(driver) ?? AppiumErrors.Live(() => _appium.FrontAppId(driver));
    }

    private readonly string? _appId;

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
        => _disposed ? null : _appium.RecordingTarget(_driver);

    #endregion

    #region Element Finding

    /// <inheritdoc />
    public IReadOnlyList<IMauiElement> FindElements(Locator locator)
    {
        var by = _appium.ToBy(locator);

        var found = AppiumErrors.Live(() => FindAll(by, null));
        if (found.Count > 0)
        {
            ScrollMemory.Seen(locator);
        }

        return found;
    }

    /// <summary>Views found and vertical moves, for the order of the Android scroll search (D12).</summary>
    internal ScrollMemory ScrollMemory { get; } = new();

    #region Finds that bring facts

    private const string FindElementCommand = "bruinetFindElement";
    private const string FindElementsCommand = "bruinetFindElements";
    private const string FindChildElementCommand = "bruinetFindChildElement";
    private const string FindChildElementsCommand = "bruinetFindChildElements";
    private bool _findCommandsRegistered;

    /// <summary>
    /// Whether finds bring each element's facts with them: iOS, whose session asks WebDriverAgent
    /// for them (<c>elementResponseAttributes</c>). There a read is a request of 90 ms or more, and
    /// the facts cost about a tenth of that per element. UiAutomator2 reads in about 12 ms, so the
    /// facts would cost Android about what they save (.my/performance, step 7).
    /// </summary>
    internal bool FindsWithFacts => _appium.FindsWithFacts;

    /// <summary>The first element <paramref name="by"/> matches, in the page or under <paramref name="parent"/>.</summary>
    /// <exception cref="NoSuchElementException">Nothing matches.</exception>
    internal AppiumMauiElement FindOne(By by, AppiumElement? parent)
    {
        if (!FindsWithFacts)
        {
            return new AppiumMauiElement((AppiumElement)(parent is null ? _driver.FindElement(by) : parent.FindElement(by)), this);
        }

        return Wrap(Find(parent is null ? FindElementCommand : FindChildElementCommand, by, parent))
            ?? throw new NoSuchElementException($"No element matches {by}.");
    }

    /// <summary>Every element <paramref name="by"/> matches, in the page or under <paramref name="parent"/>.</summary>
    internal IReadOnlyList<AppiumMauiElement> FindAll(By by, AppiumElement? parent)
    {
        if (!FindsWithFacts)
        {
            return (parent is null ? _driver.FindElements(by) : parent.FindElements(by))
                .Select(e => new AppiumMauiElement(e, this)).ToList();
        }

        return Find(parent is null ? FindElementsCommand : FindChildElementsCommand, by, parent) is System.Collections.IEnumerable found
               and not IDictionary<string, object>
            ? found.Cast<object?>().Select(Wrap).OfType<AppiumMauiElement>().ToList()
            : [];
    }

    // The client's own find keeps only each element's id; the same request sent as a command of
    // ours returns the whole answer, facts included. Errors are the client's: no such element,
    // stale parent, ended session.
    private object? Find(string command, By by, AppiumElement? parent)
    {
        if (!_findCommandsRegistered)
        {
            _driver.RegisterCustomDriverCommand(FindElementCommand, new HttpCommandInfo(HttpCommandInfo.PostCommand, "/session/{sessionId}/element"));
            _driver.RegisterCustomDriverCommand(FindElementsCommand, new HttpCommandInfo(HttpCommandInfo.PostCommand, "/session/{sessionId}/elements"));
            _driver.RegisterCustomDriverCommand(FindChildElementCommand, new HttpCommandInfo(HttpCommandInfo.PostCommand, "/session/{sessionId}/element/{id}/element"));
            _driver.RegisterCustomDriverCommand(FindChildElementsCommand, new HttpCommandInfo(HttpCommandInfo.PostCommand, "/session/{sessionId}/element/{id}/elements"));
            _findCommandsRegistered = true;
        }

        var (strategy, value) = Strategy(by);
        var parameters = new Dictionary<string, object> { ["using"] = strategy, ["value"] = value };
        if (parent is not null)
        {
            parameters["id"] = parent.Id;
        }

        return _driver.ExecuteCustomDriverCommand(command, parameters);
    }

    private const string ElementKey = "element-6066-11e4-a52e-4f735466cecf";

    /// <summary>
    /// The locator strategy and value the client sends for <paramref name="by"/>. Appium's
    /// <c>MobileBy</c> leaves <see cref="By.Mechanism"/> and <see cref="By.Criteria"/> empty and hands
    /// them only to the context it searches, so they are taken from a context that keeps them.
    /// </summary>
    internal static (string Using, string Value) Strategy(By by)
    {
        var capture = new StrategyCapture();
        _ = by.FindElements(capture);
        return capture.Found ?? throw new InvalidOperationException($"{by} named no locator strategy.");
    }

    private sealed class StrategyCapture : ISearchContext, OpenQA.Selenium.Internal.IFindsElement,
        OpenQA.Selenium.Appium.Interfaces.IFindsByFluentSelector<IWebElement>
    {
        public (string Using, string Value)? Found { get; private set; }

        public IWebElement FindElement(By by) => throw new NotSupportedException();

        public System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> FindElements(By by) => throw new NotSupportedException();

        public IWebElement FindElement(string mechanism, string value) => throw new NotSupportedException();

        public System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> FindElements(string mechanism, string value)
        {
            Found = (mechanism, value);
            return new([]);
        }

        IWebElement OpenQA.Selenium.Appium.Interfaces.IFindsByFluentSelector<IWebElement>.FindElement(string selector, string selectorValue)
            => throw new NotSupportedException();

        IReadOnlyCollection<IWebElement> OpenQA.Selenium.Appium.Interfaces.IFindsByFluentSelector<IWebElement>.FindElements(string selector, string selectorValue)
        {
            Found = (selector, selectorValue);
            return [];
        }
    }

    private AppiumMauiElement? Wrap(object? found)
    {
        switch (found)
        {
            case AppiumElement element:
                return new AppiumMauiElement(element, this);
            case IDictionary<string, object> answer
                when (answer.TryGetValue(ElementKey, out var id) || answer.TryGetValue("ELEMENT", out id)) && id is string elementId:
                var wrapped = new AppiumMauiElement(new AppiumElement(_driver, elementId), this);
                wrapped.Know(answer);
                return wrapped;
            default:
                return null;
        }
    }

    #endregion

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
    /// <remarks>
    /// UiAutomator2 reports a clipped element's visible part, so an element cut by a scroller ends
    /// on that scroller's edge; XCUITest reports the whole frame, which then reaches past it. Either
    /// way an element whose top and bottom are inside every scroller it overlaps is shown whole.
    /// One lookup of the scrollers, asked only when a control in the element missed: a sweep it
    /// saves costs seconds (a form input's absent error label swept its form for 5 s).
    /// </remarks>
    public bool IsWhollyInView(IMauiElement element)
    {
        try
        {
            var own = element.Rect;
            if (own.Height <= 0)
            {
                return false;
            }

            foreach (var scroller in _appium.Scrollers(_driver))
            {
                var area = new System.Drawing.Rectangle(scroller.Location, scroller.Size);
                var overlaps = own.Left < area.Right && own.Right > area.Left
                               && own.Top < area.Bottom && own.Bottom > area.Top;
                if (overlaps && (own.Top <= area.Top || own.Bottom >= area.Bottom))
                {
                    return false;
                }
            }

            return true;
        }
        catch (WebDriverException error) when (!AppiumErrors.IsGone(error))
        {
            return false;
        }
    }

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

    /// <inheritdoc />
    public bool ListsCancelFirstInTwoButtonAlerts => _appium.ListsCancelFirstInTwoButtonAlerts;

    /// <inheritdoc />
    public bool ReportsFullRowFrames => _appium.ReportsFullRowFrames;

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
        try
        {
            return AppiumErrors.Live(() => _driver.ExecuteScript(script, args));
        }
        finally
        {
            // A script can act (a mobile: gesture); what the attempt read before it may be wrong now.
            Bruinet.Core.Calls.AttemptMemo.Forget();
        }
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

    private void GoBack() => _appium.GoBack(_driver);

    private bool IsKeyboardShown() => AppiumPlatform.IsKeyboardShown(_driver);

    /// <summary>Closes the soft keyboard when it is up.</summary>
    /// <returns>Whether a keyboard was shown and a close was attempted.</returns>
    internal bool HideKeyboardIfShown()
    {
        try
        {
            if (!IsKeyboardShown())
                return false;

            _appium.HideKeyboard(_driver);
            Bruinet.Core.Calls.AttemptMemo.Forget();
            return true;
        }
        catch (WebDriverException error) when (!AppiumErrors.IsGone(error))
        {
            return false;
        }
    }

    /// <inheritdoc />
    /// <remarks>See <see cref="AppiumPlatform.IsAtNavigationRoot"/> for how each platform tells.</remarks>
    public bool IsAtNavigationRoot() => AppiumErrors.Live(() => _appium.IsAtNavigationRoot(_driver));

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
    public void ResetAppState()
    {
        Bruinet.Core.Calls.AttemptMemo.Forget();
        AppiumErrors.Live(ResetApp);
    }

    private void ResetApp() => _appium.Relaunch(_driver);

    /// <inheritdoc />
    /// <remarks>Also true while the system's share sheet is open over the app (iOS).</remarks>
    public bool IsAnotherAppInFront()
        => (_appId is { } appId
            && AppiumErrors.Live(() => _appium.FrontAppId(_driver)) is { Length: > 0 } front
            && !string.Equals(front, appId, StringComparison.Ordinal))
           || AppiumErrors.Live(() => _appium.ShowsShareSheet(_driver));

    /// <inheritdoc />
    /// <remarks>Closes the system's share sheet when one is open over the app (iOS).</remarks>
    public void ReturnToApp()
    {
        // Handles found before the other app came in front may be gone.
        Bruinet.Core.Calls.AttemptMemo.Forget();
        if (AppiumErrors.Live(() => _appium.TryCloseShareSheet(_driver)) || _appId is not { } appId)
            return;

        AppiumErrors.Live(() => _driver.ActivateApp(appId));
    }

    #endregion

    #region Dialogs

    /// <summary>The dialog on screen, or null. Answers <c>AppElement.TryFindActiveDialog</c>.</summary>
    internal IMauiElement? TryFindActiveDialogRoot()
    {
        var root = _driver.FindElements(_appium.ActiveDialog).LastOrDefault() ?? _appium.TryFindPopupWindow(_driver);
        return root == null ? null : new AppiumMauiElement(root, this);
    }

    /// <inheritdoc />
    /// <remarks>Every gesture is touch input on Android and iOS; see <c>AppiumMauiElement.PerformGesture</c>.</remarks>
    public bool SupportsGesture(string automationId, MauiGesture gesture)
        => true;

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

        return Rectangle((Dictionary<string, object>)_driver.ExecuteCustomDriverCommand(
            ElementRectCommand, new Dictionary<string, object> { ["id"] = elementId }));
    }

    /// <summary>A WebDriver rect, <c>x</c>, <c>y</c>, <c>width</c> and <c>height</c>, in whole pixels.</summary>
    internal static System.Drawing.Rectangle Rectangle(IDictionary<string, object> rect)
    {
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

    internal bool FindsOnlyVisibleElements => _findsOnlyVisible ??= _appium.FindsOnlyVisible(_driver);

    private bool? _findsOnlyVisible;

    /// <summary>Screen pixels per device-independent unit, read once per session.</summary>
    internal double PixelsPerUnit()
    {
        if (_pixelsPerUnit is { } known)
        {
            return known;
        }

        var scale = _appium.PixelsPerUnit(_driver);
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
    internal AlertContents? ReadActiveAlert() => _appium.ReadAlert(_driver);

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
        => _appium.FindByScrolling(this, container, locator);

    /// <summary>Android: lookups and <c>mobile: scrollGesture</c> swipes, as described above.</summary>
    internal IMauiElement? FindByScrollingAndroid(IMauiElement? container, Locator locator)
    {
        if (FindOnScreen(locator) is { } already)
        {
            ScrollMemory.Seen(locator);
            return already;
        }

        var scroller = AndroidScroller(container);
        if (scroller is null)
        {
            return null;
        }

        // Inside a call, what the call has left: a miss used to cost 24 swipes and 25 lookups,
        // near nine seconds, inside one attempt of a 5 s call. Outside a call, the swipes' cap alone.
        var budgetMs = Bruinet.Core.Calls.CallBudget.RemainingOr(int.MaxValue);
        var gesture = new Dictionary<string, object> { { "elementId", scroller.Id } };
        var horizontal = IsHorizontalScroller(scroller);
        var strip = horizontal ? TryGetResourceId(container) ?? string.Empty : null;
        var directions = !horizontal ? ScrollMemory.Directions(locator)
            : _lastSideDirection.TryGetValue(strip!, out var last) && last == "right" ? ["left", "right"] : SideScrollDirections;

        var found = ScrollUntil(gesture, directions, () => FindOnScreen(locator) is not null, budgetMs, atLeastOneSwipe: false,
            scroller.Rect, horizontal ? () => ScrollPosition(scroller) : null,
            horizontal ? direction => _lastSideDirection[strip!] = direction : null);
        if (!found)
        {
            return null;
        }

        ScrollMemory.Seen(locator);
        return ReResolveAfterScrolling(locator) ?? FindOnScreen(locator);
    }

    /// <summary>
    /// The view to swipe: the container when it scrolls, else the first scroller inside it. Without
    /// a container, the first scroller that is not a horizontal strip, or none: a project page's
    /// first is its tab strip, and a lookup that names no container is a lookup down the page.
    /// </summary>
    /// <remarks>
    /// A MAUI horizontal ScrollView is an <c>android.widget.ScrollView</c> that carries the
    /// AutomationId and does not scroll, around an <c>android.widget.HorizontalScrollView</c> that does.
    /// </remarks>
    private AppiumElement? AndroidScroller(IMauiElement? container)
    {
        if (TryGetResourceId(container) is not null)
        {
            var root = _driver.FindElements(MobileBy.AndroidUIAutomator(ScrollableSelector(container))).FirstOrDefault();
            if (root is null || root.GetAttribute("scrollable") == "true")
            {
                return root;
            }

            return root.FindElements(MobileBy.AndroidUIAutomator(AnyScrollable)).OfType<AppiumElement>().FirstOrDefault() ?? root;
        }

        return _driver.FindElements(MobileBy.AndroidUIAutomator(AnyScrollable))
            .FirstOrDefault(scroller => !IsHorizontalScroller(scroller));
    }

    /// <summary>
    /// Where a scroller stands: its first descendant on screen with an id or a text, and that view's
    /// clipped bounds. Either changes when the content moves. A MAUI tab button is not clickable.
    /// </summary>
    private static string? ScrollPosition(AppiumElement scroller)
    {
        var first = scroller.FindElements(MobileBy.AndroidUIAutomator(NamedView)).FirstOrDefault();
        return first is null ? null : first.Id + first.GetAttribute("bounds");
    }

    private const string NamedView = "new UiSelector().resourceIdMatches(\".+\"); new UiSelector().textMatches(\".+\")";

    // The way each horizontal strip last moved to show a view: the next search looks back that way
    // first. Tabs are opened back and forth, and the wrong way first cost the whole call's budget.
    private readonly Dictionary<string, string> _lastSideDirection = new(StringComparer.Ordinal);

    private static bool IsHorizontalScroller(IWebElement scroller)
        => scroller.GetAttribute("className") == "android.widget.HorizontalScrollView";

    private const string AnyScrollable = "new UiSelector().scrollable(true)";

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
    /// <param name="bounds">
    /// The scrolled area, or null. A swipe that starts on a view that keeps drags for itself (a
    /// signature pad) does not move the page and reads as the end. With the area known, such a
    /// swipe is tried again from further along it (<see cref="LaterStarts"/>) once every direction's
    /// plain swipe has stopped; a start that moves the page is kept.
    /// </param>
    /// <param name="position">
    /// Where the scroller stands, or null to trust the gesture's answer. UiAutomator2 answers false
    /// for every swipe of a <c>HorizontalScrollView</c>, also one that moved it, so with a position
    /// a swipe that left it unchanged ends the direction, without the later starts.
    /// </param>
    /// <param name="foundIn">Told the direction whose swipe made <paramref name="found"/> hold.</param>
    /// <returns>Whether <paramref name="found"/> held after a swipe.</returns>
    internal bool ScrollUntil(IReadOnlyDictionary<string, object> gesture, IReadOnlyList<string> directions,
        Func<bool> found, int budgetMs, bool atLeastOneSwipe, System.Drawing.Rectangle? bounds = null,
        Func<string?>? position = null, Action<string>? foundIn = null)
        => Sweep(parameters =>
            {
                try
                {
                    return _driver.ExecuteScript("mobile: scrollGesture", parameters) is true;
                }
                catch (WebDriverException error) when (!AppiumErrors.IsGone(error))
                {
                    return null;
                }
            },
            ScrollMemory.Moved, gesture, directions, found, budgetMs, atLeastOneSwipe, bounds, position, foundIn);

    /// <summary>
    /// <see cref="ScrollUntil"/> over any swipe. Two passes: every direction with the plain swipe,
    /// then, with <paramref name="bounds"/> known, every direction again from the later starts. A
    /// blocked swipe at a list's end costs about 2 s on Android; spending the later starts there
    /// first used up the budget before the search turned (D24).
    /// </summary>
    /// <param name="swipe">One <c>mobile: scrollGesture</c>: whether it can move further, or null when it failed.</param>
    /// <param name="moved">Told each direction that moved the content.</param>
    /// <param name="gesture"><inheritdoc cref="ScrollUntil" path="/param[@name='gesture']"/></param>
    /// <param name="directions"><inheritdoc cref="ScrollUntil" path="/param[@name='directions']"/></param>
    /// <param name="found"><inheritdoc cref="ScrollUntil" path="/param[@name='found']"/></param>
    /// <param name="budgetMs"><inheritdoc cref="ScrollUntil" path="/param[@name='budgetMs']"/></param>
    /// <param name="atLeastOneSwipe"><inheritdoc cref="ScrollUntil" path="/param[@name='atLeastOneSwipe']"/></param>
    /// <param name="bounds"><inheritdoc cref="ScrollUntil" path="/param[@name='bounds']"/></param>
    /// <param name="position"><inheritdoc cref="ScrollUntil" path="/param[@name='position']"/></param>
    /// <param name="foundIn"><inheritdoc cref="ScrollUntil" path="/param[@name='foundIn']"/></param>
    internal static bool Sweep(Func<IReadOnlyDictionary<string, object>, bool?> swipe, Action<string> moved,
        IReadOnlyDictionary<string, object> gesture, IReadOnlyList<string> directions,
        Func<bool> found, int budgetMs, bool atLeastOneSwipe, System.Drawing.Rectangle? bounds = null,
        Func<string?>? position = null, Action<string>? foundIn = null)
    {
        var where = position?.Invoke();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var swiped = false;

        // A position check ends a direction on its own; such a scroller has no later starts.
        var passes = bounds is null || position is not null ? 1 : 2;
        for (var pass = 0; pass < passes; pass++)
        {
            foreach (var direction in directions)
            {
                var starts = pass == 0
                    ? new List<IReadOnlyDictionary<string, object>> { gesture }
                    : [.. LaterStarts(bounds!.Value, direction), gesture];

                var start = 0;
                var blocked = 0;
                for (var step = 0; step < MaxScrollSwipes; step++)
                {
                    if ((swiped || !atLeastOneSwipe) && clock.ElapsedMilliseconds >= budgetMs)
                    {
                        return false;
                    }

                    var parameters = new Dictionary<string, object>(starts[start])
                    {
                        ["direction"] = direction,
                        ["percent"] = ScrollPercent,
                    };
                    if (swipe(parameters) is not { } more)
                    {
                        return false;
                    }

                    swiped = true;
                    var hit = found();
                    if (more || hit)
                    {
                        moved(direction);
                    }

                    if (hit)
                    {
                        foundIn?.Invoke(direction);
                        return true;
                    }

                    if (position is not null)
                    {
                        var now = position();
                        if (now == where)
                        {
                            break;
                        }

                        where = now;
                        continue;
                    }

                    if (more)
                    {
                        blocked = 0;
                        continue;
                    }

                    if (++blocked >= starts.Count)
                    {
                        break;
                    }

                    start = (start + 1) % starts.Count;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Regions of <paramref name="area"/> whose <c>mobile: scrollGesture</c> swipe starts half and
    /// three quarters of the way from where a swipe over the whole area starts. A swipe "down"
    /// starts at a region's bottom and "up" at its top, so the regions shrink from that end.
    /// </summary>
    internal static IEnumerable<IReadOnlyDictionary<string, object>> LaterStarts(System.Drawing.Rectangle area, string direction)
    {
        var sideways = direction is "left" or "right";
        foreach (var share in new[] { 0.5, 0.25 })
        {
            if (sideways)
            {
                var width = Math.Max(1, (int)(area.Width * share));
                yield return new Dictionary<string, object>
                {
                    { "left", direction == "left" ? area.Right - width : area.Left },
                    { "top", area.Top },
                    { "width", width },
                    { "height", area.Height },
                };
                continue;
            }

            var height = Math.Max(1, (int)(area.Height * share));
            var top = direction == "up" ? area.Bottom - height : area.Top;
            yield return new Dictionary<string, object>
            {
                { "left", area.Left },
                { "top", top },
                { "width", area.Width },
                { "height", height },
            };
        }
    }

    /// <summary>
    /// iOS scroll-to-find. A UICollectionView only has cells for its visible rows, so a row below
    /// the fold is not in the XCUITest tree and XCUITest's own scroll-to-element cannot reach it.
    /// The scroller is swiped as a list scan swipes it, down then up, with a lookup after each swipe.
    /// </summary>
    /// <remarks>
    /// Not XCUITest's <c>mobile: scroll</c>, which waits for the page to settle and took 7.5 s a
    /// page, against 1.7 s for the swipe (.my/performance, step 8). A swipe does not say whether the scroller moved, so the visible
    /// rows' names and positions are compared before and after: unchanged means that end is
    /// reached. Without a container, the tallest scroller on screen is taken, not the first: a
    /// project page's first is its horizontal tab strip.
    /// </remarks>
    internal IMauiElement? FindByScrollingIos(IMauiElement? container, Locator locator)
    {
        if (FindOnScreenIos(locator) is { } already)
        {
            return already;
        }

        // A swipe from the list's lower edge lands on an open keyboard and moves nothing; a user
        // closes it first, as ScrollIntoView does (D14). Rows behind it may already be the hit.
        if (HideKeyboardIfShown() && FindOnScreenIos(locator) is { } uncovered)
        {
            return uncovered;
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
                    // The list scan's own swipe, about 1.7 s; XCUITest's 'mobile: scroll' took 7.5 s a page.
                    // NotMoved: the scroll bar says this end is reached, so no swipe (D13).
                    if (new AppiumMauiElement(scroller, this).ScrollContent(direction == "down" ? 1 : -1) == ScrollStep.NotMoved)
                    {
                        break;
                    }
                }
                catch (WebDriverException error) when (!AppiumErrors.IsGone(error))
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

    internal const string IosScrollerTypes =
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
        catch (WebDriverException error) when (!AppiumErrors.IsGone(error))
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
        catch (WebDriverException error) when (!AppiumErrors.IsGone(error))
        {
            return string.Empty;
        }
    }

    /// <summary>Down first: a page opens at its top, and most lookups are for what follows.</summary>
    private static readonly string[] ScrollDirections = ["down", "up"];

    private static readonly string[] SideScrollDirections = ["right", "left"];

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
        catch (Exception error) when (error is WebDriverException or StaleElementException && !AppiumErrors.IsGone(error))
        {
            // A container without the attribute, or one replaced since it was found: no id. A lost session goes on.
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
        catch (Exception error) when (error is WebDriverException or StaleElementException && !AppiumErrors.IsGone(error))
        {
            // Not found in this look, or replaced while it settled: none. A lost session goes on.
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
