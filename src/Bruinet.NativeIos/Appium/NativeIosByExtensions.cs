using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Bruinet.NativeIos.Appium;

/// <summary>How a Bruinet locator becomes an XCUITest selector.</summary>
internal static class NativeIosByExtensions
{
    /// <summary>The selector for <paramref name="locator"/>, searching the whole screen.</summary>
    /// <exception cref="LocatorNotSupportedException">A web-only strategy (CSS, link text).</exception>
    public static By ToIosBy(this Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        return locator.Strategy switch
        {
            LocatorStrategy.XPath => By.XPath(locator.Value),
            LocatorStrategy.ClassName or LocatorStrategy.TagName or LocatorStrategy.ControlType => By.ClassName(locator.Value),
            _ => locator.ToPredicate() is { } predicate
                ? MobileBy.IosNSPredicate(predicate)
                : throw new LocatorNotSupportedException(
                    locator.Strategy,
                    "Native iOS Appium",
                    "Use an accessibility identifier, label, text, element type, or an iOS predicate instead."),
        };
    }

    /// <summary>
    /// The selector for a search under an element. Unlike UiAutomator2, XCUITest evaluates every
    /// strategy in the element's own subtree: <c>//X</c> is a descendant, <c>./*</c> a child and
    /// <c>.</c> the element (measured on the iOS 27 simulator, 2026-11-16). So no rewrite is needed.
    /// </summary>
    public static By ToIosChildBy(this Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        return locator.ToIosBy();
    }

    /// <summary>
    /// The locator as an <c>NSPredicate</c> over XCUITest's attributes, for a lookup or
    /// <c>mobile: scroll</c>; null for a locator a predicate cannot express (an XPath, CSS).
    /// </summary>
    public static string? ToPredicate(this Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        var value = ToPredicateLiteral(locator.Value);
        return locator.Strategy switch
        {
            // XCUITest's predicates know the identifier as "name"; "identifier" matches nothing
            // (measured on the iOS 27 simulator, 2026-11-16).
            LocatorStrategy.AutomationId or LocatorStrategy.Id => $"name == {value}",
            LocatorStrategy.AccessibilityId => $"label == {value}",
            LocatorStrategy.Name or LocatorStrategy.Text => $"label == {value} OR value == {value}",
            LocatorStrategy.ClassName or LocatorStrategy.TagName or LocatorStrategy.ControlType => $"type == {value}",
            LocatorStrategy.DataTestId or LocatorStrategy.DataAutomationId => $"name == {value} OR label == {value}",
            _ => null,
        };
    }

    /// <summary><paramref name="value"/> as a quoted NSPredicate string.</summary>
    public static string ToPredicateLiteral(string value)
        => "'" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal) + "'";

    /// <summary><paramref name="value"/> as an XPath string literal.</summary>
    public static string ToXPathLiteral(string value)
    {
        if (!value.Contains('\''))
        {
            return $"'{value}'";
        }

        if (!value.Contains('"'))
        {
            return $"\"{value}\"";
        }

        var parts = value.Split('\'');
        return "concat(" + string.Join(", \"'\", ", parts.Select(part => $"'{part}'")) + ")";
    }
}
