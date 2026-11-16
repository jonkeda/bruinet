namespace Bruinet.Scraper.Html;

/// <summary>
/// What Explore says when the browser refused a click: Playwright's first line ("Timeout 3000ms exceeded."), and the
/// reason from its call log (what covers the element, or that it is hidden, disabled or moving), which the first line
/// leaves out.
/// </summary>
internal static class ClickRefusal
{
    private static readonly string[] Reasons =
    [
        "intercepts pointer events",
        "element is not visible",
        "element is not enabled",
        "element is not stable",
        "element is outside of the viewport",
        "element was detached",
        "element is not attached",
    ];

    /// <summary>The message's first line, with the last reason its call log gives.</summary>
    /// <param name="message">The exception's message, call log included.</param>
    public static string Describe(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var parts = message.Split("Call log:", 2);
        var first = parts[0].ReplaceLineEndings(" ").Trim();
        if (parts.Length < 2)
            return first;

        var reason = parts[1]
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.TrimStart('-', ' '))
            .LastOrDefault(line => Reasons.Any(r => line.Contains(r, StringComparison.OrdinalIgnoreCase)));
        return reason is null ? first : $"{first} ({reason})";
    }

    /// <summary>
    /// Whether going from one address to another loads nothing: the same address up to its fragment, and the target
    /// has a fragment (a hash route). The browser then only moves within the document it has.
    /// </summary>
    /// <param name="current">The address the page is at.</param>
    /// <param name="target">The address asked for.</param>
    public static bool StaysInDocument(string current, string target)
    {
        if (!Uri.TryCreate(current, UriKind.Absolute, out var from) || !Uri.TryCreate(target, UriKind.Absolute, out var to))
            return false;
        return to.Fragment.Length > 0
            && string.Equals(from.GetLeftPart(UriPartial.Query), to.GetLeftPart(UriPartial.Query), StringComparison.Ordinal);
    }
}
