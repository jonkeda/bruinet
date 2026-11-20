using Bruinet.Html.Context;
using Bruinet.Html.Interfaces;
using Bruinet.Html.Playwright;

namespace __NAMESPACE__;

// A browser for one test of a suite the Scraper's factory drafted from nothing (scrape-loop.ps1 -Factory): headless
// Chromium on the site the run started, whose address the run gives in SCRAPE_SITE_URL. A suite of its own app has
// its own sign-in and sessions instead of this.
internal static class SiteContext
{
    /// <summary>Opens a browser on the site.</summary>
    public static async Task<IHtmlTestContext> OpenAsync()
    {
        var site = Environment.GetEnvironmentVariable("SCRAPE_SITE_URL")
            ?? throw new InvalidOperationException("SCRAPE_SITE_URL is not set: the tests of this suite run against the site its run started.");
        return await PlaywrightTestContext.CreateAsync(new HtmlTestContextOptions { BaseUrl = site.TrimEnd('/'), Headless = true, BrowserType = "chromium" });
    }

    /// <summary>Closes a test's browser.</summary>
    public static async Task CloseAsync(IHtmlTestContext? context)
    {
        if (context is IAsyncDisposable disposable)
            await disposable.DisposeAsync();
    }
}
