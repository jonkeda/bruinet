using Bruinet.Scraper.Factory;
using Bruinet.Scraper.Places;

namespace Bruinet.Scraper.Tests.Places;

// A field's line read one way, how a place is reached, the kind of a place, and what a list still owes.
public sealed class PlaceModelTests
{
    [Theory]
    [InlineData("Name (text)")]
    [InlineData("Name (text, VTextField, required)")]
    [InlineData("Status (combobox, VSelect?, disabled)")]
    [InlineData("Landing page (text, VSelect, in a Card)")]
    [InlineData("Search (text, in a table)")]
    [InlineData("Total (hours) (number, VTextField, required, disabled, in a FormRow)")]
    public void A_field_s_line_reads_and_writes_back_the_same(string line) =>
        Assert.Equal(line, PlaceField.Parse(line).ToText());

    [Fact]
    public void A_field_s_line_says_its_label_kind_control_and_marks()
    {
        var field = PlaceField.Parse("Total (hours) (number, VTextField?, required, in a Card)");

        Assert.Equal(("Total (hours)", "number", "VTextField", false), (field.Label, field.Kind, field.Control, field.ControlMade));
        Assert.True(field.Required);
        Assert.False(field.Disabled);
        Assert.Equal("Card", field.Holder);
        Assert.False(field.HasControl);
    }

    [Fact]
    public void A_field_has_a_control_when_a_sign_named_a_made_one_or_it_is_a_table_s_own()
    {
        Assert.True(PlaceField.Parse("Name (text, VTextField)").HasControl);
        Assert.True(PlaceField.Parse("Search (text, in a table)").HasControl);
        Assert.False(PlaceField.Parse("Name (text)").HasControl);
        Assert.False(PlaceField.Parse("Time (text, VTimeField?)").HasControl);
        Assert.Equal(("just a label", ""), (PlaceField.Parse("just a label").Label, PlaceField.Parse("just a label").Kind));
    }

    [Theory]
    [InlineData("+ Contact", false, false, OpenerKind.AddButton)]
    [InlineData("Avatar: Edit", false, false, OpenerKind.CardButton)]
    [InlineData("Edit", false, true, OpenerKind.RowButton)]
    [InlineData("the first row", false, true, OpenerKind.Row)]
    [InlineData("the first row (2nd table)", false, true, OpenerKind.Row)]
    [InlineData("Financial", true, false, OpenerKind.Tab)]
    [InlineData("Create PDF", false, false, OpenerKind.Button)]
    public void A_press_is_a_kind_of_opener(string label, bool isTab, bool inRow, OpenerKind expected) =>
        Assert.Equal(expected, PlaceOpener.KindOf(label, isTab, inRow));

    [Fact]
    public void How_a_place_is_reached_is_what_the_walk_kept_else_what_its_key_and_via_say()
    {
        var kept = new Place { Key = "/contact › Edit", Kind = PlaceKind.Dialog, Parent = "/contact", Via = "'Edit' on /contact", Opener = OpenerKind.RowButton };
        var old = kept with { Opener = OpenerKind.Unknown };
        var page = new Place { Key = "/contact", Kind = PlaceKind.Page, Address = "/contact", Via = "a link on /dashboard" };
        var tab = new Place { Key = "/contact#/view/{id} › Files", Kind = PlaceKind.Tab, Parent = "/contact#/view/{id}" };

        Assert.Equal(new PlaceOpener(OpenerKind.RowButton, "Edit", "/contact"), PlaceOpener.Of(kept));
        Assert.Equal(OpenerKind.Button, PlaceOpener.Of(old).Kind);
        Assert.Equal(new PlaceOpener(OpenerKind.Address, "", "/dashboard"), PlaceOpener.Of(page));
        Assert.Equal(new PlaceOpener(OpenerKind.Tab, "Files", "/contact#/view/{id}"), PlaceOpener.Of(tab));
    }

    [Fact]
    public void A_list_keeps_how_a_place_was_reached_when_it_is_found_again_without_it()
    {
        var list = new PlacesList();
        list.Add(new Place { Key = "/contact › Edit", Kind = PlaceKind.Dialog, State = PlaceState.Seen, Parent = "/contact", Opener = OpenerKind.RowButton });

        list.Add(new Place { Key = "/contact › Edit", Kind = PlaceKind.Dialog, State = PlaceState.Seen, Parent = "/contact" });

        Assert.Equal(OpenerKind.RowButton, list["/contact › Edit"]!.Opener);
    }

    [Fact]
    public void A_place_s_kind_is_read_from_what_the_list_holds_on_it()
    {
        var list = Shop();

        Assert.Equal(PlaceGroupKind.ListWithDialog, PlaceKinds.Of(list["/contact"]!, list));
        Assert.Equal(PlaceGroupKind.RowDialog, PlaceKinds.Of(list["/contact › Edit"]!, list));
        Assert.Equal(PlaceGroupKind.Dialog, PlaceKinds.Of(list["/contact › + Contact"]!, list));
        Assert.Equal(PlaceGroupKind.RecordPage, PlaceKinds.Of(list["/contact#/view/{id}"]!, list));
        Assert.Equal(PlaceGroupKind.FormPage, PlaceKinds.Of(list["/contact#/create"]!, list));
        Assert.Equal(PlaceGroupKind.SettingsPage, PlaceKinds.Of(list["/settings"]!, list));
        Assert.Equal(PlaceGroupKind.TabbedPage, PlaceKinds.Of(list["/orders"]!, list));
        Assert.Equal(PlaceGroupKind.Nothing, PlaceKinds.Of(list["/about"]!, list));
        Assert.Equal(PlaceGroupKind.Part, PlaceKinds.Of(list["/orders › Open"]!, list));
    }

