using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Code;
using Bruinet.Scraper.Places;

namespace Bruinet.Scraper.Tests.Code;

/// <summary>A class drafted from a place of the list, in a suite's own templates.</summary>
public sealed class PlaceDrafterTests
{
    private static readonly DraftTemplates Templates = DraftTemplates.Parse(
        """
        # Draft templates

        ## Field members

        | Control | Kind | Member |
        | --- | --- | --- |
        | VTextField | number | `public NumberRow<{Self}> {Name} => new(this, "{Label}");` |
        | VTextField | | `public TextRow<{Self}> {Name} => new(this, "{Label}");` |
        | VSelect | | `public SelectRow<{Self}> {Name} => new(this, "{Label}");` |

        ## Button member

        ```csharp
        [BddControl("{Label}")]
        public Button<{Self}> {Name} => new(this, "{Label}");
        ```

        ## Dialog

        ```csharp
        {Header}
        namespace {Namespace};

        /// <summary>{Summary}</summary>
        public sealed partial class {Self}({Page} page) : Dialog<{Page}, {Self}>(page)
        {
        {Members}
        }
        ```

        ## Row

        ```csharp
        {Header}
        namespace {Namespace};

        public sealed partial class {Self} : Row<{Self}>
        {
        {Members}
        }
        ```

        ## Column member

        ```csharp
        public Label<{Self}> {Name} => Cell("{Label}");
        ```
        """);

    // The app's icons are named by a class that starts so: a button or a column that shows only an icon is no member.
    private static readonly PlaceSigns Signs = new() { IconName = ["fa-"] };

    private static readonly Place DayOff = new()
    {
        Key = "/day-off › + Day off",
        Kind = PlaceKind.Dialog,
        Parent = "/day-off",
        State = PlaceState.FormNotSaved,
        Snapshot = "2026-11-18-walk-creator/002-day-off.nodes.jsonl",
        Fields = ["Description (text, VTextField, required)", "Days (number, VTextField)", "Type (text, VSelect)", "Logo (file, VFileInput?)", "Colour (text)", "Name (text, VTextField)"],
        Buttons = ["Cancel", "Create", "fa-close", "Save (disabled)"],
    };

