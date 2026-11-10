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





    public bool EnableTracing { get; set; }

    public string? CdpEndpoint { get; set; }
}
