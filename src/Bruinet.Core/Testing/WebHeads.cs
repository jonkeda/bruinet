
namespace Bruinet.Core.Testing;

/// <summary>How a web suite runs its pages (AD-030): in a browser, or rendered by bUnit.</summary>
public enum WebHead
{
    /// <summary>A real browser through Playwright: the default, and the judge at every gate.</summary>
    Browser,

    /// <summary>The app's components rendered by bUnit: no browser, no layout, no script.</summary>
    Bunit,
}

/// <summary>
/// The head this run uses, from <c>BRUINET_HEAD</c> (<c>browser</c> or <c>bunit</c>; unset is the browser).
/// One variable, so a fixture, a <c>BrowserFact</c> (<c>Bruinet.Html.Testing</c>) and the scenario runner agree. Not to be
/// confused with a UAT suite's <c>Head</c> (maui, web, mixed, service), which says which app's pages it uses.
/// </summary>
public static class WebHeads
{
    /// <summary>The environment variable that picks the head.</summary>
    public const string Variable = "BRUINET_HEAD";

    /// <summary>The head of this run.</summary>
    public static WebHead Current => Parse(Environment.GetEnvironmentVariable(Variable));

    /// <summary>Whether this run renders with bUnit.</summary>
    public static bool IsBunit => Current == WebHead.Bunit;

    /// <summary>The head a value of <see cref="Variable"/> names; anything but <c>bunit</c> is the browser.</summary>
    public static WebHead Parse(string? value)
        => string.Equals(value?.Trim(), "bunit", StringComparison.OrdinalIgnoreCase) ? WebHead.Bunit : WebHead.Browser;
}
