using Bruinet.Scraper.Snapshots;
using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.TestSite;
using Microsoft.Playwright;

namespace Bruinet.Scraper.Tests.Explore;

// Headless Chromium against the test site: needs Playwright's Chromium, as the Html UI tests do.
[Trait("Category", "Browser")]
public sealed class GuardedSourceTests : IAsyncLifetime
{
    private readonly TestSiteServer _site = new();
    private readonly PlaywrightScrapeSource _source = new();
    private readonly string _profile = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _source.DisposeAsync();
        _site.Dispose();
        if (Directory.Exists(_profile))
            Directory.Delete(_profile, recursive: true);
    }

    private Task StartAsync(string url) => _source.StartAsync(new ScrapeStart(url, _profile, Visible: false));

    // The status the page's fetch got, or -1 when it was aborted.
    private Task<int> FetchAsync(string method, string path) =>
        _source.Page!.EvaluateAsync<int>("([m, p]) => fetch(p, { method: m }).then(r => r.status, () => -1)", new[] { method, path });

    [Fact]
    public async Task Only_the_server_s_cookies_are_watched_for_a_sign_out_not_a_script_s()
    {
        await StartAsync(_site.Url("index.html"));
        await _source.Page!.Context.AddCookiesAsync(
        [
            new Cookie { Name = "session", Value = "1", Url = _site.Url(""), HttpOnly = true },
            new Cookie { Name = "_gat", Value = "1", Url = _site.Url("") },
        ]);

        Assert.Equal(["session"], await _source.CookieNamesAsync());
    }

    [Fact]
    public async Task A_post_is_blocked_by_default_and_never_reaches_the_site()
    {
        await StartAsync(_site.Url("index.html"));
        var guard = new RequestGuard(new ExploreSettings());
        await _source.GuardAsync(guard);

        var status = await FetchAsync("POST", "/api/orders/4/delete");
        var read = await FetchAsync("GET", "/api/orders");

        Assert.Equal((-1, 200), (status, read));
        Assert.Equal(0, _site.Count("POST", "/api/"));
        Assert.Equal(1, _site.Count("GET", "/api/orders"));
        var blocked = Assert.Single(guard.Blocked);
        Assert.Equal(("POST", _site.Url("api/orders/4/delete")), (blocked.Method, blocked.Url));
    }

    [Fact]
    public async Task A_post_goes_through_when_posts_are_not_blocked_or_its_endpoint_is_allowed()
    {
        await StartAsync(_site.Url("index.html"));
        await _source.GuardAsync(new RequestGuard(new ExploreSettings { BlockMethods = ["DELETE"], AllowRequests = [new("DELETE", "/api/search")] }));

        Assert.Equal(200, await FetchAsync("POST", "/api/orders"));
        Assert.Equal(200, await FetchAsync("DELETE", "/api/search/recent"));
        Assert.Equal(-1, await FetchAsync("DELETE", "/api/orders/4"));
        Assert.Equal((1, 1, 0), (_site.Count("POST", "/api/orders"), _site.Count("DELETE", "/api/search"), _site.Count("DELETE", "/api/orders")));
    }

    [Fact]
    public async Task Unguarded_everything_goes_through_again()
    {
        await StartAsync(_site.Url("index.html"));
        await _source.GuardAsync(new RequestGuard(new ExploreSettings()));

        await _source.UnguardAsync();

        Assert.Equal(200, await FetchAsync("POST", "/api/orders"));
    }

    [Fact]
    public async Task A_page_not_on_an_explore_host_is_refused()
    {
        await StartAsync(_site.OtherOriginUrl("index.html"));

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => _source.GuardAsync(new RequestGuard(new ExploreSettings { Hosts = ["localhost"] })));

        Assert.StartsWith("Explore may not run against 127.0.0.1:", refused.Message);
    }

    [Fact]
    public async Task The_page_is_not_taken_to_a_host_that_is_not_an_explore_host()
    {
        await StartAsync(_site.Url("index.html"));
        var guard = new RequestGuard(new ExploreSettings { Hosts = ["localhost"] });
        await _source.GuardAsync(guard);

        await Assert.ThrowsAsync<PlaywrightException>(() => _source.Page!.GotoAsync(_site.OtherOriginUrl("pages/catalog.html")));

        Assert.Equal(_site.Url("index.html"), _source.Page!.Url);
        Assert.Equal(0, _site.Requests.Count(r => r.Path == "/pages/catalog.html"));
        Assert.Contains("not an Explore host", Assert.Single(guard.Blocked).Reason);
    }
}
