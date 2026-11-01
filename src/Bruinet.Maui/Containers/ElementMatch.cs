namespace Bruinet.Maui.Containers;

/// <summary>
/// Answers whether an element the caller already holds matches a locator.
/// </summary>
public static class ElementMatch
{
    /// <summary>
    /// Whether <paramref name="element"/> answers to <paramref name="locator"/>.
    /// </summary>
    /// <param name="element">The element to test.</param>
    /// <param name="locator">What the caller is looking for.</param>
    /// <returns>True when the element matches.</returns>
    /// <exception cref="NotSupportedException">
    /// The locator strategy cannot be evaluated against an element in hand. Searching by it
    /// may still work; only matching an element already found is unsupported.
    /// </exception>
    public static bool Matches(IMauiElement element, Locator locator)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(locator);

        try
        {
            return locator.Strategy switch
            {
                LocatorStrategy.AutomationId or LocatorStrategy.Id or LocatorStrategy.AccessibilityId
                    => Exactly(element.AutomationId, locator.Value),
                LocatorStrategy.Text => Loosely(element.Text, locator.Value),
                LocatorStrategy.Name => Loosely(element.Name, locator.Value),
                LocatorStrategy.ControlType => MatchesControlType(element, locator.Value),
                _ => throw new RouteUnavailableException(
                    $"A '{locator.Strategy}' locator cannot be matched against an element that is " +
                    "already found. Supported: AutomationId, Id, AccessibilityId, Text, Name, ControlType.")
            };
        }
        catch (StaleElementException)
        {
            // A dead element matches nothing; the caller re-resolves and asks again.
            return false;
        }
    }

    /// <summary>
    /// Whether the element is of the given control type.
    /// </summary>
    private static bool MatchesControlType(IMauiElement element, string controlType)
    {
        var tagName = element.TagName;
        if (string.IsNullOrEmpty(tagName))
            return false;

        var lastSegment = tagName[(tagName.LastIndexOf('.') + 1)..];
        if (string.Equals(lastSegment, controlType.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        // XCUITest reports iOS types, e.g. XCUIElementTypeCell for a list item.
        var iosType = lastSegment.StartsWith("XCUIElementType", StringComparison.Ordinal)
            ? lastSegment["XCUIElementType".Length..]
            : null;
        return iosType != null && controlType.Trim().ToLowerInvariant() switch
        {
            "listitem" => iosType == "Cell",
            "list" => iosType is "Table" or "CollectionView",
            "entry" => iosType == "TextField",
            "text" => iosType == "StaticText",
            _ => string.Equals(iosType, controlType.Trim(), StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool Exactly(string? actual, string expected)
        => string.Equals(actual, expected, StringComparison.Ordinal);

    private static bool Loosely(string? actual, string expected)
        => string.Equals(actual?.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
}
