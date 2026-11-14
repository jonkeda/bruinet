using Bruinet.Core.Calls;
using Bruinet.Core.Configuration;
using Bruinet.Core.Logging;

namespace Bruinet.Html.Context;

public class HtmlTestContextOptions : Bruinet.Core.Configuration.TestContextOptionsBase
{
    public string? BaseUrl { get; set; }

    public bool Headless { get; set; } = true;

    public string BrowserType { get; set; } = "chromium";

    /// <summary>
    /// One browser per process for every context of the same type and headless mode, each context its own browser context
    /// (its own cookies, storage and cache), as a browser's windows in private mode are. A launch costs about a second, so a
    /// suite that makes a context per test paid it per test (next-plan item 8). False launches a browser of its own; a
    /// window someone watches (<c>BRUINET_AUT_WINDOW=shown</c>) always has its own.
    /// </summary>
    public bool ShareBrowser { get; set; } = true;

    /// <summary>
    /// A browser's saved sign-in (cookies and storage) to start the context from, as written by
    /// <c>SaveStorageState</c>; ignored when the file does not exist. Browser heads only.
    /// </summary>
    public string? StorageStatePath { get; set; }

    /// <summary>The browser's language, such as <c>en-US</c>, or null for the browser's own. Browser heads only.</summary>
    public string? Locale { get; set; }

    /// <summary>The page's width in CSS pixels, or null for the browser's default. Browser heads only.</summary>
    public int? ViewportWidth { get; set; }

    /// <summary>The page's height in CSS pixels, or null for the browser's default. Browser heads only.</summary>
    public int? ViewportHeight { get; set; }

    /// <summary>
    /// The hosts the page may navigate to, or null for any. A navigation of the page to another host is
    /// refused and listed in the context's <c>RefusedNavigations</c>; other hosts' assets still load. Browser heads only.
    /// </summary>
    public IReadOnlyList<string>? AllowedHosts { get; set; }

    public bool EnableTracing { get; set; }

    public string? CdpEndpoint { get; set; }
}
