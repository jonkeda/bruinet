using Bruinet.Core.Scraping;
using Bruinet.Scraper.Code;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;

namespace Bruinet.Scraper.Tests.Code;

public sealed class WorkflowTests : IAsyncDisposable
{
    private readonly string _solution = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));
    private readonly string _folder;
    private readonly ScraperService _service;

    public WorkflowTests()
    {
        _folder = Path.Combine(_solution, "Shop.UITests");
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(_folder, ScraperConfig.FileName), "## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | https://shop.example/menu |\n| Namespace | Shop.UITests.Pages |\n");
        _service = new ScraperService(_solution);
    }

    public async ValueTask DisposeAsync()
    {
        await _service.DisposeAsync();
        if (Directory.Exists(_solution))
            Directory.Delete(_solution, recursive: true);
    }

    private static ScrapeNode N(int n, int p, string type, string? text = null, params (string Key, string Value)[] attributes) =>
        new() { Number = n, Parent = p, Type = type, Text = text, Bounds = new(0, 0, 10, 10), Attributes = attributes.ToDictionary(a => a.Key, a => a.Value) };

    // A menu of three groups, each a header and a list of links; the third group's header is a span.title, not a span.header.
    private static ScrapeSnapshot Menu() => new()
    {
        Platform = "html",
        Location = "https://shop.example/menu",
        Title = "Menu",
        CapturedAt = DateTimeOffset.UnixEpoch,
        Nodes =
        [
            N(1, 0, "body"),
            N(2, 1, "div", null, ("class", "group")), N(3, 2, "span", "Orders", ("class", "header")), N(4, 2, "ul"), N(5, 4, "li"), N(6, 5, "a", "Open orders", ("href", "/orders")), N(7, 4, "li"), N(8, 7, "a", "Archive", ("href", "/archive")),
            N(9, 1, "div", null, ("class", "group")), N(10, 9, "span", "Customers", ("class", "header")), N(11, 9, "ul"), N(12, 11, "li"), N(13, 12, "a", "All customers", ("href", "/customers")), N(14, 11, "li"), N(15, 14, "a", "Groups", ("href", "/groups")),
            N(16, 1, "div", null, ("class", "group")), N(17, 16, "span", "Reports", ("class", "title")), N(18, 16, "ul"), N(19, 18, "li"), N(20, 19, "a", "Sales", ("href", "/sales")), N(21, 18, "li"), N(22, 21, "a", "Stock", ("href", "/stock")),
        ],
    };

    private void Record(ScrapeSnapshot snapshot) =>
        ScrapeProject.Open(_folder).StartSession("explore", DateTimeOffset.UnixEpoch).AddPage(snapshot);

    [Theory]
    [InlineData("span.header", new[] { 3, 10 })]
    [InlineData("ul > li > a", new[] { 6, 8, 13, 15, 20, 22 })]
    [InlineData("div.group span", new[] { 3, 10, 17 })]
    [InlineData("a:has-text('customers')", new[] { 13 })]
    [InlineData("li:nth-of-type(2) a", new[] { 8, 15, 22 })]
    [InlineData("a[href^='/s']", new[] { 20, 22 })]
    [InlineData(".title, .header", new[] { 3, 10, 17 })]
    public void A_selector_finds_the_nodes_a_browser_would(string css, int[] nodes)
    {
        var page = Menu();

        Assert.Equal(nodes, SnapshotSelector.Parse(css)!.Under(page, page[1]).Select(n => n.Number));
    }

    [Theory]
    [InlineData("div:has(span)")]
    [InlineData("xpath=//div")]
    [InlineData("a ~ b")]
    public void A_selector_this_cannot_read_is_said_so(string css)
    {
        Assert.Null(SnapshotSelector.Parse(css));
    }

    [Fact]
    public void A_control_s_parts_are_checked_in_every_occurrence_of_its_pattern()
    {
        var menu = Menu();
        Record(menu);
        var signature = NodeSignature.Of(menu, menu[2]);
        Directory.CreateDirectory(Path.Combine(_folder, "Controls"));
        File.WriteAllText(Path.Combine(_folder, "Controls", "MenuGroup.cs"), $$"""
            // <scraped control="{{signature}}" from="explore/001-menu.nodes.jsonl#2" />
            namespace Shop.UITests.Controls;

            public sealed class MenuGroup<TParent> : ContainerBase<TParent, MenuGroup<TParent>>
            {
                public LabelControl<MenuGroup<TParent>> Header => new(this, Locator.ByCss("span.header"));
                public MenuLinks Links => new(this);

                public sealed class MenuLink : ItemObjectBase<MenuLink>
                {
                    public LinkControl<MenuLink> Link => new(this, Locator.ByCss("a"));
                    public LabelControl<MenuLink> Badge => new(this, $"span.{BadgeClass}");
                }
            }
            """);

        var check = Assert.Single(_service.ControlChecks(null));

        Assert.Equal(("MenuGroup", 3, 1), (check.Name, check.Occurrences, check.Pages));
        var header = check.Parts.Single(p => p.Name == "Header");
        Assert.Equal((2, 3), (header.Found, header.Of));
        Assert.Equal(16, Assert.Single(header.Misses).Node);
        var link = check.Parts.Single(p => p.Name == "MenuLink.Link");
        Assert.Equal((3, 3), (link.Found, link.Of)); // an item's part: in every group at least once
        Assert.False(check.Parts.Single(p => p.Name == "MenuLink.Badge").Readable); // made in code: not read
        Assert.False(check.Done);

        _service.AcceptControl(null, "MenuGroup");

        Assert.True(Assert.Single(_service.ControlChecks(null)).Done);
    }

    [Fact]
    public void Progress_says_what_to_do_next_from_a_new_project_on()
    {
        var fresh = _service.Progress(null);
        Assert.Equal([StepState.Done, StepState.Next, StepState.Todo, StepState.Todo, StepState.Todo, StepState.Todo, StepState.Todo, StepState.Todo], fresh.Steps.Select(s => s.State));
        Assert.Equal((2, "Check that the Scraper can reach the app"), (fresh.Next!.Step, fresh.Next.Text));

        Record(Menu());
        var recorded = _service.Progress(null);
        Assert.Equal(StepState.Done, recorded.Steps[2].State);
        Assert.Equal("1 page, 1 capture", recorded.Steps[2].Summary);
        Assert.Equal(recorded.Next!.Step, recorded.Steps.First(s => s.State is StepState.Next or StepState.Attention).Step);

        _service.SkipControls(null);
        var skipped = _service.Progress(null);
        Assert.Equal(StepState.Done, skipped.Steps[4].State);
        Assert.Equal((6, StepState.Next), (skipped.Next!.Step, skipped.Steps[5].State));
        Assert.StartsWith("Draft page objects for the 1 page", skipped.Next.Text);
    }

    [Fact]
    public void Several_config_changes_are_written_once_and_none_when_one_does_not_read()
    {
        var path = Path.Combine(_folder, ScraperConfig.FileName);

        var edits = _service.EditConfig(null, [new ConfigChange("set", "Explore", ["MaxPages", "9"]), new ConfigChange("set", "Explore", ["ClickPolicy", "links"]), new ConfigChange("add", "Explore hosts", ["shop.example"])]);
        var after = File.ReadAllText(path);
        Assert.Equal(3, edits.Count(e => e.Changed));
        var explore = ScraperConfig.Parse(after).Explore;
        Assert.Equal((9, ClickPolicy.Links), (explore.MaxPages, explore.ClickPolicy));
        Assert.Contains("shop.example", explore.Hosts);

        Assert.Throws<ScraperProblem>(() => _service.EditConfig(null, [new ConfigChange("set", "Explore", ["MaxDepth", "2"]), new ConfigChange("set", "Explore", ["ClickPolicy", "everything"])]));
        Assert.Equal(after, File.ReadAllText(path));
    }

    [Fact]
    public async Task Drafting_chosen_pages_refuses_one_that_is_not_recorded()
    {
        Record(Menu());

        var problem = await Assert.ThrowsAsync<ScraperProblem>(() => _service.DraftAsync(null, ["/menu", "/nowhere"]));

        Assert.Contains("No recorded page '/nowhere'", problem.Message);
    }

    [Fact]
    public void A_page_object_is_renamed_in_its_draft_and_the_person_s_part_not_where_it_is_used()
    {
        Record(Menu());
        var pages = Path.Combine(_folder, "Pages");
        Directory.CreateDirectory(pages);
        File.WriteAllText(Path.Combine(pages, "MenuPage.scraped.cs"), """
            // <scraped page="/menu" from="explore/001-menu.nodes.jsonl" hash="1" />
            namespace Shop.UITests.Pages;

            public sealed partial class MenuPage(IHtmlTestContext context) : PageObjectBase<MenuPage>(context)
            {
                public LabelControl<MenuPage> Title => new(this, Locator.ByCss("h1"));
            }
            """);
        File.WriteAllText(Path.Combine(pages, "MenuPage.cs"), "namespace Shop.UITests.Pages;\n\npublic sealed partial class MenuPage\n{\n    public MenuPage Self2 => this;\n}\n");
        Directory.CreateDirectory(Path.Combine(_folder, "Tests"));
        File.WriteAllText(Path.Combine(_folder, "Tests", "MenuTests.cs"), "namespace Shop.UITests.Tests;\n\npublic class MenuTests { MenuPage? page; }\n");

        var result = _service.RenamePageObject(null, "/menu", "MainMenuPage");

        Assert.Equal([Path.Combine("Pages", "MainMenuPage.cs"), Path.Combine("Pages", "MainMenuPage.scraped.cs")], result.Files.Order(StringComparer.Ordinal));
        Assert.Equal([Path.Combine("Tests", "MenuTests.cs")], result.UsedElsewhere);
        Assert.False(File.Exists(Path.Combine(pages, "MenuPage.scraped.cs")));
        var draft = File.ReadAllText(Path.Combine(pages, "MainMenuPage.scraped.cs"));
        Assert.Contains("public sealed partial class MainMenuPage(IHtmlTestContext context) : PageObjectBase<MainMenuPage>(context)", draft);
        Assert.Contains("LabelControl<MainMenuPage> Title", draft);
        Assert.Contains("public MainMenuPage Self2 => this;", File.ReadAllText(Path.Combine(pages, "MainMenuPage.cs")));
        Assert.Contains("MenuPage? page", File.ReadAllText(Path.Combine(_folder, "Tests", "MenuTests.cs")));
        Assert.Throws<ScraperProblem>(() => _service.RenamePageObject(null, "/menu", "not a name"));
    }
}
