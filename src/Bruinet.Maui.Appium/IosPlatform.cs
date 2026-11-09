using Bruinet.Maui.Enums;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Bruinet.Maui.Appium;

/// <summary>iOS through XCUITest and WebDriverAgent.</summary>
internal sealed class IosPlatform : AppiumPlatform
{
    /// <summary>
    /// The names MAUI's flyout opener has carried in the navigation bar. It sits where a back button
    /// would, on a root page. Not measured yet.
    /// </summary>
    internal static readonly IReadOnlyList<string> FlyoutOpenerNames = ["OK", "Menu", "FlyoutIcon"];

    // After FlyoutOpenerNames: the instance's FlyoutOpener reads it.
    public static readonly IosPlatform Instance = new();

    private IosPlatform()
    {
    }

    public override void GoBack(AppiumDriver driver)
    {
        // A popup lives in a window of its own and neither the back button nor the edge swipe
        // reaches it. A failed test that left one open made recovery swipe the page under it, and
        // every later test in the class failed "Tab 'Projects' was not found".
        if (TryDismissPopup(driver))
            return;

        // XCUITest's back taps the navigation bar's back button and silently does nothing when the
        // app hides that bar behind its own title bar. The system's left-edge swipe is the
        // platform's other way back and does not depend on the bar.
        if (driver.FindElements(NavigationBar).Count == 0)
        {
            var size = driver.Manage().Window.Size;
            driver.ExecuteScript("mobile: dragFromToForDuration", new Dictionary<string, object>
            {
                { "duration", 0.1 },
                { "fromX", 1 },
                { "fromY", size.Height / 2 },
                { "toX", size.Width * 3 / 4 },
                { "toY", size.Height / 2 },
            });
            return;
        }

        driver.Navigate().Back();
    }

    /// <summary>
    /// True when no navigation bar starts with a back button (<see cref="IsBackButton"/>). UIKit
    /// names that button after the previous page's title or "Back", so it is known by its place,
    /// not its name.
    /// </summary>
    public override bool IsAtNavigationRoot(AppiumDriver driver)
    {
        foreach (var bar in driver.FindElements(NavigationBar))
        {
            var first = bar.FindElements(BarButton).FirstOrDefault();
            if (first is not null
                && IsBackButton(first.GetAttribute("name"), first.Location.X - bar.Location.X, bar.Size.Width))
            {
                return false;
            }
        }

        return true;
    }

    private static readonly By NavigationBar = MobileBy.ClassName("XCUIElementTypeNavigationBar");

    private static readonly By BarButton = By.XPath("./XCUIElementTypeButton");

    /// <summary>
    /// Whether a navigation bar's first button is UIKit's back button: in the bar's leading third
    /// (toolbar items sit trailing) and not the flyout opener.
    /// </summary>
    /// <param name="name">The button's <c>name</c>.</param>
    /// <param name="offsetInBar">The button's x, from the bar's left edge.</param>
    /// <param name="barWidth">The bar's width.</param>
    internal static bool IsBackButton(string? name, int offsetInBar, int barWidth)
        => offsetInBar < barWidth / 3
           && !(name is not null && FlyoutOpenerNames.Contains(name, StringComparer.Ordinal));

    /// <summary>
    /// XCUITest taps the first of the return keys the keyboard has; a MAUI Entry's Return key
    /// resigns the field. A numeric pad has no such key: MAUI gives it a toolbar above the
    /// keyboard, DoneAccessory, whose Done button does it instead.
    /// </summary>
    public override void HideKeyboard(AppiumDriver driver)
    {
        var done = driver.FindElements(MobileBy.AccessibilityId("Done")).FirstOrDefault(button => button.Displayed);
        if (done != null)
        {
            done.Click();
            return;
        }

        driver.ExecuteScript("mobile: hideKeyboard", new Dictionary<string, object>
        {
            { "keys", new[] { "return", "Return", "done", "Done" } },
        });
    }

    // DisplayActionSheet is a UIAlertController too, but with the sheet style.
    public override By ActiveDialog { get; } =
        MobileBy.IosNSPredicate("type == 'XCUIElementTypeAlert' OR type == 'XCUIElementTypeSheet'");

