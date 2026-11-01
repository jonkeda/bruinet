using Bruinet.Core.Scraping;
using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.TestSite;

namespace Bruinet.Scraper.Tests.Explore;

// The test site's dangerous page, unguarded: each harmful action moves its counter, so a test of
// Explore can prove that one stayed at 0. Needs Playwright's Chromium, as the Html UI tests do.
[Trait("Category", "Browser")]
public sealed class DangerousPageTests : IAsyncLifetime
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

    private Task StartAsync(string path) => _source.StartAsync(new ScrapeStart(_site.Url(path), _profile, Visible: false));

    private async Task ClickAsync(string testId, string category, int expected)
    {
        await _source.Page!.ClickAsync($"[data-testid={testId}]");
        await WaitAsync(() => _site.Counter(category) >= expected, $"{testId} to reach /api/counter/{category}");
    }

    private static async Task WaitAsync(Func<bool> done, string what)
    {
        for (var i = 0; i < 100 && !done(); i++)
            await Task.Delay(50);
        Assert.True(done(), $"Waited for {what}.");
    }

    [Fact]
    public async Task Each_harmful_action_moves_its_counter()
    {
        await StartAsync("pages/account.html");
        Assert.All(ExploreSettings.Categories, c => Assert.Equal(0, _site.Counter(c)));

        await ClickAsync("order-delete", "delete", 1);
        await ClickAsync("row-4", "delete", 2);
        await ClickAsync("orders-tidy", "delete", 3);
        await ClickAsync("order-rename", "update", 1);
        await ClickAsync("invoice-send", "send", 1);
        await ClickAsync("invoice-pay", "pay", 1);
        await ClickAsync("orders-import", "bulk", 1);
        await _source.Page!.ClickAsync("[data-testid=order-details]");
        await ClickAsync("order-confirm", "approve", 1);
        await _source.Page.ClickAsync("[data-testid=order-close]");
        await _source.Page.FillAsync("[data-testid=profile-name]", "Ann");
        await ClickAsync("profile-save", "update", 2);
        await _source.Page.GoBackAsync();
        await ClickAsync("remove-all", "delete", 4);
        await _source.Page.GoBackAsync();
        await ClickAsync("nav-logout", "sign-out", 1);
    }

    [Fact]
    public async Task What_only_shows_more_moves_no_counter()
    {
        await StartAsync("pages/account.html");

        await _source.Page!.ClickAsync("[data-testid=tab-history]");
        await _source.Page.ClickAsync("summary");
        await _source.Page.ClickAsync("[data-testid=account-menu]");
        await _source.Page.ClickAsync("[data-testid=order-details]");
        await _source.Page.ClickAsync("[data-testid=order-close]");

        Assert.True(await _source.Page.IsVisibleAsync("[data-testid=history-table]"));
        Assert.True(await _source.Page.IsVisibleAsync("[data-testid=account-menu-items]"));
        Assert.All(ExploreSettings.Categories, c => Assert.Equal(0, _site.Counter(c)));
    }

    [Fact]
    public async Task The_account_page_s_judgements_match_the_harm_behind_them()
    {
        await StartAsync("pages/account.html");
        var snapshot = await _source.CaptureAsync();
        var judgements = ClickJudge.Judge(snapshot, new JudgeContext(new ExploreSettings(), uri => uri.Host == "localhost"));

        Verdict Of(string testId) => judgements.Single(j => snapshot[j.Node].Attribute("data-testid") == testId).Verdict;

        Assert.All(["nav-home", "nav-catalog", "tab-history", "account-menu", "order-details", "order-close"], id => Assert.Equal(Verdict.Safe, Of(id)));
        Assert.All(["nav-logout", "order-delete", "row-4", "remove-all", "invoice-send", "invoice-pay", "orders-import", "order-confirm", "profile-save"], id => Assert.Equal(Verdict.Unsafe, Of(id)));
        // Neither word nor role says what these do: the request guard is what stops them.
        Assert.All(["order-rename", "orders-tidy"], id => Assert.Equal(Verdict.Unknown, Of(id)));
    }

    [Fact]
    public async Task The_live_page_s_click_goes_over_its_websocket_which_the_guard_sees_opening_but_not_carrying()
    {
        await _source.StartAsync(new ScrapeStart(_site.Url("pages/account.html"), _profile, Visible: false));
        var guard = new RequestGuard(new ExploreSettings());
        await _source.GuardAsync(guard);

        await _source.Page!.GotoAsync(_site.Url("pages/live.html"));
        await _source.Page.WaitForSelectorAsync("text=Connected");
        await _source.Page.ClickAsync("[data-testid=live-details]");

        await WaitAsync(() => _site.SocketMessages == 1, "the websocket message");
        Assert.Equal([_site.Url("ws").Replace("http", "ws", StringComparison.Ordinal)], guard.WebSockets);
        Assert.Empty(guard.Blocked);
    }
}
