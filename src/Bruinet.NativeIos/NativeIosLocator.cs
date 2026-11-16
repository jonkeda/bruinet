using Bruinet.NativeIos.Appium;

namespace Bruinet.NativeIos;

/// <summary>
/// iOS locator helpers for XCUITest's attributes. Prefer an identifier; a predicate is fast, an XPath is slow.
/// </summary>
public static class NativeIosLocator
{
    /// <summary>The element whose accessibility identifier is <paramref name="identifier"/>.</summary>
    public static Locator ByIdentifier(string identifier) => Locator.ById(identifier);

    /// <summary>The element whose accessibility label is <paramref name="label"/>.</summary>
    public static Locator ByLabel(string label) => Locator.ByAccessibilityId(label);

    /// <summary>The element of XCUITest type <paramref name="elementType"/> (<c>XCUIElementTypeButton</c>).</summary>
    public static Locator ByType(string elementType) => Locator.ByClassName(elementType);

    /// <summary>The element whose label or value is <paramref name="text"/>.</summary>
    public static Locator ByText(string text) => Locator.ByText(text);

    /// <summary>The element whose label or value contains <paramref name="text"/>.</summary>
    public static Locator ByTextContains(string text)
    {
        var literal = NativeIosByExtensions.ToXPathLiteral(text);
        return Locator.ByXPath($"//*[contains(@label, {literal}) or contains(@value, {literal})]");
    }

    /// <summary>The element whose label or value is <paramref name="value"/>.</summary>
    public static Locator ByLabelOrValue(string value)
    {
        var literal = NativeIosByExtensions.ToXPathLiteral(value);
        return Locator.ByXPath($"//*[@label={literal} or @value={literal}]");
    }

    /// <summary>The element whose accessibility identifier ends with <paramref name="suffix"/>.</summary>
    public static Locator ByIdentifierEndsWith(string suffix)
    {
        var literal = NativeIosByExtensions.ToXPathLiteral(suffix);
        return Locator.ByXPath($"//*[substring(@name, string-length(@name) - string-length({literal}) + 1) = {literal}]");
    }

    /// <summary>An XPath over XCUITest's page source. Slow on a large screen; prefer a predicate.</summary>
    public static Locator ByIosXPath(string xpath) => Locator.ByXPath(xpath);
}
