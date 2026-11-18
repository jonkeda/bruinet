using System.Globalization;
using System.Text;

namespace Bruinet.Scraper.Places;

/// <summary>The list of places as a document a person reads: <c>.testing/175-Places.md</c>.</summary>
public static class PlacesDocument
{
    /// <summary>The document's file in a suite's <c>.testing</c> folder.</summary>
    public const string FileName = "175-Places.md";

    /// <summary>The document.</summary>
    public static string Markdown(PlacesList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        var places = list.Places.ToList();
        var md = new StringBuilder();
        md.Append("# The places of the app\n\n");
        md.Append("Every place the app is known to have: what the map, the suite, the recordings, the app's own code and the walk\n");
        md.Append("found, added up. Written by `bruinet scrape places show --write` from `").Append(PlacesList.FileName).Append("` beside it; do not edit it by hand.\n");
        md.Append("A place is what a page object would be written for (a page, a tab, a dialog, a menu), or a button that only\n");
        md.Append("calls the back end (an action). It holds how a place is built and never what a record says.\n\n");

        md.Append("## Counts\n\n| | |\n| --- | --- |\n");
        md.Append(CultureInfo.InvariantCulture, $"| Places | {places.Count} |\n");
        foreach (var kind in Enum.GetValues<PlaceKind>())
            md.Append(CultureInfo.InvariantCulture, $"| {Name(kind)} | {places.Count(place => place.Kind == kind)} |\n");
        md.Append(CultureInfo.InvariantCulture, $"| Still to open (the frontier) | {list.Frontier.Count()} |\n");
        md.Append(CultureInfo.InvariantCulture, $"| Known from the code or a link, with no address that opens them | {places.Count(place => place.Kind == PlaceKind.Page && place.State == PlaceState.Known && place.Address is null)} |\n");
        md.Append(CultureInfo.InvariantCulture, $"| Forms not saved | {places.Count(place => place.State == PlaceState.FormNotSaved)} |\n");
        md.Append(CultureInfo.InvariantCulture, $"| Forms saved | {places.Count(place => place.State == PlaceState.Saved)} |\n");
        md.Append(CultureInfo.InvariantCulture, $"| Refused | {places.Count(place => place.State == PlaceState.Refused)} |\n");

        md.Append("\n## The places\n\n");
        md.Append("| Place | Kind | State | Size | Calls | Seen by | Refused to | From | Note |\n| --- | --- | --- | --- | --- | --- | --- | --- | --- |\n");
        foreach (var place in places)
        {
            md.Append("| `").Append(Cell(place.Key)).Append("` | ").Append(Name(place.Kind)).Append(" | ").Append(State(place)).Append(" | ")
                .Append(Size(place)).Append(" | ").Append(Cell(string.Join("; ", place.Calls))).Append(" | ")
                .Append(Cell(string.Join(", ", place.Accounts))).Append(" | ").Append(Cell(string.Join(", ", place.RefusedTo))).Append(" | ").Append(Cell(string.Join(", ", place.Sources))).Append(" | ")
                .Append(Cell(Note(place))).Append(" |\n");
        }

        var forms = places.Where(place => place.State == PlaceState.FormNotSaved).ToList();
        if (forms.Count > 0)
        {
            md.Append("\n## Forms not saved\n\nEach is owed a page object and a save through it (`PlacesSaveTests`), to see where the save leads.\n\n");
            md.Append("| Form | Fields |\n| --- | --- |\n");
            foreach (var form in forms)
                md.Append("| `").Append(Cell(form.Key)).Append("` | ").Append(Cell(string.Join("; ", form.Fields))).Append(" |\n");
        }

        return md.ToString();
    }

    private static string Name(PlaceKind kind) => kind.ToString().ToLowerInvariant();

    private static string State(Place place) => place.State switch
    {
        PlaceState.Known => place.Address is null ? "known, no address" : "known",
        PlaceState.Seen => "seen",
        PlaceState.FormNotSaved => "form, not saved",
        PlaceState.Saved => "saved",
        _ => "refused",
    };

    private static string Size(Place place)
    {
        var parts = new List<string>();
        if (place.Fields.Count > 0)
            parts.Add(Count(place.Fields.Count, "field"));
        if (place.Buttons.Count > 0)
            parts.Add(Count(place.Buttons.Count, "button"));
        if (place.Tabs.Count > 0)
            parts.Add(Count(place.Tabs.Count, "tab"));
        if (place.Columns.Count > 0)
            parts.Add(Count(place.Columns.Count, "column"));
        return string.Join(", ", parts);
    }

    private static string Count(int n, string what) => string.Create(CultureInfo.InvariantCulture, $"{n} {what}{(n == 1 ? "" : "s")}");

    private static string Note(Place place)
    {
        var notes = new List<string>();
        if (place.Heading is { Length: > 0 } heading)
            notes.Add(heading);
        if (place.Via is { Length: > 0 } via)
            notes.Add("via " + via);
        if (place.Note is { Length: > 0 } note)
            notes.Add(note);
        if (place.NotPressed.Count > 0)
            notes.Add($"not pressed: {string.Join(", ", place.NotPressed)}");
        return string.Join(". ", notes);
    }

    private static string Cell(string text) => text.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
}
