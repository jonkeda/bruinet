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
        var draft = PlaceDrafter.Form(DayOff, Templates, PlaceDrafter.ClassName(DayOff, PlaceDraftKind.Form), "Shop.Pages", "DaysOffPage");

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

        var draft = PlaceDrafter.Row(list, Templates, PlaceDrafter.ClassName(list, PlaceDraftKind.Row), "Shop.Pages");

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
