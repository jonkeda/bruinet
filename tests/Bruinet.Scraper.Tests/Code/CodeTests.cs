using Bruinet.Core.Scraping;
using Bruinet.Scraper.Code;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Recordings;
using Bruinet.Scraper.Service;

namespace Bruinet.Scraper.Tests.Code;

public sealed class CodeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private ScrapeProject MakeProject(string name, string csproj = "<Project Sdk=\"Microsoft.NET.Sdk\" />")
    {
        var folder = Path.Combine(_root, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name + ".csproj"), csproj);
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), "## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | https://shop.example/ |\n");
        return ScrapeProject.Open(folder);
    }

    private static ScrapeNode N(int n, int p, string type, string? text = null, string? cls = null) => new()
    {
        Number = n,
        Parent = p,
        Type = type,
        Text = text,
        Attributes = cls is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["class"] = cls },
    };

    private static ScrapeSnapshot Page(string location, params ScrapeNode[] nodes) => new() { Platform = "html", Location = location, Nodes = nodes };

    // A page with a list of cards: each card a div.card with an h3 and a div.card-body holding a p and a button.
    private static ScrapeSnapshot Cards(string location, int count)
    {
        var nodes = new List<ScrapeNode> { N(1, 0, "body") };
        for (var i = 0; i < count; i++)
        {
            var card = nodes.Count + 1;
            nodes.Add(N(card, 1, "div", cls: "card"));
            nodes.Add(N(card + 1, card, "h3", $"Card {i}"));
            nodes.Add(N(card + 2, card, "div", cls: "card-body"));
            nodes.Add(N(card + 3, card + 2, "p", "Text"));
            nodes.Add(N(card + 4, card + 2, "button", "Open"));
        }
        return Page(location, [.. nodes]);
    }

    [Fact]
    public void A_header_reads_back_as_written()
    {
        var header = new ScrapedHeader { Page = "/invoices/{id}", From = "2026-09-30-a/001-x.nodes.jsonl", Hash = "1a2b3c4d" };
        var control = new ScrapedHeader { Control = "div|dialog|\"quoted\"|h2,div+", From = "s/f#12" };

        Assert.Equal("// <scraped page=\"/invoices/{id}\" from=\"2026-09-30-a/001-x.nodes.jsonl\" hash=\"1a2b3c4d\" />", header.ToString());
        Assert.Equal(header, ScrapedHeader.Find("using X;\n" + header + "\nnamespace Y;"));
        Assert.Equal(control, ScrapedHeader.Find(control.ToString()));
        Assert.Null(ScrapedHeader.Find("// just a comment"));
    }

    [Fact]
    public void The_code_is_read_for_pages_containers_and_their_locators()
    {
        var project = MakeProject("Shop.UITests");
        Directory.CreateDirectory(Path.Combine(project.Folder, "Pages"));
        File.WriteAllText(Path.Combine(project.Folder, "Pages", "OrdersPage.cs"), """
            // <scraped page="/orders" from="s/001-orders.nodes.jsonl" hash="aaaaaaaa" />
            using Bruinet.Html;
            namespace Shop.UITests.Pages;

            public sealed class OrdersPage(IHtmlTestContext context) : HtmlPageObjectBase<OrdersPage>(context)
            {
                public ButtonControl<OrdersPage> New => new(this, Locator.ByDataTestId("new-order"));
                private string Hidden => "";
            }

            public sealed class Card<TParent>(IHtmlScope<TParent> parent, string testId)
                : ContainerBase<TParent, Card<TParent>>(parent, Locator.ByDataTestId(testId))
                where TParent : IHtmlScope<TParent>
            {
                public override Card<TParent> Self => this;
            }

            internal static class NotAPageObject { }
            """);

        var classes = ProjectCode.Read(project);

        var page = Assert.Single(classes, c => c.Kind == CodeKind.Page);
        Assert.Equal("OrdersPage", page.Name);
        Assert.Equal("Shop.UITests.Pages", page.Namespace);
        Assert.Equal("/orders", page.Header!.Page);
        var member = Assert.Single(page.Members);
        Assert.Equal(("New", "ButtonControl<OrdersPage>", "Locator.ByDataTestId(\"new-order\")"), (member.Name, member.Type, member.Locator));
        var card = Assert.Single(classes, c => c.Kind == CodeKind.Container);
        Assert.Equal("Card<TParent>", card.Name);
        Assert.Equal(2, classes.Count);
    }

    [Fact]
    public void A_referenced_scraped_project_s_controls_come_with_it_and_other_references_do_not()
    {
        var shared = MakeProject("Shared.UITests");
        File.WriteAllText(Path.Combine(shared.Folder, "Dialog.cs"), "// <scraped control=\"div|dialog||h2,div+\" from=\"s/f#3\" />\nclass Dialog<T> : ContainerBase<T, Dialog<T>> { }");
        var library = Path.Combine(_root, "Library");
        Directory.CreateDirectory(library);
        File.WriteAllText(Path.Combine(library, "Library.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(library, "Other.cs"), "class Other<T> : ContainerBase<T, Other<T>> { }");
        var module = MakeProject("Module.UITests", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Shared.UITests\Shared.UITests.csproj" />
                <ProjectReference Include="..\Library\Library.csproj" />
              </ItemGroup>
            </Project>
            """);

        var classes = ProjectCode.Read(module);

        var dialog = Assert.Single(classes);
        Assert.Equal("Shared.UITests", dialog.Project);
        Assert.Equal("div|dialog||h2,div+", dialog.Header!.Control);
    }

    [Fact]
    public void Code_is_written_only_inside_the_project_and_only_when_its_syntax_is_valid()
    {
        var project = MakeProject("Shop.UITests");

        var outside = CodeGate.Write(project, "../Elsewhere.cs", "class A { }");
        var generated = CodeGate.Write(project, "Pages/A.gen.cs", "class A { }");
        var broken = CodeGate.Write(project, "Pages/A.cs", "class A {");
        var written = CodeGate.Write(project, "Pages/A.cs", "class A { }");
        var replaced = CodeGate.Write(project, "Pages/A.cs", "class A { int x; }");

        Assert.False(outside.Written);
        Assert.Contains("outside Shop.UITests", outside.Reason);
        Assert.False(generated.Written);
        Assert.False(broken.Written);
        Assert.Contains(broken.Problems, p => p.Code == "CS1513" && p.Line == 1);
        Assert.True(written.Written);
        Assert.False(written.Replaced);
        Assert.Equal(Path.Combine("Pages", "A.cs"), written.Path);
        Assert.True(replaced.Replaced);
        Assert.Equal("class A { int x; }", File.ReadAllText(Path.Combine(project.Folder, "Pages", "A.cs")));
    }

    [Fact]
    [Trait("Category", "Build")]
    public async Task A_build_gives_its_errors_with_their_place_and_passes_once_fixed()
    {
        var project = MakeProject("Tiny.UITests", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Library</OutputType></PropertyGroup></Project>");
        CodeGate.Write(project, "Pages/A.cs", "namespace Tiny;\npublic class A\n{\n    public Missing B => null!;\n}\n");

        var failed = await CodeGate.BuildAsync(project);
        CodeGate.Write(project, "Pages/A.cs", "namespace Tiny;\npublic class A\n{\n    public string B => \"\";\n}\n");
        var passed = await CodeGate.BuildAsync(project);

        Assert.False(failed.Succeeded);
        var problem = Assert.Single(failed.Problems, p => p.Severity == "error");
        Assert.Equal(("CS0246", 4), (problem.Code, problem.Line));
        Assert.EndsWith("A.cs", problem.File);
        Assert.True(passed.Succeeded, string.Join('\n', passed.Problems) + passed.Output);
    }

    [Fact]
    public async Task A_template_that_does_not_generate_fails_the_build_before_it_runs_and_one_that_does_is_built()
    {
        var project = MakeProject("Tiny.UITests", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Library</OutputType></PropertyGroup></Project>");
        CodeGate.Write(project, "Controls/Field.tpl.cs", "namespace Tiny;\npublic partial class Field { }\n");
        var folders = new List<string>();

        var failed = await CodeGate.BuildAsync(project, new FakeTemplates(folders, new CodeProblem("Field.tpl.cs", 1, 1, "error", "BRGEN", "no target class")));
        var passed = await CodeGate.BuildAsync(project, new FakeTemplates(folders));

        Assert.False(failed.Succeeded);
        Assert.Equal("BRGEN", Assert.Single(failed.Problems).Code);
        Assert.True(passed.Succeeded, string.Join('\n', passed.Problems) + passed.Output);
        Assert.Equal([Path.Combine(project.Folder, "Controls"), Path.Combine(project.Folder, "Controls")], folders);
    }

    private sealed class FakeTemplates(List<string> folders, params CodeProblem[] problems) : ITemplateGenerator
    {
        public IReadOnlyList<CodeProblem> Generate(string folder)
        {
            folders.Add(folder);
            return problems;
        }
    }

    [Fact]
    public void A_part_that_repeats_across_pages_is_a_pattern_and_its_inner_parts_are_not_repeated_as_patterns()
    {
        var pages = new[] { ("/a", Cards("https://shop.example/a", 2)), ("/b", Cards("https://shop.example/b", 1)) };

        var patterns = PatternDetector.Detect(pages, []);

        var card = Assert.Single(patterns);
        Assert.Equal(PatternKind.Repeated, card.Kind);
        Assert.Equal((2, 3), (card.Pages, card.Occurrences));
        Assert.Equal("div||card|h3,div", card.Signature);
        Assert.Equal(["/a", "/a", "/b"], card.Examples.Select(e => e.Page));
    }

    [Fact]
    public void A_pattern_is_named_by_the_id_its_occurrences_share_else_by_its_class_and_a_bare_wrapper_is_none()
    {
        static ScrapeSnapshot Framed(string location, string id)
        {
            var frame = N(2, 1, "div");
            frame = frame with { Attributes = new Dictionary<string, string> { ["id"] = id } };
            return Page(location, N(1, 0, "body"), frame, N(3, 2, "h1", "Title"), N(4, 2, "p", "Text"));
        }

        var shared = PatternDetector.Detect([("/a", Framed("https://shop.example/a", "frame-content")), ("/b", Framed("https://shop.example/b", "frame-content"))], []);
        var numbered = PatternDetector.Detect([("/a", Framed("https://shop.example/a", "row-1")), ("/b", Framed("https://shop.example/b", "row-2"))], []);
        var cards = PatternDetector.Detect([("/a", Cards("https://shop.example/a", 2)), ("/b", Cards("https://shop.example/b", 1))], []);
        var wrappers = PatternDetector.Detect([("/a", Framed("https://shop.example/a", "frame-logo")), ("/b", Framed("https://shop.example/b", "workspace"))], []);

        Assert.Equal("FrameContent", Assert.Single(shared).Name);
        Assert.Equal("Row", Assert.Single(numbered).Name);
        Assert.Equal("Card", Assert.Single(cards).Name);
        Assert.Contains("bare wrapper", Assert.Single(wrappers).Hint);
        Assert.Empty(DraftRun.ControlsToMake(wrappers));
    }

    [Fact]
    public void A_pattern_is_named_after_a_custom_elements_tag_or_its_class_that_says_what_it_is()
    {
        static ScrapeSnapshot Part(string location, string type, string cls) =>
            Page(location, N(1, 0, "body"), N(2, 1, type, cls: cls), N(3, 2, "span", "Label"), N(4, 2, "i"));

        var button = PatternDetector.Detect([("/a", Part("https://shop.example/a", "button", "theme--light v-btn v-btn--icon pb-4")), ("/b", Part("https://shop.example/b", "button", "theme--light v-btn v-btn--icon pb-4"))], []);
        var custom = PatternDetector.Detect([("/a", Part("https://shop.example/a", "b7-navbar-button", "hydrated interactive")), ("/b", Part("https://shop.example/b", "b7-navbar-button", "hydrated interactive"))], []);

        Assert.Equal("VBtn", Assert.Single(button).Name);
        Assert.Equal("B7NavbarButton", Assert.Single(custom).Name);
    }

    [Fact]
    public void Structure_is_a_pattern_even_once_and_a_control_that_stands_for_one_is_named()
    {
        var table = Page("https://shop.example/t",
            N(1, 0, "body"), N(2, 1, "table"), N(3, 2, "thead"), N(4, 3, "tr"), N(5, 2, "tbody"), N(6, 5, "tr"));
        var control = new CodeClass("OrderTable<T>", "", CodeKind.Container, "ContainerBase", "f", "p", new ScrapedHeader { Control = "table|||thead,tbody" }, []);

        var patterns = PatternDetector.Detect([("/t", table)], [control]);

        var found = Assert.Single(patterns, p => p.Kind == PatternKind.Table);
        Assert.Equal("OrderTable<T>", found.Control);
    }

    [Fact]
    public void The_content_hash_ignores_boxes_and_changes_with_content()
    {
        var a = Page("https://x/", N(1, 0, "p", "Hi") with { Bounds = new(0, 0, 10, 10) });
        var b = Page("https://x/", N(1, 0, "p", "Hi") with { Bounds = new(5, 5, 20, 20) });
        var c = Page("https://x/", N(1, 0, "p", "Hello"));

        Assert.Equal(SnapshotFile.ContentHash(a), SnapshotFile.ContentHash(b));
        Assert.NotEqual(SnapshotFile.ContentHash(a), SnapshotFile.ContentHash(c));
    }

    [Fact]
    public void A_diff_gives_what_was_added_removed_and_changed_by_the_nodes_path()
    {
        var before = Page("https://x/",
            N(1, 0, "body"), N(2, 1, "h1", "Orders"), N(3, 1, "button", "New") with { Attributes = new Dictionary<string, string> { ["data-testid"] = "new" } },
            N(4, 1, "p", "Old note"));
        var after = Page("https://x/",
            N(1, 0, "body"), N(2, 1, "h1", "All orders"), N(3, 1, "button", "New") with { Attributes = new Dictionary<string, string> { ["data-testid"] = "new" }, Bounds = new(1, 2, 3, 4) },
            N(4, 1, "input") with { Attributes = new Dictionary<string, string> { ["data-testid"] = "search" } });

        var diff = SnapshotDiff.Compare(before, after);

        Assert.Equal((1, 1, 1), (diff.AddedCount, diff.RemovedCount, diff.ChangedCount));
        Assert.Equal(["#4 input [data-testid=search]"], diff.Added);
        Assert.Equal(["#4 p \"Old note\""], diff.Removed);
        Assert.Equal(new NodeChange("#2 h1 \"Orders\"", "#2 h1 \"All orders\""), Assert.Single(diff.Changed));
    }

    [Fact]
    public async Task The_diff_of_a_page_with_a_page_object_starts_from_the_capture_it_was_made_from()
    {
        var project = MakeProject("Shop.UITests");
        project.StartSession("One", DateTimeOffset.UnixEpoch).AddPage(Cards("https://shop.example/cards", 1) with { CapturedAt = DateTimeOffset.UnixEpoch });
        await using var service = new ScraperService(_root);
        File.WriteAllText(Path.Combine(project.Folder, "CardsPage.cs"), service.Header(null, "/cards") + "\nclass CardsPage : HtmlPageObjectBase<CardsPage> { }");
        project.StartSession("Two", DateTimeOffset.UnixEpoch.AddDays(1)).AddPage(Cards("https://shop.example/cards", 2) with { CapturedAt = DateTimeOffset.UnixEpoch.AddDays(1) });
        project.StartSession("Three", DateTimeOffset.UnixEpoch.AddDays(2)).AddPage(Cards("https://shop.example/cards", 3) with { CapturedAt = DateTimeOffset.UnixEpoch.AddDays(2) });

        var sinceMade = service.Diff(null, "/cards");
        var sinceTwo = service.Diff(null, "/cards", fromSession: "1970-01-02-two");
        var status = service.PageObjects(null)["/cards"];

        Assert.Equal(10, sinceMade.AddedCount); // two cards of five nodes
        Assert.Equal(5, sinceTwo.AddedCount);
        Assert.Equal(("CardsPage", true), (status.PageObject, status.Changed));
    }

    [Fact]
    public async Task A_page_object_made_from_a_capture_is_matched_and_known_to_be_current_until_the_page_changes()
    {
        var project = MakeProject("Shop.UITests");
        project.StartSession("One", DateTimeOffset.UnixEpoch).AddPage(Cards("https://shop.example/cards", 2) with { CapturedAt = DateTimeOffset.UnixEpoch });
        await using var service = new ScraperService(_root);
        var pageHeader = service.Header(null, "/cards");
        var cardHeader = service.Header(null, "/cards", node: 2);
        File.WriteAllText(Path.Combine(project.Folder, "CardsPage.cs"), pageHeader + "\nclass CardsPage : HtmlPageObjectBase<CardsPage> { }");
        File.WriteAllText(Path.Combine(project.Folder, "Card.cs"), cardHeader + "\nclass Card<T> : ContainerBase<T, Card<T>> { }");

        var before = service.Match(null, "/cards");
        project.StartSession("Two", DateTimeOffset.UnixEpoch.AddDays(1)).AddPage(Cards("https://shop.example/cards", 3) with { CapturedAt = DateTimeOffset.UnixEpoch.AddDays(1) });
        var after = service.Match(null, "/cards");

        Assert.StartsWith("// <scraped page=\"/cards\" from=\"1970-01-01-one/001-page.nodes.jsonl\" hash=\"", pageHeader);
        Assert.Equal("CardsPage", before.PageObject);
        Assert.False(before.ChangedSinceGenerated);
        Assert.Equal([2, 7], before.Matches.Select(m => m.Node));
        Assert.All(before.Matches, m => Assert.Equal(["Card<T>"], m.Controls));
        Assert.True(after.ChangedSinceGenerated);
        Assert.Equal(3, after.Matches.Count);
    }
}
