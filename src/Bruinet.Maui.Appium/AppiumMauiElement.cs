using System.Drawing;
using System.Globalization;
using Bruinet.Core;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Recording;
using Bruinet.Core.Utilities;
using Bruinet.Maui.Enums;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;

namespace Bruinet.Maui.Appium;

/// <summary>
/// Appium-based implementation of <see cref="IMauiElement"/> for <b>Android and iOS</b>.
/// Delegates all operations to the underlying AppiumElement.
/// </summary>
/// <remarks>
/// <para>
/// Windows is served by <c>Bruinet.Maui.FlaUI</c>, not by this driver. Anything here shaped
/// for the desktop is a mistake rather than a fallback: a Windows default on
/// <c>Locator.ToBy</c> silently resolved every Android AutomationId as an AccessibilityId, and
/// an iOS-only script name (<c>mobile: setValue</c>) was called on Android where it does not
/// exist. Both were invisible until a device ran.
/// </para>
/// <para>
/// Answers <see cref="IMauiElement"/> in its own terms. It used to implement three UI Automation
/// <c>*PatternElement</c> interfaces - toggle, selection item, value - so controls written against
/// Windows could cast to them; controls now name what they mean (<see cref="Invoke"/>,
/// <see cref="Select"/>, <see cref="Checked"/>) and a member this platform has no route for keeps
/// the interface's default, which says so (step 107).
/// </para>
/// <para>
/// Android and iOS express toggle and selection state through different attributes, so each
/// capability reads the platform's own attribute rather than a common one. That difference
/// stops here, at the element: no control object above this layer branches on platform.
/// </para>
/// </remarks>
public sealed partial class AppiumMauiElement : IMauiElement, IShownWhenFound
{
    private readonly AppiumElement? _wrapped;
    private readonly AppiumMauiDriver _driver;

    /// <summary>
    /// Creates a new AppiumMauiElement wrapper.
    /// </summary>
    /// <param name="element">The AppiumElement to wrap.</param>
    /// <param name="driver">The driver that owns this element.</param>
    /// <exception cref="ArgumentNullException">Thrown when element or driver is null.</exception>
    public AppiumMauiElement(AppiumElement element, AppiumMauiDriver driver)
    {
        _wrapped = element ?? throw new ArgumentNullException(nameof(element));
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }

    /// <summary>Creates the app element, which stands for the whole screen's hierarchy.</summary>
    private AppiumMauiElement(AppiumMauiDriver driver)
    {
        _driver = driver;
    }

    /// <summary>The app element: <see cref="IMauiDriver.AppElement"/>.</summary>
    internal static AppiumMauiElement ForApp(AppiumMauiDriver driver) => new(driver);

    /// <summary>Whether this element stands for the app rather than one of its elements.</summary>
    private bool IsApp => _wrapped is null;

    /// <summary>
    /// The Appium element: the one wrapped, or for the app element the hierarchy's root node.
    /// </summary>
    private AppiumElement _element => _wrapped ?? _driver.FindRootNode();

    #region Live: how a removed element is reported

    /// <summary>
    /// Runs an action no interaction report covers (a scroll): what the attempt read before it is
    /// forgotten once it ran (<see cref="Bruinet.Core.Calls.AttemptMemo.Forget"/>).
    /// </summary>
    private static void Acting(Action act)
    {
        try
        {
            act();
        }
        finally
        {
            Bruinet.Core.Calls.AttemptMemo.Forget();
        }
    }

    /// <inheritdoc cref="Acting(Action)"/>
    private static T Acting<T>(Func<T> act)
    {
        try
        {
            return act();
        }
        finally
        {
            Bruinet.Core.Calls.AttemptMemo.Forget();
        }
    }

    /// <summary>
    /// Runs a WebDriver read or action, reporting a replaced element as
    /// <see cref="StaleElementException"/> and an ended session as <see cref="AppUnavailableException"/>.
    /// </summary>
    private static T Live<T>(Func<T> touch) => AppiumErrors.Live(touch);

    /// <inheritdoc cref="Live{T}(Func{T})"/>
    private void Live(Action touch) => Live(() =>
    {
        touch();
        return true;
    });

    /// <summary>
    /// A read of the element that went to the driver: one that did not throw also proves the
    /// element alive for the rest of the attempt (<see cref="InstanceKey"/>).
    /// </summary>
    private T Read<T>(Func<T> read)
    {
        var value = Live(read);
        _ = Bruinet.Core.Calls.AttemptMemo.Remember(this, AliveQuestion, () => true);
        return value;
    }

    private const string AliveQuestion = "alive";

    // The text as the driver reports it, before an empty field's placeholder is told apart.
    private const string TextQuestion = "text";

    /// <summary>
    /// A read the find already answered in this attempt (<see cref="Know"/>), or else the driver's.
    /// Only a find's facts are kept: a read that went to the driver is asked again next time, as before.
    /// </summary>
    private T Fact<T>(string question, Func<T> read)
        => Bruinet.Core.Calls.AttemptMemo.Knows(this, question)
            ? Bruinet.Core.Calls.AttemptMemo.Remember(this, question, read)
            : Read(read);

