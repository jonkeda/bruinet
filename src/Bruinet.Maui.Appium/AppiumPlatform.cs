using Bruinet.Core.Locators;
using Bruinet.Maui.Enums;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Bruinet.Maui.Appium;

/// <summary>
/// What the Appium driver does differently on Android and iOS. The driver picks one platform
/// when it is created, so the rest of the code asks the platform instead of branching on
/// <see cref="MauiPlatform"/>.
/// </summary>
internal abstract class AppiumPlatform
{
    public static AppiumPlatform For(MauiPlatform platform) => platform switch
    {
        MauiPlatform.Android => AndroidPlatform.Instance,
        MauiPlatform.iOS => IosPlatform.Instance,
        _ => throw new ArgumentOutOfRangeException(
            nameof(platform), platform,
            "Bruinet.Maui.Appium drives Android and iOS. Windows uses Bruinet.Maui.FlaUI."),
    };

    public abstract MauiPlatform Platform { get; }

    /// <summary>
    /// The id a page object asks for the Shell's title by: WinUI's id for it, which MauiHeadless
    /// maps too. Android and iOS publish none, so it is the title's place in the navigation bar.
    /// </summary>
    public const string ShellTitleId = "title";

    public By ToBy(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);

        return locator.Strategy switch
        {
            LocatorStrategy.AutomationId when locator.Value == ShellTitleId => ShellTitle,
            LocatorStrategy.AutomationId => AutomationId(locator.Value),
            LocatorStrategy.AccessibilityId => MobileBy.AccessibilityId(locator.Value),
            LocatorStrategy.Id => Id(locator.Value),
            LocatorStrategy.Name => Name(locator.Value),
            LocatorStrategy.Text => Text(locator.Value),
            // Selenium's By.ClassName becomes a CSS selector, which XCUITest rejects.
            LocatorStrategy.ClassName => MobileBy.ClassName(locator.Value),
            LocatorStrategy.XPath => By.XPath(locator.Value),
            LocatorStrategy.Css => By.CssSelector(locator.Value),
            LocatorStrategy.TagName => By.TagName(locator.Value),
            LocatorStrategy.LinkText => By.LinkText(locator.Value),
            LocatorStrategy.PartialLinkText => By.PartialLinkText(locator.Value),
            LocatorStrategy.ControlType => ControlType(locator.Value),
            LocatorStrategy.ControlTypeAndName when locator.TypeName is { } typeName
                => ControlTypeAndName(typeName, locator.Value),
            _ => throw new ArgumentOutOfRangeException(
                nameof(locator),
                locator.Strategy,
                $"Locator strategy '{locator.Strategy}' is not supported for MAUI/Appium.")
        };
    }

    /// <summary>
    /// A locator for a search under an element, which must stay inside that element's subtree.
    /// </summary>
    public virtual By ToChildBy(Locator locator) => ToBy(locator);

    /// <summary>How MAUI surfaces an AutomationId on this platform.</summary>
    protected abstract By AutomationId(string id);

    protected abstract By Id(string id);

    protected abstract By Name(string name);

    protected abstract By Text(string text);

    protected abstract By ShellTitle { get; }

    protected virtual By ControlType(string controlType)
        => MobileBy.ClassName(ControlTypeClassName(controlType));

    /// <summary>
    /// A control of a type with an accessible name, as Windows matches a control type and a Name:
    /// a dialog's "Ok" button rather than the text inside it.
    /// </summary>
    protected abstract By ControlTypeAndName(string controlType, string name);

    protected abstract string? ClassNameOf(string controlType);

    protected string ControlTypeClassName(string controlType)
        => ClassNameOf(controlType.ToLowerInvariant())
            ?? throw new ArgumentOutOfRangeException(
                nameof(controlType), controlType,
                $"Control type '{controlType}' is not supported on {Platform}.");

    /// <summary>Whether an element the session finds is necessarily on screen.</summary>
    public abstract bool FoundMeansVisible { get; }

    /// <summary>The XPath that walks an element's children.</summary>
    public abstract Locator ChildrenPath { get; }

    public abstract IReadOnlyList<Locator> AlertButtons { get; }

    public abstract bool DrawsRatingStars { get; }

    public abstract Locator? IndicatorDots { get; }

    public abstract (Locator Within, string Type)? PageControl { get; }

    public virtual bool ListsCancelFirstInTwoButtonAlerts => false;

    public virtual bool ReportsFullRowFrames => false;

    public abstract ShellChromeLocators ShellChrome { get; }

    /// <summary>The hierarchy's root node.</summary>
    public abstract By RootNode { get; }

    /// <summary>Whether an element of this native type is a text input, whose placeholder reads as its text.</summary>
    public abstract bool IsTextField(string? tagName);

    /// <summary>The element's native type, read once per element id.</summary>
    public abstract string? ReadTagName(AppiumMauiElement element);

    public abstract string? ReadAutomationId(AppiumMauiElement element);

    /// <summary>The attribute holding an input's placeholder.</summary>
    public abstract string HintAttribute { get; }

    public abstract bool ReadFocused(AppiumMauiElement element);

    /// <summary>The accessible name, what UI Automation's Name is on Windows.</summary>
    public abstract string? ReadName(AppiumMauiElement element);

    /// <summary>The attribute carrying checked state.</summary>
    public abstract string ToggleStateAttribute { get; }

    /// <summary>
    /// Whether the checked state counts only on an element that reports itself checkable.
    /// </summary>
    public abstract bool CheckedNeedsCheckable { get; }

    /// <summary>
    /// Stops the app and starts it again with the launch settings its session started it with,
    /// which a plain <c>activateApp</c> leaves out. Does nothing when the session names no app.
    /// </summary>
    public abstract void Relaunch(AppiumDriver driver);

    /// <summary>One step back, the way a user of the platform would take it.</summary>
    public abstract void GoBack(AppiumDriver driver);

    /// <summary>Whether the page on screen offers no way back.</summary>
    public abstract bool IsAtNavigationRoot(AppiumDriver driver);

    /// <summary>Closes the soft keyboard, which the caller has seen is up.</summary>
    public abstract void HideKeyboard(AppiumDriver driver);

    /// <summary>Whether the soft keyboard is up.</summary>
    public static bool IsKeyboardShown(AppiumDriver driver)
        => driver.ExecuteScript("mobile: isKeyboardShown", new Dictionary<string, object>()) is true;

    /// <summary>The root of a platform alert or sheet.</summary>
    public abstract By ActiveDialog { get; }

    /// <summary>A popup the app draws in a window of its own, or null.</summary>
    public virtual AppiumElement? TryFindPopupWindow(AppiumDriver driver) => null;

    /// <summary>The device to record the screen of, or null when it cannot be told.</summary>
    public virtual Bruinet.Core.Recording.RecordingTarget? RecordingTarget(AppiumDriver driver) => null;

    /// <summary>Whether a find asks the driver to send each element's facts with it.</summary>
    public abstract bool FindsWithFacts { get; }

    /// <summary>Whether a find leaves out elements that are not on screen.</summary>
    public virtual bool FindsOnlyVisible(AppiumDriver driver) => false;

    /// <summary>Screen pixels per device-independent unit.</summary>
    public virtual double PixelsPerUnit(AppiumDriver driver) => 1.0;

    /// <summary>The alert on screen as MAUI was asked for it, or null.</summary>
    public abstract AlertContents? ReadAlert(AppiumDriver driver);

    /// <summary>
    /// Taps the element at a point none of its own controls covers, when its centre is covered.
    /// </summary>
    /// <returns>Whether it tapped; false leaves the plain tap to the caller.</returns>
    public virtual bool TryTapFreePoint(AppiumMauiElement element) => false;

    /// <summary>Whether AppSupport publishes a range control's bounds in app units here.</summary>
    public abstract bool PublishesAppRange { get; }

    /// <summary>
    /// Whether the app draws edge to edge, so a swipe keeps clear of the system gesture bar and
    /// first checks that something can scroll.
    /// </summary>
    public abstract bool ScrollsEdgeToEdge { get; }

    /// <summary>Scrolls until the element is shown, within what is left of the timeout.</summary>
    public abstract void ScrollIntoView(AppiumMauiElement element, DateTime startTime, TimeSpan timeout);

    /// <summary>Picks a picker's item by its text.</summary>
    public abstract void SelectByText(AppiumMauiElement element, string text);

    /// <summary>Picks a picker's item by its position.</summary>
    public abstract void SelectIndex(AppiumMauiElement element, int index);

    /// <summary>Sets a date picker.</summary>
    public abstract void SetDate(AppiumMauiElement element, DateTime date);

    /// <summary>Sets a time picker.</summary>
    public abstract void SetTime(AppiumMauiElement element, TimeSpan time);

    /// <summary>The Shell flyout's opener.</summary>
    public abstract Locator FlyoutOpener { get; }

    /// <summary>Dismisses the open Shell flyout.</summary>
    public abstract void CloseFlyout(AppiumDriver driver);

    /// <summary>Whether the Shell flyout is open.</summary>
    public abstract bool IsFlyoutOpen(AppiumMauiDriver driver);

    /// <summary>Finds an element by scrolling a container until it enters the tree.</summary>
    public abstract IMauiElement? FindByScrolling(AppiumMauiDriver driver, IMauiElement? container, Locator locator);

    /// <summary>A session capability as text, or null.</summary>
    protected static string? CapabilityText(AppiumDriver driver, string name)
        => driver.Capabilities.GetCapability(name)?.ToString() is { Length: > 0 } text ? text : null;

    /// <summary>The script that double-taps an element.</summary>
    public abstract string DoubleTapScript { get; }

    /// <summary>A drag between two screen points, in pixels.</summary>
    public abstract void Drag(AppiumMauiElement element, int fromX, int fromY, int toX, int toY);

    /// <summary>A pinch on the element: <paramref name="size"/> is the fraction, negative <paramref name="percent"/> closes.</summary>
    public abstract void Pinch(AppiumMauiElement element, int percent, double size);

    /// <summary>Sets an element's value directly, bypassing the keyboard.</summary>
    public abstract void SetValue(AppiumMauiElement element, string text);

    public abstract void LongPress(AppiumMauiElement element, int durationMs);

    /// <summary>A swipe between two screen points, in pixels, taking about <paramref name="durationMs"/>.</summary>
    public abstract void Swipe(AppiumMauiElement element, int startX, int startY, int endX, int endY, int durationMs);

    protected static void Script(AppiumMauiElement element, string command, Dictionary<string, object> arguments)
        => element.Driver.Driver.ExecuteScript(command, arguments);
}