    [Fact]
    public void A_form_gets_a_member_per_field_typed_by_its_control_and_one_per_button()
    {
        var draft = PlaceDrafter.Form(DayOff, Templates, PlaceDrafter.ClassName(DayOff, PlaceDraftKind.Form), "Shop.Pages", "DaysOffPage", signs: Signs);

        Assert.Equal("DayOffDialog", draft.ClassName);
        Assert.StartsWith("// <scraped page=\"/day-off › + Day off\" variant=\"form\" from=\"2026-11-18-walk-creator/002-day-off.nodes.jsonl\" hash=\"", draft.Code, StringComparison.Ordinal);
        Assert.Contains("public sealed partial class DayOffDialog(DaysOffPage page) : Dialog<DaysOffPage, DayOffDialog>(page)", draft.Code, StringComparison.Ordinal);
        Assert.Contains("    /// <summary>Required.</summary>\n    public TextRow<DayOffDialog> Description => new(this, \"Description\");", draft.Code, StringComparison.Ordinal);
        Assert.Contains("    public NumberRow<DayOffDialog> Days => new(this, \"Days\");", draft.Code, StringComparison.Ordinal);
        Assert.Contains("    public SelectRow<DayOffDialog> Type => new(this, \"Type\");", draft.Code, StringComparison.Ordinal);
        // A name the base has already says what it is, so the class builds.
        Assert.Contains("    public TextRow<DayOffDialog> NameField => new(this, \"Name\");", draft.Code, StringComparison.Ordinal);
        Assert.Contains("    [BddControl(\"Save\")]\n    public Button<DayOffDialog> Save => new(this, \"Save\");", draft.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("fa-close", draft.Code, StringComparison.Ordinal);

        // What has no control yet, or no sign, is said, not guessed.
        Assert.Equal(["Logo (file): its control, VFileInput, is still to make", "Colour (text): no sign says which control it is"], draft.NotDrafted);
        Assert.Contains("    // Not drafted:\n    // Logo (file): its control, VFileInput, is still to make", draft.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void A_name_the_suite_s_own_base_has_is_not_given_to_a_member()
    {
        // The suite says which names its bases have: a table of the templates.
        var templates = Templates with { Reserved = DraftTemplates.Parse("## Reserved names\n\n| Name |\n| --- |\n| `Title` |\n| `Create` |\n").Reserved };
        var complaint = DayOff with { Fields = ["Title (text, VTextField, required)", "Days (number, VTextField)"] };

        var draft = PlaceDrafter.Form(complaint, templates, "ComplaintDialog", "Shop.Pages", "DaysOffPage", signs: Signs);

        Assert.Contains("public TextRow<ComplaintDialog> TitleField => new(this, \"Title\");", draft.Code, StringComparison.Ordinal);
        Assert.Contains("public Button<ComplaintDialog> CreateField => new(this, \"Create\");", draft.Code, StringComparison.Ordinal);
        Assert.DoesNotContain(" Title =>", draft.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void A_card_s_dialog_on_a_record_s_page_gets_the_test_that_opens_the_page_on_a_record()
    {
        var templates = DraftTemplates.Parse("## Card form test\n\n```csharp\ncard {Page}.{OpenerMember}\n```\n\n## Record card form test\n\n```csharp\nrecord card {Page}.{OpenerMember}\n```\n");
        var page = new Place { Key = "/project/view?id={id}#/timeline", State = PlaceState.Seen };
        var dialog = new Place { Key = "/project/view?id={id}#/timeline › Timeline: fa-calendar", Kind = PlaceKind.Dialog, Parent = page.Key, State = PlaceState.FormNotSaved };

        var onRecord = PlaceDrafter.FormTest(dialog, templates, "TimelineDialogTests", "Shop.Tests", "TimelineDialog", "TimelinePage", signs: Signs, onPage: page);
        var onPlain = PlaceDrafter.FormTest(dialog, templates, "TimelineDialogTests", "Shop.Tests", "TimelineDialog", "TimelinePage", signs: Signs, onPage: page with { Key = "/timeline" });

        Assert.Equal("record card TimelinePage.EditTimeline\n", onRecord.Code);
        Assert.Equal("card TimelinePage.EditTimeline\n", onPlain.Code);
    }

    [Fact]
    public void A_card_with_two_buttons_that_each_open_a_dialog_gets_a_member_and_a_class_per_button()
    {
        var templates = DraftTemplates.Parse("## Form page\n\n```csharp\n{Members}\n```\n\n## Opener member\n\n```csharp\none {Name} of {Label}\n```\n\n## Opener member by icon\n\n```csharp\n{Name} of {Label} by {Icon}\n```\n\n## Card form test\n\n```csharp\ncard {Page}.{OpenerMember}\n```\n");
        var page = new Place { Key = "/timeline", State = PlaceState.Seen };
        var calendar = new Place { Key = "/timeline › Timeline: fa-calendar", Kind = PlaceKind.Dialog, Parent = page.Key, State = PlaceState.FormNotSaved };
        var list = calendar with { Key = "/timeline › Timeline: fa-list" };
        var avatar = calendar with { Key = "/timeline › Avatar: fa-pen" };
        Place[] places = [page, calendar, list, avatar];
        var beside = PlaceDrafter.Beside(places, calendar);

        Assert.Equal(["Timeline: fa-calendar", "Timeline: fa-list", "Avatar: fa-pen"], beside);
        Assert.Equal("TimelineCalendarDialog", PlaceDrafter.ClassName(calendar, PlaceDraftKind.Form, Signs, beside));
        Assert.Equal("TimelineListDialog", PlaceDrafter.ClassName(list, PlaceDraftKind.Form, Signs, beside));
        // A card with one button is named as it was, and so is a card whose page is not known.
        Assert.Equal("AvatarDialog", PlaceDrafter.ClassName(avatar, PlaceDraftKind.Form, Signs, beside));
        Assert.Equal("TimelineDialog", PlaceDrafter.ClassName(calendar, PlaceDraftKind.Form, Signs));

        var drafted = PlaceDrafter.Form(page, templates, "TimelinePage", "Shop.Pages", null, [.. beside], Signs);
        Assert.Contains("EditTimelineCalendar of Timeline by fa-calendar", drafted.Code, StringComparison.Ordinal);
        Assert.Contains("EditTimelineList of Timeline by fa-list", drafted.Code, StringComparison.Ordinal);
        Assert.Contains("one EditAvatar of Avatar", drafted.Code, StringComparison.Ordinal);

        var test = PlaceDrafter.FormTest(list, templates, "TimelineListDialogTests", "Shop.Tests", "TimelineListDialog", "TimelinePage", signs: Signs, onPage: page, beside: beside);
        Assert.Equal("card TimelinePage.EditTimelineList\n", test.Code);
    }

    [Fact]
    public void A_dialog_a_row_s_button_opens_is_named_after_its_tab_and_its_test_presses_the_first_row_s_button()
    {
        var templates = DraftTemplates.Parse("## Form test\n\n```csharp\nplain {Page}.{OpenerMember}\n```\n\n## Tab step\n\n```csharp\ntab(\"{Label}\");\n```\n\n## Row form test\n\n```csharp\nopen {Page}\n        {TabSteps}\npress {Icon} for {Form}\n```\n");
        var onTab = new Place { Key = "/equipment › Bookings › fa-pen", Kind = PlaceKind.Dialog, Parent = "/equipment", State = PlaceState.FormNotSaved };
        var onPage = onTab with { Key = "/equipment › fa-pen" };

        Assert.Equal("EquipmentBookingsFormDialog", PlaceDrafter.ClassName(onTab, PlaceDraftKind.Form, Signs));
        Assert.Equal("EquipmentFormDialog", PlaceDrafter.ClassName(onPage, PlaceDraftKind.Form, Signs));
        Assert.Equal("open EquipmentPage\n        tab(\"Bookings\");\npress fa-pen for EquipmentBookingsFormDialog\n",
            PlaceDrafter.FormTest(onTab, templates, "T", "Shop.Tests", "EquipmentBookingsFormDialog", "EquipmentPage", signs: Signs).Code);
        Assert.Equal("open EquipmentPage\npress fa-pen for EquipmentFormDialog\n",
            PlaceDrafter.FormTest(onPage, templates, "T", "Shop.Tests", "EquipmentFormDialog", "EquipmentPage", signs: Signs).Code);
        // A button named in words is a member of its page, as before.
        Assert.Equal("plain EquipmentPage.AddEquipment\n",
            PlaceDrafter.FormTest(onTab with { Key = "/equipment › + Equipment" }, templates, "T", "Shop.Tests", "EquipmentDialog", "EquipmentPage", signs: Signs).Code);
    }

    [Fact]
    public void A_dialog_s_test_waits_for_its_page_s_list_when_the_page_shows_one()
    {
        var templates = DraftTemplates.Parse("## Form test\n\n```csharp\nopen {Page}\n        {ListStep}\npress {OpenerMember}\n```\n\n## List step\n\n```csharp\nwait(list of {Page});\n```\n");
        var list = new Place { Key = "/day-off", State = PlaceState.Seen, Columns = ["Description", "Start date"] };

        Assert.Equal("open DaysOffPage\n        wait(list of DaysOffPage);\npress AddDayOff\n",
            PlaceDrafter.FormTest(DayOff, templates, "T", "Shop.Tests", "DayOffDialog", "DaysOffPage", signs: Signs, onPage: list).Code);
        // A page without a list has nothing to wait for.
        Assert.Equal("open DaysOffPage\npress AddDayOff\n",
            PlaceDrafter.FormTest(DayOff, templates, "T", "Shop.Tests", "DayOffDialog", "DaysOffPage", signs: Signs, onPage: list with { Columns = [] }).Code);
    }

    [Fact]
    public void A_member_that_finds_its_field_through_a_holder_is_only_given_to_a_field_in_that_holder()
    {
        var templates = DraftTemplates.Parse("## Field members\n\n| Control | Kind | Holder | Member |\n| --- | --- | --- | --- |\n| VSelect | | FormRow | `public SelectRow<{Self}> {Name} => new(this, \"{Label}\");` |\n\n## Button member\n\n```csharp\nB\n```\n\n## Form page\n\n```csharp\n{Members}\n```\n");
        var page = new Place { Key = "/settings", State = PlaceState.Seen, Fields = ["Status (text, VSelect, in a FormRow)", "Landing page (text, VSelect, in a Card)", "Loose (text, VSelect)"] };

        var draft = PlaceDrafter.Form(page, templates, "SettingsPage", "Shop.Pages", null);

        Assert.Equal(["Status"], draft.Members.Select(member => member.Label));
        Assert.Equal(["Landing page (text): it is in a Card: no member is given for a VSelect there", "Loose (text): it is in no holder the suite names: no member is given for a VSelect there"], draft.NotDrafted);
    }

    [Fact]
    public void A_list_s_row_gets_a_member_per_column_that_has_a_name()
    {
        var list = new Place { Key = "/day-off", State = PlaceState.Seen, Columns = ["Description", "Start date", "", "fa-pen"] };

        var draft = PlaceDrafter.Row(list, Templates, PlaceDrafter.ClassName(list, PlaceDraftKind.Row), "Shop.Pages", Signs);

        Assert.Equal("DayOffRow", draft.ClassName);
        Assert.Contains("    public Label<DayOffRow> StartDate => Cell(\"Start date\");", draft.Code, StringComparison.Ordinal);
        Assert.Equal(2, draft.Members.Count);
        Assert.Equal(2, draft.NotDrafted.Count);
    }

    [Fact]
    public void A_class_and_its_place_differ_by_what_one_names_and_the_other_does_not()
    {
        var draft = PlaceDrafter.Form(DayOff, Templates, "DayOffDialog", "Shop.Pages", "DaysOffPage");
        var same = PlaceDrafter.Drift(DayOff, draft.Code, PlaceDraftKind.Form);
        // What was not drafted is on the place and not in the class: the draft says so itself.
        Assert.Empty(same.NotOnPlace);
        Assert.Equal(["Logo", "Colour"], same.NotInClass);

        // The app drops a field and adds one.
        var changed = DayOff with { Fields = ["Description (text, VTextField, required)", "Type (text, VSelect)", "Name (text, VTextField)", "Reason (text, VTextField)"] };
        var drift = PlaceDrafter.Drift(changed, draft.Code, PlaceDraftKind.Form);

        Assert.Equal(["Days"], drift.NotOnPlace);
        Assert.Equal(["Reason"], drift.NotInClass);
        Assert.NotEqual(PlaceDrafter.Shape(DayOff, PlaceDraftKind.Form), PlaceDrafter.Shape(changed, PlaceDraftKind.Form));
        // A field that is only switched off is the same field.
        Assert.Equal(PlaceDrafter.Shape(DayOff, PlaceDraftKind.Form), PlaceDrafter.Shape(DayOff with { Fields = [.. DayOff.Fields.Select(field => field.Replace("Days (number, VTextField)", "Days (number, VTextField, disabled)", StringComparison.Ordinal))] }, PlaceDraftKind.Form));
    }
}
