using Bruinet.Scraper.Snapshots;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Recordings;

namespace Bruinet.Scraper.Tests.Projects;

public sealed class ScrapeProjectTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string MakeProject(string name, string config)
    {
        var folder = Path.Combine(_root, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name + ".csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), config);
        return folder;
    }

    private const string Config = """
        # Scraper Config

        ## App

        | Field | Value |
        | --- | --- |
        | Platform | blazor |
        | StartUrl | https://app.example.nl/ |
        | Namespace | MyApp.UITests |
        | Screenshots | true |

        ## Aliases

        | Url |
        | --- |
        | https://app.example.be/ |

        ## Test ids

        | Attribute |
        | --- |
        | data-test |

        ## Masks

        | Pattern | Replacement |
        | --- | --- |
        | `NL\d{2}[A-Z]{4}\d{10}` | `IBAN` |
        """;

    private static ScrapeSnapshot Page(string location, string title, params ScrapeNode[] nodes) => new()
    {
        Platform = "html",
        Location = location,
        Title = title,
        CapturedAt = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero),
        Nodes = nodes.Length > 0 ? nodes : [new() { Number = 1, Parent = 0, Type = "html" }],
    };

    [Fact]
    public void The_config_is_read_from_its_tables()
    {
        var project = ScrapeProject.Open(MakeProject("MyApp.UITests", Config));

        Assert.Equal("MyApp.UITests", project.Name);
        Assert.Equal("blazor", project.Config.Platform);
        Assert.Equal("https://app.example.nl/", project.Config.StartUrl);
        Assert.Equal(["https://app.example.be/"], project.Config.Aliases);
        Assert.Equal(["data-test"], project.Config.TestIds);
        Assert.True(project.Config.Screenshots);
        Assert.Equal("Pages", project.Config.PagesFolder);
        Assert.Single(project.Config.Masks);
    }

    [Fact]
    public void An_unknown_field_is_refused_with_its_line()
    {
        var e = Assert.Throws<FormatException>(() => ScraperConfig.Parse("## App\n\n| Field | Value |\n| --- | --- |\n| Colour | red |\n"));

        Assert.Contains("Line 5", e.Message);
        Assert.Contains("Colour", e.Message);
    }

    [Fact]
    public void A_folder_without_a_config_is_not_a_project()
    {
        Directory.CreateDirectory(_root);

        Assert.Throws<FileNotFoundException>(() => ScrapeProject.Open(_root));
        Assert.Null(ScrapeProject.Find(_root));
    }

    [Fact]
    public void A_project_is_found_from_a_file_in_it_and_all_are_found_under_a_root()
    {
        var a = MakeProject("A.UITests", Config);
        MakeProject("B.UITests", Config);
        Directory.CreateDirectory(Path.Combine(a, "Pages"));

        Assert.Equal(a, ScrapeProject.Find(Path.Combine(a, "Pages", "Home.cs"))!.Folder);
        Assert.Equal(["A.UITests", "B.UITests"], ScrapeProject.FindAll(_root).Select(p => p.Name));
    }

    [Fact]
    public void A_project_the_solution_lists_is_found_where_it_is_and_the_loop_s_copies_are_not()
    {
        MakeProject("A.UITests", Config);
        var copy = Path.Combine(_root, "TestResults", "scrape-loop", "run-1", "tests", "A.UITests");
        Directory.CreateDirectory(copy);
        File.WriteAllText(Path.Combine(copy, "A.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(copy, ScraperConfig.FileName), Config);
        var solution = Path.Combine(_root, "app");
        Directory.CreateDirectory(solution);
        File.WriteAllText(Path.Combine(solution, "App.sln"), "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"B.UITests\", \"..\\B.UITests\\B.UITests.csproj\", \"{1}\"\nEndProject\n");
        MakeProject("B.UITests", Config);

        Assert.Equal([Path.Combine(_root, "A.UITests"), Path.Combine(_root, "B.UITests")], ScrapeProject.FindAll(_root).Select(p => p.Folder));
        Assert.Equal([Path.Combine(_root, "B.UITests")], ScrapeProject.FindAll(solution).Select(p => p.Folder)); // outside the folder, listed by its solution
    }

    [Fact]
    public void The_app_s_own_pages_are_on_its_start_host_or_an_alias()
    {
        var project = ScrapeProject.Open(MakeProject("MyApp.UITests", Config));

        Assert.True(project.IsOwnPage("https://app.example.nl/invoices"));
        Assert.True(project.IsOwnPage("https://app.example.be/invoices"));
        Assert.False(project.IsOwnPage("https://login.microsoftonline.com/x"));
    }

    [Fact]
    public void Secrets_are_fields_with_a_placeholder_named_after_the_field_unless_given()
    {
        var project = ScrapeProject.Open(MakeProject("MyApp.UITests", Config + """

            ## Secrets

            | Field | Placeholder |
            | --- | --- |
            | API key | |
            | pin-code | `{pin}` |
            """));

        Assert.Equal([new ScrapeSecret("API key", "apiKey"), new ScrapeSecret("pin-code", "pin")], project.Config.Secrets);
    }

    [Fact]
    public void A_page_is_written_into_the_session_folder_and_listed_in_recording_json()
    {
        var project = ScrapeProject.Open(MakeProject("MyApp.UITests", Config));
        var session = project.StartSession("Invoices", new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero));

        var e = session.AddPage(Page("https://app.example.nl/invoices/12/edit", "Edit invoice"), [1, 2, 3]);

        Assert.Equal("2026-09-30-invoices", session.Id);
        Assert.Equal(Path.Combine(project.RecordingsFolder, "2026-09-30-invoices"), session.Folder);
        Assert.Equal("001-edit-invoice" + SnapshotFile.Extension, e.Snapshot);
        Assert.Equal("001-edit-invoice.png", e.Screenshot);
        Assert.Equal("/invoices/{id}/edit", e.Page);
        Assert.True(File.Exists(Path.Combine(session.Folder, e.Snapshot!)));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(session.Folder, e.Screenshot!)));

        var reopened = project.Session(session.Id)!;
        Assert.Equal("Invoices", reopened.Name);
        Assert.Equal(e, Assert.Single(reopened.Events));
        Assert.Equal("Edit invoice", reopened.ReadSnapshot(reopened.Events[0]).Title);
    }

    [Fact]
    public void A_second_session_on_the_same_day_and_name_gets_its_own_folder()
    {
        var project = ScrapeProject.Open(MakeProject("MyApp.UITests", Config));
        var now = new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero);

        project.StartSession("Invoices", now);
        var second = project.StartSession("Invoices", now);

        Assert.Equal("2026-09-30-invoices-2", second.Id);
        Assert.Equal(2, project.Sessions().Count);
    }

    [Fact]
    public void Masks_are_applied_before_the_snapshot_is_written()
    {
        var project = ScrapeProject.Open(MakeProject("MyApp.UITests", Config));
        var session = project.StartSession("Bank", DateTimeOffset.UnixEpoch);
        var node = new ScrapeNode
        {
            Number = 1,
            Parent = 0,
            Type = "td",
            Text = "Pay to NL91ABNA0417164300 today",
            Attributes = new Dictionary<string, string> { ["title"] = "NL91ABNA0417164300" },
        };

        var e = session.AddPage(Page("https://app.example.nl/bank", "Bank", node));

        var text = File.ReadAllText(Path.Combine(session.Folder, e.Snapshot!));
        Assert.DoesNotContain("NL91ABNA0417164300", text);
        Assert.Contains("Pay to IBAN today", text);
    }

    [Fact]
    public void A_reopened_session_still_masks_what_is_added()
    {
        var project = ScrapeProject.Open(MakeProject("MyApp.UITests", Config));
        var id = project.StartSession("Bank", DateTimeOffset.UnixEpoch).Id;
        var node = new ScrapeNode { Number = 1, Parent = 0, Type = "td", Text = "NL91ABNA0417164300" };

        var e = project.Session(id)!.AddPage(Page("https://app.example.nl/bank", "Bank", node));

        Assert.DoesNotContain("NL91ABNA0417164300", File.ReadAllText(Path.Combine(project.RecordingsFolder, id, e.Snapshot!)));
    }

    [Fact]
    public void The_index_groups_captures_by_page_newest_first_and_searches_the_newest()
    {
        var project = ScrapeProject.Open(MakeProject("MyApp.UITests", Config));
        var first = project.StartSession("One", DateTimeOffset.UnixEpoch);
        first.AddPage(Page("https://app.example.nl/invoices/1", "Invoice") with { CapturedAt = DateTimeOffset.UnixEpoch });
        var second = project.StartSession("Two", DateTimeOffset.UnixEpoch.AddDays(1));
        second.AddPage(Page("https://app.example.nl/invoices/2", "Invoice",
            new ScrapeNode { Number = 1, Parent = 0, Type = "button", Text = "Approve" }) with
        { CapturedAt = DateTimeOffset.UnixEpoch.AddDays(1) });
        second.AddPage(Page("https://app.example.nl/#/dashboard", "Dashboard"));

        var index = project.Index();

        Assert.Equal(["#/dashboard", "/invoices/{id}"], index.Pages.Select(p => p.Key));
        var invoice = index.Page("/invoices/{id}")!;
        Assert.Equal(2, invoice.Captures.Count);
        Assert.Equal(second.Id, invoice.Latest.Session);
        var (page, found) = Assert.Single(index.Search("approve"));
        Assert.Equal("/invoices/{id}", page.Key);
        Assert.Equal("button", found.Type);
    }

    [Theory]
    [InlineData("https://app/invoices/1234/edit?tab=2", "/invoices/{id}/edit")]
    [InlineData("https://app/invoices/9c1f3e2a-1b2c-4d5e-8f90-123456789abc", "/invoices/{id}")]
    [InlineData("https://app/", "/")]
    [InlineData("https://app/#/orders/12?x=1", "#/orders/{id}")]
    [InlineData("https://app/docs#top", "/docs")]
    [InlineData("https://app/shop/#/cart", "/shop#/cart")]
    public void A_page_key_leaves_out_host_query_and_ids(string location, string key)
    {
        Assert.Equal(key, PageKey.Of(location));
    }

    [Theory]
    [InlineData("Edit invoice", "edit-invoice")]
    [InlineData("  Überweisung / Détails ", "uberweisung-details")]
    [InlineData("???", "page")]
    public void A_slug_is_fit_for_a_file_name(string text, string slug)
    {
        Assert.Equal(slug, Slug.Of(text, "page"));
    }
}
