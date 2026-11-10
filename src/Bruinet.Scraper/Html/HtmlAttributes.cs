namespace Bruinet.Scraper.Html;

/// <summary>What an Html node keeps in <see cref="Bruinet.Scraper.Snapshots.ScrapeNode.Attributes"/>.</summary>
public static class HtmlAttributes
{
    /// <summary>
    /// The attributes kept by name, besides every <c>data-*</c> and <c>aria-*</c> one. <c>value</c> is never
    /// kept: what a user typed is not recorded.
    /// </summary>
    public static readonly IReadOnlyList<string> Kept =
    [
        "id", "class", "name", "type", "placeholder", "title", "alt", "for", "href",
        "disabled", "readonly", "checked", "selected", "required", "contenteditable", "tabindex", "download", "open",
    ];

    /// <summary>The test-id attributes in the order a locator prefers them, when the config names none.</summary>
    public static readonly IReadOnlyList<string> TestIds = ["data-testid", "data-test", "data-test-id", "data-qa", "data-cy"];

    /// <summary>True when an attribute of this name is kept; the capture script holds the same rule.</summary>
    public static bool IsKept(string name) =>
        (name.StartsWith("data-", StringComparison.Ordinal) && !IsVueScope(name))
        || (name.StartsWith("aria-", StringComparison.Ordinal) && name != "aria-label")
        || Kept.Contains(name);

    // Vue's scoped-style markers (data-v-1a2b3c4d) change with every build and say nothing about the element.
    private static bool IsVueScope(string name) =>
        name.Length == 15 && name.StartsWith("data-v-", StringComparison.Ordinal) && name[7..].All(char.IsAsciiHexDigitLower);
}