    /// <summary>
    /// Keeps the facts a find returned with this element for the running attempt: iOS sends the
    /// element's shown and enabled state, text, bounds and AutomationId with the find, each of which
    /// was a WebDriverAgent request of 90 ms or more of its own. Outside an attempt nothing is kept.
    /// An action ends them (<see cref="Bruinet.Core.Calls.AttemptMemo.Forget"/>).
    /// </summary>
    internal void Know(IDictionary<string, object> facts)
    {
        _ = Bruinet.Core.Calls.AttemptMemo.Remember(this, AliveQuestion, () => true);

        if (facts.TryGetValue("displayed", out var displayed) && displayed is bool shown)
            _ = Bruinet.Core.Calls.AttemptMemo.Remember(this, nameof(Visible), () => shown);

        if (facts.TryGetValue("enabled", out var enabled) && enabled is bool usable)
            _ = Bruinet.Core.Calls.AttemptMemo.Remember(this, nameof(Enabled), () => usable);

        if (facts.TryGetValue("text", out var text) && text is null or string)
            _ = Bruinet.Core.Calls.AttemptMemo.Remember(this, TextQuestion, () => (string?)text);

        if (facts.TryGetValue("rect", out var rect) && rect is IDictionary<string, object> bounds)
            _ = Bruinet.Core.Calls.AttemptMemo.Remember(this, nameof(Rect), () => AppiumMauiDriver.Rectangle(bounds));

        if (facts.TryGetValue("attribute/name", out var name) && name is null or string)
            _ = Bruinet.Core.Calls.AttemptMemo.Remember(this, nameof(AutomationId), () => Present((string?)name));
    }

    /// <inheritdoc />
    /// <remarks>
    /// The WebDriver element id; read live, so it doubles as the "still there" check. A read of
    /// this element earlier in the same attempt already proved that, so it is not asked again: the
    /// key after a polled read was an extra round trip on every attempt.
    /// </remarks>
    public string InstanceKey => Bruinet.Core.Calls.AttemptMemo.Knows(this, AliveQuestion)
        ? _element.Id
        : Read(() =>
        {
            // A cheap round trip that fails on a replaced element; the id alone is held client-side.
            _ = _element.Enabled;
            return _element.Id;
        });

    #endregion

    #region State Properties

    /// <inheritdoc />
    public bool Visible => Fact(nameof(Visible), () => _element.Displayed);

    /// <inheritdoc />
    public bool Enabled => Fact(nameof(Enabled), () => _element.Enabled);

    /// <inheritdoc />
    public bool Selected => Read(() => _element.Selected);

    /// <inheritdoc />
    /// <remarks>
    /// An empty Android text field reports its placeholder as its text: UiAutomator2 gives an empty
    /// <c>EditText</c> with <c>hint="Title"</c> the text "Title" as well (probed 2026-09-19 in the
    /// Todo sample). Such a field reads as empty here, as it does on Windows, where the placeholder
    /// is a separate property. iOS does the same: XCUITest reports an empty <c>UITextField</c>'s
    /// value as its <c>placeholderValue</c> (probed 2026-11-04 on iOS 27). The limit: a field whose value is exactly its own placeholder reads
    /// as empty too. UiAutomator2 lists <c>showing-hint</c>, which would say so directly, but refuses
    /// to read it ("attribute is unknown" on Android 16). A non-empty Android text read costs one
    /// class read more, and a text field's one hint read on top.
    /// </remarks>
    public string? Text => Read(() =>
    {
        var text = Fact(TextQuestion, () => _element.Text);

        if (string.IsNullOrEmpty(text))
            return text;

        var isTextField = _driver.Appium.IsTextField(TagName);

        return isTextField && text == Hint ? string.Empty : text;
    });

    /// <inheritdoc />
    /// <remarks>
    /// On Android the widget class, <c>android.widget.EditText</c>: UiAutomator2 answers the tag
    /// name request with null (probed 2026-09-19), so every Android type check - an EditText's
    /// placeholder, a control-type item key - silently failed. Read once per element id on both
    /// platforms: every text read asks it, and on iOS it was one round trip in seven of a list scan.
    /// </remarks>
    public string? TagName => Live(() => _driver.Appium.ReadTagName(this));

    /// <inheritdoc />
    /// <remarks>
    /// Android, unless the session asked UiAutomator2 for invisible elements: its tree holds only
    /// what the user can see. Not the app element, which stands for the whole screen.
    /// </remarks>
    public bool IsShownWhenFound => !IsApp && _driver.FindsOnlyVisibleElements;

    /// <inheritdoc />
    public Point Location => Live(() => _element.Location);

    /// <inheritdoc />
    public Size Size => Live(() => _element.Size);

    /// <inheritdoc />
    /// <remarks>One request: <c>Location</c> and <c>Size</c> each fetch the element's rect, so building
    /// it from them cost two, and bounds are read on every readiness check (.my/android-performance, P2).</remarks>
    public Rectangle Rect => Fact(nameof(Rect), () => _driver.ElementRect(_element.Id));

    // A recorded video's touch mark (AD-024): the target as it is, before the action runs.
    private Interactions.Scope Report(InteractionKind kind, string? key = null, TimeSpan duration = default)
        => Interactions.Report(kind, () => Rect, () => AutomationId, duration: duration, key: key, target: () => _driver.RecordingTarget);

    private void Marked(InteractionKind kind, Action action) => Marked(kind, null, action);

    private void Marked(InteractionKind kind, string? key, Action action)
    {
        using var mark = Report(kind, key);
        action();
    }

    private void Marked(InteractionKind kind, TimeSpan duration, Action action)
    {
        using var mark = Report(kind, duration: duration);
        action();
    }

    private void Marked(MauiGesture gesture, Action action)
    {
        using var mark = MauiInteractions.Report(gesture, () => Rect, () => AutomationId);
        action();
    }

    // A swipe's own points, in the device's pixels as the element's bounds are.
    private void Marked(Point from, Point to, TimeSpan duration, Action action)
    {
        using var mark = Interactions.Report(InteractionKind.Swipe, () => Rect, () => AutomationId, from, to, duration, target: () => _driver.RecordingTarget);
        action();
    }

    #endregion

    #region Actions

    /// <inheritdoc />
    public void Click() => Marked(InteractionKind.Tap, () => Live(() =>
    {
        if (_driver.Appium.TryTapFreePoint(this))
            return;
        _element.Click();
    }));

