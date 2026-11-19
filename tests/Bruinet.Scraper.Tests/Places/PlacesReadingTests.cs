using Bruinet.Core.Utilities;
using Bruinet.Scraper.Places;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Tests.Places;

/// <summary>What the list and the kept captures say together, without a browser.</summary>
public sealed class PlacesReadingTests
{
    private static readonly ScrapeBounds OnScreen = new(0, 0, 100, 20);

    private static ScrapeNode N(int number, int parent, string type, string? text = null, params (string Key, string Value)[] attributes)
        => new()
        {
            Number = number,
            Parent = parent,
            Type = type,
            Text = text,
            Bounds = OnScreen,
            Role = attributes.FirstOrDefault(a => a.Key == "role").Value,
            Attributes = attributes.Where(a => a.Key != "role").ToDictionary(a => a.Key, a => a.Value),
        };

    private static ScrapeSnapshot Page(params ScrapeNode[] nodes)
        => new() { Platform = "html", Location = "https://app.example/day-off", Title = "Days off", Nodes = nodes };

    private static IReadOnlyList<ControlSign> Signs(params string[] rows)
        => ControlSign.Read(MarkdownTables.ReadSections("## Signs\n\n| Control | Sign | State |\n| --- | --- | --- |\n" + string.Join('\n', rows) + "\n")["Signs"].Rows, "a library");

    [Fact]
    public void Fields_are_counted_by_control_and_what_no_sign_fits_and_what_is_to_make_come_first()
    {
        var list = new PlacesList();
        list.Add(new Place { Key = "/a", State = PlaceState.Seen, Fields = ["Name (text, VTextField, required)", "Logo (file, VFileInput?)", "Colour (text)"] });
        list.Add(new Place { Key = "/a › + Add", Kind = PlaceKind.Dialog, Parent = "/a", State = PlaceState.FormNotSaved, Fields = ["Name (text, VTextField)", "Note (textarea, disabled)"] });
        // The same form opened by a row's pen is not counted twice.
        list.Add(new Place { Key = "/a › fa-pen", Kind = PlaceKind.Dialog, Parent = "/a", State = PlaceState.FormNotSaved, SameAs = "/a › + Add", Fields = ["Name (text, VTextField)", "Note (textarea, disabled)"] });

        var counts = PlacesReading.Fields(list);

        Assert.Equal(["(text)", "(textarea)", "VFileInput", "VTextField"], counts.Select(count => count.Control));
        var made = counts[3];
        Assert.Equal((2, 2, true, true), (made.Fields, made.Places, made.Recognized, made.Made));
        Assert.False(counts[2].Made);
        Assert.False(counts[0].Recognized);
        Assert.Equal("Colour on /a", counts[0].Example);
    }

    [Fact]
    public void The_app_s_own_elements_are_counted_with_the_control_a_sign_names_for_them()
    {
        var page = new Place { Key = "/a", State = PlaceState.Seen };
        var dialog = new Place { Key = "/a › + Add", Kind = PlaceKind.Dialog, Parent = "/a", State = PlaceState.Seen };
        var kept = new[]
        {
            (page, Page(N(1, 0, "body"), N(2, 1, "b7-data-table"), N(3, 1, "b7-alert"), N(4, 1, "b7-alert"), N(5, 1, "div"))),
            (dialog, Page(N(1, 0, "body"), N(2, 1, "b7-alert"), N(3, 1, "a-date-picker"), N(4, 1, "x-hidden") with { Bounds = null })),
        };

        var counts = PlacesReading.Elements(kept, Signs("| ADatePicker | a-date-picker | to make |", "| B7DataTable | b7-data-table | made |"), []);

        // Nothing stands for the alert: it comes first. A dialog's elements are its page's.
        Assert.Equal(["b7-alert", "a-date-picker", "b7-data-table"], counts.Select(count => count.Tag));
        Assert.Equal((3, 1, (string?)null), (counts[0].Times, counts[0].Places, counts[0].CoveredBy));
        Assert.Equal(("ADatePicker", false), (counts[1].CoveredBy, counts[1].Made));
        Assert.Equal(("B7DataTable", true), (counts[2].CoveredBy, counts[2].Made));
    }

    [Fact]
    public void A_place_read_again_takes_its_fields_controls_from_the_signs_of_now_and_a_menu_its_component()
    {
        var form = Page(
            N(1, 0, "body"),
            N(2, 1, "div", null, ("role", "dialog")),
            N(3, 2, "div", null, ("class", "v-input v-text-field v-select")),
            N(4, 3, "label", "Country"),
            N(5, 3, "input", null, ("type", "text")));
        var dialog = new Place { Key = "/a › + Add", Kind = PlaceKind.Dialog, Parent = "/a", State = PlaceState.FormNotSaved, Fields = ["Country (text)", "Gone (text)"] };

        var read = PlacesReading.Read(dialog, form, Signs("| VSelect | .v-select | made |"), [], []);

        // The capture is what the walk saw: a field it does not show is no field of the place.
        Assert.Equal(["Country (text, VSelect)"], read.Fields);
        Assert.Null(read.Component);

        var chooser = Page(
            N(1, 0, "body"),
            N(2, 1, "div", null, ("role", "menu"), ("class", "v-menu__content")),
            N(3, 2, "div", "Columns", ("class", "list-header")),
            N(4, 2, "div", "Name"));
        var menu = new Place { Key = "/a › fa-line-columns", Kind = PlaceKind.Menu, Parent = "/a", State = PlaceState.Seen };
        var rules = new[] { new ComponentRule(new ElementMatch("selector", ".v-menu__content .list-header"), "component", "ColumnChooser") };

        Assert.Equal("ColumnChooser", PlacesReading.Read(menu, chooser, [], rules, []).Component);
        // Another menu of the page is not that component.
        var other = Page(N(1, 0, "body"), N(2, 1, "div", null, ("role", "menu")), N(3, 2, "div", "Export"));
        Assert.Null(PlacesReading.Read(menu, other, [], rules, []).Component);
    }
}
