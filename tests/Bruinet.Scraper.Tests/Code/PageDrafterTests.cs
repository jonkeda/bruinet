using Bruinet.Core.Scraping;
using Bruinet.Scraper.Code;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Tests.Code;

public sealed class PageDrafterTests : IDisposable
{
    private readonly string _solution = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_solution))
            Directory.Delete(_solution, recursive: true);
    }

    [Theory]
    [InlineData("/invoices/{id}/edit", "InvoiceEditPage")]
    [InlineData("/", "HomePage")]
    [InlineData("/index.html", "HomePage")]
    [InlineData("/pages/account.html", "AccountPage")]
    [InlineData("/user-settings", "UserSettingsPage")]
    [InlineData("/categories/{id}", "CategoryPage")]
    [InlineData("#/orders/{id}/lines", "OrderLinesPage")]
    [InlineData("/2024/report", "Page2024ReportPage")]
    [InlineData("/checkout-page", "CheckoutPage")]
    public void A_class_is_named_from_the_page_key(string key, string name)
    {
        Assert.Equal(name, PageDrafter.ClassName(key));
    }

    [Theory]
    [InlineData("/userform", "User Form - Bruinet Blazor Sample", "UserFormPage")]
    [InlineData("/mediagallery", "Media Gallery", "MediaGalleryPage")]
    [InlineData("/orders/{id}/lineitems", "Order 12: line items", "OrderLineItemsPage")]
    [InlineData("/userform", "Registration", "UserformPage")]
    [InlineData("/counter", "Counter", "CounterPage")]
    public void A_key_s_run_together_words_are_broken_where_the_title_breaks_them(string key, string title, string name)
    {
        Assert.Equal(name, PageDrafter.ClassName(key, title));
    }

    private static ScrapeNode N(int n, int p, string type, string? text = null, params (string Key, string Value)[] attributes) =>
        new() { Number = n, Parent = p, Type = type, Text = text, Bounds = new(0, 0, 10, 10), Attributes = attributes.ToDictionary(a => a.Key, a => a.Value) };

    [Fact]
    public void A_draft_is_the_page_as_it_opens_and_lists_the_unstable_members_and_what_only_a_click_shows()
    {
        var folder = Path.Combine(_solution, "Shop.UITests");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), "## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | https://shop.example/ |\n| Namespace | Shop.UITests.Pages |\n");
        var project = ScrapeProject.Open(folder);
        var session = project.StartSession("explore", new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero));
        session.AddPage(new ScrapeSnapshot
        {
            Platform = "html",
            Location = "https://shop.example/orders/12",
            Title = "Order",
            CapturedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
            Nodes = [N(1, 0, "body"), N(2, 1, "button", "Details", ("data-testid", "order-details"), ("type", "button")), N(3, 1, "div", null, ("class", "x")), N(4, 3, "button", null, ("type", "button"))],
        });
        // A state a click showed (Explore names it): a dialog with a close button.
        session.AddPage(new ScrapeSnapshot
        {
            Platform = "html",
            Location = "https://shop.example/orders/12",
            Title = "Order",
            CapturedAt = DateTimeOffset.UnixEpoch.AddMinutes(2),
            Nodes = [N(1, 0, "body"), N(2, 1, "button", "Details", ("data-testid", "order-details"), ("type", "button")), N(3, 1, "dialog"), N(4, 3, "button", "×", ("data-testid", "order-close"), ("type", "button"))],
        }, pageName: "Order: Details");
        var index = project.Index();

        var draft = PageDrafter.Draft(project, index, index.Page("/orders/{id}")!, "OrderPage");

        Assert.Equal(Path.Combine("Pages", "OrderPage.scraped.cs"), draft.Path);
        Assert.StartsWith("// <scraped page=\"/orders/{id}\" from=\"2026-09-30-explore/001-order.nodes.jsonl\" hash=\"", draft.Code);
        Assert.Contains("using Bruinet.Html.Controls.Buttons;\nusing Bruinet.Html.Interfaces;\nusing Bruinet.Html.Pages;", draft.Code.ReplaceLineEndings("\n"));
        Assert.Contains("namespace Shop.UITests.Pages;", draft.Code);
        Assert.Contains("public sealed partial class OrderPage(IHtmlTestContext context) : HtmlPageObjectBase<OrderPage>(context)", draft.Code);
        Assert.Contains("    public ButtonControl<OrderPage> OrderDetails => new(this, Locator.ByDataTestId(\"order-details\"));", draft.Code);
        Assert.Single(draft.Members);
        Assert.Contains("    // Not drafted: shown only after a click", draft.Code);
        Assert.Contains("    // ButtonControl OrderClose: Locator.ByDataTestId(\"order-close\"), in \"Order: Details\"", draft.Code);
        Assert.Contains("    // Not drafted: no stable locator.", draft.Code);
        Assert.Contains("    // ButtonControl ", draft.Code);
        Assert.Empty(CodeGate.Syntax(draft.Code));
    }

    [Fact]
    public void A_member_is_never_named_as_the_base_class_s_members_or_the_class()
    {
        var folder = Path.Combine(_solution, "Shop.UITests");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), "## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | https://shop.example/ |\n");
        var project = ScrapeProject.Open(folder);
        project.StartSession("s", DateTimeOffset.UnixEpoch).AddPage(new ScrapeSnapshot
        {
            Platform = "html",
            Location = "https://shop.example/url",
            Title = "Url",
            Nodes = [N(1, 0, "body"), N(2, 1, "button", "x", ("data-testid", "url")), N(3, 1, "button", "y", ("data-testid", "url-page")), N(4, 1, "input", null, ("data-testid", "name"))],
        });
        var index = project.Index();

        var draft = PageDrafter.Draft(project, index, index.Pages[0], "UrlPage");

        Assert.Equal(["Url2", "UrlPage2", "Name2"], draft.Members.Select(m => m.Name));
    }

    [Fact]
    public void A_container_is_listed_to_make_a_control_of_not_drafted_as_a_member()
    {
        var folder = Path.Combine(_solution, "Shop.UITests");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), "## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | https://shop.example/ |\n");
        var project = ScrapeProject.Open(folder);
        project.StartSession("s", DateTimeOffset.UnixEpoch).AddPage(new ScrapeSnapshot
        {
            Platform = "html",
            Location = "https://shop.example/tabs",
            Title = "Tabs",
            Nodes = [N(1, 0, "body"), N(2, 1, "div", null, ("data-testid", "tabs")) with { Role = "tablist" }, N(3, 2, "button", "One", ("data-testid", "one"), ("type", "button"))],
        });
        var index = project.Index();

        var draft = PageDrafter.Draft(project, index, index.Pages[0], "TabsPage");

        Assert.Equal(["One"], draft.Members.Select(m => m.Name));
        Assert.Contains("    // Not drafted: a container is a control of its own.", draft.Code);
        Assert.Contains("    // TabContainerControl<TParent, TScope> Tabs: Locator.ByDataTestId(\"tabs\")", draft.Code);
    }

    [Fact]
    public void A_part_repeated_on_one_page_asks_for_a_control_as_one_on_two_pages_does()
    {
        static Pattern P(string signature, PatternKind kind, int pages, string? control = null) => new(signature, kind, pages, 6, [], control, "Part");
        var patterns = new[]
        {
            P("li.menu-item", PatternKind.Repeated, 1),
            P("header.site", PatternKind.Navigation, 4),
            P("tr", PatternKind.Repeated, 2, control: "OrderRow"),
            P("table#orders", PatternKind.Table, 1),
        };

        Assert.Equal(["li.menu-item", "header.site"], DraftRun.ControlsToMake(patterns).Select(p => p.Signature));
    }
}