    #region Activation

    // All three are a tap, and that is not a fallback - it is how a touch platform performs
    // these semantics. Android and iOS have no automation patterns to invoke, toggle or select
    // through; a user does all three with one finger, and so does the driver.
    //
    // Writing it here, once, is the point of the design. The shared ladder used to reach the
    // same conclusion by probing three patterns that are never present on this platform, on
    // every control, on every call.

    /// <inheritdoc />
    public void Invoke() => Click();

    /// <inheritdoc />
    public void Toggle() => Click();

    /// <inheritdoc />
    public void Select() => Click();

    /// <inheritdoc />
    /// <remarks>
    /// A tap on the item, which is the ordinary route on a touch platform. On an item the id is not
    /// needed: the element was found already. On the app element the item is found first, by the
    /// accessibility id MAUI puts a toolbar item's <c>AutomationId</c> in - on Android the node's
    /// <c>resource-id</c> is empty and the value is in <c>content-desc</c>.
    /// </remarks>
    public void InvokeToolbarItem(string automationId)
    {
        if (!IsApp)
        {
            Click();
            return;
        }

        if (string.IsNullOrEmpty(automationId))
        {
            throw new BruinetException("Could not invoke a toolbar item: no AutomationId was given.");
        }

        // Refused the way Windows refuses, so a test reads the same on both: an id no item carries
        // is a BruinetException with the reason, a disabled item is not ready and is not pressed.
        IMauiElement item;
        try
        {
            item = _driver.FindChrome(Locator.ByAccessibilityId(automationId), ChromeFindTimeoutMs);
        }
        catch (ElementNotFoundException exception)
        {
            throw new BruinetException(
                $"Could not invoke toolbar item '{automationId}': no toolbar item on the page on screen "
                + "carries that AutomationId. Toolbar items are matched by the id in the app's markup, "
                + "not by their text.", exception);
        }

        if (!item.Enabled)
        {
            throw new ElementNotReadyException(
                Locator.ByAccessibilityId(automationId), NotReadyReason.Disabled, "the toolbar item is disabled");
        }

        item.Click();
    }

    #endregion

    #region Focus and text

    // The mobile routes that used to sit in the control objects, behind a Supports* question
    // whose false branch only ever ran here. Windows performs these through the app's verbs.

    /// <inheritdoc />
    /// <remarks>A tap: how a touch platform focuses a field.</remarks>
    public void Focus() => Click();

    /// <inheritdoc />
    /// <remarks>
    /// Tab, which moves focus on to the next control rather than removing it. This platform has
    /// no way to simply drop focus.
    /// </remarks>
    public void ClearFocus() => Marked(InteractionKind.Key, "Tab", () => SendKeys(Keys.Tab));

    /// <inheritdoc />
    /// <remarks>Types the text at the end of what the field holds.</remarks>
    public void AppendText(string text) => SendKeys(text);

    #endregion

    #region Range

    // Android publishes a range through the node's range info: UiAutomator2 reports its current
    // value as the node's text (Java's Float.toString, e.g. "2.1474836E7") and sets it with
    // ACTION_SET_PROGRESS when sent a plain number. It publishes neither bound.
    //
    // What a stock MAUI Slider publishes there is its SeekBar's raw progress, 0 to int.MaxValue,
    // and a Stepper publishes nothing. Bruinet.Maui.AppSupport replaces both with the MAUI values
    // and marks the node with the bounds in its extras. Only a marked node answers here: an
    // unmarked range's text is a raw fraction, and it is never presented as a value.
    // Probed 2026-09-19 on Android 36.

    /// <summary>The node extras AppSupport writes the bounds under; their presence is the mark.</summary>
    private const string RangeMinimumExtra = "Bruinet.Range.Minimum";

    /// <inheritdoc cref="RangeMinimumExtra"/>
    private const string RangeMaximumExtra = "Bruinet.Range.Maximum";

    /// <inheritdoc />
    /// <remarks>
    /// Android: the node's text, parsed, when the node is marked as in app units (see the region
    /// comment). Null otherwise, and on iOS.
    /// </remarks>
    public double? RangeValue => Live(() => ReadAppRange() is not null ? ParseRangeNumber(_element.Text) : null);

    /// <inheritdoc />
    /// <remarks>Android: the minimum AppSupport published in the node's extras; null without it.</remarks>
    public double? RangeMinimum => Live(() => ReadAppRange()?.Minimum);

    /// <inheritdoc />
    /// <remarks>Android: the maximum AppSupport published in the node's extras; null without it.</remarks>
    public double? RangeMaximum => Live(() => ReadAppRange()?.Maximum);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Android: the value as a plain number, which UiAutomator2 turns into
    /// <c>ACTION_SET_PROGRESS</c>, the action TalkBack uses as well. Performs or throws; the control
    /// confirms the effect.
    /// </para>
    /// <para>
    /// Only on a node marked as in app units: on a stock <c>SeekBar</c> the number would be taken as
    /// raw progress out of <c>int.MaxValue</c>, so that throws, naming AppSupport.
    /// </para>
    /// </remarks>
    /// <exception cref="NotSupportedException">The node does not publish its range in app units, or the platform is not Android.</exception>
    public void SetRangeValue(double value) => Marked(InteractionKind.Tap, () => Live(() =>
    {
        if (!_driver.Appium.PublishesAppRange)
        {
            throw new NotSupportedException(
                $"{nameof(AppiumMauiElement)}.{nameof(SetRangeValue)} has no route for {_driver.Platform} yet.");
        }

        if (ReadAppRange() is null)
        {
            throw new NotSupportedException(
                $"'{AutomationId}' does not publish its range in the app's units, so a value cannot be set on it. "
                + "The app has to include Bruinet.Maui.AppSupport (AddBruinetAutomationHandlers), which "
                + "publishes a Slider's and a Stepper's range on Android.");
        }

        _element.SendKeys(value.ToString("R", CultureInfo.InvariantCulture));
    }));

