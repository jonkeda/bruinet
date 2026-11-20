using Bruinet.Scraper.Code;
using Bruinet.Scraper.Factory;
using Bruinet.Scraper.Places;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Recordings;
using Bruinet.Scraper.Service;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Tests.Service;

// Replay and the examples: a change to a sign or a template is seen from the kept captures, without a browser.
public sealed class ReplayTests : IAsyncLifetime
{
    private const string Dialog = "/contact › + Contact";
    private static readonly ScrapeBounds OnScreen = new(0, 0, 100, 20);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));
    private string _project = "";
    private ScraperService _service = null!;
    private PlacesList _list = null!;

    public Task InitializeAsync()
    {
        _project = Path.Combine(_root, "Shop.UITests");
        Directory.CreateDirectory(Path.Combine(_project, "Pages"));
        File.WriteAllText(Path.Combine(_project, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        WriteConfig(".v-select");
        WriteTemplates("SelectRow");
        File.WriteAllText(Path.Combine(_project, "Pages", "ContactsPage.cs"), """
            namespace Shop.UITests.Pages;

            public sealed class ContactsPage(IHtmlTestContext context) : PageObjectBase<ContactsPage>(context, "/contact");

            /// <summary>What a person wrote for the same dialog.</summary>
            public sealed class ContactForm(ContactsPage page)
            {
                public SelectRow<ContactForm> Country => new(this, "Country");

                public TextRow<ContactForm> Nickname => new(this, "Nickname");

                public TextRow<ContactForm> Fax => new(this, "Fax");
            }
            """);

        var session = Path.Combine(_project, ScrapeProject.RecordingsFolderName, "walk");
        Directory.CreateDirectory(session);
        SnapshotFile.Write(Capture(), Path.Combine(session, "001.nodes.jsonl"));

        _list = new PlacesList();
        _list.Add(new Place { Key = "/contact", Kind = PlaceKind.Page, State = PlaceState.Seen, Address = "/contact" });
        _list.Add(new Place { Key = Dialog, Kind = PlaceKind.Dialog, State = PlaceState.FormNotSaved, Parent = "/contact", Snapshot = "walk/001.nodes.jsonl", Fields = ["Country (text, VSelect)"], Buttons = ["Create"] });
        _service = new ScraperService(_root);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _service.DisposeAsync();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void A_class_drafted_from_a_kept_place_replays_the_same()
    {
        WriteDraft();

        var report = _service.ReplayPlaces(null, _list);

        Assert.True(report.Same, string.Join("; ", report.Changed.Select(change => $"{change.Class}: {change.Note}")));
        Assert.Equal((1, 1), (report.Places, report.Classes));
        Assert.Empty(report.CannotDraft);
    }

    [Fact]
    public void A_sign_that_no_longer_fits_shows_as_a_field_that_lost_its_control_and_a_class_that_lost_a_member()
    {
        WriteDraft();
        WriteConfig(".v-something-else");

        var report = new ScraperService(_root).ReplayPlaces(null, _list);

        var field = Assert.Single(report.Fields);
        Assert.Equal((Dialog, "Country", "Country (text, VSelect)", "Country (text)"), (field.Place, field.Label, field.Before, field.After));
        Assert.Equal((0, 1), (report.ControlsGained, report.ControlsLost));
        var changed = Assert.Single(report.Changed);
        Assert.Equal("ContactDialog", changed.Class);
        Assert.Contains("Country", changed.Removed);
        Assert.False(report.Same);
    }

    [Fact]
    public void A_template_that_changed_shows_as_the_same_members_written_differently_with_the_first_line_that_differs()
    {
        WriteDraft();
        WriteTemplates("ChoiceRow");

        var report = _service.ReplayPlaces(null, _list);

        Assert.Empty(report.Fields);
        var changed = Assert.Single(report.Changed);
        Assert.Empty(changed.Added);
        Assert.Empty(changed.Removed);
        Assert.Contains("ChoiceRow<ContactDialog> Country", changed.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void A_place_that_owes_a_class_is_said_to_draft_or_not_with_why()
    {
        // Nothing drafted yet: the dialog owes a class, and its page has one, so it can be drafted.
        Assert.Equal(1, _service.ReplayPlaces(null, _list).WouldDraft);

        File.Delete(Path.Combine(_project, "Pages", "ContactsPage.cs"));
        var without = _service.ReplayPlaces(null, _list);

        Assert.Equal(0, without.WouldDraft);
        Assert.Contains(without.CannotDraft, each => each.Place == Dialog && each.Reason.Contains("No page class", StringComparison.Ordinal));
    }

    [Fact]
    public void An_example_is_held_against_its_place_and_each_difference_says_who_is_right()
    {
        File.AppendAllText(Path.Combine(_project, ScraperConfig.FileName), $"\n## Examples\n\n| Place | Class |\n| --- | --- |\n| `{Dialog}` | ContactForm |\n| `/nowhere` | ContactForm |\n| `{Dialog}` | NoSuchClass |\n");

        var report = new ScraperService(_root).BenchPlaces(null, _list);

        Assert.Equal(3, report.Examples);
        // The page shows "Nickname" as a text and the reading gives no such field: the example is right.
        Assert.Contains(new BenchDifference("ContactForm", Dialog, "Nickname", BenchVerdict.ExampleIsRight), report.Differences);
        // The capture shows no "Fax" at all.
        Assert.Contains(new BenchDifference("ContactForm", Dialog, "Fax", BenchVerdict.NotOnThePage), report.Differences);
        // The place has a Create button the example does not name.
        Assert.Contains(new BenchDifference("ContactForm", Dialog, "Create", BenchVerdict.DraftIsRight), report.Differences);
        Assert.Equal(1, report.Missed);
        Assert.Equal(2, report.Problems.Count);
    }

    [Fact]
    public void A_place_s_draft_goes_to_its_class_s_own_file_and_an_older_draft_keeps_its_file_as_a_part()
    {
        var (_, file) = _service.DraftPlace(null, _list, Dialog, PlaceDraftKind.Form);

        Assert.EndsWith(".cs", file, StringComparison.Ordinal);
        Assert.False(PageDrafter.IsOlderDraft(file!));

        // A class drafted before a draft was the whole class: its file is there, beside a person's part.
        var older = file![..^PageDrafter.Extension.Length] + PageDrafter.OlderExtension;
        Directory.CreateDirectory(Path.GetDirectoryName(older)!);
        File.WriteAllText(older, "");

        var (draft, again) = _service.DraftPlace(null, _list, Dialog, PlaceDraftKind.Form);

        Assert.Equal(older, again);
        Assert.Contains($" partial class {draft.ClassName}(", draft.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void A_draft_written_into_a_file_that_is_there_is_merged_and_a_person_s_class_is_only_added_to()
    {
        var (draft, file) = _service.DraftPlace(null, _list, Dialog, PlaceDraftKind.Form);
        Assert.Null(ScraperService.WriteDraft(file!, draft));
        Assert.Equal(draft.Code, File.ReadAllText(file!));

        // A person adds a member to the drafted class: drafting again keeps it.
        File.WriteAllText(file!, draft.Code.TrimEnd()[..^1] + "\n    public int Mine => 1;\n}\n");
        var again = ScraperService.WriteDraft(file!, draft)!;
        Assert.True(again.Same);
        Assert.Contains("Mine", again.Kept);
        Assert.Contains("public int Mine => 1;", File.ReadAllText(file!), StringComparison.Ordinal);

        // A class of that name a person wrote, with its own Country: the draft adds what it lacks and changes nothing else.
        var hand = $"namespace Shop.UITests.Pages;\n\npublic sealed class {draft.ClassName}\n{{\n    public int Country => 2;\n}}\n";
        File.WriteAllText(file!, hand);
        var added = ScraperService.WriteDraft(file!, draft)!;
        Assert.DoesNotContain("Country", added.Added);
        Assert.NotEmpty(added.Added);
        var text = File.ReadAllText(file!);
        Assert.Contains("public int Country => 2;", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<scraped", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_page_s_class_gets_the_member_a_dialog_s_test_names_and_no_other()
    {
        _list.Set(_list["/contact"]! with { Buttons = ["Export", "Import"] });
        File.AppendAllText(Path.Combine(_project, DraftTemplates.FileName), "\n## Form page\n\n```csharp\n{Header}\nnamespace {Namespace};\n\npublic sealed class {Self}(IHtmlTestContext context) : PageObjectBase<{Self}>(context)\n{\n{Members}\n}\n```\n");
        var project = _service.Projects().Single();
        var (page, _) = _service.DraftPlace(null, _list, "/contact", PlaceDraftKind.Form, "ContactsPage");
        Assert.Contains(page.Members, member => member.Name == "Export");
        var file = Path.Combine(_project, "Pages", "ContactsPage.cs");
        var before = File.ReadAllText(file);

        // A test that names nothing the page's draft has leaves the class alone.
        Assert.Null(_service.GivePageWhatTheTestNames(project, _list, "/contact", "page.Somewhere.Click();"));
        Assert.Equal(before, File.ReadAllText(file));

        var given = _service.GivePageWhatTheTestNames(project, _list, "/contact", "page.Export.Click();");

        Assert.Equal((file, before, "ContactsPage"), given);
        var after = File.ReadAllText(file);
        Assert.Contains(" Export ", after, StringComparison.Ordinal);
        Assert.DoesNotContain(" Import ", after, StringComparison.Ordinal);
        // The class had no body; it has one now, and the person's other class is as it was.
        Assert.Contains("PageObjectBase<ContactsPage>(context, \"/contact\")", after, StringComparison.Ordinal);
        Assert.Contains("public TextRow<ContactForm> Fax", after, StringComparison.Ordinal);
        Assert.DoesNotContain("<scraped", after, StringComparison.Ordinal);
        // The same test again adds nothing.
        Assert.Null(_service.GivePageWhatTheTestNames(project, _list, "/contact", "page.Export.Click();"));
    }

    private void WriteDraft()
    {
        var (draft, file) = _service.DraftPlace(null, _list, Dialog, PlaceDraftKind.Form);
        Directory.CreateDirectory(Path.GetDirectoryName(file!)!);
        File.WriteAllText(file!, draft.Code);
    }

    private void WriteConfig(string selectSign) => File.WriteAllText(Path.Combine(_project, ScraperConfig.FileName), $"""
        # Scraper Config

        ## App

        | Field | Value |
        | --- | --- |
        | Platform | html |
        | StartUrl | https://shop.example/ |
        | Namespace | Shop.UITests.Pages |

        ## Control signs

        | Control | Sign | State |
        | --- | --- | --- |
        | VSelect | {selectSign} | made |

        """);

    private void WriteTemplates(string selectRow) => File.WriteAllText(Path.Combine(_project, DraftTemplates.FileName), $$"""
        # Draft templates

        ## Field members

        | Control | Kind | Member |
        | --- | --- | --- |
        | VSelect | | `public {{selectRow}}<{Self}> {Name} => new(this, "{Label}");` |

        ## Button member

        ```csharp
        public Button<{Self}> {Name} => new(this, "{Label}");
        ```

        ## Dialog

        ```csharp
        {Header}
        namespace {Namespace};

        /// <summary>{Summary}</summary>
        public sealed partial class {Self}({Page} page) : PageObjectBase<{Self}>(page)
        {
        {Members}
        }
        ```
        """);

    // A dialog with one select the sign recognizes, a text that is no field, and a button.
    private static ScrapeSnapshot Capture() => new()
    {
        Platform = "html",
        Location = "https://shop.example/contact",
        Title = "Contacts",
        Nodes =
        [
            N(1, 0, "body"),
            N(2, 1, "div", null, ("role", "dialog")),
            N(3, 2, "div", null, ("class", "v-input v-text-field v-select")),
            N(4, 3, "label", "Country"),
            N(5, 3, "input", null, ("type", "text")),
            N(6, 2, "span", "Nickname"),
            N(7, 2, "button", "Create"),
        ],
    };

    private static ScrapeNode N(int number, int parent, string type, string? text = null, params (string Key, string Value)[] attributes) => new()
    {
        Number = number,
        Parent = parent,
        Type = type,
        Text = text,
        Bounds = OnScreen,
        Role = attributes.FirstOrDefault(a => a.Key == "role").Value,
        Attributes = attributes.Where(a => a.Key != "role").ToDictionary(a => a.Key, a => a.Value),
    };
}