    [Theory]
    [InlineData("/project/view#/planning › + Phase", "/project/view#/planning", "/project")]
    [InlineData("/my-account#/tariffs", null, "/my-account")]
    [InlineData("/contact", null, "/contact")]
    [InlineData("/report?kind=1", null, "/report")]
    public void A_place_s_group_is_the_first_part_of_its_page_s_address(string key, string? parent, string group) =>
        Assert.Equal(group, PlaceKinds.GroupOf(new Place { Key = key, Parent = parent }));

    [Fact]
    public void What_is_owed_is_counted_by_group_against_the_suite_s_source()
    {
        var suite = new SuiteSource("""
            public sealed class ContactsPage(IHtmlContext context) : Bouw7Page<ContactsPage>(context, "/contact");
            // a page that opens itself names its address in quotes
            public sealed class ContactViewPage(IHtmlContext context) : PageAt<ContactViewPage>(context, "/contact#/view");
            """, new HashSet<string>(StringComparer.Ordinal) { "/contact › + Contact" });

        var report = Owed.Of(Shop(), suite);

        var contact = report.Groups.Single(group => group.Group == "/contact");
        Assert.Equal(["/contact#/create"], contact.PagesWithoutClass);
        Assert.Equal(["/contact › Edit"], contact.DialogFormsNotDrafted);
        Assert.Empty(contact.RecordPages);
        Assert.Equal(3, contact.Pages);
        Assert.Equal(1, contact.FieldsWithoutControl);
        Assert.Equal((3, 1, 0), (report.PagesWithoutClass, report.DialogFormsNotDrafted, report.RecordPages));
        Assert.Equal((6, 5), (report.Fields, report.FieldsWithControl));
        // The one that owes most comes first.
        Assert.Equal("/contact", report.Groups[0].Group);
    }

    [Fact]
    public void A_page_of_one_record_without_a_class_is_owed_apart_and_a_form_shown_twice_counts_once()
    {
        var list = Shop();
        list.Add(new Place { Key = "/contact › fa-pen", Kind = PlaceKind.Dialog, State = PlaceState.FormNotSaved, Parent = "/contact", SameAs = "/contact › Edit", Fields = ["Name (text, VTextField)"] });

        var report = Owed.Of(list, new SuiteSource("", new HashSet<string>(StringComparer.Ordinal)));

        var contact = report.Groups.Single(group => group.Group == "/contact");
        Assert.Equal(["/contact#/view/{id}"], contact.RecordPages);
        Assert.Equal(2, contact.DialogFormsNotDrafted.Count);
        Assert.Equal(6, report.Fields);
    }

    // A small app: a list with two dialogs, a record's page, a form page, a settings page, a page with tabs, a page of nothing.
    private static PlacesList Shop()
    {
        var list = new PlacesList();
        foreach (var place in new[]
        {
            new Place { Key = "/contact", Kind = PlaceKind.Page, State = PlaceState.Seen, Address = "/contact", Columns = ["Name", "City"], Fields = ["Search (text, in a table)"] },
            new Place { Key = "/contact › + Contact", Kind = PlaceKind.Dialog, State = PlaceState.FormNotSaved, Parent = "/contact", Via = "'+ Contact' on /contact", Fields = ["Name (text, VTextField, required)"] },
            new Place { Key = "/contact › Edit", Kind = PlaceKind.Dialog, State = PlaceState.FormNotSaved, Parent = "/contact", Via = "'Edit' on /contact", Opener = OpenerKind.RowButton, Fields = ["Name (text, VTextField)"] },
            new Place { Key = "/contact#/view/{id}", Kind = PlaceKind.Page, State = PlaceState.Seen, Address = "/contact#/view/12" },
            new Place { Key = "/contact#/create", Kind = PlaceKind.Page, State = PlaceState.FormNotSaved, Address = "/contact#/create", Fields = ["Name (text, VTextField, required)", "Time (text)"] },
            new Place { Key = "/settings", Kind = PlaceKind.Page, State = PlaceState.Seen, Address = "/settings", Fields = ["Landing page (text, VSelect, in a Card)"] },
            new Place { Key = "/orders", Kind = PlaceKind.Page, State = PlaceState.Seen, Address = "/orders", Tabs = ["Open", "Closed"], Columns = ["Number"] },
            new Place { Key = "/orders › Open", Kind = PlaceKind.Tab, State = PlaceState.Seen, Parent = "/orders" },
            new Place { Key = "/about", Kind = PlaceKind.Page, State = PlaceState.Known, Address = "/about" },
        })
        {
            list.Add(place);
        }

        return list;
    }
}
