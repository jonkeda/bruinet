using Bruinet.Core.Locators;
using Bruinet.Maui.Enums;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Bruinet.Maui.Appium;

/// <summary>Android through UiAutomator2.</summary>
internal sealed class AndroidPlatform : AppiumPlatform
{
    public static readonly AndroidPlatform Instance = new();

    private AndroidPlatform()
    {
    }

    public override void GoBack(AppiumDriver driver)
    {
        // With the soft keyboard up, the system's back only hides the keyboard. A prompt left open
        // by a failed test showed it - one back hid the keyboard, the dialog stayed, and every later
        // test in the class failed "could not get back to the hub". Hiding it first makes this one
        // back mean what the caller asked for.
        if (IsKeyboardShown(driver))
        {
            driver.ExecuteScript("mobile: hideKeyboard", new Dictionary<string, object>());
        }

        driver.Navigate().Back();
    }

    /// <summary>
    /// True when the toolbar shows no "Navigate up" button. Without this the interface default
    /// answered true on every page, so <c>MauiFixture</c> never went back and every test after the
    /// first failed to reach the hub.
    /// </summary>
    public override bool IsAtNavigationRoot(AppiumDriver driver) => driver.FindElements(NavigateUp).Count == 0;

    /// <summary>The toolbar's up button, which a <c>NavigationPage</c> shows on every page above its root.</summary>
    private static readonly By NavigateUp = By.XPath("//android.widget.ImageButton[@content-desc='Navigate up']");

    public override void HideKeyboard(AppiumDriver driver)
        => driver.ExecuteScript("mobile: hideKeyboard", new Dictionary<string, object>());

    // Any package: the framework AlertDialog's root is android:id/parentPanel, but MAUI's
    // DisplayAlert is AndroidX AppCompat's, whose ids carry the app's package
    // (probed 2026-09-19 in the Todo sample: com.bruinet.samples.todo:id/parentPanel).
    // Looking for the framework id only never found a MAUI alert at all.
    public override By ActiveDialog { get; } =
        MobileBy.AndroidUIAutomator("new UiSelector().resourceIdMatches(\".*:id/parentPanel\")");

    /// <summary>
    /// The serial UiAutomator2 reports (<c>deviceUDID</c>), else the <c>udid</c> asked for.
    /// </summary>
    public override Bruinet.Core.Recording.RecordingTarget? RecordingTarget(AppiumDriver driver)
        => (driver.Capabilities.GetCapability("deviceUDID") ?? driver.Capabilities.GetCapability("udid"))?.ToString() is { Length: > 0 } serial
            ? new Bruinet.Core.Recording.RecordingTarget.AndroidDevice(serial)
            : null;

    /// <summary>
    /// A popup the app draws itself: a view added to the decor view above the app's content. Probed
    /// 2026-11-05 on API 36: a Core.Apps drawer was the decor's second child, a full-screen
    /// ViewGroup; the childless status-bar background came after it and is skipped.
    /// </summary>
    public override AppiumElement? TryFindPopupWindow(AppiumDriver driver)
        => driver.FindElements(PopupOverlay).LastOrDefault(element => element.Displayed);

    private static readonly By PopupOverlay = By.XPath("/hierarchy/*[1]/*[position()>1][*]");

    public override bool FindsWithFacts => false;

