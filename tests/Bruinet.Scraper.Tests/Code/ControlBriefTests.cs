using Bruinet.Core.Scraping;
using Bruinet.Scraper.Code;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;

namespace Bruinet.Scraper.Tests.Code;

public sealed class ControlBriefTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bruinet-brief-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private ScrapeProject MakeProject(string name, string config = "")
    {
        var folder = Path.Combine(_root, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name + ".csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), "## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | https://shop.example/ |\n" + config);
        return ScrapeProject.Open(folder);
    }

    private static ScrapeNode N(int n, int p, string type, string? role = null, string? text = null, params (string Key, string Value)[] attributes) => new()
    {
        Number = n,
        Parent = p,
        Type = type,
        Role = role,
        Text = text,
        Attributes = attributes.ToDictionary(a => a.Key, a => a.Value),
    };

    // A select made of divs, as a UI library draws one: a field with a label, a box that pops a list
    // up, a read-only input showing the choice. Without a label when unlabelled.
    private static IEnumerable<ScrapeNode> Select(int at, int parent, string label, bool labelled = true)
    {
        yield return N(at, parent, "div", attributes: ("class", "field field--dense"));
        yield return N(at + 1, at, "div", attributes: ("class", "field__control"));
        yield return N(at + 2, at + 1, "div", "button", null, ("class", "field__slot"), ("aria-haspopup", "listbox"), ("aria-expanded", "false"));
        yield return labelled ? N(at + 3, at + 2, "label", text: label) : N(at + 3, at + 2, "span");
        yield return N(at + 4, at + 2, "input", attributes: [("type", "text"), ("readonly", "readonly"), ("placeholder", "Select")]);
    }

    private static ScrapeSnapshot Page(string location, params ScrapeNode[] nodes) =>
        new() { Platform = "html", Location = location, Nodes = [N(1, 0, "body"), .. nodes], CapturedAt = DateTimeOffset.UnixEpoch };

    [Fact]
    public async Task A_brief_says_where_to_write_and_what_to_read_and_gives_the_facts_and_hints()
    {
        var project = MakeProject("Shop.UITests");
        var session = project.StartSession("One", DateTimeOffset.UnixEpoch);
        session.AddPage(Page("https://shop.example/orders", [.. Select(2, 1, "Status"), .. Select(7, 1, "Type")]));
        session.AddPage(Page("https://shop.example/customers", [.. Select(2, 1, "Country", labelled: false)]));
        await using var service = new ScraperService(_root);

        var brief = service.ControlBrief(null, "div||field__control|div", "StatusSelect");

        Assert.Equal("StatusSelect", brief.Name);
        Assert.False(brief.Cut);
        var text = brief.Text;
        Assert.Contains("## StatusSelect (3× on 2 pages)", text);
        Assert.Contains($"Write:      {Path.Combine(project.Folder, "Controls", "StatusSelect.tpl.cs")}", text);
        Assert.Contains("Namespace:  Shop.UITests.Controls", text);
        Assert.Contains("First line: // <scraped control=\"div||field__control|div\"", text);
        Assert.Contains("public partial class StatusSelect<TScope> : SelectorControlBase<TScope>", text);
        Assert.Contains("html-control", text);
        Assert.Contains("#2 div .field.field--dense", text); // the parent, above the pattern's node
        Assert.Matches(@"div\.field__slot\s+3 of 3\s+selector \(aria-haspopup=listbox on role=button\)", text);
        Assert.Matches(@"label\s+2 of 3\s+readable", text);
        Assert.Contains("Example (/orders, node 3, with its parent)", text); // the labelled one shows more
        Assert.Contains("Kind: a simple control: selector", text);
        Assert.Contains("SelectItem(text)", text);
        Assert.Contains("not an order", text);
    }

    // A field as many UI libraries draw it: a block (field) holding its label and an inner box
    // (field__box) around the input. A select's box pops a list up; a text field's input is editable.
    // Each block has a class of its own too (f-status), so the blocks are not one pattern by themselves.
    private static IEnumerable<ScrapeNode> Field(int at, int parent, string label, bool select)
    {
        yield return N(at, parent, "div", attributes: ("class", $"field f-{label.ToLowerInvariant()}{(select ? " field--select" : "")}"));
        yield return N(at + 1, at, "label", text: label);
        yield return N(at + 2, at, "div", attributes: ("class", "field__box"));
        yield return select
            ? N(at + 3, at + 2, "div", "button", null, ("aria-haspopup", "listbox"))
            : N(at + 3, at + 2, "div");
        yield return N(at + 4, at + 2, "input", attributes: select ? [("type", "text"), ("readonly", "readonly")] : [("type", "text")]);
    }

    [Fact]
    public async Task An_inner_box_is_lifted_to_the_field_that_holds_its_label_and_split_by_what_a_test_does_with_it()
    {
        var project = MakeProject("Form.UITests");
        var session = project.StartSession("One", DateTimeOffset.UnixEpoch);
        var form = N(2, 1, "form");
        session.AddPage(Page("https://shop.example/a", [form, .. Field(3, 2, "Status", select: true), .. Field(8, 2, "Name", select: false)]));
        session.AddPage(Page("https://shop.example/b", [form, .. Field(3, 2, "Country", select: true), .. Field(8, 2, "City", select: false)]));
        await using var service = new ScraperService(_root);

        var patterns = service.Patterns(null).Where(p => p.Signature.StartsWith("div||field__box", StringComparison.Ordinal)).ToArray();
        var select = Assert.Single(patterns, p => p.Variant == "selector");
        var text = Assert.Single(patterns, p => p.Variant == "text");
        var brief = service.ControlBrief(null, select.Signature, select.Name, select.Variant, select.Lifted).Text;

        Assert.True(select.Lifted);
        Assert.Equal((2, 2), (select.Occurrences, text.Occurrences));
        Assert.All(select.Examples, e => Assert.StartsWith($"#{3} div", e.Line));
        Assert.Equal("FieldSelect", select.Name);
        Assert.Equal("FieldText", text.Name);
        Assert.Contains("variant=\"selector\" root=\"lifted\"", brief);
        Assert.Matches(@"label\s+2 of 2\s+readable", brief);
        Assert.Contains("← this brief", brief);
    }

    [Fact]
    public async Task Rows_under_one_parent_make_a_collection_whose_brief_gives_the_rows_parts_and_keys()
    {
        var project = MakeProject("List.UITests");
        static IEnumerable<ScrapeNode> Rows(int count)
        {
            yield return N(2, 1, "ul", attributes: ("class", "orders"));
            for (var i = 0; i < count; i++)
            {
                var at = 3 + i * 3;
                yield return N(at, 2, "li", attributes: [("class", "order"), ("data-id", $"o-{i}")]);
                yield return N(at + 1, at, "span", text: $"Order {i}");
                yield return N(at + 2, at, "button", text: "Open");
            }
        }
        var session = project.StartSession("One", DateTimeOffset.UnixEpoch);
        session.AddPage(Page("https://shop.example/a", [.. Rows(3)]));
        session.AddPage(Page("https://shop.example/b", [.. Rows(2)]));
        await using var service = new ScraperService(_root);

        var collection = Assert.Single(service.Patterns(null), p => p.Kind == PatternKind.Collection);
        var brief = service.ControlBrief(null, collection.Signature, collection.Name).Text;

        Assert.Equal("li||order|span,button", collection.Row);
        Assert.Equal(2, collection.Occurrences);
        Assert.Contains("a collection with rows", brief);
        Assert.Contains("Rows: li||order|span,button", brief);
        Assert.Matches(@"button\s+5 of 5\s+clickable", brief);
        Assert.Contains("data-id (unique on every row, e.g. o-0)", brief);
        Assert.Contains(".cs   (new file; a container is written by hand", brief);
    }

    [Fact]
    public async Task A_brief_shows_the_list_a_select_owns_and_what_Explore_saw_when_it_clicked_one()
    {
        var project = MakeProject("Owns.UITests");
        static IEnumerable<ScrapeNode> Picker(bool open)
        {
            yield return N(2, 1, "div", attributes: ("class", "picker"));
            yield return N(3, 2, "label", text: "Status");
            yield return N(4, 2, "div", "button", "Open", ("aria-haspopup", "listbox"), ("aria-owns", "list-1"), ("aria-expanded", open ? "true" : "false"));
            yield return N(5, 4, "input", attributes: [("type", "text"), ("readonly", "readonly")]);
            if (!open)
                yield break;
            yield return N(6, 1, "div", "listbox", attributes: ("id", "list-1"));
            yield return N(7, 6, "div", "option", "Open");
            yield return N(8, 6, "div", "option", "Closed");
        }
        var session = project.StartSession("Explore", DateTimeOffset.UnixEpoch);
        session.AddPage(Page("https://shop.example/a", [.. Picker(open: false)]));
        session.AddPage(Page("https://shop.example/b", [.. Picker(open: false)]));
        session.AddPage(Page("https://shop.example/a", [.. Picker(open: true)]) with { CapturedAt = DateTimeOffset.UnixEpoch.AddMinutes(1) });
        File.WriteAllText(Path.Combine(session.Folder, "explore.md"), "# Explore\n\n## Captures\n\n| # | Page | Via | Depth |\n| --- | --- | --- | --- |\n| 1 | `/a` | the start URL | 0 |\n| 3 | `/a` | click 'Open' on /a | 1 |\n");
        await using var service = new ScraperService(_root);

        var brief = service.ControlBrief(null, "div||picker|label,div", "StatusPicker").Text;

        Assert.Contains("#4 aria-owns=list-1: #6 div #list-1 role=listbox, outside the control; 2 options: \"Open\", \"Closed\"", brief);
        Assert.Contains("clicked 'Open' on /a (1970-01-01-explore capture #3): appeared role=listbox ×1, role=option ×2; +3 nodes", brief);
    }

    [Fact]
    public async Task The_configs_components_name_what_the_rules_cannot_tell_and_win()
    {
        var project = MakeProject("Given.UITests", "\n## Components\n\n| Match | Kind | Name |\n| --- | --- | --- |\n| selector: .tile | component | ProductTile |\n");
        static IEnumerable<ScrapeNode> Tiles()
        {
            yield return N(2, 1, "div", attributes: ("class", "tile"));
            yield return N(3, 2, "div", text: "Shoes");
            yield return N(4, 2, "div", text: "Shoes on sale");
        }
        var session = project.StartSession("One", DateTimeOffset.UnixEpoch);
        session.AddPage(Page("https://shop.example/a", [.. Tiles()]));
        session.AddPage(Page("https://shop.example/b", [.. Tiles()]));
        await using var service = new ScraperService(_root);

        var tile = Assert.Single(service.Patterns(null), p => p.Given is not null);
        var brief = service.ControlBrief(null, tile.Signature).Text;

        Assert.Equal(("ProductTile", "component", 2), (tile.Name, tile.Given, tile.Occurrences));
        Assert.Contains("## ProductTile", brief);
        Assert.Contains("The config's ## Components names it: component, ProductTile (selector: .tile). That wins.", brief);
        Assert.Contains("Kind: a component", brief);
    }

    [Fact]
    public async Task The_agent_and_model_are_the_configs_until_the_person_chooses_their_own()
    {
        MakeProject("Chat.UITests", "\n## Agent\n\n| Field | Value |\n| --- | --- |\n| Agent | agent |\n| Model | gpt-4.1 |\n");
        await using var service = new ScraperService(_root);

        var configured = service.ChatChoice(null);
        service.SetChatChoice(null, "bruinet-scraper", "");
        var chosen = service.ChatChoice(null);

        Assert.Equal(new ChatChoice("agent", "gpt-4.1"), configured);
        Assert.Equal(new ChatChoice("bruinet-scraper", ""), chosen);
    }

    [Fact]
    public void The_controls_namespace_swaps_the_pages_folder_for_the_controls_folder()
    {
        var plain = MakeProject("Plain.UITests");
        var named = MakeProject("Named.UITests", "| Namespace | Named.Tests.Pages |\n");

        Assert.Equal("Plain.UITests.Controls", ControlBrief.ControlsNamespace(plain));
        Assert.Equal("Named.Tests.Controls", ControlBrief.ControlsNamespace(named));
    }
}
