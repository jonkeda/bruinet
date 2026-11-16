using Bruinet.Appium;
using Bruinet.Core.Recording;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Bruinet.NativeIos.Appium;

/// <summary>
/// One element through XCUITest. Every read and action goes through <see cref="Live{T}"/>, so a
/// removed element, an ended session and a command the server lacks each surface as their own
/// exception; nothing here waits or retries.
/// </summary>
internal sealed class AppiumNativeIosElement : INativeIosElement
{
    private const string FocusAttribute = "focused";
    private const string SwitchType = "XCUIElementTypeSwitch";
    private const string ToggleType = "XCUIElementTypeToggle";
    private const string TextFieldType = "XCUIElementTypeTextField";
    private const string SecureTextFieldType = "XCUIElementTypeSecureTextField";
    private const string TextViewType = "XCUIElementTypeTextView";
    private const string SearchFieldType = "XCUIElementTypeSearchField";
    private const string SliderType = "XCUIElementTypeSlider";

    // How long the finger rests before a drag moves, in seconds: short, so it is no long press.
    private const double DragPressSeconds = 0.05;

    private const string ContentPredicate
        = "type IN {'XCUIElementTypeCell', 'XCUIElementTypeStaticText', 'XCUIElementTypeButton', 'XCUIElementTypeImage'}";

    // How many content elements make the signature: enough to see a move, few enough to be quick.
    private const int SignatureChildren = 4;

    private readonly AppiumElement _element;
    private readonly AppiumNativeIosDriver _driver;

