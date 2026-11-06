using Bruinet.Maui.Enums;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Bruinet.Maui.Appium;

/// <summary>iOS through XCUITest and WebDriverAgent.</summary>
internal sealed class IosPlatform : AppiumPlatform
{
    public static readonly IosPlatform Instance = new();

    private IosPlatform()
    {
    }

    public override void Relaunch(AppiumDriver driver)
    {
        // A session started from an app path has no bundleId capability; the app in front is ours.
        var bundleId = CapabilityText(driver, "bundleId")
                       ?? (driver.ExecuteScript("mobile: activeAppInfo") as IDictionary<string, object>)?["bundleId"]?.ToString();
        if (string.IsNullOrEmpty(bundleId))
        {
            return;
        }

        driver.TerminateApp(bundleId);
        var launch = new Dictionary<string, object> { ["bundleId"] = bundleId };
        if (driver.Capabilities.GetCapability("processArguments") is IDictionary<string, object> process)
        {
            if (process.TryGetValue("env", out var environment))
            {
                launch["environment"] = environment;
            }

            if (process.TryGetValue("args", out var arguments))
            {
                launch["arguments"] = arguments;
            }
        }

        driver.ExecuteScript("mobile: launchApp", launch);
    }

    public override MauiPlatform Platform => MauiPlatform.iOS;

    // MAUI surfaces an AutomationId as the accessibility identifier.
    protected override By AutomationId(string id) => MobileBy.AccessibilityId(id);

    protected override By Id(string id) => By.Id(id);

    protected override By Name(string name) => By.Name(name);

    /// <summary>Displayed text: a label's text is its <c>label</c>, an input's is its <c>value</c>.</summary>
    protected override By Text(string text)
    {
        var literal = Literal(text);
        return MobileBy.IosNSPredicate($"label == {literal} OR value == {literal}");
    }

    /// <summary>
    /// An element of the app's own with the title id first, else the navigation bar's static text.
    /// </summary>
    protected override By ShellTitle => By.XPath(
        $"//*[@name='{ShellTitleId}'] | //XCUIElementTypeNavigationBar/XCUIElementTypeStaticText");

    // A MAUI ListView renders as a table and a CollectionView as a collection view; both hold cells.
    protected override By ControlType(string controlType)
        => controlType.Equals("list", StringComparison.OrdinalIgnoreCase)
            ? MobileBy.IosNSPredicate("type == 'XCUIElementTypeTable' OR type == 'XCUIElementTypeCollectionView'")
            : base.ControlType(controlType);

    protected override By ControlTypeAndName(string controlType, string name)
    {
        var literal = Literal(name);
        return MobileBy.IosNSPredicate(
            $"type == '{ControlTypeClassName(controlType)}' AND (name == {literal} OR label == {literal})");
    }

    protected override string? ClassNameOf(string controlType) => controlType switch
    {
        "entry" => "XCUIElementTypeTextField",
        "button" => "XCUIElementTypeButton",
        "text" => "XCUIElementTypeStaticText",
        "image" => "XCUIElementTypeImage",
        "listitem" => "XCUIElementTypeCell",
        "group" or "pane" or "custom" => "XCUIElementTypeOther",
        _ => null,
    };

    private static string Literal(string value)
        => "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

    public override bool FoundMeansVisible => false;

    // XCUITest's "./*" is the children.
    public override Locator ChildrenPath { get; } = Locator.ByXPath("./*");

    public override IReadOnlyList<Locator> AlertButtons => [];

    public override bool DrawsRatingStars => false;

    public override Locator? IndicatorDots => null;

    // UIKit's UIPageControl; not yet run on a simulator.
    public override (Locator Within, string Type)? PageControl { get; } =
        (Locator.ByXPath(".//XCUIElementTypePageIndicator"), "XCUIElementTypePageIndicator");

    /// <summary>
    /// UIKit's tab bar and the table MAUI draws its flyout with. Not yet run on a simulator; a page
    /// of its own with a table may pass for the flyout.
    /// </summary>
    public override ShellChromeLocators ShellChrome { get; } = new(
        TabHost: Locator.ByXPath("//XCUIElementTypeTabBar"),
        Tab: Locator.ByXPath("//XCUIElementTypeTabBar/XCUIElementTypeButton"),
        FlyoutHost: Locator.ByXPath("//XCUIElementTypeTable[XCUIElementTypeCell]"),
        FlyoutItem: Locator.ByXPath("//XCUIElementTypeCell"));

    public override By RootNode { get; } = MobileBy.ClassName("XCUIElementTypeApplication");

    public override bool IsTextField(string? tagName)
        => tagName is "XCUIElementTypeTextField" or "XCUIElementTypeSecureTextField" or "XCUIElementTypeSearchField";

    // Every text read asks the type; it was one round trip in seven of a list scan.
    public override string? ReadTagName(AppiumMauiElement element)
        => element.Driver.ElementFact(element.Native.Id, "type", () => element.Native.TagName);

    // The accessibility identifier, which the driver reports as name. Not kept: it can follow the label.
    public override string? ReadAutomationId(AppiumMauiElement element)
        => AppiumMauiElement.Present(element.GetAttribute("name"));

    public override string HintAttribute => "placeholderValue";

    // WDA's 'focused' is focus-engine focus; keyboard focus is only visible as the active element.
    public override bool ReadFocused(AppiumMauiElement element) => element.IsActiveElement();

    // iOS reports the AutomationId as name when one is set; the accessible name is the label.
    public override string? ReadName(AppiumMauiElement element)
        => AppiumMauiElement.Present(element.GetAttribute("label")) ?? AppiumMauiElement.Present(element.GetAttribute("name"));

    public override string ToggleStateAttribute => "value";

    public override bool CheckedNeedsCheckable => false;

    public override string DoubleTapScript => "mobile: doubleTap";

    public override void Drag(AppiumMauiElement element, int fromX, int fromY, int toX, int toY)
        => Script(element, "mobile: dragFromToForDuration", new Dictionary<string, object>
        {
            { "fromX", fromX }, { "fromY", fromY }, { "toX", toX }, { "toY", toY }, { "duration", 0.5 },
        });

    public override void Pinch(AppiumMauiElement element, int percent, double size)
        => Script(element, "mobile: pinch", new Dictionary<string, object>
        {
            { "elementId", element.Native.Id },
            { "scale", percent < 0 ? 1 - size : 1 + size },
            { "velocity", percent < 0 ? -1.0 : 1.0 },
        });

    // XCUITest has no "mobile: setValue" extension; its element value endpoint is the direct
    // route. An empty value has nothing to type: callers clear first.
    public override void SetValue(AppiumMauiElement element, string text)
    {
        if (text.Length > 0)
            element.Native.SendKeys(text);
    }

    public override void LongPress(AppiumMauiElement element, int durationMs)
        => Script(element, "mobile: touchAndHold", new Dictionary<string, object>
        {
            { "elementId", element.Native.Id },
            { "duration", durationMs / 1000.0 } // iOS uses seconds
        });

    // XCUITest's "mobile: swipe" only takes a direction; this is its coordinate-based swipe.
    public override void Swipe(AppiumMauiElement element, int startX, int startY, int endX, int endY, int durationMs)
    {
        var distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));
        Script(element, "mobile: dragFromToWithVelocity", new Dictionary<string, object>
        {
            { "fromX", startX },
            { "fromY", startY },
            { "toX", endX },
            { "toY", endY },
            { "pressDuration", 0.05 },
            { "holdDuration", 0.05 },
            { "velocity", Math.Max(100, distance / Math.Max(0.05, durationMs / 1000.0)) }
        });
    }
}
