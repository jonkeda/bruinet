using System.Text.RegularExpressions;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Places;

/// <summary>What is known of an app's places before a walk: from a map, from the recordings, from the app's own code.</summary>
public static partial class PlacesSeeds
{
    /// <summary>The source name of a map.</summary>
    public const string Map = "map";

    /// <summary>The source name of a suite's page objects.</summary>
    public const string Suite = "suite";

    /// <summary>The source name of the recordings.</summary>
    public const string Recording = "recording";

    /// <summary>The source name of the app's code.</summary>
    public const string Code = "code";

    /// <summary>The source name of the walk.</summary>
    public const string Walk = "walk";

    /// <summary>The source name of a test's run.</summary>
    public const string Run = "run";

    /// <summary>The addresses a Markdown file names in backticks (<c>`/contact#/create`</c>), as known pages.</summary>
    public static IEnumerable<Place> FromMarkdown(string markdown, string source = Map)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return Backticked().Matches(markdown).Select(match => PlacesList.Known(match.Groups[1].Value, source));
    }

    /// <summary>The pages a project's recordings captured, each with an address that opened it.</summary>
    public static IEnumerable<Place> FromRecordings(ScrapeProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Sessions()
            .SelectMany(session => session.Events)
            .Where(e => e.Kind == RecordingEvent.PageKind && project.IsOwnPage(e.Location))
            .Select(e => Opened(e.Location, Recording));
    }

    /// <summary>A page that was opened at an address: known, with that address, ids and all.</summary>
    public static Place Opened(string location, string source, string? via = null)
    {
        var (key, path) = PlacesList.KeyAndPath(location);
        return new Place { Key = key, Kind = PlaceKind.Page, State = PlaceState.Known, Address = path, Via = via, Sources = [source] };
    }

    /// <summary>
    /// The routes a script names, as pages under the page that loads the script: a route table of a single-page app
    /// survives in what the app serves, as <c>path:"/view/:id",component:…</c>, with the component's name in the name
    /// of the part it loads.
    /// </summary>
    /// <param name="page">The address of the page that loads the script.</param>
    /// <param name="script">The script's text.</param>
    public static IEnumerable<Place> FromScript(string page, string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var (_, basePath) = PlacesList.KeyAndPath(page);
        var hash = basePath.IndexOf('#', StringComparison.Ordinal);
        if (hash >= 0)
            basePath = basePath[..hash];
        var question = basePath.IndexOf('?', StringComparison.Ordinal);
        if (question >= 0)
            basePath = basePath[..question];
        basePath = basePath.TrimEnd('/');

        var routes = Route().Matches(script);
        for (var i = 0; i < routes.Count; i++)
        {
            var path = routes[i].Groups[1].Value;
            if (!path.StartsWith('/'))
                continue; // a child route's own name, or a field's path in a table: not an address
            var end = Math.Min(i + 1 < routes.Count ? routes[i + 1].Index : script.Length, routes[i].Index + routes[i].Length + 900);
            var after = script[(routes[i].Index + routes[i].Length)..end];
            if (after.StartsWith("redirect:", StringComparison.Ordinal))
                continue;
            // An optional parameter is left out, a parameter is an id.
            var route = Parameter().Replace(OptionalParameter().Replace(path, ""), "{id}").TrimEnd('/');
            // The route's own component: what it loads, up to where the route ends. A route that loads nothing names none.
            var own = after.StartsWith("component:", StringComparison.Ordinal) ? after : "";
            var ends = own.IndexOf("}]", StringComparison.Ordinal);
            var component = Component().Matches(ends < 0 ? own : own[..ends]).Select(match => match.Groups[1].Value).LastOrDefault();
            var place = PlacesList.Known($"{basePath}#{(route.Length == 0 ? "/" : route)}", Code);
            yield return place with { Note = component is null ? null : "component " + component.Replace('_', '/') };
        }
    }

    /// <summary>
    /// Adds what tests wrote down about the forms they saved (one <see cref="SavedForm"/> as JSON per line) to a list:
    /// the form is saved, where it landed is a page, and the record's address is one the walk owns.
    /// </summary>
    /// <returns>The addresses of the records the tests made.</returns>
    public static IReadOnlyList<string> AddRun(PlacesList list, string lines)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(lines);
        var owned = new List<string>();
        var json = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        foreach (var line in lines.Split((char)10, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (System.Text.Json.JsonSerializer.Deserialize<SavedForm>(line, json) is not { Place.Length: > 0 } saved)
                continue;
            // A test that only typed, and saved nothing: what it saw of the fields is added, and the place is as it was.
            if (saved.Facts is { Count: > 0 } seen && saved.Required is null && saved.Landed is null && saved.Record is null)
            {
                if (list[saved.Place] is not null)
                    list.Add(new Place { Key = saved.Place, Kind = list[saved.Place]!.Kind, State = PlaceState.Known, Sources = [Run], FieldFacts = [.. seen.Where(fact => FieldFact.Parse(fact) is { Level: FieldFact.SeenLevel })] });
                continue;
            }

            var notes = new List<string>();
            if (saved.Required is { Count: > 0 } required)
                notes.Add("saved empty it says: " + string.Join("; ", required));
            if (saved.Landed is { Length: > 0 } landed)
                notes.Add("a save lands on " + PlacesList.KeyAndPath(landed).Key);
            if (saved.Note is { Length: > 0 } note)
                notes.Add(note);
            list.Add(new Place { Key = saved.Place, Kind = list[saved.Place]?.Kind ?? PlaceKind.Dialog, State = PlaceState.Saved, Sources = [Run], Note = notes.Count == 0 ? null : string.Join(". ", notes), FieldFacts = [.. (saved.Facts ?? []).Where(fact => FieldFact.Parse(fact) is { Level: FieldFact.SeenLevel }), .. FieldFact.SavedEmpty(saved.Required ?? []).Select(fact => fact.ToText())] });
            foreach (var address in new[] { saved.Landed, saved.Record }.Where(address => address is { Length: > 0 }))
                list.Add(Opened(address!, Run, $"saving {saved.Place}"));
            if (saved.Record is { Length: > 0 } record)
                owned.Add(PlacesList.KeyAndPath(record).Path);
        }

        return owned;
    }

    [GeneratedRegex(@"`(/[^`\s]*)`")]
    private static partial Regex Backticked();

    [GeneratedRegex(@"path:""([^""]*)"",(?=name:|component:|redirect:|children:|props:|meta:|alias:|components:)")]
    private static partial Regex Route();

    [GeneratedRegex(@"/:[A-Za-z_]+\?")]
    private static partial Regex OptionalParameter();

    [GeneratedRegex(@":[A-Za-z_]+")]
    private static partial Regex Parameter();

    [GeneratedRegex(@"_pages_([A-Za-z0-9_-]+?)_(?:vue|js|ts)\b")]
    private static partial Regex Component();
}

/// <summary>
/// What a test wrote down when it saved a form through its page object: the form's place, what the empty form said
/// was required, where the save landed, and the record it made.
/// </summary>
/// <param name="Place">The form's key in the list.</param>
/// <param name="Required">What the form said when it was saved empty.</param>
/// <param name="Landed">The address after the save; null when the form stayed.</param>
/// <param name="Record">The address of the record the save made; null when it has none of its own.</param>
/// <param name="Note">Anything else: how the record is deleted.</param>
public sealed record SavedForm(string Place, IReadOnlyList<string>? Required, string? Landed, string? Record, string? Note)
{
    /// <summary>What a test saw of the form's fields when it typed in them, each a fact's line (<c>Name: max length 255 (seen, typed 300 characters)</c>).</summary>
    public IReadOnlyList<string>? Facts { get; init; }
}