    /// <summary>
    /// Closes the alert, sheet or popup window on top of the app, the way a user would.
    /// </summary>
    /// <returns>Whether there was one to close.</returns>
    /// <remarks>
    /// A system alert takes its cancel action. A popup window takes its own cancel or close
    /// control when it has one, and a tap on the dimmed backdrop above it otherwise, which a
    /// Mopups popup closes on by default.
    /// </remarks>
    private bool TryDismissPopup(AppiumDriver driver)
    {
        if (driver.FindElements(ActiveDialog).Count > 0)
        {
            driver.ExecuteScript("mobile: alert", new Dictionary<string, object> { { "action", "dismiss" } });
            return true;
        }

        if (TryFindPopupWindow(driver) is not { } popup)
            return false;

        var close = popup.FindElements(CloseControl).FirstOrDefault(element => element.Displayed);
        if (close is not null)
        {
            close.Click();
            return true;
        }

        var size = driver.Manage().Window.Size;
        driver.ExecuteScript("mobile: tap", new Dictionary<string, object>
        {
            { "x", size.Width / 2 },
            { "y", size.Height / 10 },
        });
        return true;
    }

    private static readonly By CloseControl = MobileBy.IosNSPredicate(
        "name ENDSWITH '_Cancel' OR name ENDSWITH '_Close' OR name == 'Cancel' OR name == 'Close'");

