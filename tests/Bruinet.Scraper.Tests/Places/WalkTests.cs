using Bruinet.Scraper.Html;
using Bruinet.Scraper.Places;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;
using Bruinet.Scraper.TestSite;

namespace Bruinet.Scraper.Tests.Places;

// The walk through the service against the test site, headless: the dangerous page's counters prove what reached the
// server. Needs Playwright's Chromium, as the Html UI tests do.
[Trait("Category", "Browser")]
public sealed class WalkTests : IAsyncLifetime
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

    private string ListFile => Path.Combine(_solution, PlacesList.FileName);

    // A project on the test site that is an acceptance environment: what deletes, updates, approves and imports may be
    // pressed; what sends, pays and signs out may not.
    private void MakeProject(string environment)
    {
        var folder = Path.Combine(_solution, "tests", "Shop.UITests");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName),
            $"## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | {_site.Url("index.html")} |\n| Environment | {environment} |\n\n" +
            "## Explore\n\n| Field | Value |\n| --- | --- |\n| DelayMs | 1 |\n| BlockMethods | |\n| WebsocketApps | ignore |\n\n" +
            "## Danger words\n\n| Category | On |\n| --- | --- |\n| delete | false |\n| update | false |\n| approve | false |\n| bulk | false |\n");
    }

    private async Task<(WalkResult Result, PlacesList List)> WalkAsync(WalkOptions? options = null, string page = "/pages/account.html")
    {
        var list = new PlacesList();
        list.Add(PlacesList.Known(page, PlacesSeeds.Map));
        list.Save(ListFile);
        var result = await _service.WalkPlacesAsync(null, ListFile, (options ?? new WalkOptions()) with { MaxPlaces = 1, MaxPresses = 40 }, account: null, visible: false);
        return (result, PlacesList.Load(ListFile));
    }

    [Fact]
    public async Task On_acceptance_every_button_is_pressed_and_no_write_reaches_a_record_that_is_not_the_walk_s()
    {
        MakeProject("acceptance");

        var (result, list) = await WalkAsync();

        // Nothing that changes something reached the server, though Delete, Rename, Confirm and Import were pressed.
        foreach (var category in ExploreSettings.Categories)
            Assert.True(_site.Counter(category) == 0, $"{category} was reached {_site.Counter(category)} times.");
        Assert.True(result.Held >= 4, $"{result.Held} writes were held back.");

        // What the page leads to: a tab, a menu, a dialog, and buttons that only call the back end.
        var account = list["/pages/account.html"]!;
        Assert.Equal(PlaceState.Seen, account.State);
        Assert.Equal(PlaceKind.Tab, list["/pages/account.html › History"]!.Kind);
        Assert.Equal(PlaceKind.Menu, list["/pages/account.html › Account menu"]!.Kind);
        Assert.Equal(PlaceKind.Dialog, list["/pages/account.html › Order details"]!.Kind);
        var delete = list["/pages/account.html › Delete"]!;
        Assert.Equal(PlaceKind.Action, delete.Kind);
        Assert.Contains("DELETE /api/counter/delete (held)", delete.Calls);
        Assert.Contains("PUT /api/counter/update (held)", list["/pages/account.html › Rename"]!.Calls);

        // What sends, pays or signs out is not pressed, and the list says so.
        Assert.Contains(account.NotPressed, not => not.StartsWith("Send invoice", StringComparison.Ordinal));
        Assert.Contains(account.NotPressed, not => not.StartsWith("Pay now", StringComparison.Ordinal));
        // The links of the page are on the frontier, not followed by a press.
        Assert.NotNull(list["/pages/catalog.html"]);
        Assert.True(list["/pages/catalog.html"]!.OnFrontier);
    }

    [Fact]
    public async Task On_a_record_the_walk_owns_a_write_goes_through()
    {
        MakeProject("acceptance");

        var (_, list) = await WalkAsync(new WalkOptions { Own = ["/pages/account.html"] });

        Assert.True(_site.Counter("delete") > 0, "Delete did not reach the server on the walk's own record.");
        Assert.True(_site.Counter("update") > 0, "Rename did not reach the server on the walk's own record.");
        // What reaches outside the environment stays off, whoever owns the record.
        Assert.Equal(0, _site.Counter("send"));
        Assert.Equal(0, _site.Counter("pay"));
        Assert.Equal(0, _site.Counter("sign-out"));
        Assert.Contains("DELETE /api/counter/delete", list["/pages/account.html › Delete"]!.Calls);
    }

    // A UI library draws a dialog where it likes in the document: its Delete is not the page's Delete (friction 131).
    [Fact]
    public async Task A_dialog_s_button_of_the_page_s_name_is_pressed_too()
    {
        MakeProject("acceptance");

        var (_, list) = await WalkAsync(new WalkOptions { Own = ["/pages/record.html"] }, "/pages/record.html");

        Assert.Equal(PlaceKind.Dialog, list["/pages/record.html › Delete"]!.Kind);
        Assert.True(_site.Counter("delete") > 0, "The yes of the question was not pressed.");
    }

    [Fact]
    public async Task Each_place_is_kept_as_a_capture_the_recordings_tools_read()
    {
        MakeProject("acceptance");

        var (_, list) = await WalkAsync();

        var project = _service.Projects().Single();
        var session = Assert.Single(project.Sessions());
        var page = list["/pages/account.html"]!;
        var dialog = list["/pages/account.html › Order details"]!;
        Assert.StartsWith(session.Id + "/", page.Snapshot, StringComparison.Ordinal);
        Assert.NotEqual(page.Snapshot, dialog.Snapshot);
        Assert.True(File.Exists(Path.Combine(project.RecordingsFolder, dialog.Snapshot!)), dialog.Snapshot);
        // The page as it opened has no name, so a draft is made from it; a part's capture is named after what opened it.
        Assert.Contains(session.Events, e => e.Name is null && e.Snapshot == page.Snapshot![(session.Id.Length + 1)..]);
        Assert.Contains(session.Events, e => e.Name == "Order details");
    }

    [Fact]
    public async Task On_production_only_what_is_known_to_show_more_is_pressed()
    {
        MakeProject("production");

        var (result, list) = await WalkAsync();

        foreach (var category in ExploreSettings.Categories)
            Assert.Equal(0, _site.Counter(category));
        Assert.Equal(0, result.Held);
        Assert.NotNull(list["/pages/account.html › History"]);
        Assert.NotNull(list["/pages/account.html › Order details"]);
        Assert.Null(list["/pages/account.html › Delete"]);
        Assert.Contains(list["/pages/account.html"]!.NotPressed, not => not.StartsWith("Delete", StringComparison.Ordinal));
    }

    [Fact]
    public async Task With_links_and_tabs_only_no_button_is_pressed()
    {
        MakeProject("acceptance");

        var (result, list) = await WalkAsync(new WalkOptions { LinksAndTabs = true, HoldAll = true });

        Assert.Equal(0, result.Held);
        Assert.NotNull(list["/pages/account.html › History"]);
        Assert.Null(list["/pages/account.html › Order details"]);
        Assert.Null(list["/pages/account.html › Delete"]);
    }
}
