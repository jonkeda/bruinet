using Bruinet.Core.Locators;
using Bruinet.Maui.Enums;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Bruinet.Maui.Appium;

/// <summary>
/// Extension methods for converting Bruinet Locator to Appium/Selenium By selectors.
/// Internal to the Appium driver implementation.
/// </summary>
internal static class LocatorExtensions
{
    // There is deliberately no platform-less ToBy overload. One existed and defaulted to
    // MauiPlatform.Windows, so every AutomationId on Android resolved as an AccessibilityId
    // (content-desc) instead of By.Id (resource-id) - which is how MAUI actually surfaces it
    // there. The result was that a control was found only when it happened to carry a
    // content-desc, and the caller had no way to see why. The platform is always known at the
    // call site; requiring it makes the mistake unrepresentable.

    /// <summary>
    /// Converts a Bruinet Locator to an Appium/Selenium By selector for a specific platform.
    /// </summary>
    /// <param name="locator">The locator to convert.</param>
    /// <param name="platform">The target platform.</param>
    /// <returns>A By selector that can be used with Appium WebDriver.</returns>
    /// <exception cref="ArgumentNullException">Thrown when locator is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when locator strategy is not supported.</exception>
    public static By ToBy(this Locator locator, MauiPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(locator);

        return locator.Strategy switch
        {
            // MAUI surfaces AutomationId differently on each mobile platform:
            // - Android: the resource-id attribute, so the id strategy
            // - iOS: the accessibility identifier
            LocatorStrategy.AutomationId when locator.Value == ShellTitleId => ShellTitleBy(platform),
            LocatorStrategy.AutomationId => platform switch
            {
                MauiPlatform.Android => AndroidIdBy(locator.Value),
                MauiPlatform.iOS => MobileBy.AccessibilityId(locator.Value),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(platform), platform,
                    "Bruinet.Maui.Appium drives Android and iOS. Windows uses Bruinet.Maui.FlaUI.")
            },
            LocatorStrategy.AccessibilityId => MobileBy.AccessibilityId(locator.Value),
            LocatorStrategy.Id => platform == MauiPlatform.Android ? AndroidIdBy(locator.Value) : By.Id(locator.Value),
            LocatorStrategy.Name => platform == MauiPlatform.Android
                ? AndroidNameBy(locator.Value)
                : By.Name(locator.Value),
            LocatorStrategy.Text => platform == MauiPlatform.Android
                ? By.XPath(AndroidTextPath(locator.Value))
                : MobileBy.IosNSPredicate(IosTextPredicate(locator.Value)),
            // Selenium's By.ClassName becomes a CSS selector, which XCUITest rejects.
            LocatorStrategy.ClassName => MobileBy.ClassName(locator.Value),
            LocatorStrategy.XPath => By.XPath(locator.Value),
            LocatorStrategy.Css => By.CssSelector(locator.Value),
            LocatorStrategy.TagName => By.TagName(locator.Value),
            LocatorStrategy.LinkText => By.LinkText(locator.Value),
            LocatorStrategy.PartialLinkText => By.PartialLinkText(locator.Value),
            LocatorStrategy.ControlType => ToControlTypeBy(locator.Value, platform),
            LocatorStrategy.ControlTypeAndName when locator.TypeName is { } typeName
                => ToControlTypeAndNameBy(typeName, locator.Value, platform),
            _ => throw new ArgumentOutOfRangeException(
                nameof(locator),
                locator.Strategy,
                $"Locator strategy '{locator.Strategy}' is not supported for MAUI/Appium.")
        };
    }

    /// <summary>
    /// Converts a locator for a search under an element, which must stay inside that element's
    /// subtree.
    /// </summary>
    /// <remarks>
    /// Differs from <see cref="ToBy"/> only for a name on Android, whose XPath is made relative: an
    /// absolute one searches the whole page from any element.
    /// </remarks>
    /// <param name="locator">The locator to convert.</param>
    /// <param name="platform">The target platform.</param>
    /// <returns>A By selector for <c>element.FindElement</c>.</returns>
    public static By ToChildBy(this Locator locator, MauiPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(locator);

        if (platform != MauiPlatform.Android)
            return locator.ToBy(platform);

        return locator.Strategy switch
        {
            LocatorStrategy.Name => By.XPath("." + AndroidNamePath(locator.Value)),
            LocatorStrategy.Text => By.XPath("." + AndroidTextPath(locator.Value)),
            LocatorStrategy.ControlTypeAndName when locator.TypeName is { } typeName
                => By.XPath("." + ToControlTypeAndNameBy(typeName, locator.Value, platform).Criteria),
            _ => locator.ToBy(platform)
        };
    }

    /// <summary>
    /// A resource id on Android, sent as the W3C <c>id</c> strategy.
    /// </summary>
    private static By AndroidIdBy(string id) => new ResourceIdBy(id);

    /// <summary>
    /// The id a page object asks for the Shell's title by: WinUI's id for it, which MauiHeadless
    /// maps too. Android and iOS publish none, so it is the title's place in the navigation bar.
    /// </summary>
    internal const string ShellTitleId = "title";

    /// <summary>
    /// The Shell title: an element of the app's own with that id first, else the text the
    /// navigation bar shows. Android: the text view directly in the toolbar under Shell's app bar
    /// (<c>shellcontent.appbar</c>, probed 2026-10-31 on API 36); iOS: the navigation bar's static
    /// text (not yet run on a device).
    /// </summary>
    private static By ShellTitleBy(MauiPlatform platform) => platform switch
    {
        MauiPlatform.Android => By.XPath(
            $"//*[substring-after(@resource-id, ':id/')='{ShellTitleId}']"
            + " | //*[contains(@resource-id, 'shellcontent.appbar')]/android.view.ViewGroup/android.widget.TextView"),
        MauiPlatform.iOS => By.XPath(
            $"//*[@name='{ShellTitleId}'] | //XCUIElementTypeNavigationBar/XCUIElementTypeStaticText"),
        _ => throw new ArgumentOutOfRangeException(
            nameof(platform), platform,
            "Bruinet.Maui.Appium drives Android and iOS. Windows uses Bruinet.Maui.FlaUI."),
    };

    private sealed class ResourceIdBy(string id) : By("id", id);

    /// <summary>
    /// A name on Android: the element's accessible name, which is its <c>content-desc</c> when it
    /// has one and its <c>text</c> otherwise - what UI Automation's Name is on Windows.
    /// </summary>
    /// <exception cref="ArgumentException">The name holds both quote characters, which no XPath 1.0 literal can.</exception>
    private static By AndroidNameBy(string name) => By.XPath(AndroidNamePath(name));

    private static string AndroidNamePath(string name)
    {
        var literal = XPathLiteral(name, name);
        return $"//*[@content-desc={literal} or @text={literal}]";
    }

    private static string AndroidTextPath(string text)
        => $"//*[@text={XPathLiteral(text, text)}]";

    /// <summary>
    /// Displayed text on iOS: a label's text is its <c>label</c>, an input's is its <c>value</c>.
    /// </summary>
    private static string IosTextPredicate(string text)
    {
        var literal = "'" + text.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
        return $"label == {literal} OR value == {literal}";
    }

    /// <exception cref="ArgumentException">The value holds both quote characters, which no XPath 1.0 literal can.</exception>
    private static string XPathLiteral(string value, string located)
        => !value.Contains('\'') ? $"'{value}'"
            : !value.Contains('"') ? $"\"{value}\""
            : throw new ArgumentException($"A value with both quote characters cannot be located on Android: {located}", nameof(located));

    private static By ToControlTypeBy(string controlType, MauiPlatform platform)
    {
        // A MAUI ListView renders as a table and a CollectionView as a collection view; both hold cells.
        if (platform == MauiPlatform.iOS && controlType.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            return MobileBy.IosNSPredicate("type == 'XCUIElementTypeTable' OR type == 'XCUIElementTypeCollectionView'");
        }

        return MobileBy.ClassName(ControlTypeClassName(controlType, platform));
    }

    /// <summary>
    /// A control of a type with an accessible name, as Windows matches a control type and a Name:
    /// a dialog's "Ok" button rather than the text inside it.
    /// </summary>
    private static By ToControlTypeAndNameBy(string controlType, string name, MauiPlatform platform)
    {
        var className = ControlTypeClassName(controlType, platform);
        if (platform == MauiPlatform.iOS)
        {
            var literal = "'" + name.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
            return MobileBy.IosNSPredicate($"type == '{className}' AND (name == {literal} OR label == {literal})");
        }

        var value = XPathLiteral(name, name);
        return By.XPath($"//{className}[@content-desc={value} or @text={value}]");
    }

    private static string ControlTypeClassName(string controlType, MauiPlatform platform)
    {
        var className = (platform, controlType.ToLowerInvariant()) switch
        {
            (MauiPlatform.Android, "entry") => "android.widget.EditText",
            (MauiPlatform.iOS, "entry") => "XCUIElementTypeTextField",
            // A MAUI Button renders as a MaterialButton, but Android reports the accessibility
            // class of its Button ancestor, which is what a class-name match sees.
            (MauiPlatform.Android, "button") => "android.widget.Button",
            (MauiPlatform.iOS, "button") => "XCUIElementTypeButton",
            // Display text and images, as Windows reads "text" (ControlType.Text) and "image". A
            // MAUI Label renders as a TextView and an Image as an ImageView.
            (MauiPlatform.Android, "text") => "android.widget.TextView",
            (MauiPlatform.iOS, "text") => "XCUIElementTypeStaticText",
            (MauiPlatform.Android, "image") => "android.widget.ImageView",
            (MauiPlatform.iOS, "image") => "XCUIElementTypeImage",
            (MauiPlatform.iOS, "listitem") => "XCUIElementTypeCell",
            // UI Automation's generic containers. A MAUI layout is a plain view on both platforms.
            (MauiPlatform.Android, "group" or "pane" or "custom") => "android.view.ViewGroup",
            (MauiPlatform.iOS, "group" or "pane" or "custom") => "XCUIElementTypeOther",
            _ => throw new ArgumentOutOfRangeException(
                nameof(controlType), controlType,
                $"Control type '{controlType}' is not supported on {platform}.")
        };

        return className;
    }
}
