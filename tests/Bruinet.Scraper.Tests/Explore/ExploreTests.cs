using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;
using Bruinet.Scraper.TestSite;

namespace Bruinet.Scraper.Tests.Explore;

// Explore through the service against the test site, headless: the dangerous page's counters prove
// what it did and did not do. Needs Playwright's Chromium, as the Html UI tests do.
[Trait("Category", "Browser")]
public sealed class ExploreTests : IAsyncLifetime
{
    private readonly TestSiteServer _site = new();
    private readonly string _solution = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));
    private ScraperService _service = null!;

    public Task InitializeAsync()
    {
        _service = new ScraperService(_solution, _ => new PlaywrightScrapeSource());
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _service.DisposeAsync();
        _site.Dispose();
        if (Directory.Exists(_solution))
            Directory.Delete(_solution, recursive: true);
    }

    // A project on the test site; `explore` holds the ## Explore rows and any other sections.
    private void MakeProject(string startPath, string explore = "", string sections = "")
    {
        var folder = Path.Combine(_solution, "tests", "Shop.UITests");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName),
            $"## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | {_site.Url(startPath)} |\n\n" +
            $"## Explore\n\n| Field | Value |\n| --- | --- |\n| DelayMs | 1 |\n{explore}\n{sections}");
    }

    private Task<ExploreResult> ExploreAsync(ExploreOptions? options = null) =>
        _service.ExploreAsync(null, (options ?? new ExploreOptions()) with { Visible = false });

    private void AssertNoHarm(params string[] except)
    {
        foreach (var category in ExploreSettings.Categories.Except(except))
            Assert.True(_site.Counter(category) == 0, $"{category} was reached {_site.Counter(category)} times.");
    }

    private static string Report(ExploreResult result) => File.ReadAllText(result.Report!);

    [Fact]
    public async Task With_the_defaults_every_page_and_state_is_found_and_nothing_harmful_is_done()
    {
        MakeProject("index.html");

        var result = await ExploreAsync();

        Assert.Equal(ExploreStop.Done, result.Stop);
        Assert.True(result.WithinGuards);
        AssertNoHarm();
        Assert.Equal(0, _site.SocketMessages);
        var pages = result.Captures.Select(c => c.Page).ToHashSet();
        Assert.Superset(new HashSet<string> { "/index.html", "/pages/catalog.html", "/pages/support.html", "/pages/account.html" }, pages);
        // A link is judged by its words too: "Checkout" is a pay word, so the careful default leaves it.
        Assert.DoesNotContain("/pages/checkout.html", pages);
        Assert.Contains(result.Skipped, s => s.Label == "Checkout" && s.Reason.Contains("(pay)", StringComparison.Ordinal));
        // The account page's states: a tab, the expander, the menu, the dialog.
        var vias = result.Captures.Where(c => c.Page == "/pages/account.html").Select(c => c.Via).ToArray();
        Assert.Contains(vias, v => v.StartsWith("click 'History'", StringComparison.Ordinal));
        Assert.Contains(vias, v => v.StartsWith("click 'Account menu'", StringComparison.Ordinal));
        Assert.Contains(vias, v => v.StartsWith("click 'Order details'", StringComparison.Ordinal));
        Assert.True(vias.Any(v => v.StartsWith("click 'More about this account'", StringComparison.Ordinal)), Report(result));
        Assert.Contains(result.Skipped, s => s.Label == "Log out" && s.Reason.Contains("(sign-out)", StringComparison.Ordinal));
        Assert.Contains(result.Skipped, s => s.Label == "Opslaan" && s.Reason == "submits a form");
        Assert.Contains(result.Skipped, s => s.Label == "Tidy up" && s.Reason.Contains("ClickPolicy safe does not click it", StringComparison.Ordinal));
        Assert.True(result.Notes.Count == 0, Report(result));
        Assert.Contains("| Stopped | **Done**", Report(result));
        Assert.Equal(result.Captures.Count, _service.Project(null).Session(Path.GetFileName(result.Folder!))!.Events.Count);
    }

    [Fact]
    public async Task On_a_test_tenant_with_delete_off_and_DELETE_let_through_only_the_delete_counter_moves()
    {
        MakeProject("pages/account.html",
            "| ClickPolicy | not-unsafe |\n| BlockMethods | POST, PUT, PATCH |\n| MaxDepth | 2 |\n",
            "## Danger words\n\n| Category | On |\n| --- | --- |\n| delete | false |\n");

        var result = await ExploreAsync();

        Assert.True(result.WithinGuards, result.StopDetail);
        Assert.True(_site.Counter("delete") > 0);
        AssertNoHarm(except: "delete");
        // "Tidy up" is clicked (unknown) and its POST is stopped by the request guard; "Rename"'s PUT too.
        Assert.Contains(result.Blocked, b => b.Method == "POST" && b.Url.EndsWith("/api/counter/delete?via=tidy", StringComparison.Ordinal));
        Assert.Contains(result.Blocked, b => b.Method == "PUT" && b.Url.EndsWith("/api/counter/update", StringComparison.Ordinal));
        Assert.Contains("| Blocked requests |", Report(result).Replace("## Blocked requests", "| Blocked requests |", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_dry_run_visits_links_clicks_nothing_and_lists_what_it_would_click()
    {
        MakeProject("pages/account.html", "| MaxDepth | 1 |\n");

        var result = await ExploreAsync(new ExploreOptions { DryRun = true });

        Assert.Equal(0, result.Clicks);
        Assert.All(result.Captures, c => Assert.DoesNotContain("click", c.Via, StringComparison.Ordinal));
        Assert.Contains(result.WouldClick, w => w.Page == "/pages/account.html" && w.Label == "History" && w.Reason == "a tab");
        Assert.Contains("## Would click", Report(result));
        Assert.EndsWith("explore-dry-run", result.Folder!, StringComparison.Ordinal);
        AssertNoHarm();
    }

    [Fact]
    public async Task A_websocket_app_is_explored_by_links_only_by_default()
    {
        MakeProject("pages/live.html", "| MaxDepth | 1 |\n");

        var result = await ExploreAsync();

        Assert.Equal(0, _site.SocketMessages);
        Assert.Contains(result.Notes, n => n.Contains("websocket", StringComparison.Ordinal) && n.Contains("only links", StringComparison.Ordinal));
        Assert.Contains(result.Skipped, s => s.Label == "Show details" && s.Reason.Contains("WebsocketApps", StringComparison.Ordinal));
        Assert.Single(result.WebSockets.Distinct());
    }

    [Fact]
    public async Task A_websocket_app_set_to_ignore_is_clicked_as_any_other_and_the_guard_cannot_stop_what_it_sends()
    {
        MakeProject("pages/live.html", "| MaxDepth | 1 |\n| WebsocketApps | ignore |\n");

        var result = await ExploreAsync();

        Assert.Equal(1, _site.SocketMessages);
        Assert.Empty(result.Blocked);
    }

    [Fact]
    public async Task MaxPages_stops_it_and_says_so()
    {
        MakeProject("index.html", "| MaxPages | 2 |\n");

        var result = await ExploreAsync();

        Assert.Equal(ExploreStop.MaxPages, result.Stop);
        Assert.True(result.WithinGuards);
        Assert.Equal(2, result.Captures.Count);
        Assert.StartsWith("MaxPages (2) reached", result.StopDetail);
    }

    [Fact]
    public async Task Within_keeps_it_on_one_page_and_its_states()
    {
        MakeProject("pages/account.html", "| MaxDepth | 1 |\n| Within | /pages/account.html |\n");

        var result = await ExploreAsync();

        Assert.Equal(ExploreStop.Done, result.Stop);
        Assert.All(result.Captures, c => Assert.Equal("/pages/account.html", c.Page));
        Assert.True(result.Captures.Count > 1); // its states, a click away
        Assert.Contains(result.Skipped, s => s.Reason == "outside Within /pages/account.html (the config)");
    }

    [Fact]
    public async Task MaxDepth_leaves_what_lies_deeper()
    {
        MakeProject("index.html", "| MaxDepth | 1 |\n");

        var result = await ExploreAsync();

        Assert.Equal(ExploreStop.Done, result.Stop);
        Assert.All(result.Captures, c => Assert.True(c.Depth <= 1));
        Assert.DoesNotContain(result.Captures, c => c.Via.StartsWith("click 'History'", StringComparison.Ordinal));
        Assert.Contains("deeper than MaxDepth (1)", result.StopDetail);
    }

    [Fact]
    public async Task At_the_sign_in_page_it_stops_headless_and_waits_for_the_person_when_asked()
    {
        MakeProject("index.html", "| LoginUrl | /pages/support.html |\n");
        var waited = 0;

        var result = await ExploreAsync(new ExploreOptions { WaitForSignIn = _ => { waited++; return Task.CompletedTask; } });

        // Still the sign-in page after continuing: nobody signed in, so it stops there.
        Assert.Equal(1, waited);
        Assert.Equal(ExploreStop.SignIn, result.Stop);
        Assert.False(result.WithinGuards);
        Assert.Contains("support.html", result.StopDetail);
    }

    [Fact]
    public async Task A_start_that_is_not_an_explore_host_is_refused_before_the_browser_opens()
    {
        MakeProject("index.html", "", "## Explore hosts\n\n| Host |\n| --- |\n| 127.0.0.1 |\n");

        var result = await ExploreAsync();

        Assert.Equal(ExploreStop.HostRefused, result.Stop);
        Assert.Null(result.Folder);
        Assert.Empty(_site.Requests);
    }

    [Fact]
    public async Task Stop_ends_it_and_keeps_what_it_recorded()
    {
        MakeProject("index.html");
        var progress = new Now(p => { if (p.Captures >= 1) _service.StopExploring(); });

        var result = await ExploreAsync(new ExploreOptions { Progress = progress });

        Assert.Equal(ExploreStop.Stopped, result.Stop);
        Assert.True(result.WithinGuards);
        Assert.Single(result.Captures);
        Assert.False(_service.IsExploring);
        Assert.Null(_service.Active);
    }

    [Fact]
    public async Task Never_click_by_selector_leaves_the_elements_it_matches()
    {
        MakeProject("pages/account.html", "| MaxDepth | 1 |\n", "## Never click\n\n| Match |\n| --- |\n| selector: [role=tablist] button |\n");

        var result = await ExploreAsync();

        Assert.Contains(result.Skipped, s => s.Label == "History" && s.Reason == "never click (the config)");
        Assert.DoesNotContain(result.Captures, c => c.Via.StartsWith("click 'History'", StringComparison.Ordinal));
        Assert.Contains(result.Captures, c => c.Via.StartsWith("click 'Account menu'", StringComparison.Ordinal));
    }

    // Reports on the thread that reports, so the test can stop Explore right after a capture.
    private sealed class Now(Action<ExploreProgress> report) : IProgress<ExploreProgress>
    {
        public void Report(ExploreProgress value) => report(value);
    }
}
