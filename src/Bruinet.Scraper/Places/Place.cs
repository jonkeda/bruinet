using System.Text.Json.Serialization;

namespace Bruinet.Scraper.Places;

/// <summary>What a place is: something a page object would be written for, or a button that only calls the back end.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlaceKind>))]
public enum PlaceKind
{
    /// <summary>A page: an address, its ids left out.</summary>
    Page,

    /// <summary>A tab of a page.</summary>
    Tab,

    /// <summary>A dialog or a drawer a press opened over a page.</summary>
    Dialog,

    /// <summary>A menu a press opened.</summary>
    Menu,

    /// <summary>A part of a page a press showed, that is none of the others.</summary>
    Part,

    /// <summary>A button that opens nothing and calls the back end.</summary>
    Action,
}

/// <summary>How far the walk is with a place.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlaceState>))]
public enum PlaceState
{
    /// <summary>Known, not opened yet: on the frontier when it has an address.</summary>
    Known,

    /// <summary>Opened and read.</summary>
    Seen,

    /// <summary>A form that was read and never saved: a page object and a save are owed.</summary>
    FormNotSaved,

    /// <summary>A form a test filled in through its page object and saved.</summary>
    Saved,

    /// <summary>It could not be opened, with why in the note.</summary>
    Refused,
}

/// <summary>
/// One place of an app, as the list of places holds it. It holds how the place is built (labels, buttons, columns,
/// calls) and never what a record says: no row's text, no field's value.
/// </summary>
public sealed record Place
{
    /// <summary>What finds the place again: a page's address with ids left out, and for a part of a page what opened it (<c>/kam#/ › Activities</c>).</summary>
    public required string Key { get; init; }

    /// <summary>What kind of place it is.</summary>
    public PlaceKind Kind { get; init; }

    /// <summary>How far the walk is with it.</summary>
    public PlaceState State { get; init; }

    /// <summary>An address that opens it (a path on the app's own site, ids as they were); null when none is known.</summary>
    public string? Address { get; init; }

    /// <summary>The page a part belongs to; null for a page.</summary>
    public string? Parent { get; init; }

    /// <summary>How it was reached: from which place, by which press.</summary>
    public string? Via { get; init; }

    /// <summary>The browser tab's title.</summary>
    public string? Title { get; init; }

    /// <summary>Its first heading; left out for a record's own page, where the heading is the record's name.</summary>
    public string? Heading { get; init; }

    /// <summary>Its fields, each as its label and kind (<c>Name (text, required)</c>).</summary>
    public IReadOnlyList<string> Fields { get; init; } = [];

    /// <summary>What its buttons say.</summary>
    public IReadOnlyList<string> Buttons { get; init; } = [];

    /// <summary>Its tabs.</summary>
    public IReadOnlyList<string> Tabs { get; init; } = [];

    /// <summary>The headers of its tables.</summary>
    public IReadOnlyList<string> Columns { get; init; } = [];

    /// <summary>The calls the press that led here made, each as its method and address with ids left out; a held one says so.</summary>
    public IReadOnlyList<string> Calls { get; init; } = [];

    /// <summary>What was not pressed here, and why.</summary>
    public IReadOnlyList<string> NotPressed { get; init; } = [];

    /// <summary>The accounts that saw it.</summary>
    public IReadOnlyList<string> Accounts { get; init; } = [];

    /// <summary>The accounts the app refused it to: who has no access to it.</summary>
    public IReadOnlyList<string> RefusedTo { get; init; } = [];

    /// <summary>Where it is known from: the map, the suite, a recording, the code, the walk, a test's run.</summary>
    public IReadOnlyList<string> Sources { get; init; } = [];

    /// <summary>
    /// Where the place as the walk read it is kept: a capture of a recording session (<c>2026-11-18-walk-creator/012-day-off.nodes.jsonl</c>),
    /// which the patterns, the control suggestions and the drafts read. Null when none was kept.
    /// </summary>
    public string? Snapshot { get; init; }

    /// <summary>Another place of the same page that shows the same form (a row's pen opens what "+ Add" opens); null when none.</summary>
    public string? SameAs { get; init; }

    /// <summary>The component a dialog or a menu is, as the project names it or made it (<c>ColumnChooser</c>); null when none.</summary>
    public string? Component { get; init; }

    /// <summary>The day it was last seen (<c>2026-11-18</c>); null when never.</summary>
    public string? Seen { get; init; }

    /// <summary>How often opening it failed.</summary>
    public int Tries { get; init; }

    /// <summary>Anything else: where it lands, why it was refused, the component the code names.</summary>
    public string? Note { get; init; }

    /// <summary>True when the walk can open it next: a page that is known, with an address.</summary>
    [JsonIgnore]
    public bool OnFrontier => Kind == PlaceKind.Page && State == PlaceState.Known && Address is not null;

    /// <summary>The key of a part of a page: the page's key and what opened the part.</summary>
    public static string PartKey(string parent, string label) => $"{parent} › {Clean(label)}";

    /// <summary>A label as a key holds it: one line, no count in brackets (<c>Activities (3)</c> is Activities), not too long.</summary>
    public static string Clean(string label)
    {
        var one = string.Join(' ', label.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var open = one.LastIndexOf('(');
        if (open > 0 && one.EndsWith(')') && one[(open + 1)..^1].All(c => char.IsDigit(c) || c is '.' or ',' or ' '))
            one = one[..open].TrimEnd();
        return one.Length > 60 ? one[..60] + "…" : one;
    }
}