    /// <summary>
    /// A popup the app draws itself: Mopups (MAUI's common popup library) puts each popup in a
    /// UIWindow of its own above the app's. iOS lists the keyboard and its input accessory as
    /// windows too, so those are skipped. Probed 2026-11-04 on iOS 27: a Core.Apps drawer was
    /// the second window, the main window read visible=false under it.
    /// </summary>
    public override AppiumElement? TryFindPopupWindow(AppiumDriver driver)
    {
        var windows = driver.FindElements(MobileBy.IosClassChain("XCUIElementTypeWindow"));
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

    // WebDriverAgent sends the facts the session asked for with each found element.
    public override bool FindsWithFacts => true;

    /// <summary>A <c>UIAlertController</c>, its title and message as static texts.</summary>
    /// <remarks>
    /// MAUI gives the cancel the cancel style, which UIKit puts first (left of two), so one button
    /// reads as the cancel with an empty accept, as on Android. Not yet run on a simulator.
    /// </remarks>
    public override AlertContents? ReadAlert(AppiumDriver driver)
    {
        var alert = driver.FindElements(By.XPath("//XCUIElementTypeAlert")).LastOrDefault();
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

    // XCUITest taps an element at its centre. When a tappable row holds its own buttons, the
    // centre can sit on one of them, and the tap runs that button instead of the row. A user
    // taps the row where it is free, so the driver does the same.
    public override bool TryTapFreePoint(AppiumMauiElement element)
    {
        var nested = element.Native.FindElements(NestedControls);
        if (nested.Count == 0)
            return false;

        var driver = element.Driver;
        var rect = driver.ElementRect(element.Native.Id);
        var covered = nested.Select(child => driver.ElementRect(child.Id))
            .Where(child => child.Width > 0 && child.Height > 0 && child != rect)
            .ToList();
        var centre = new System.Drawing.Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        if (!covered.Any(child => child.Contains(centre)))
            return false;

        foreach (var fy in FreePointRows)
        {
            foreach (var fx in FreePointColumns)
            {
                var point = new System.Drawing.Point(rect.X + (int)(rect.Width * fx), rect.Y + (int)(rect.Height * fy));
                if (covered.Any(child => child.Contains(point)))
                    continue;

                driver.Driver.ExecuteScript("mobile: tap", new Dictionary<string, object>
                {
                    ["x"] = point.X,
                    ["y"] = point.Y,
                });
                return true;
            }
        }

        return false;
    }

    private static readonly By NestedControls = MobileBy.IosNSPredicate(
        "visible == 1 AND type IN {'XCUIElementTypeButton', 'XCUIElementTypeSwitch', 'XCUIElementTypeTextField', " +
        "'XCUIElementTypeSecureTextField', 'XCUIElementTypeTextView', 'XCUIElementTypeLink', 'XCUIElementTypeSlider'}");

    private static readonly double[] FreePointRows = [0.5, 0.25, 0.75, 0.1, 0.9];
    private static readonly double[] FreePointColumns = [0.1, 0.05, 0.25, 0.5, 0.75, 0.9, 0.95];

    public override bool PublishesAppRange => false;

    public override bool ScrollsEdgeToEdge => false;

    /// <summary>
    /// XCUITest's own scroll to the element, then its scroll to visible, then one swipe down; each
    /// after the first only while the budget lasts. Not yet run on a simulator.
    /// </summary>
    public override void ScrollIntoView(AppiumMauiElement element, DateTime startTime, TimeSpan timeout)
    {
        var budgetMs = (int)Math.Max(0, (timeout - (DateTime.UtcNow - startTime)).TotalMilliseconds);

        // The keyboard covers the lower half of the screen and scrolling does not move content out
        // from under it; a user closes it first. Probed 2026-11-04: a form footer and the last
        // inputs read visible=false until the keyboard went away.
        if (element.Driver.HideKeyboardIfShown() && element.IsDisplayedNow())
        {
            return;
        }

        var id = element.Native.Id;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        AppiumMauiElement.TryInOrder(
            [
                () => element.TryScript("mobile: scrollToElement", new() { ["elementId"] = id }),
                () => element.TryScript("mobile: scroll", new() { ["elementId"] = id, ["toVisible"] = true }),
                () => element.TryScript("mobile: scroll", new() { ["direction"] = "down" }),
            ],
            budgetMs,
            () => clock.ElapsedMilliseconds);
    }

    // A MAUI Picker is a wheel in the keyboard's place.
    public override void SelectByText(AppiumMauiElement element, string text)
        => element.OnTheWheel(nameof(SelectByText), wheel => element.TurnTo(wheel, text));

    public override void SelectIndex(AppiumMauiElement element, int index)
        => element.OnTheWheel(nameof(SelectIndex), wheel => element.TurnToIndex(wheel, index));

    public override void SetDate(AppiumMauiElement element, DateTime date) => element.SetDateOnWheels(date);

    public override void SetTime(AppiumMauiElement element, TimeSpan time) => element.SetTimeOnWheels(time);

    /// <summary>
    /// The flyout button MAUI puts left in the navigation bar; its name is not measured yet, so the
    /// names it has carried are all tried.
    /// </summary>
    public override Locator FlyoutOpener { get; } = Locator.ByXPath(
        "//XCUIElementTypeNavigationBar/XCUIElementTypeButton["
        + string.Join(" or ", FlyoutOpenerNames.Select(name => $"@name='{name}'"))
        + " or @label='Menu']");

    // There is no back for the flyout: a tap on the dimmed page right of it closes it.
    public override void CloseFlyout(AppiumDriver driver)
    {
        var size = driver.Manage().Window.Size;
        driver.ExecuteScript("mobile: tap", new Dictionary<string, object>
        {
            ["x"] = size.Width - 10,
            ["y"] = size.Height / 2,
        });
    }

    // XCUITest keeps an off-screen view in the tree with visible="false", so the flyout is open
    // when its table is found and visible. Not yet run on a simulator.
    public override bool IsFlyoutOpen(AppiumMauiDriver driver)
        => driver.FindElements(driver.ShellChrome.FlyoutHost).Any(host => IsVisible(host.GetAttribute("visible")));

    /// <summary>Reads XCUITest's <c>visible</c> attribute; a missing one is not visible.</summary>
    internal static bool IsVisible(string? visible)
        => string.Equals(visible, "true", StringComparison.OrdinalIgnoreCase);

    public override IReadOnlyCollection<OpenQA.Selenium.IWebElement> Scrollers(AppiumDriver driver)
        => driver.FindElements(MobileBy.IosNSPredicate(AppiumMauiDriver.IosScrollerTypes + " AND visible == 1"));

    public override IMauiElement? FindByScrolling(AppiumMauiDriver driver, IMauiElement? container, Locator locator)
        => driver.FindByScrollingIos(container, locator);

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

    /// <summary>UIKit always puts the cancel action of a two-button alert on the left.</summary>
    public override bool ListsCancelFirstInTwoButtonAlerts => true;

    /// <summary>XCUITest reports a row's full frame, also where the list's top edge cuts it off.</summary>
    public override bool ReportsFullRowFrames => true;

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
