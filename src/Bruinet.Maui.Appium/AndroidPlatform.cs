using Bruinet.Core.Locators;
using Bruinet.Maui.Enums;
using OpenQA.Selenium;

namespace Bruinet.Maui.Appium;

/// <summary>Android through UiAutomator2.</summary>
internal sealed class AndroidPlatform : AppiumPlatform
{
    public static readonly AndroidPlatform Instance = new();

    private AndroidPlatform()
    {
    }

    public override MauiPlatform Platform => MauiPlatform.Android;

    /// <summary>
    /// Differs from <see cref="AppiumPlatform.ToBy"/> only where the locator is an XPath, which is
    /// made relative: an absolute one searches the whole page from any element.
    /// </summary>
    public override By ToChildBy(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);

        return locator.Strategy switch
        {
            LocatorStrategy.Name => By.XPath("." + NamePath(locator.Value)),
            LocatorStrategy.Text => By.XPath("." + TextPath(locator.Value)),
            LocatorStrategy.ControlTypeAndName when locator.TypeName is { } typeName
                => By.XPath("." + ControlTypeAndName(typeName, locator.Value).Criteria),
            _ => ToBy(locator)
        };
    }

    // MAUI surfaces an AutomationId as the resource-id, so the id strategy.
    protected override By AutomationId(string id) => new ResourceIdBy(id);

    protected override By Id(string id) => new ResourceIdBy(id);

    /// <summary>
    /// The element's accessible name, which is its <c>content-desc</c> when it has one and its
    /// <c>text</c> otherwise - what UI Automation's Name is on Windows.
    /// </summary>
    /// <exception cref="ArgumentException">The name holds both quote characters, which no XPath 1.0 literal can.</exception>
    protected override By Name(string name) => By.XPath(NamePath(name));

    protected override By Text(string text) => By.XPath(TextPath(text));

    /// <summary>
    /// An element of the app's own with the title id first, else the text view directly in the
    /// toolbar under Shell's app bar (<c>shellcontent.appbar</c>, probed 2026-10-31 on API 36).
    /// </summary>
    protected override By ShellTitle => By.XPath(
        $"//*[substring-after(@resource-id, ':id/')='{ShellTitleId}']"
        + " | //*[contains(@resource-id, 'shellcontent.appbar')]/android.view.ViewGroup/android.widget.TextView");

    protected override By ControlTypeAndName(string controlType, string name)
    {
        var value = XPathLiteral(name, name);
        return By.XPath($"//{ControlTypeClassName(controlType)}[@content-desc={value} or @text={value}]");
    }

    protected override string? ClassNameOf(string controlType) => controlType switch
    {
        "entry" => "android.widget.EditText",
        // A MAUI Button renders as a MaterialButton, but Android reports the accessibility
        // class of its Button ancestor, which is what a class-name match sees.
        "button" => "android.widget.Button",
        // Display text and images, as Windows reads "text" (ControlType.Text) and "image". A
        // MAUI Label renders as a TextView and an Image as an ImageView.
        "text" => "android.widget.TextView",
        "image" => "android.widget.ImageView",
        // UI Automation's generic containers. A MAUI layout is a plain view.
        "group" or "pane" or "custom" => "android.view.ViewGroup",
        _ => null,
    };

    // UiAutomator2 leaves invisible elements out of its tree unless allowInvisibleElements is set.
    public override bool FoundMeansVisible => true;

    // Measured on UiAutomator2: "./*" walked the element itself.
    public override Locator ChildrenPath { get; } = Locator.ByXPath("./*/*");

    // Android's alert buttons, whichever package their ids carry.
    public override IReadOnlyList<Locator> AlertButtons { get; } =
    [
        Locator.ByXPath(".//*[contains(@resource-id, ':id/button1')]"),
        Locator.ByXPath(".//*[contains(@resource-id, ':id/button2')]"),
        Locator.ByXPath(".//*[contains(@resource-id, ':id/button3')]"),
    ];

    public override bool DrawsRatingStars => true;

    public override Locator? IndicatorDots { get; } = Locator.ByXPath("./*/*");

    public override (Locator Within, string Type)? PageControl => null;

    public override ShellChromeLocators ShellChrome { get; } = new(
        TabHost: Locator.ByXPath("//android.view.ViewGroup[android.widget.FrameLayout[@content-desc!='']]"),
        Tab: Locator.ByXPath("//android.widget.FrameLayout[@content-desc!='']"),
        FlyoutHost: Locator.ByXPath(
            "//androidx.recyclerview.widget.RecyclerView[.//android.view.ViewGroup[@content-desc!='']]"),
        FlyoutItem: Locator.ByXPath("//android.view.ViewGroup[@content-desc!='']"));

    public override By RootNode { get; } = By.XPath("/hierarchy/*[1]");

    public override bool IsTextField(string? tagName) => tagName == "android.widget.EditText";

    // UiAutomator2 answers the tag name request with null (probed 2026-09-19); the class is the type.
    public override string? ReadTagName(AppiumMauiElement element)
        => element.Driver.ElementFact(element.Native.Id, "class", () => element.GetAttribute("class"));

    // Reported fully qualified, com.example.app:id/SaveButton, where MAUI put only the AutomationId.
    // The template's id never changes for a native view (D1), so it is read once.
    public override string? ReadAutomationId(AppiumMauiElement element)
        => element.Driver.ElementFact(element.Native.Id, "resource-id",
            () => WithoutResourcePackage(AppiumMauiElement.Present(element.GetAttribute("resource-id"))));

    public override string HintAttribute => "hint";

    public override bool ReadFocused(AppiumMauiElement element)
        => AppiumMauiElement.IsTrue(element.GetAttribute("focused"));

    // The content description; an element without one is named by its text, as a screen reader does.
    public override string? ReadName(AppiumMauiElement element)
        => AppiumMauiElement.Present(element.GetAttribute("content-desc")) ?? AppiumMauiElement.Present(element.Native.Text);

    public override string ToggleStateAttribute => "checked";

    // Android reports checked="false" on every view, a plain Button included.
    public override bool CheckedNeedsCheckable => true;

    public override string DoubleTapScript => "mobile: doubleClickGesture";

    public override void Drag(AppiumMauiElement element, int fromX, int fromY, int toX, int toY)
        => Script(element, "mobile: dragGesture", new Dictionary<string, object>
        {
            { "startX", fromX }, { "startY", fromY }, { "endX", toX }, { "endY", toY },
        });

    public override void Pinch(AppiumMauiElement element, int percent, double size)
        => Script(element, percent < 0 ? "mobile: pinchCloseGesture" : "mobile: pinchOpenGesture",
            new Dictionary<string, object> { { "elementId", element.Native.Id }, { "percent", size } });

    public override void SetValue(AppiumMauiElement element, string text)
        => Script(element, "mobile: replaceElementValue", new Dictionary<string, object>
        {
            { "elementId", element.Native.Id },
            { "text", text }
        });

    public override void LongPress(AppiumMauiElement element, int durationMs)
        => Script(element, "mobile: longClickGesture", new Dictionary<string, object>
        {
            { "elementId", element.Native.Id },
            { "duration", durationMs }
        });

    public override void Swipe(AppiumMauiElement element, int startX, int startY, int endX, int endY, int durationMs)
        => Script(element, "mobile: swipeGesture", new Dictionary<string, object>
        {
            { "left", Math.Min(startX, endX) },
            { "top", Math.Min(startY, endY) },
            { "width", Math.Abs(endX - startX) + 1 },
            { "height", Math.Abs(endY - startY) + 1 },
            { "direction", SwipeDirection(endX - startX, endY - startY) },
            { "percent", 1.0 },
            { "speed", (int)(1000.0 / durationMs * 1000) }
        });

    private static string SwipeDirection(int deltaX, int deltaY)
        => Math.Abs(deltaX) > Math.Abs(deltaY)
            ? deltaX > 0 ? "right" : "left"
            : deltaY > 0 ? "down" : "up";

    private static string? WithoutResourcePackage(string? resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
            return resourceId;

        var separator = resourceId.LastIndexOf('/');
        return separator >= 0 ? resourceId[(separator + 1)..] : resourceId;
    }

    private static string NamePath(string name)
    {
        var literal = XPathLiteral(name, name);
        return $"//*[@content-desc={literal} or @text={literal}]";
    }

    private static string TextPath(string text)
        => $"//*[@text={XPathLiteral(text, text)}]";

    /// <exception cref="ArgumentException">The value holds both quote characters, which no XPath 1.0 literal can.</exception>
    private static string XPathLiteral(string value, string located)
        => !value.Contains('\'') ? $"'{value}'"
            : !value.Contains('"') ? $"\"{value}\""
            : throw new ArgumentException($"A value with both quote characters cannot be located on Android: {located}", nameof(located));

    /// <summary>A resource id, sent as the W3C <c>id</c> strategy.</summary>
    private sealed class ResourceIdBy(string id) : By("id", id);
}
