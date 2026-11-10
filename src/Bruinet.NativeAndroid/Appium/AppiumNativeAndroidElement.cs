using Bruinet.Appium;
using Bruinet.Core.Recording;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Bruinet.NativeAndroid.Appium;

/// <summary>
/// One view through UiAutomator2. Every read and action goes through <see cref="Live{T}"/>, so a
/// recreated view, an ended session and a command the server lacks each surface as their own
/// exception; nothing here waits or retries.
/// </summary>
internal sealed class AppiumNativeAndroidElement : INativeAndroidElement
{
    private readonly AppiumElement _element;
    private readonly AppiumNativeAndroidDriver _driver;

    /// <summary>Wraps <paramref name="element"/> of <paramref name="driver"/>'s session.</summary>
    public AppiumNativeAndroidElement(AppiumElement element, AppiumNativeAndroidDriver driver)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }

    /// <summary>The Appium element, for a test of this adapter.</summary>
    internal AppiumElement Element => _element;

    #region Live

    /// <summary>
    /// Runs a WebDriver read or action, reporting a recreated view as <see cref="StaleElementException"/>,
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
        // A cheap round trip that fails on a recreated view; the id alone is held client-side.
        _ = _element.Enabled;
        return _element.Id;
    });

    /// <inheritdoc />
    public string? ResourceId => Present(GetAttribute("resource-id"));

    /// <inheritdoc />
    public string? ContentDescription => Present(GetAttribute("content-desc"));

    /// <inheritdoc />
    public string? ClassName => Present(GetAttribute("class"));

    #endregion

    #region State

    /// <inheritdoc />
    public bool Visible => Live(() => _element.Displayed);

    /// <inheritdoc />
    public bool Enabled => Live(() => _element.Enabled);

    /// <inheritdoc />
    public bool Selected => Live(() => _element.Selected);

    /// <inheritdoc />
    public bool Focused => IsTrue(GetAttribute("focused"));

    /// <inheritdoc />
    public bool? Checked => IsTrue(GetAttribute("checkable")) ? IsTrue(GetAttribute("checked")) : null;

    /// <inheritdoc />
    /// <remarks>
    /// Android reports an empty text field's hint as its text. A field whose text is its hint
    /// reads as empty, which also covers a user who typed exactly the hint; no test should do that.
    /// </remarks>
    public string? Text => Live(() =>
    {
        var text = _element.Text;
        return Hint is { } hint && text == hint ? string.Empty : text;
    });

    /// <inheritdoc />
    public string? Hint => Present(GetAttribute("hint"));

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
        using var _ = Interactions.Report(InteractionKind.Tap, () => new Rectangle(point.X - 1, point.Y - 1, 3, 3), Label);
        Live(() => _driver.Raw.ExecuteScript("mobile: clickGesture", new Dictionary<string, object> { ["x"] = point.X, ["y"] = point.Y }));
    }

    /// <inheritdoc />
    public void DoubleTap()
    {
        using var _ = Report(InteractionKind.DoubleTap);
        Gesture("mobile: doubleClickGesture", []);
    }

    /// <inheritdoc />
    public void LongPress(int durationMs = 1000)
    {
        using var _ = Report(InteractionKind.LongPress, TimeSpan.FromMilliseconds(durationMs));
        Gesture("mobile: longClickGesture", new() { ["duration"] = durationMs });
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
                Gesture("mobile: replaceElementValue", new() { ["text"] = text });
                break;
            default:
                throw new RouteUnavailableException($"Text input by {method} has no route on native Android; use {TextInputMethod.Keys} or {TextInputMethod.SetValue}.");
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
        using var _ = Interactions.ReportSwipe(() => Rect, dx, dy, Label);
        Gesture("mobile: swipeGesture", new() { ["direction"] = Name(direction), ["percent"] = percent });
    }

    /// <inheritdoc />
    public bool Scroll(GestureDirection direction, double percent = 0.75)
    {
        var (dx, dy) = Vector(direction);
        using var _ = Interactions.ReportSwipe(() => Rect, dx, dy, Label);

        // scrollGesture names the way the content moves, the opposite of the finger.
        return Gesture("mobile: scrollGesture", new() { ["direction"] = Name(Opposite(direction)), ["percent"] = percent }) is true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// UiAutomator2 leaves views outside the screen out of the tree, so a view that was found is
    /// usually shown; this is for one at the edge. It scrolls the nearest scrollable with
    /// <c>UiScrollable</c> by the view's own id, description or text, once.
    /// </remarks>
    public void ScrollIntoView(int timeoutMs)
    {
        if (Visible)
        {
            return;
        }

        var self = ResourceId is { } id ? Locator.ById(id)
            : ContentDescription is { } description ? Locator.ByAccessibilityId(description)
            : Text is { Length: > 0 } text ? Locator.ByText(text)
            : null;
        if (self is not null)
        {
            _driver.TryScrollIntoView(self);
        }
    }

    #endregion

    #region Child lookup

    /// <inheritdoc />
    public INativeAndroidElement? TryFindElement(Locator locator) => FindElements(locator).FirstOrDefault();

    /// <inheritdoc />
    public IReadOnlyList<INativeAndroidElement> FindElements(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        var by = locator.ToAndroidChildBy();
        return Live(() => _element.FindElements(by).Select(child => (INativeAndroidElement)new AppiumNativeAndroidElement(child, _driver)).ToList());
    }

    #endregion

    private object? Gesture(string command, Dictionary<string, object> arguments)
    {
        arguments["elementId"] = _element.Id;
        return Live(() => _driver.Raw.ExecuteScript(command, arguments));
    }

    private Interactions.Scope Report(InteractionKind kind, TimeSpan duration = default)
        => Interactions.Report(kind, () => Rect, Label, duration: duration);

    private string? Label() => ContentDescription ?? ResourceId;

    private static string Name(GestureDirection direction) => direction.ToString().ToLowerInvariant();

    private static GestureDirection Opposite(GestureDirection direction) => direction switch
    {
        GestureDirection.Up => GestureDirection.Down,
        GestureDirection.Down => GestureDirection.Up,
        GestureDirection.Left => GestureDirection.Right,
        _ => GestureDirection.Left,
    };

    private static (int Dx, int Dy) Vector(GestureDirection direction) => direction switch
    {
        GestureDirection.Up => (0, -1),
        GestureDirection.Down => (0, 1),
        GestureDirection.Left => (-1, 0),
        _ => (1, 0),
    };

    private static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static string? Present(string? value) => string.IsNullOrEmpty(value) || value == "null" ? null : value;
}
