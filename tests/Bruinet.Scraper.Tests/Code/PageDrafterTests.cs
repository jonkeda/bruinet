using Bruinet.Scraper.Snapshots;
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
    [InlineData("/contact#/", "ContactPage")]
    [InlineData("/contact#/create", "ContactCreatePage")]
    [InlineData("/hour-log#/", "HourLogPage")]
    [InlineData("/hour-log#/contact", "HourLogContactPage")]
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
    public void A_page_a_module_claims_is_drafted_into_the_module_s_folder_and_namespace()
    {
        var folder = Path.Combine(_solution, "Shop.UITests");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), """
            ## App

            | Field | Value |
            | --- | --- |
            | StartUrl | https://shop.example/ |
            | Namespace | Shop.UITests.Pages |

            ## Modules

            | Path | Pages | Namespace |
            | --- | --- | --- |
            | /orders | Modules/Orders/Pages | Shop.UITests.Modules.Orders.Pages |
            """);
        var project = ScrapeProject.Open(folder);
        var session = project.StartSession("explore", new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero));
        foreach (var (location, minute) in new[] { ("https://shop.example/orders/12", 1), ("https://shop.example/help", 2) })
        {
            session.AddPage(new ScrapeSnapshot
            {
                Platform = "html",
                Location = location,
                Title = "Shop",
                CapturedAt = DateTimeOffset.UnixEpoch.AddMinutes(minute),
                Nodes = [N(1, 0, "body"), N(2, 1, "button", "Go", ("data-testid", "go"), ("type", "button"))],
            });
        }
        var index = project.Index();

        var order = PageDrafter.Draft(project, index, index.Page("/orders/{id}")!, "OrderPage");
        var help = PageDrafter.Draft(project, index, index.Page("/help")!, "HelpPage");

        Assert.Equal(Path.Combine("Modules/Orders/Pages", "OrderPage.scraped.cs"), order.Path);
        Assert.Contains("namespace Shop.UITests.Modules.Orders.Pages;", order.Code);
        Assert.Equal(Path.Combine("Pages", "HelpPage.scraped.cs"), help.Path);
        Assert.Contains("namespace Shop.UITests.Pages;", help.Code);
    }

    [Fact]
    public void What_a_part_a_library_or_a_table_header_covers_is_left_out_and_named()
    {
        var folder = Path.Combine(_solution, "Shop.UITests");
        Directory.CreateDirectory(Path.Combine(folder, "Components"));
        File.WriteAllText(Path.Combine(folder, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), "## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | https://shop.example/ |\n\n## Control libraries\n\n| Class prefix | Library |\n| --- | --- |\n| v- | Shop.Vuetify |\n");
        File.WriteAllText(Path.Combine(folder, "Components", "OrderCard.cs"), "// <scraped control=\"div||card|h3,div\" from=\"s/f#2\" />\nnamespace Shop.UITests.Components;\n\npublic sealed class OrderCard<TParent> : ContainerBase<TParent, OrderCard<TParent>>\n{\n}\n");
        var project = ScrapeProject.Open(folder);
        var session = project.StartSession("explore", new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero));
        session.AddPage(new ScrapeSnapshot
        {
            Platform = "html",
            Location = "https://shop.example/orders",
            Title = "Orders",
            CapturedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
            Nodes =
            [
                N(1, 0, "body"),
                N(2, 1, "div", null, ("class", "card")), N(3, 2, "h3", "Order"), N(4, 2, "div", null, ("class", "card-body")), N(5, 4, "button", "Open", ("data-testid", "open"), ("type", "button")),
                N(6, 1, "div", null, ("class", "v-input v-text-field")), N(7, 6, "input", null, ("data-testid", "name"), ("type", "text")),
                N(8, 1, "table"), N(9, 8, "thead"), N(10, 9, "tr"), N(11, 10, "th"), N(12, 11, "a", "Date", ("data-testid", "sort-date"), ("href", "#")),
                N(13, 1, "button", "Save", ("data-testid", "save"), ("type", "button")),
            ],
        });
        var index = project.Index();

        var draft = PageDrafter.Draft(project, index, index.Page("/orders")!, "OrderPage");

        Assert.Contains("Save", draft.Members.Select(m => m.Name));
        Assert.DoesNotContain(draft.Members, m => m.Name is "Open" or "Name" or "SortDate");
        Assert.Contains("// OrderCard (a part of this project): Order (LabelControl), Open (ButtonControl)", draft.Code, StringComparison.Ordinal);
        Assert.Contains("// Shop.Vuetify (classes v-): Name (", draft.Code, StringComparison.Ordinal);
        Assert.Contains("// its table's header (part of the table): SortDate (", draft.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void A_library_covers_only_what_is_near_its_classes_a_control_on_a_concrete_base_is_a_part_and_your_part_s_names_are_yours()
    {
        var folder = Path.Combine(_solution, "Shop.UITests");
        Directory.CreateDirectory(Path.Combine(folder, "Components"));
        Directory.CreateDirectory(Path.Combine(folder, "Pages"));
        File.WriteAllText(Path.Combine(folder, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), "## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | https://shop.example/ |\n\n## Control libraries\n\n| Class prefix | Library |\n| --- | --- |\n| v- | Shop.Vuetify |\n");
        File.WriteAllText(Path.Combine(folder, "Components", "B7Table.tpl.cs"), "// <scraped control=\"b7-table||hydrated|#shadow\" from=\"s/f#7\" />\nnamespace Shop.UITests.Components;\n\npublic partial class B7Table<TScope> : TableControl<TScope>\n{\n}\n");
        File.WriteAllText(Path.Combine(folder, "Pages", "OrderPage.scraped.cs"), "// <scraped page=\"/orders\" from=\"s/f\" />\nnamespace Shop.UITests.Pages;\n\npublic sealed partial class OrderPage(IHtmlTestContext context) : PageObjectBase<OrderPage>(context)\n{\n}\n");
        File.WriteAllText(Path.Combine(folder, "Pages", "OrderPage.cs"), "namespace Shop.UITests.Pages;\n\npublic sealed partial class OrderPage\n{\n    public LabelControl<OrderPage> Title => new(this, \"h1\");\n}\n");
        var project = ScrapeProject.Open(folder);
        var session = project.StartSession("explore", new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero));
        session.AddPage(new ScrapeSnapshot
        {
            Platform = "html",
            Location = "https://shop.example/orders",
            Title = "Orders",
            CapturedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
            Nodes =
            [
                N(1, 0, "body"),
                N(2, 1, "div", null, ("class", "v-application")), N(3, 2, "main"), N(4, 3, "section"), N(5, 4, "div"),
                N(6, 5, "button", "Save", ("data-testid", "save"), ("type", "button")),
                N(7, 2, "b7-table", null, ("class", "hydrated")),
                new() { Number = 8, Parent = 7, Kind = ScrapeNodeKind.ShadowRoot, Bounds = new(0, 0, 10, 10) },
                N(9, 8, "button", "Sort", ("data-testid", "sort"), ("type", "button")),
                N(10, 3, "div", null, ("class", "v-input")), N(11, 10, "input", null, ("data-testid", "name"), ("type", "text")),
                N(12, 5, "h1", "Orders", ("data-testid", "title")),
            ],
        });
        var index = project.Index();

        var draft = PageDrafter.Draft(project, index, index.Page("/orders")!, "OrderPage");

        Assert.Equal(["Save"], draft.Members.Select(m => m.Name));
        Assert.Contains("// B7Table (a part of this project): Sort (", draft.Code, StringComparison.Ordinal);
        Assert.Contains("// Shop.Vuetify (classes v-): Name (", draft.Code, StringComparison.Ordinal);
        Assert.Contains("// OrderPage.cs (your part, by name): Title (", draft.Code, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void With_privacy_on_an_e_mail_address_is_masked_in_the_capture_and_kept_out_of_the_draft(bool privacy)
    {
        var folder = Path.Combine(_solution, "Shop.UITests");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var privacyRow = privacy ? "| Privacy | true |\n" : string.Empty;
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), "## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | https://shop.example/ |\n" + privacyRow);
        var project = ScrapeProject.Open(folder);
        var session = project.StartSession("explore", new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero));
        session.AddPage(new ScrapeSnapshot
        {
            Platform = "html",
            Location = "https://shop.example/orders",
            Title = "Orders",
            CapturedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
            Nodes = [N(1, 0, "body"), N(2, 1, "a", "Log out (ann.smith@example.com)", ("href", "/logout")), N(3, 1, "button", "Save", ("data-testid", "save"), ("type", "button"))],
        });
        var index = project.Index();

        var draft = PageDrafter.Draft(project, index, index.Page("/orders")!, "OrderPage");
        var recorded = string.Concat(Directory.EnumerateFiles(project.RecordingsFolder, "*", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.Contains("Save", draft.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("e-mail}", draft.Code, StringComparison.Ordinal);
        if (privacy)
        {
            Assert.DoesNotContain("ann.smith", draft.Code, StringComparison.Ordinal);
            Assert.Contains("1 element(s) whose text is personal data", draft.Code, StringComparison.Ordinal);
            Assert.DoesNotContain("ann.smith", recorded, StringComparison.Ordinal);
        }
        else
        {
            // Off by default: a dev, test or acceptance environment's data is kept as it is.
            Assert.Contains("ann.smith@example.com", draft.Code, StringComparison.Ordinal);
            Assert.DoesNotContain("personal data", draft.Code, StringComparison.Ordinal);
            Assert.Contains("ann.smith", recorded, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_module_row_without_a_namespace_is_refused_naming_its_line()
    {
        var error = Assert.Throws<FormatException>(() => ScraperConfig.Parse("## Modules\n\n| Path | Pages | Namespace |\n| --- | --- | --- |\n| /orders | Modules/Orders/Pages | |\n"));

        Assert.StartsWith("Line 5:", error.Message);
    }

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
        Assert.Contains("public sealed partial class OrderPage(IHtmlTestContext context) : PageObjectBase<OrderPage>(context, \"/orders/{id}\")", draft.Code);
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
