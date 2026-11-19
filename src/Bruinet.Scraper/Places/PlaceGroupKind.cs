using System.Text.Json.Serialization;

namespace Bruinet.Scraper.Places;

/// <summary>
/// What kind of thing a place is to draft and test: the factory takes one group of each kind first, because a kind
/// that was never drafted is where the next fault is.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlaceGroupKind>))]
public enum PlaceGroupKind
{
    /// <summary>A page that shows nothing of its own to name: no field, column, tab or dialog.</summary>
    Nothing,

    /// <summary>A page with a table.</summary>
    List,

    /// <summary>A page with a table and a dialog that holds a form.</summary>
    ListWithDialog,

    /// <summary>A page that is a form.</summary>
    FormPage,

    /// <summary>A page of settings: its fields are held by titled parts, each saved by itself.</summary>
    SettingsPage,

    /// <summary>A page with tabs.</summary>
    TabbedPage,

    /// <summary>The page of one record: its address has the record's id, or a row leads to it.</summary>
    RecordPage,

    /// <summary>A dialog a row's button opens: it needs a row.</summary>
    RowDialog,

    /// <summary>Another dialog.</summary>
    Dialog,

    /// <summary>A tab, a menu, a part or a button that only calls the back end: part of its page's group.</summary>
    Part,
}

/// <summary>Says what kind a place is (<see cref="PlaceGroupKind"/>).</summary>
public static class PlaceKinds
{
    /// <summary>The holder that marks a page of settings: a titled part of the page.</summary>
    public const string SettingsHolder = "Card";

    /// <summary>The kind of a place, from the place and what the list holds on it.</summary>
    public static PlaceGroupKind Of(Place place, PlacesList list)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(list);
        var opener = PlaceOpener.Of(place);
        if (place.Kind == PlaceKind.Dialog)
            return opener.Kind == OpenerKind.RowButton ? PlaceGroupKind.RowDialog : PlaceGroupKind.Dialog;
        if (place.Kind != PlaceKind.Page)
            return PlaceGroupKind.Part;
        if (IsRecord(place) || opener.Kind == OpenerKind.Row)
            return PlaceGroupKind.RecordPage;

        var parts = list.Places.Where(other => other.Parent == place.Key).ToList();
        if (place.Tabs.Count > 1 || parts.Any(part => part.Kind == PlaceKind.Tab))
            return PlaceGroupKind.TabbedPage;
        var fields = place.Fields.Select(PlaceField.Parse).Where(field => !field.InTable).ToList();
        if (place.Columns.Count > 0)
            return parts.Any(part => part.Kind == PlaceKind.Dialog && part.Fields.Count > 0) ? PlaceGroupKind.ListWithDialog : PlaceGroupKind.List;
        if (fields.Count > 0)
            return fields.Count(field => field.Holder == SettingsHolder) * 2 > fields.Count ? PlaceGroupKind.SettingsPage : PlaceGroupKind.FormPage;
        return parts.Any(part => part.Kind == PlaceKind.Dialog) ? PlaceGroupKind.ListWithDialog : PlaceGroupKind.Nothing;
    }

    /// <summary>
    /// True for the page of one record: its key holds an id's place (<c>/contact#/view/{id}</c>), or the address it was
    /// opened at has a record's number in its query (<c>/project/view?id=17805#/planning</c>).
    /// </summary>
    public static bool IsRecord(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);
        return place.Key.Contains('{', StringComparison.Ordinal) || IdsInQuery(place.Address).Count > 0;
    }

    /// <summary>
    /// A page's address with a placeholder where a record's id is, in its path, its hash route or its query:
    /// <c>/contact#/view/{id}</c>, <c>/project/view?id={id}#/planning</c>. The key itself for a page that is no record's.
    /// </summary>
    public static string RecordAddress(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);
        var ids = IdsInQuery(place.Address);
        if (ids.Count == 0)
            return place.Key;
        var query = "?" + string.Join('&', ids.Select(name => $"{name}={{id}}"));
        var hash = place.Key.IndexOf('#', StringComparison.Ordinal);
        return hash < 0 ? place.Key + query : place.Key[..hash] + query + place.Key[hash..];
    }

    // The names of an address's query parameters whose value is a number: a record's id, not a view's own setting.
    private static List<string> IdsInQuery(string? address)
    {
        if (address is null)
            return [];
        var start = address.IndexOf('?', StringComparison.Ordinal);
        if (start < 0)
            return [];
        var end = address.IndexOf('#', start);
        var query = end < 0 ? address[(start + 1)..] : address[(start + 1)..end];
        return [.. query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(pair => pair.Length == 2 && pair[1].Length > 0 && pair[1].All(char.IsDigit))
            .Select(pair => pair[0])];
    }

    /// <summary>
    /// The group a place belongs to: the first part of its page's address (<c>/project</c> for
    /// <c>/project/view#/planning › + Phase</c>). A group is what one loop of the factory takes, or is cut from.
    /// </summary>
    public static string GroupOf(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);
        var page = place.Parent ?? place.Key;
        var part = page.IndexOf(" › ", StringComparison.Ordinal);
        if (part >= 0)
            page = page[..part];
        var path = page.TrimStart('/');
        var end = path.IndexOfAny(['/', '#', '?']);
        return "/" + (end < 0 ? path : path[..end]);
    }
}