    public override bool FindsOnlyVisible(AppiumDriver driver)
        => !string.Equals(
            Convert.ToString(driver.Capabilities.GetCapability("settings[allowInvisibleElements]"),
                System.Globalization.CultureInfo.InvariantCulture),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public override double PixelsPerUnit(AppiumDriver driver)
        => driver.ExecuteScript("mobile: deviceInfo", new Dictionary<string, object>())
               is IDictionary<string, object> info
           && info.TryGetValue("displayDensity", out var dpi)
           && double.TryParse(
               Convert.ToString(dpi, System.Globalization.CultureInfo.InvariantCulture),
               System.Globalization.NumberStyles.Float,
               System.Globalization.CultureInfo.InvariantCulture,
               out var density)
           && density > 0
            ? density / 160.0
            : 1.0;

    /// <remarks>
    /// AppCompat's alert keeps its title, message and both buttons inside <c>parentPanel</c>, by
    /// ids that carry the app's package (see <see cref="ActiveDialog"/>). MAUI sets the accept as
    /// the positive button (<c>button1</c>) and the cancel as the negative one (<c>button2</c>), so
    /// an alert with one button has only <c>button2</c> and reads with an empty accept, as on Windows.
    /// </remarks>
    public override AlertContents? ReadAlert(AppiumDriver driver)
    {
        var root = driver.FindElements(ActiveDialog).LastOrDefault();
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

    public override bool PublishesAppRange => true;

    // UiAutomator2 joins the statements: anything below the element a finger can press.
    protected override By NestedControls { get; } = MobileBy.AndroidUIAutomator(
        "new UiSelector().clickable(true); new UiSelector().checkable(true)");

    protected override void TapAt(AppiumDriver driver, System.Drawing.Point point)
        => driver.ExecuteScript("mobile: clickGesture", new Dictionary<string, object>
        {
            ["x"] = point.X,
            ["y"] = point.Y,
        });

    // Android draws edge to edge: a list can reach the gesture navigation bar, and a swipe that
    // starts next to it goes home (or back, at a side).
    public override bool ScrollsEdgeToEdge => true;

    public override void ScrollIntoView(AppiumMauiElement element, DateTime startTime, TimeSpan timeout)
    {
        try
        {
            var windowSize = element.Driver.Driver.Manage().Window.Size;
            var region = new Dictionary<string, object>
            {
                { "left", 50 },
                { "top", 150 },
                { "width", windowSize.Width - 100 },
                { "height", windowSize.Height - 300 },
            };

            // Towards the element first, then back: the driver's one scroll routine, within what is
            // left of the caller's budget.
            string[] directions = element.Native.Location.Y < 0 ? ["up", "down"] : ["down", "up"];
            var budgetMs = (int)Math.Max(0, (timeout - (DateTime.UtcNow - startTime)).TotalMilliseconds);
            element.Driver.ScrollUntil(region, directions, element.IsDisplayedNow, budgetMs, atLeastOneSwipe: true,
                new System.Drawing.Rectangle(50, 150, windowSize.Width - 100, windowSize.Height - 300));
        }
        catch (WebDriverException error) when (!AppiumErrors.IsGone(error))
        {
            // Scroll attempt failed: the caller's poll sees it still not visible.
        }
    }

    // A MAUI Picker opens a dialog with one row per item.
    public override void SelectByText(AppiumMauiElement element, string text) => element.SelectByTextInDialog(text);

    public override void SelectIndex(AppiumMauiElement element, int index) => element.SelectIndexInDialog(index);

    public override void SetDate(AppiumMauiElement element, DateTime date) => element.SetDateInDialog(date);

    public override void SetTime(AppiumMauiElement element, TimeSpan time) => element.SetTimeInDialog(time);

    public override Locator FlyoutOpener { get; } = Locator.ByAccessibilityId("Open navigation drawer");

    // Back is how the drawer is dismissed. Its opener changes its content description once open,
    // so it is not a handle for closing.
    public override void CloseFlyout(AppiumDriver driver) => driver.Navigate().Back();

    // The drawer's items leave the tree entirely while it is shut, so their presence is its state.
    public override bool IsFlyoutOpen(AppiumMauiDriver driver)
        => driver.FindElements(driver.ShellChrome.FlyoutHost).Count > 0;

    public override IReadOnlyCollection<OpenQA.Selenium.IWebElement> Scrollers(AppiumDriver driver)
        => driver.FindElements(MobileBy.AndroidUIAutomator("new UiSelector().scrollable(true)"));

    public override IMauiElement? FindByScrolling(AppiumMauiDriver driver, IMauiElement? container, Locator locator)
        => driver.FindByScrollingAndroid(container, locator);

    public override string? AppId(AppiumDriver driver) => CapabilityText(driver, "appPackage");

    public override string? FrontAppId(AppiumDriver driver)
        => driver.ExecuteScript("mobile: getCurrentPackage")?.ToString();

    public override void Relaunch(AppiumDriver driver)
    {
        if (CapabilityText(driver, "appPackage") is not { } package)
        {
            return;
        }

        // A session started from an APK has no appActivity capability; read it while the app is in front.
        var activity = CapabilityText(driver, "appActivity")
                       ?? driver.ExecuteScript("mobile: getCurrentActivity")?.ToString();
        driver.TerminateApp(package);
        if (string.IsNullOrEmpty(activity))
        {
            driver.ActivateApp(package);
            return;
        }

        var start = new Dictionary<string, object>
        {
            // As component (-n): am start reads a positional intent as the target and drops the extras after it.
            ["component"] = $"{package}/{activity}",
            ["action"] = "android.intent.action.MAIN",
            ["categories"] = "android.intent.category.LAUNCHER",
            ["wait"] = true
        };
        if (LaunchExtras(CapabilityText(driver, "optionalIntentArguments")) is { Count: > 0 } extras)
        {
            start["extras"] = extras;
        }

        driver.ExecuteScript("mobile: startActivity", start);
    }

    /// <summary>
    /// The string extras of <c>optionalIntentArguments</c> as <c>mobile: startActivity</c> takes
    /// them. Bruinet writes that capability as <c>--es name value</c> pairs without whitespace.
    /// </summary>
    internal static List<string[]> LaunchExtras(string? intentArguments)
    {
        var extras = new List<string[]>();
        var parts = (intentArguments ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i + 2 < parts.Length; i++)
        {
            if (parts[i] == "--es")
            {
                extras.Add(["s", parts[i + 1], parts[i + 2]]);
                i += 2;
            }
        }

        return extras;
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
            LocatorStrategy.ControlType when IsListItem(locator.Value) => By.XPath("." + ListItemPath),
            _ => ToBy(locator)
        };
    }

    // MAUI surfaces an AutomationId as the resource-id.
    protected override By AutomationId(string id) => ResourceId(id);

    protected override By Id(string id) => ResourceId(id);

    /// <summary>
    /// A <c>UiSelector</c> where the id allows one, since it returns matches in tree order, as
    /// Windows and iOS do. The <c>id</c> strategy does not: of a search slot and the search button
    /// inside it, both <c>Search</c> (probed 2026-11-05 on API 36), it returned the button first,
    /// and the slot's text field was "not found" in it.
    /// </summary>
    private static By ResourceId(string id)
        => PlainId.IsMatch(id)
            ? MobileBy.AndroidUIAutomator($"new UiSelector().resourceIdMatches(\".*:id/{id}\")")
            : new ResourceIdBy(id);

    private static readonly System.Text.RegularExpressions.Regex PlainId = new("^[A-Za-z0-9_]+$");

    // A CollectionView renders as a RecyclerView and a ListView as a ListView.
    private const string ListClasses = "@class='androidx.recyclerview.widget.RecyclerView' or @class='android.widget.ListView'";

    /// <summary>
    /// A list's rows: its children that have content. The list may be the element searched from.
    /// A CollectionView's empty footer is a child too, but iOS has no cell for it.
    /// </summary>
    private const string ListItemPath = $"/descendant-or-self::*[{ListClasses}]/*[*]";

    private static bool IsListItem(string controlType) => controlType.Equals("listitem", StringComparison.OrdinalIgnoreCase);

    protected override By ControlType(string controlType)
        => controlType.ToLowerInvariant() switch
        {
            "list" => MobileBy.AndroidUIAutomator(
                "new UiSelector().classNameMatches(\"androidx\\\\.recyclerview\\\\.widget\\\\.RecyclerView|android\\\\.widget\\\\.ListView\")"),
            "listitem" => By.XPath(ListItemPath),
            _ => base.ControlType(controlType),
        };

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

    public override void DoubleTap(AppiumMauiElement element)
        => Script(element, "mobile: doubleClickGesture", new Dictionary<string, object> { { "elementId", element.Native.Id } });

    /// <inheritdoc />
    public override bool TypingReplaces => true;

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
