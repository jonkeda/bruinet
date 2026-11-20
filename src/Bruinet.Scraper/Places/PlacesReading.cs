using Bruinet.Scraper.Code;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Places;

/// <summary>How many fields of the list are one control.</summary>
/// <param name="Control">The control; for a field no sign fits, its kind in brackets (<c>(text)</c>).</param>
/// <param name="Recognized">Whether a sign named it.</param>
/// <param name="Made">Whether the control exists; false for one to make.</param>
/// <param name="Fields">How many fields.</param>
/// <param name="Places">On how many places.</param>
/// <param name="Example">One of them: its label and its place.</param>
public sealed record FieldCount(string Control, bool Recognized, bool Made, int Fields, int Places, string Example);

/// <summary>An element of the app's own making (a custom element, <c>b7-data-table</c>) and how often the places show it.</summary>
/// <param name="Tag">Its tag.</param>
/// <param name="Times">How often it is on the screen, over all places.</param>
/// <param name="Places">On how many places.</param>
/// <param name="CoveredBy">The control or component that stands for it; null when none does.</param>
/// <param name="Made">False when what covers it is still to make.</param>
/// <param name="Example">A place that shows it.</param>
public sealed record ElementCount(string Tag, int Times, int Places, string? CoveredBy, bool Made, string Example);

/// <summary>
/// What the list of places and the captures the walk kept say together, without opening the app: which control each
/// field is, which fields have none, which of the app's own elements no control or component stands for, and which
/// component a dialog or a menu is.
/// </summary>
public static class PlacesReading
{
    /// <summary>The fields of the list by control, the most first; a field no sign fits is counted under its kind.</summary>
    public static IReadOnlyList<FieldCount> Fields(PlacesList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        var fields = list.Places
            // A form shown by two buttons is one form.
            .Where(place => place.SameAs is null)
            .SelectMany(place => place.Fields.Select(field => (Place: place.Key, Field: field, Read: ReadField(field))))
            .ToList();
        return [.. fields.GroupBy(field => (field.Read.Control, field.Read.Recognized, field.Read.Made))
            .Select(group => new FieldCount(group.Key.Control, group.Key.Recognized, group.Key.Made, group.Count(), group.Select(field => field.Place).Distinct(StringComparer.Ordinal).Count(), $"{group.First().Read.Label} on {group.First().Place}"))
            .OrderBy(count => !count.Recognized ? 0 : count.Made ? 2 : 1).ThenByDescending(count => count.Fields).ThenBy(count => count.Control, StringComparer.Ordinal)];
    }

    /// <summary>A field of the list (<c>Name (text, VTextField, required)</c>) as its label, its control and whether that is made.</summary>
    public static (string Label, string Kind, string Control, bool Recognized, bool Made) ReadField(string field)
    {
        var read = PlaceField.Parse(field);
        if (read.Kind.Length == 0)
            return (read.Label, "", "(unknown)", false, true);
        if (read.InTable)
            return (read.Label, read.Kind, "(a table's own)", true, true);
        return read.Control is null
            ? (read.Label, read.Kind, $"({read.Kind})", false, true)
            : (read.Label, read.Kind, read.Control, true, read.ControlMade);
    }

    /// <summary>What holds a field of the list (<c>Landing page (text, VSelect, in a Card)</c> is in a <c>Card</c>); null when the list names none.</summary>
    public static string? HolderOf(string field)
    {
        return PlaceField.Parse(field).Holder;
    }

    /// <summary>
    /// The app's own elements over the kept captures, the most widespread first: every element on the screen whose
    /// tag is a custom element's (it has a hyphen), with the control a sign names for it or the class of the project
    /// that was made from it.
    /// </summary>
    public static IReadOnlyList<ElementCount> Elements(IReadOnlyList<(Place Place, ScrapeSnapshot Snapshot)> kept, IReadOnlyList<ControlSign> signs, IReadOnlyList<CodeClass> code)
    {
        ArgumentNullException.ThrowIfNull(kept);
        ArgumentNullException.ThrowIfNull(signs);
        ArgumentNullException.ThrowIfNull(code);
        var seen = new Dictionary<string, (int Times, HashSet<string> Places)>(StringComparer.Ordinal);
        foreach (var (place, snapshot) in kept)
        {
            foreach (var node in snapshot.Nodes.Where(node => node.Kind == ScrapeNodeKind.Element && node.Bounds is { Width: > 0, Height: > 0 } && node.Type.Contains('-', StringComparison.Ordinal)))
            {
                if (!seen.TryGetValue(node.Type, out var at))
                    at = (0, new HashSet<string>(StringComparer.Ordinal));
                at.Places.Add(place.Parent ?? place.Key);
                seen[node.Type] = (at.Times + 1, at.Places);
            }
        }

        return [.. seen.Select(pair =>
            {
                var (by, made) = CoveredBy(pair.Key, signs, code);
                return new ElementCount(pair.Key, pair.Value.Times, pair.Value.Places.Count, by, made, pair.Value.Places.Order(StringComparer.Ordinal).First());
            })
            .OrderBy(count => count.CoveredBy is null ? 0 : count.Made ? 2 : 1).ThenByDescending(count => count.Places).ThenBy(count => count.Tag, StringComparer.Ordinal)];
    }