    /// <summary>The bounds AppSupport published on this node, or null when it published none.</summary>
    private (double Minimum, double Maximum)? ReadAppRange()
    {
        if (!_driver.Appium.PublishesAppRange)
        {
            return null;
        }

        var extras = ParseExtras(GetAttribute("extras"));
        return extras.TryGetValue(RangeMinimumExtra, out var min)
               && extras.TryGetValue(RangeMaximumExtra, out var max)
               && ParseRangeNumber(min) is { } minimum
               && ParseRangeNumber(max) is { } maximum
            ? (minimum, maximum)
            : null;
    }

    /// <summary>
    /// UiAutomator2's <c>extras</c>: <c>key=value</c> pairs separated by semicolons.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> ParseExtras(string? extras)
    {
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(extras) || extras == "null")
        {
            return pairs;
        }

        foreach (var pair in extras.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator > 0)
            {
                pairs[pair[..separator]] = pair[(separator + 1)..];
            }
        }

        return pairs;
    }

    /// <summary>A number as Java prints it (<c>5.0</c>, <c>2.1474836E7</c>), or null.</summary>
    internal static double? ParseRangeNumber(string? text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    #endregion

    #region Gestures

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>This is where a swipe becomes a swipe on a touch device.</b> A control object names the
    /// gesture and its element performs it however the platform does - on Windows by handing a
    /// verb to the app, here by dragging across the element's own bounds. Both are written once,
    /// per platform, which is the whole shape of the design: the control knows what, the element
    /// knows how.
    /// </para>
    /// <para>
    /// <b>The switch below is the honest list, and the only one.</b> Every <see cref="MauiGesture"/>
    /// is real touch input here: tap, double tap and long press on the element, swipes across its
    /// bounds, a pan as a drag from its centre by the offset given, a pinch as UiAutomator2's or
    /// XCUITest's own pinch. There used to be a <c>SupportsGesture</c> naming a subset separately -
    /// a second list to keep in step with this one - and it went when no control was left asking.
    /// </para>
    /// <para>
    /// A gesture the switch does not handle throws rather than doing something near enough.
    /// Delivering a press for a long press, or a tap for a pinch, would report a gesture the app
    /// never received.
    /// </para>
    /// </remarks>
    public void PerformGesture(MauiGesture gesture) => PerformGesture(gesture, 0, 0);

    /// <summary>Performs a gesture that may carry a magnitude, as touch input.</summary>
    /// <param name="gesture">The gesture.</param>
    /// <param name="arg1">
    /// A pan's horizontal offset, or a pinch's size in percent (positive opens, negative closes);
    /// in device-independent units, as the app declares them on Windows.
    /// </param>
    /// <param name="arg2">A pan's vertical offset, in device-independent units.</param>
    internal void PerformGesture(MauiGesture gesture, int arg1, int arg2) => Marked(gesture, () => Live(() =>
    {
        switch (gesture)
        {
            case MauiGesture.Tap:
                Click();
                return;

            case MauiGesture.DoubleTap:
                Mobile(_driver.Appium.DoubleTapScript,
                    new Dictionary<string, object> { { "elementId", _element.Id } });
                return;

            case MauiGesture.LongPress:
                LongPress();
                return;

            case MauiGesture.Pan:
                Pan(arg1, arg2);
                return;

            case MauiGesture.Pinch:
                Pinch(arg1);
                return;

            case MauiGesture.SwipeLeft:
                this.SwipeLeft();
                return;

            case MauiGesture.SwipeRight:
                this.SwipeRight();
                return;

            case MauiGesture.SwipeUp:
                this.SwipeUp();
                return;

            case MauiGesture.SwipeDown:
                this.SwipeDown();
                return;

            default:
                throw new NotSupportedException($"{nameof(AppiumMauiElement)} does not know the gesture {gesture}.");
        }
    }));

    /// <summary>A drag from the element's centre by an offset in device-independent units.</summary>
    private void Pan(int deltaX, int deltaY)
    {
        var rect = Rect;
        var scale = _driver.PixelsPerUnit();
        var (fromX, fromY) = (rect.Left + (rect.Width / 2), rect.Top + (rect.Height / 2));
        var (toX, toY) = (fromX + (int)Math.Round(deltaX * scale), fromY + (int)Math.Round(deltaY * scale));

        _driver.Appium.Drag(this, fromX, fromY, toX, toY);
    }

    /// <summary>A pinch on the element: positive opens, negative closes, by that many percent (50 when 0).</summary>
    private void Pinch(int percent)
    {
        var size = Math.Clamp(percent == 0 ? 50 : Math.Abs(percent), 1, 100) / 100.0;
        _driver.Appium.Pinch(this, percent, size);
    }

    private void Mobile(string command, Dictionary<string, object> arguments)
        => _driver.Driver.ExecuteScript(command, arguments);

    #endregion


    /// <inheritdoc />
    public void SendKeys(string text, TextInputMethod method = TextInputMethod.Keys) => Marked(InteractionKind.Type, () => Live(() =>
    {
        switch (method)
        {
            case TextInputMethod.Keys:
                _element.SendKeys(text);
                break;
            case TextInputMethod.Paste:
                // Set clipboard and paste (platform-specific)
                SetClipboardAndPaste(text);
                break;
            case TextInputMethod.SetValue:
                SetValueDirectly(text);
                break;
        }
    }));

    private void SetClipboardAndPaste(string text)
    {
        // Clipboard/paste operations are complex and platform-specific.
        // For now, fall back to SendKeys which works across all platforms.
        // Full clipboard support can be added when we have platform-specific driver implementations.
        _element.SendKeys(text);
    }

    /// <summary>
    /// Sets an element's value directly, bypassing the keyboard.
    /// </summary>
    private void SetValueDirectly(string text) => _driver.Appium.SetValue(this, text);

    /// <inheritdoc />
    public void Clear() => Marked(InteractionKind.Clear, () => Live(() => _element.Clear()));

    /// <inheritdoc />
    public void DoubleClick() => Marked(InteractionKind.DoubleTap, () => Live(() =>
    {
        var actions = new Actions(_driver.Driver);
        actions.DoubleClick(_element).Perform();
    }));

    /// <inheritdoc />
    public void RightClick()
    {
        using var mark = Report(InteractionKind.RightClick);

        var actions = new Actions(_driver.Driver);
        actions.ContextClick(_element).Perform();
    }

    /// <inheritdoc />
    public void Hover()
    {
        using var mark = Report(InteractionKind.Hover);

        var actions = new Actions(_driver.Driver);
        actions.MoveToElement(_element).Perform();
    }

    /// <inheritdoc />
    public void LongPress(int durationMs = 1000) => Marked(InteractionKind.LongPress, TimeSpan.FromMilliseconds(durationMs), () => Live(() =>
        _driver.Appium.LongPress(this, durationMs)));

    /// <inheritdoc />
    public void ScrollIntoView(int timeoutMs) => Acting(() => Live(() =>
    {
        try
        {
            if (_element.Displayed)
                return;
        }
        catch (Exception error) when (!AppiumErrors.IsGone(error))
        {
            // Continue with scroll attempt
        }

        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);

        _driver.Appium.ScrollIntoView(this, startTime, timeout);
    }));

    internal bool IsDisplayedNow()
    {
        try
        {
            return _element.Displayed;
        }
        catch (Exception error) when (!AppiumErrors.IsGone(error))
        {
            return false;
        }
    }

    /// <summary>
    /// Runs <paramref name="strategies"/> in order until one succeeds. The first always runs, so a
    /// spent budget does not mean "never tries"; the rest only while <paramref name="elapsedMs"/> is
    /// under <paramref name="budgetMs"/>.
    /// </summary>
    /// <returns>Whether one succeeded.</returns>
    internal static bool TryInOrder(IReadOnlyList<Func<bool>> strategies, int budgetMs, Func<long> elapsedMs)
    {
        for (var i = 0; i < strategies.Count; i++)
        {
            if (i > 0 && elapsedMs() >= budgetMs)
            {
                return false;
            }

            if (strategies[i]())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Runs one <c>mobile:</c> script; false when the driver refused it.</summary>
    internal bool TryScript(string script, Dictionary<string, object> arguments)
    {
        try
        {
            _driver.Driver.ExecuteScript(script, arguments);
            return true;
        }
        catch (Exception error) when (!AppiumErrors.IsGone(error))
        {
            // This way does not apply here; the next is tried, and the caller's poll sees whether
            // the element came into view.
            return false;
        }
    }

    /// <summary>
    /// Waits until the element stops moving, so a caller acts on where it is rather than where it
    /// was.
    /// </summary>
    /// <summary>
    /// Scrolls the element clear of the bottom of the screen when it has come to rest there.
    /// </summary>
    internal void NudgeClearOfBottomEdge()
    {
        try
        {
            var screenHeight = _driver.Driver.Manage().Window.Size.Height;
            var margin = screenHeight / 8;
            if (Rect.Bottom <= screenHeight - margin)
            {
                return;
            }

            _driver.Driver.ExecuteScript("mobile: scrollGesture", new Dictionary<string, object>
            {
                { "left", 50 },
                { "top", 150 },
                { "width", _driver.Driver.Manage().Window.Size.Width - 100 },
                { "height", screenHeight - 300 },
                { "direction", "down" },
                { "percent", 0.25 }
            });

            WaitUntilPositionSettles();
        }
        catch (Exception error) when (!AppiumErrors.IsGone(error))
        {
            // Not being able to move it is not a reason to refuse to act on it.
        }
    }

    internal void WaitUntilPositionSettles()
    {
        // At most half a second, and never past what the running call has left.
        const int IntervalMs = 50;
        var maxChecks = Bruinet.Core.Calls.CallBudget.Within(500) / IntervalMs;

        try
        {
            var previous = Rect;
            for (var check = 0; check < maxChecks; check++)
            {
                WaitHelper.Pause(IntervalMs);
                var current = Rect;
                if (current == previous)
                {
                    return;
                }

                previous = current;
            }
        }
        catch (Exception error) when (!AppiumErrors.IsGone(error))
        {
            // A position we cannot read is one we cannot wait on; let the caller proceed.
        }
    }


    /// <inheritdoc />
    public void Swipe(int startX, int startY, int endX, int endY, int durationMs = 500) => Marked(new Point(startX, startY), new Point(endX, endY), TimeSpan.FromMilliseconds(durationMs), () => Live(() =>
        _driver.Appium.Swipe(this, startX, startY, endX, endY, durationMs)));

    #endregion

    #region Element Finding

    /// <inheritdoc />
    public IMauiElement? TryFindElement(Locator locator) => Live<IMauiElement?>(() =>
    {
        var by = _driver.Appium.ToChildBy(locator);

        try
        {
            var found = _driver.FindOne(by, _element);
            _driver.ScrollMemory.Seen(locator);
            return found;
        }
        catch (NoSuchElementException)
        {
            return null;
        }
    });

    /// <inheritdoc />
    public IReadOnlyList<IMauiElement> FindElements(Locator locator) => Live<IReadOnlyList<IMauiElement>>(() =>
    {
        var by = _driver.Appium.ToChildBy(locator);

        var found = _driver.FindAll(by, _element);
        if (found.Count > 0)
        {
            _driver.ScrollMemory.Seen(locator);
        }

        return found;
    });

    #endregion

    #region Attribute Access (IMauiElement)

    /// <inheritdoc />
    /// <remarks>
    /// Android reports the resource id fully qualified - <c>com.example.app:id/SaveButton</c> -
    /// where MAUI put only the <c>AutomationId</c>, so the package prefix is stripped and the
    /// caller gets back what the app author wrote. iOS carries the accessibility identifier,
    /// which the driver reports as <c>name</c>. Kept for the attempt: a list scan read each row's
    /// id for its duplicate check and again for its key, one round trip in six of a scan on iOS.
    /// </remarks>
    public string? AutomationId
        => Bruinet.Core.Calls.AttemptMemo.Remember(this, nameof(AutomationId), () => _driver.Appium.ReadAutomationId(this));

    /// <inheritdoc />
    public string? Hint => Present(GetAttribute(_driver.Appium.HintAttribute));

    /// <inheritdoc />
    public bool Focused => _driver.Appium.ReadFocused(this);

    internal bool IsActiveElement() => Live(() =>
    {
        IWebElement active;
        try
        {
            active = _driver.Driver.SwitchTo().ActiveElement();
        }
        catch (WebDriverException)
        {
            return false;
        }

        return _element.Equals(active);
    });

    internal static bool IsTrue(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    /// <remarks>
    /// Android's accessible name is the content description; an element without one is named by
    /// its text, which is what a screen reader falls back to as well.
    /// </remarks>
    public string? Name => Live(() => _driver.Appium.ReadName(this));

    /// <summary>
    /// An attribute value, or null when there is none.
    /// </summary>
    internal static string? Present(string? value)
        => string.IsNullOrEmpty(value) || value == "null" ? null : value;

    /// <summary>The Appium element, for the platform's reads.</summary>
    internal AppiumElement Native => _element;

    /// <summary>The driver that owns this element.</summary>
    internal AppiumMauiDriver Driver => _driver;

    /// <inheritdoc />
    /// <remarks>
    /// An attribute the platform does not expose reads as null rather than throwing. UiAutomator2
    /// raises <c>NotImplementedException</c> for anything outside its fixed list, so a control
    /// probing several candidate names — as the progress and placeholder readers do — would abort
    /// on the first miss, while the same probe returns null on Windows.
    /// UI Automation's <c>Name</c> reads as <see cref="Name"/>, the accessible name: XCUITest's
    /// own <c>name</c> is the AutomationId, and it has no attribute spelled <c>Name</c>.
    /// </remarks>
    public string? GetAttribute(string attributeName)
        => attributeName == "Name"
        ? Name
        : Read<string?>(() =>
    {
        try
        {
            return _element.GetAttribute(attributeName);
        }
        catch (NotImplementedException)
        {
            return null;
        }
        catch (WebDriverException error) when (error.Message.Contains("is unknown", StringComparison.Ordinal))
        {
            // XCUITest rejects attributes outside its list, such as Windows' HelpText.
            return null;
        }
    });

    /// <inheritdoc />
    public string? GetDomAttribute(string attributeName) => Live(() => _element.GetDomAttribute(attributeName));

    /// <inheritdoc />
    public string? GetDomProperty(string propertyName) => Live(() => _element.GetDomProperty(propertyName));

    /// <inheritdoc />
    public string? GetCssValue(string propertyName) => Live(() => _element.GetCssValue(propertyName));

    /// <inheritdoc />
    public void Submit() => Marked(InteractionKind.Key, "Enter", () => Live(() => _element.Submit()));

    #endregion

    #region Checked state

    /// <summary>
    /// The attribute carrying checked state, per platform.
    /// </summary>
    private string? ToggleStateAttribute => _driver.Appium.ToggleStateAttribute;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Null unless the element really has a checked state. Android reports <c>checked="false"</c>
    /// on <i>every</i> view, a plain Button included, so the attribute being present cannot
    /// distinguish a real toggle; <c>checkable</c> is the attribute that does.
    /// </para>
    /// </remarks>
    public bool? Checked => Live<bool?>(() =>
    {
        var attribute = ToggleStateAttribute;
        if (attribute == null)
            return null;

        try
        {
            if (_driver.Appium.CheckedNeedsCheckable && !IsAttributeTrue("checkable"))
            {
                return null;
            }

            var value = _element.GetAttribute(attribute);
            if (string.IsNullOrEmpty(value))
                return null;

            return value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("1", StringComparison.Ordinal);
        }
        catch (Exception error) when (!AppiumErrors.IsGone(error))
        {
            // A driver may throw rather than return null for an absent attribute.
            return null;
        }
    });

    /// <inheritdoc />
    /// <remarks>
    /// Neither platform has a set-state command, so this reads the state and taps when it
    /// differs. The control verifies the state it asked for.
    /// </remarks>
    public void SetChecked(bool isChecked) => Marked(InteractionKind.Tap, () => Live(() =>
    {
        if (Checked == isChecked)
            return;

        Toggle();
    }));

    /// <summary>
    /// Whether an attribute is present and reads as true.
    /// </summary>
    private bool IsAttributeTrue(string name)
    {
        try
        {
            var value = _element.GetAttribute(name);
            return !string.IsNullOrEmpty(value)
                && value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (!AppiumErrors.IsGone(error))
        {
            return false;
        }
    }

    #endregion

    #region Scrolling

    /// <summary>Margin in pixels kept away from an element's edges when swiping.</summary>
    private const int EdgeInset = 20;

    /// <summary>
    /// An element shorter (or narrower) than this cannot be swiped meaningfully - the start and end
    /// points would collapse onto each other.
    /// </summary>
    private const int MinimumSwipeExtent = 40;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>A swipe across the element's own bounds</b>, one per step: from far to near drags the
    /// content towards the start, revealing what follows. A swipe cannot report whether anything
    /// moved, so a performed step is <see cref="ScrollStep.Unconfirmed"/> and the caller checks
    /// the content.
    /// </para>
    /// <para>
    /// This geometry lived in the cross-platform <c>ScrollHelper</c>, behind a
    /// <c>SupportsScrollContent</c> question this platform always answered false.
    /// </para>
    /// </remarks>
    public ScrollStep ScrollContent(int verticalSteps, int horizontalSteps = 0) => Acting(() => Live<ScrollStep>(() =>
    {
        if (verticalSteps == 0 && horizontalSteps == 0)
            return ScrollStep.NotMoved;

        var android = _driver.Appium.ScrollsEdgeToEdge;

        // Android says when content fits: nothing at or in the element is scrollable. A swipe there
        // moved nothing: a step up was a pull-to-refresh, whose reloaded rows looked like a move,
        // so a scroll to the top never ended.
        if (android && !CanScrollOnAndroid())
            return ScrollStep.NotMoved;

        var rect = Rect;

        if (verticalSteps != 0)
        {
            if (rect.Height <= MinimumSwipeExtent)
                return ScrollStep.NotMoved;

            var centerX = rect.X + (rect.Width / 2);
            var inset = SwipeInset(rect.Height, android);
            var near = rect.Y + inset;
            var far = rect.Y + rect.Height - inset;

            for (var step = 0; step < Math.Abs(verticalSteps); step++)
            {
                if (verticalSteps > 0)
                    Swipe(centerX, far, centerX, near);
                else
                    Swipe(centerX, near, centerX, far);
            }
        }

        if (horizontalSteps != 0)
        {
            if (rect.Width <= MinimumSwipeExtent)
                return ScrollStep.NotMoved;

            var centerY = rect.Y + (rect.Height / 2);
            var inset = SwipeInset(rect.Width, android);
            var near = rect.X + inset;
            var far = rect.X + rect.Width - inset;

            for (var step = 0; step < Math.Abs(horizontalSteps); step++)
            {
                if (horizontalSteps > 0)
                    Swipe(far, centerY, near, centerY);
                else
                    Swipe(near, centerY, far, centerY);
            }
        }

        return ScrollStep.Unconfirmed;
    }));

    // Android draws edge to edge: a list can reach the gesture navigation bar, and a swipe that
    // starts next to it goes home (or back, at a side). A tenth of the extent keeps it clear.
    private static readonly By ScrollableDescendant = MobileBy.AndroidUIAutomator("new UiSelector().scrollable(true)");

    private bool CanScrollOnAndroid()
        => _element.GetAttribute("scrollable") == "true" || _element.FindElements(ScrollableDescendant).Count > 0;

    private static int SwipeInset(int extent, bool android)
        => android ? Math.Max(EdgeInset, extent / 10) : EdgeInset;

    /// <inheritdoc />
    /// <remarks>
    /// <c>UiScrollable</c> rooted at this element, or on the app element at the first scrollable
    /// container on screen. Android only; iOS answers null until it is written and run on a device.
    /// </remarks>
    /// <inheritdoc />
    /// <remarks>Android only: there a list runs under the tab bar, and UiAutomator2 leaves out a sliver's children.</remarks>
    public void RevealClearOfEdges()
    {
        if (_driver.Appium.ScrollsEdgeToEdge)
            Live(NudgeClearOfBottomEdge);
    }

    public IMauiElement? TryFindByScrolling(Locator locator)
    {
        // What was read before the scroll is wrong after it; the element found is read afresh.
        Bruinet.Core.Calls.AttemptMemo.Forget();
        return _driver.TryFindByScrollingWithin(IsApp ? null : this, locator);
    }

    #endregion

    #region Selection

    /// <summary>
    /// The list in the dialog an Android <c>Picker</c> opens, matched by id suffix.
    /// </summary>
    private static readonly Locator AndroidPickerList =
        Locator.ByXPath("//*[contains(@resource-id, ':id/select_dialog_listview')]");

    /// <summary>The rows of the picker's dialog: the children of <see cref="AndroidPickerList"/>.</summary>
    private static readonly Locator AndroidPickerItems =
        Locator.ByXPath("//*[contains(@resource-id, ':id/select_dialog_listview')]/*");

    /// <summary>How long the picker's dialog gets to show its rows.</summary>
    private const int PickerItemsTimeoutMs = 5000;

    /// <inheritdoc />
    /// <remarks>The picker's own text, which is its selected item.</remarks>
    public string? SelectedItemText => Text;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Taps the picker open and taps the row showing the text. The rows are the ones the dialog has
    /// realized, so an item far down a long list is not found until something scrolls to it: long
    /// pickers and pickers with repeated texts have no Android route yet. iOS turns the field's
    /// picker wheel instead and presses Done (not yet run on a simulator).
    /// </para>
    /// <para>
    /// Any failure after the dialog opened closes it again: a dialog left open covers the page and
    /// takes every later test down with it.
    /// </para>
    /// </remarks>
    public void SelectByText(string text) => Marked(InteractionKind.Tap, () => Live(() =>
    {
        _driver.Appium.SelectByText(this, text);
    }));

    /// <summary>Android: picks the dialog row showing <paramref name="text"/>.</summary>
    internal void SelectByTextInDialog(string text)
        => WithOpenPicker(nameof(SelectByText), items =>
        {
            var item = items.FirstOrDefault(i => i.Text == text)
                ?? throw new InvalidOperationException(
                    $"'{AutomationId}' has no item with text '{text}' among the {items.Count} its dialog shows.");

            item.Click();
        });

    /// <inheritdoc />
    /// <remarks>See <see cref="SelectByText"/>: the same route and the same caveats.</remarks>
    public void SelectIndex(int index) => Marked(InteractionKind.Tap, () => Live(() =>
    {
        _driver.Appium.SelectIndex(this, index);
    }));

    /// <summary>Android: picks the dialog row at <paramref name="index"/>.</summary>
    internal void SelectIndexInDialog(int index)
        => WithOpenPicker(nameof(SelectIndex), items =>
        {
            if (index < 0 || index >= items.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index), index, $"'{AutomationId}' shows {items.Count} item(s); there is no index {index}.");
            }

            items[index].Click();
        });

    /// <summary>
    /// Taps the picker open, hands its rows to <paramref name="choose"/>, and closes the dialog
    /// again when anything after the tap fails.
    /// </summary>
    private void WithOpenPicker(string operation, Action<IReadOnlyList<IMauiElement>> choose)
    {
        Click();

        var chosen = false;
        try
        {
            choose(_driver.FindAllChrome(AndroidPickerItems, PickerItemsTimeoutMs));
            chosen = true;
        }
        finally
        {
            if (!chosen)
            {
                TryDismissPicker();
            }
        }
    }

    /// <summary>
    /// Closes the picker's dialog without choosing, so a failed selection leaves nothing open.
    /// </summary>
    private void TryDismissPicker()
    {
        try
        {
            if (_driver.FindElements(AndroidPickerList).Count > 0)
            {
                _driver.Driver.Navigate().Back();
            }
        }
        catch (Exception error) when (!AppiumErrors.IsGone(error))
        {
            // The failure that brought us here is the one worth reporting.
        }
    }

    #endregion

    #region The app

    /// <summary>How long Shell's chrome gets to appear.</summary>
    private const int ChromeFindTimeoutMs = 5000;

    /// <inheritdoc />
    /// <remarks>
    /// Taps the drawer's opener, which Android names by its content description, and waits for the
    /// drawer's items. A no-op when the drawer is already open - which matters, because the opener
    /// renames itself while the drawer is open, so a second tap has nothing to find. iOS taps the
    /// navigation bar's flyout button (not yet run on a simulator).
    /// </remarks>
    public void OpenFlyout()
    {
        using var mark = Report(InteractionKind.Key, "Menu");

        RequireApp(nameof(OpenFlyout));
        if (IsFlyoutOpen == true)
        {
            return;
        }

        _driver.FindChrome(_driver.Appium.FlyoutOpener, ChromeFindTimeoutMs).Click();
        WaitForFlyout(open: true);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Back, which is how Android's drawer is dismissed. Its opener changes its content description
    /// once open, so it is not a handle for closing. A no-op when the drawer is shut: back there
    /// would leave the page instead. iOS has no back for it and taps the dimmed page right of the
    /// flyout.
    /// </remarks>
    public void CloseFlyout()
    {
        using var mark = Report(InteractionKind.Key, "Menu");

        RequireApp(nameof(CloseFlyout));
        if (IsFlyoutOpen != true)
        {
            return;
        }

        _driver.Appium.CloseFlyout(_driver.Driver);
        WaitForFlyout(open: false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Android: whether the drawer's items are in the tree. They leave it entirely while the drawer
    /// is shut, so their presence is the drawer's state. iOS: XCUITest keeps an off-screen view in
    /// the tree with <c>visible="false"</c>, so the drawer is open when its table is found and
    /// visible (not yet run on a simulator). Other platforms: null.
    /// </remarks>
    public bool? IsFlyoutOpen
    {
        get
        {
            RequireApp(nameof(IsFlyoutOpen));

            return _driver.Appium.IsFlyoutOpen(_driver);
        }
    }

    /// <summary>Waits for the drawer to finish opening or closing, bounded by the chrome timeout.</summary>
    private void WaitForFlyout(bool open)
    {
        if (!Bruinet.Core.Calls.Settle.Until(
                () => IsFlyoutOpen == open,
                windowMs: ChromeFindTimeoutMs,
                pollingIntervalMs: FlyoutPollIntervalMs))
        {
            throw new BruinetException(
                $"The flyout did not {(open ? "open" : "close")} within {ChromeFindTimeoutMs} ms.");
        }
    }

    /// <summary>How often the drawer's state is asked while it animates.</summary>
    private const int FlyoutPollIntervalMs = 100;

    /// <inheritdoc />
    /// <remarks>Android and iOS read the alert's own parts.</remarks>
    public AlertContents? ReadAlert()
    {
        RequireApp(nameof(ReadAlert));
        return _driver.ReadActiveAlert();
    }

    /// <inheritdoc />
    public IMauiElement? TryFindActiveDialog()
    {
        RequireApp(nameof(TryFindActiveDialog));
        return _driver.TryFindActiveDialogRoot();
    }

    /// <inheritdoc />
    /// <remarks>
    /// The real node, by the id MAUI maps an <c>AutomationId</c> to: the resource id on most
    /// controls, the accessibility id on the rest. Null where it is not in the tree.
    /// </remarks>
    public IMauiElement? TryFindDeclared(string automationId)
    {
        RequireApp(nameof(TryFindDeclared));

        if (string.IsNullOrEmpty(automationId))
            return null;

        return _driver.FindElements(Locator.ByAutomationId(automationId)).FirstOrDefault()
            ?? _driver.FindElements(Locator.ByAccessibilityId(automationId)).FirstOrDefault();
    }

    private void RequireApp(string operation)
    {
        if (!IsApp)
        {
            throw new NotSupportedException(
                $"{operation} is a question about the app, not about one element. "
                + "Ask IMauiTestContext.AppElement.");
        }
    }

    #endregion

    #region Internal

    /// <summary>
    /// Gets the underlying AppiumElement for internal use.
    /// </summary>
    internal AppiumElement Element => _element;

    #endregion
}
