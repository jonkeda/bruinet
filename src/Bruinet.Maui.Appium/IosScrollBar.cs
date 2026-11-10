namespace Bruinet.Maui.Appium;

/// <summary>
/// Reads UIKit's vertical scroll indicator, which XCUITest lists as a child of its scroller:
/// <c>Vertical scroll bar, 3 pages</c> with the value <c>0%</c> at the top and <c>100%</c> at the
/// bottom. iOS has no <c>scrollable</c> attribute, so this is how a swipe knows it is at an edge.
/// </summary>
internal static class IosScrollBar
{
    internal const string NamePrefix = "Vertical scroll bar";

    /// <summary>
    /// True when a vertical step in <paramref name="verticalSteps"/>'s direction cannot move the
    /// content: it fits on one page, or is at that end. Unknown bars answer false, so the swipe happens.
    /// </summary>
    internal static bool AtEdge(string? name, string? value, int verticalSteps)
    {
        if (verticalSteps == 0 || name is null || !name.StartsWith(NamePrefix, StringComparison.Ordinal))
            return false;

        if (name.EndsWith(" 1 page", StringComparison.Ordinal))
            return true;

        return verticalSteps < 0 ? value == "0%" : value == "100%";
    }
}