    /// <summary>Wraps <paramref name="element"/> of <paramref name="driver"/>'s session.</summary>
    public AppiumNativeIosElement(AppiumElement element, AppiumNativeIosDriver driver)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }

    /// <summary>The Appium element, for a test of this adapter.</summary>
    internal AppiumElement Element => _element;

    #region Live

    /// <summary>
    /// Runs a WebDriver read or action, reporting a removed element as <see cref="StaleElementException"/>,
    /// an ended session as <see cref="AppUnavailableException"/> and a command the server does not
    /// have as <see cref="RouteUnavailableException"/>.
    /// </summary>
    internal static T Live<T>(Func<T> touch)
    {
        try
        {
            return AppiumErrors.Live(touch);
        }
        catch (Exception error) when (AppiumErrors.IsUnknownCommand(error))
        {
            throw new RouteUnavailableException($"The Appium server cannot do this on this device: {error.Message}");
        }
    }

    /// <inheritdoc cref="Live{T}(Func{T})"/>
    internal static void Live(Action touch) => Live(() =>
    {
        touch();
        return true;
    });

    #endregion

    #region Identity

    /// <inheritdoc />
    /// <remarks>The WebDriver element id; read live, so it doubles as the "still there" check.</remarks>
    public string InstanceKey => Live(() =>
    {
        // A cheap round trip that fails on a removed element; the id alone is held client-side.
        _ = _element.Enabled;
        return _element.Id;
    });

    /// <inheritdoc />
    /// <remarks>XCUITest's <c>name</c> is the identifier when there is one, else the label: so <c>name</c> equal to the label is no identifier.</remarks>
    public string? Identifier => Live(() =>
    {
        var name = Present(_element.GetAttribute("name"));
        return name is null || name == Present(_element.GetAttribute("label")) ? null : name;
    });

    /// <inheritdoc />
    public string? Label => Present(GetAttribute("label"));

    /// <inheritdoc />
    public string? ElementType => Present(GetAttribute("type"));

    #endregion

    #region State

    /// <inheritdoc />
    public bool Visible => Live(() => _element.Displayed);

    /// <inheritdoc />
    public bool Enabled => Live(() => _element.Enabled);

    /// <inheritdoc />
    public bool Selected => Live(() => _element.Selected);

    /// <inheritdoc />
    public bool Focused => IsTrue(GetAttribute(FocusAttribute));

    /// <inheritdoc />
    /// <remarks>XCUITest has no checked state: a switch or toggle reports it as its value, <c>1</c> or <c>0</c>.</remarks>
    public bool? Checked => Live(() => _element.GetAttribute("type") is SwitchType or ToggleType
        ? _element.GetAttribute("value") == "1"
        : (bool?)null);

    /// <inheritdoc />
    /// <remarks>
    /// The value for a text input, else the label. iOS reports an empty text field's placeholder as
    /// its value; a field whose value is its placeholder reads as empty, which also covers a user who
    /// typed exactly the placeholder; no test should do that.
    /// </remarks>
    public string? Text => Live(() =>
    {
        var type = _element.GetAttribute("type");
        if (type is TextFieldType or SecureTextFieldType or TextViewType or SearchFieldType)
        {
            var value = Present(_element.GetAttribute("value")) ?? string.Empty;
            return Present(_element.GetAttribute("placeholderValue")) is { } placeholder && value == placeholder ? string.Empty : value;
        }

        return Present(_element.GetAttribute("value")) is { } text && type is not (SwitchType or ToggleType)
            && Present(_element.GetAttribute("label")) is null
            ? text
            : Present(_element.GetAttribute("label")) ?? string.Empty;
    });

    /// <inheritdoc />
    public string? Value => Present(GetAttribute("value"));

    /// <inheritdoc />
    public string? Placeholder => Present(GetAttribute("placeholderValue"));

    /// <inheritdoc />
    public Rectangle Rect => Live(() => new Rectangle(_element.Location, _element.Size));

    /// <inheritdoc />
    public string? GetAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Live(() => _element.GetAttribute(name));
    }

    #endregion

    #region Actions

    /// <inheritdoc />
    public void Click()
    {
        using var _ = Report(InteractionKind.Tap);
        Live(() => _element.Click());
    }

    /// <inheritdoc />
    public void TapAt(double x, double y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(x, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(y, 1);
        var bounds = Rect;
        var point = new Point(bounds.X + (int)Math.Round(bounds.Width * x), bounds.Y + (int)Math.Round(bounds.Height * y));
        using var _ = Interactions.Report(InteractionKind.Tap, () => new Rectangle(point.X - 1, point.Y - 1, 3, 3), Describe);
        Live(() => _driver.Raw.ExecuteScript("mobile: tap", new Dictionary<string, object> { ["x"] = point.X, ["y"] = point.Y }));
    }

    /// <inheritdoc />
    public void DoubleTap()
    {
        using var _ = Report(InteractionKind.DoubleTap);
        Gesture("mobile: doubleTap", []);
    }

    /// <inheritdoc />
    public void LongPress(int durationMs = 1000)
    {
        using var _ = Report(InteractionKind.LongPress, TimeSpan.FromMilliseconds(durationMs));
        Gesture("mobile: touchAndHold", new() { ["duration"] = durationMs / 1000.0 });
    }

    /// <inheritdoc />
    public void SendKeys(string text, TextInputMethod method = TextInputMethod.Keys)
    {
        ArgumentNullException.ThrowIfNull(text);
        using var _ = Report(InteractionKind.Type);
        switch (method)
        {
            case TextInputMethod.Keys:
                Live(() => _element.SendKeys(text));
                break;
            case TextInputMethod.SetValue:
                // XCUITest has no "set the value": the field is emptied, then the text is typed.
                Live(() =>
                {
                    _element.Clear();
                    _element.SendKeys(text);
                });
                break;
            default:
                throw new RouteUnavailableException($"Text input by {method} has no route on native iOS; use {TextInputMethod.Keys} or {TextInputMethod.SetValue}.");
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        using var _ = Report(InteractionKind.Clear);
        Live(() => _element.Clear());
    }

    /// <inheritdoc />
    public void Swipe(GestureDirection direction, double percent = 0.75)
    {
        var (dx, dy) = Vector(direction);
        using var _ = Interactions.ReportSwipe(() => Rect, dx, dy, Describe);
        Drag(direction, percent);
    }

    /// <inheritdoc />
    public bool Scroll(GestureDirection direction, double percent = 0.75)
    {
        var (dx, dy) = Vector(direction);
        using var _ = Interactions.ReportSwipe(() => Rect, dx, dy, Describe);

        // XCUITest's scroll answers nothing: the content moved when what is shown in it moved.
        var before = ContentSignature();
        Drag(direction, percent);
        return ContentSignature() != before;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A collection view keeps only the cells on the screen in the tree, but a scroll view keeps all of
    /// its content: an element that was found may be outside the screen. <c>mobile: scroll</c> with
    /// <c>toVisible</c> scrolls its nearest scrollable ancestor until it is, once.
    /// </remarks>
    public void ScrollIntoView(int timeoutMs)
    {
        if (Visible)
        {
            return;
        }

        Gesture("mobile: scroll", new() { ["toVisible"] = true });
    }

    /// <inheritdoc />
    public void Adjust(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using var _ = Report(InteractionKind.Type);

        // WebDriverAgent turns typing into a picker wheel or a slider into XCUITest's adjust. A slider
        // takes a fraction from 0 to 1; "50%" is read as 0.5.
        var text = ElementType == SliderType && value.EndsWith('%')
            ? (double.Parse(value.TrimEnd('%'), CultureInfo.InvariantCulture) / 100).ToString(CultureInfo.InvariantCulture)
            : value;
        Live(() => _element.SendKeys(text));
    }

    /// <inheritdoc />
    /// <remarks><c>mobile: selectPickerWheelValue</c> taps just below or above the wheel's centre.</remarks>
    public void StepWheel(bool forward)
    {
        using var _ = Report(InteractionKind.Swipe);
        Gesture("mobile: selectPickerWheelValue", new() { ["order"] = forward ? "next" : "previous", ["offset"] = 0.15 });
    }

    #endregion

    #region Child lookup

    /// <inheritdoc />
    public INativeIosElement? TryFindElement(Locator locator) => FindElements(locator).FirstOrDefault();

    /// <inheritdoc />
    public IReadOnlyList<INativeIosElement> FindElements(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        var by = locator.ToIosChildBy();
        return Live(() => _element.FindElements(by).Select(child => (INativeIosElement)new AppiumNativeIosElement(child, _driver)).ToList());
    }

    #endregion

    private object? Gesture(string command, Dictionary<string, object> arguments)
    {
        arguments["elementId"] = _element.Id;
        return Live(() => _driver.Raw.ExecuteScript(command, arguments));
    }

    private Interactions.Scope Report(InteractionKind kind, TimeSpan duration = default)
        => Interactions.Report(kind, () => Rect, Describe, duration: duration);

    private string? Describe() => Label ?? Identifier;

    // A drag across the element, centred, in the finger's direction. Not mobile: swipe, whose fling
    // ran on for up to 35 s, nor a drag by velocity, which did not always move (measured, 2026-11-16):
    // a drag of fixed length takes about 2 s and moves the content by that length.
    private void Drag(GestureDirection direction, double percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(percent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 1);
        var bounds = Rect;
        var (dx, dy) = Vector(direction);
        double cx = bounds.X + (bounds.Width / 2.0), cy = bounds.Y + (bounds.Height / 2.0);
        double hx = bounds.Width * percent / 2 * dx, hy = bounds.Height * percent / 2 * dy;
        Live(() => _driver.Raw.ExecuteScript("mobile: dragFromToForDuration", new Dictionary<string, object>
        {
            ["duration"] = DragPressSeconds,
            ["fromX"] = cx - hx,
            ["fromY"] = cy - hy,
            ["toX"] = cx + hx,
            ["toY"] = cy + hy,
        }));
    }

    // Which content elements come first, and where: changes when the content moved. By type, not
    // "visible == 1": that predicate under a collection view took 80 s (measured, 2026-11-16).
    private string ContentSignature() => Live(() => string.Join(
        ';',
        _element.FindElements(MobileBy.IosNSPredicate(ContentPredicate)).Take(SignatureChildren)
            .Select(child => child.Id + "@" + child.Location.X + "," + child.Location.Y)));

    private static (int Dx, int Dy) Vector(GestureDirection direction) => direction switch
    {
        GestureDirection.Up => (0, -1),
        GestureDirection.Down => (0, 1),
        GestureDirection.Left => (-1, 0),
        _ => (1, 0),
    };

    private static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";

    private static string? Present(string? value) => string.IsNullOrEmpty(value) || value == "null" ? null : value;
}