    // A sign that names the tag, else a class of the project made from an element of that tag (its header's signature starts with it).
    private static (string? By, bool Made) CoveredBy(string tag, IReadOnlyList<ControlSign> signs, IReadOnlyList<CodeClass> code)
    {
        if (signs.FirstOrDefault(sign => sign.Sign == tag || sign.Sign.StartsWith(tag + ".", StringComparison.Ordinal) || sign.Sign.StartsWith(tag + "[", StringComparison.Ordinal)) is { } sign)
            return (sign.Control, sign.Made);
        var made = code.FirstOrDefault(each => each.Kind != CodeKind.Page && each.Header?.Control is { } control && (control == tag || control.StartsWith(tag + "|", StringComparison.Ordinal)));
        return (made?.Name.Split('<')[0], true);
    }

    /// <summary>
    /// A place read again from its kept capture, with the signs and the components the project names now: its fields
    /// with their labels and controls, and for a dialog or a menu the component it is (<see cref="Place.Component"/>).
    /// Nothing else of the place changes; a page keeps the fields the walk read when the capture shows a dialog over it.
    /// </summary>
    public static Place Read(Place place, ScrapeSnapshot snapshot, IReadOnlyList<ControlSign> signs, IReadOnlyList<ComponentRule> rules, IReadOnlyList<CodeClass> code, PlaceSigns? places = null)
    {
        places ??= PlaceSigns.None;
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(code);
        var facts = PageFacts.Of(snapshot, signs, places);
        var read = place;
        if (place.Kind is PlaceKind.Dialog or PlaceKind.Page or PlaceKind.Tab && (place.Kind == PlaceKind.Dialog) == (facts.Dialog is not null))
        {
            // The capture is what the walk saw: its fields are read whole, labels and controls, as the reading is now.
            if (facts.Fields.Count > 0)
            {
                read = read with { Fields = [.. facts.Fields.Select(field => field.ToListText())] };
                // What a capture states of its fields; a fact that was seen on the app (typed, saved) is kept beside it.
                var stated = facts.Fields.SelectMany(field => field.Facts).Select(fact => fact.ToText()).ToList();
                var seen = place.FieldFacts.Where(line => FieldFact.Parse(line) is { Level: not FieldFact.StatedLevel });
                read = read with { FieldFacts = [.. stated.Concat(seen).Distinct(StringComparer.Ordinal)] };
            }
            if (facts.Buttons.Count > 0)
                read = read with { Buttons = [.. facts.Buttons.Select(Place.Clean).Where(button => button.Length > 0).Distinct(StringComparer.Ordinal).Take(40)] };
        }

        return place.Kind is PlaceKind.Dialog or PlaceKind.Menu ? read with { Component = Component(snapshot, place.Kind, rules, code, places) } : read;
    }

    // What lies over the page (the open dialog, the open menu) is a component when the project names an element in it
    // (## Components), or when a class of the project was made from it.
    private static string? Component(ScrapeSnapshot snapshot, PlaceKind kind, IReadOnlyList<ComponentRule> rules, IReadOnlyList<CodeClass> code, PlaceSigns places)
    {
        var shown = snapshot.Nodes.Where(node => node.Kind == ScrapeNodeKind.Element && node.Bounds is { Width: > 0, Height: > 0 }).ToList();
        var root = kind == PlaceKind.Dialog
            ? shown.LastOrDefault(node => node.Role is "dialog" or "alertdialog" || node.Type == "dialog")
            : shown.LastOrDefault(places.IsOpenMenu);
        if (root is null)
            return null;
        bool Within(ScrapeNode node)
        {
            for (var at = node.Number; at != 0; at = snapshot[at].Parent)
            {
                if (at == root.Number)
                    return true;
            }

            return false;
        }

        foreach (var rule in rules)
        {
            if (PatternDetector.Matches(snapshot, rule.Match).Any(node => node.Bounds is { Width: > 0, Height: > 0 } && Within(node)))
                return rule.Name;
        }

        var parts = code.Where(each => each.Kind != CodeKind.Page && each.Header?.Control is { Length: > 0 })
            .GroupBy(each => each.Header!.Control!, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Name.Split('<')[0], StringComparer.Ordinal);
        if (parts.Count == 0)
            return null;
        // The root itself, or what it holds two levels down: a library's dialog wraps the app's own in a card.
        var near = new List<ScrapeNode> { root };
        for (var level = 0; level < 2; level++)
            near.AddRange(near.ToList().SelectMany(node => snapshot.ChildrenOf(node.Number)).Where(node => node.Kind == ScrapeNodeKind.Element));
        return near.Select(node => parts.GetValueOrDefault(NodeSignature.Of(snapshot, node))).FirstOrDefault(name => name is not null);
    }
}
