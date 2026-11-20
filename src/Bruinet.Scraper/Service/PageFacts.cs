using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Service;

/// <summary>A field of a page: its label, what kind it is, and whether it says it is required.</summary>
/// <param name="Label">The field's name, placeholder or id.</param>
/// <param name="Kind">The input's type, or the element (<c>select</c>, <c>textarea</c>, <c>combobox</c>).</param>
/// <param name="Required">Whether the field itself says it is required.</param>
/// <param name="Disabled">Whether it is disabled.</param>
public sealed record PageField(string Label, string Kind, bool Required, bool Disabled)
{
    /// <summary>The control the field is, by the signs of the project and its control libraries; null when no sign fits.</summary>
    public string? Control { get; init; }

    /// <summary>False when <see cref="Control"/> is one still to make.</summary>
    public bool ControlMade { get; init; } = true;

    /// <summary>True for a field in a table (a list's filter row, a cell that is edited in place): it is the table's own, not a form's.</summary>
    public bool InTable { get; init; }

    /// <summary>What holds the field with its label, by the project's <c>## Field holders</c> (<c>FormRow</c>, <c>Card</c>); null when no holder's sign is around it.</summary>
    public string? Holder { get; init; }

    /// <summary>The field as a list of places holds it: <c>Name (text, VTextField, required)</c>; a control to make is marked <c>VFileInput?</c>.</summary>
    public string ToListText() =>
        new Places.PlaceField(Label, Kind) { Control = Control, ControlMade = ControlMade, Required = Required, Disabled = Disabled, InTable = InTable, Holder = Holder }.ToText();
}

/// <summary>
/// What a page shows, as the few facts a spec or a page object is written from: its headings, buttons, tabs, fields,
/// what is marked as required, its messages, a table's columns, an open list's options. Read from one capture, shadow
/// roots included; only what is on the screen counts. When a dialog is open, the facts are the dialog's.
/// </summary>
public sealed record PageFacts
{
    /// <summary>The page's address.</summary>
    public required string Location { get; init; }

    /// <summary>The browser tab's title.</summary>
    public required string Title { get; init; }

    /// <summary>The open dialog's first words, when the facts are a dialog's; otherwise null.</summary>
    public string? Dialog { get; init; }

    public IReadOnlyList<string> Headings { get; init; } = [];

    public IReadOnlyList<string> Buttons { get; init; } = [];

    /// <summary>The tabs, the selected one marked with a star.</summary>
    public IReadOnlyList<string> Tabs { get; init; } = [];

    public IReadOnlyList<PageField> Fields { get; init; } = [];

    /// <summary>Texts the page marks as required (a class named so): a label's star, where the field itself says nothing.</summary>
    public IReadOnlyList<string> MarkedRequired { get; init; } = [];

    /// <summary>Alerts and validation messages.</summary>
    public IReadOnlyList<string> Messages { get; init; } = [];

    /// <summary>The header cells of the tables on the page.</summary>
    public IReadOnlyList<string> Columns { get; init; } = [];

    /// <summary>
    /// The tables' rows: how many are on the screen, then the first few as their cells' texts (<c>3 rows</c>,
    /// <c>14/11/2026 · Arend · 2.00</c>). Empty when no table has a row with cells.
    /// </summary>
    public IReadOnlyList<string> Rows { get; init; } = [];

    /// <summary>The options of a list that is open.</summary>
    public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary>
    /// The links on the screen, each as what it says and where it goes on its own site (<c>Overview -> /contact#/</c>):
    /// an open menu read this way is the app's map.
    /// </summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    private const int Most = 60;

    /// <summary>The facts of a capture.</summary>
    /// <param name="snapshot">The capture.</param>
    /// <param name="signs">How controls are recognized (<see cref="ScrapeProject.ControlSigns"/>); none when left out.</param>
    /// <param name="places">How a tab, an open menu, a toolbar and an icon show themselves beyond their roles (<see cref="ScrapeProject.PlaceSigns"/>); the roles alone when left out.</param>
    public static PageFacts Of(ScrapeSnapshot snapshot, IReadOnlyList<ControlSign>? signs = null, PlaceSigns? places = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        places ??= PlaceSigns.None;
        var shown = Shown(snapshot);

        // An open dialog covers the page: what a person can read and press is the dialog's.
        var dialog = shown.LastOrDefault(node => node.Role == "dialog" || node.Type == "dialog");
        // A list or a calendar the dialog opened is drawn beside it, not in it: it is the dialog's too.
        var beside = dialog is null ? [] : shown.Where(node => places.IsOpenMenu(node) && !Within(snapshot, node, dialog.Number)).Select(node => node.Number).ToList();
        var scope = dialog is null ? shown : [.. shown.Where(node => Within(snapshot, node, dialog.Number) || beside.Any(popup => Within(snapshot, node, popup)))];

        string Text(ScrapeNode node) => Clean(node.Text is { Length: > 0 } own ? own : node.Name ?? snapshot.TextWithin(node) ?? string.Empty);
        // A label the page marks as required (a class named so, a star) says it for its field.
        var marked = scope.Where(node => Classes(node).Contains("required")).Select(node => Text(node).TrimEnd('*', ' ')).Where(text => text.Length > 0).ToHashSet(StringComparer.Ordinal);

        return new PageFacts
        {
            Location = snapshot.Location,
            Title = snapshot.Title,
            Dialog = dialog is null ? null : Shorten(Clean(string.Join(' ', scope.Select(node => node.Text).Where(text => text is { Length: > 0 }).Take(12))), 160),
            Headings = Texts(scope.Where(node => node.Type is "h1" or "h2" or "h3" || node.Role == "heading"), Text),
            // A text editor's own buttons (bold, italic) are the editor's, not the page's.
            // So are a field's own (the cross that clears a select): they lie inside a control a sign names.
            Buttons = Texts(scope.Where(node => IsButton(node) && !InToolbar(snapshot, node, places) && !InControl(snapshot, node, signs)), node => Text(node) + Mark(snapshot, node, places)),
            Tabs = Texts(scope.Where(places.IsTab), node => Text(node) + (node.Attribute("aria-selected") == "true" ? " *" : string.Empty)),
            Fields = [.. scope.Where(node => IsField(node) && !scope.Any(around => around.Number != node.Number && IsField(around) && Within(snapshot, node, around.Number))).Select(node => Field(snapshot, node, scope, signs ?? [], places, marked)).Where(field => field.Label.Length > 0).DistinctBy(field => (field.Label, field.Kind)).Take(Most)],
            MarkedRequired = Texts(scope.Where(node => Classes(node).Contains("required")), Text),
            Messages = Texts(scope.Where(IsMessage), Text),
            Columns = Texts(scope.Where(node => node.Type == "th" || node.Role == "columnheader"), Text),
            Rows = RowsOf(snapshot, scope),
            Options = Texts(scope.Where(node => node.Role is "option" or "menuitem"), Text),
            Links = [.. scope.Where(node => node.Type == "a" && Target(snapshot.Location, node.Attribute("href")) is not null)
                .Select(node => $"{Text(node)} -> {Target(snapshot.Location, node.Attribute("href"))}")
                .Where(link => !link.StartsWith(" ->", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal).Take(2 * Most)],
        };
    }

    private static PageField Field(ScrapeSnapshot snapshot, ScrapeNode node, IReadOnlyList<ScrapeNode> shown, IReadOnlyList<ControlSign> signs, PlaceSigns places, HashSet<string> marked)
    {
        var sign = ControlSign.At(signs, snapshot, node);
        var label = Clean(LabelOf(snapshot, node, shown, sign?.Root.Number ?? 0, places));
        var kind = node.Role == "combobox" ? "combobox" : node.Type == "input" ? node.Attribute("type") ?? "text" : node.Type;
        var required = node.Attributes.ContainsKey("required") || node.Attribute("aria-required") == "true" || label.EndsWith('*') || marked.Contains(label.TrimEnd('*', ' '));
        return new PageField(label.TrimEnd('*', ' '), kind, required, node.Attributes.ContainsKey("disabled")) { Control = sign?.Sign.Control, ControlMade = sign?.Sign.Made ?? true, InTable = InATable(snapshot, node), Holder = ControlSign.HolderOf(signs, snapshot, node)?.Control };
    }

    private static bool InATable(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        for (var at = node.Parent; at > 0; at = snapshot[at].Parent)
        {
            if (snapshot[at].Type is "td" or "th" or "tr" || snapshot[at].Role is "cell" or "gridcell" or "columnheader" or "row")
                return true;
        }

        return false;
    }

    /// <summary>
    /// What a field is called: its own name; where that is only its placeholder, or it has none, the text beside it;
    /// then its placeholder, and a name or id a person gave it.
    /// </summary>
    /// <param name="snapshot">The capture.</param>
    /// <param name="field">The field.</param>
    /// <param name="shown">The elements on the screen.</param>
    /// <param name="control">The element the field's control starts at, when a sign showed it; 0 when not known.</param>
    /// <param name="places">How an icon shows itself beyond its tag; the tag alone when left out.</param>
    internal static string LabelOf(ScrapeSnapshot snapshot, ScrapeNode field, IReadOnlyList<ScrapeNode> shown, int control = 0, PlaceSigns? places = null)
    {
        var placeholder = field.Attribute("placeholder");
        var named = field.Name is { Length: > 0 } name && !string.Equals(name, placeholder, StringComparison.Ordinal) ? name : null;
        return Clean(named ?? LabelBeside(snapshot, field, shown, control, places ?? PlaceSigns.None) ?? placeholder ?? Given(field.Attribute("name")) ?? Given(field.Attribute("id")) ?? string.Empty);
    }

    /// <summary>The elements of a capture that are on the screen.</summary>
    internal static List<ScrapeNode> Shown(ScrapeSnapshot snapshot)
        => [.. snapshot.Nodes.Where(node => node.Kind == ScrapeNodeKind.Element && node.Bounds is { Width: > 0, Height: > 0 })];

    // A field with no name of its own is named by the text beside it: the first text before the field in the smallest
    // part of the page that holds this field and no other (a form's row).
    private static string? LabelBeside(ScrapeSnapshot snapshot, ScrapeNode field, IReadOnlyList<ScrapeNode> shown, int control, PlaceSigns places)
    {
        // What lies in the field's own control is the control's (a select's chosen value, a chip), not its label,
        // unless it is a label element: a library may draw the label inside.
        bool Own(ScrapeNode node) => control != 0 && node.Type != "label" && Within(snapshot, node, control);

        var at = field.Parent;
        // A UI library wraps an input many times over (a select's input lies seven or eight elements under its row).
        for (var level = 0; level < 10 && at > 0; level++, at = snapshot[at].Parent)
        {
            var part = at;
            var inPart = shown.Where(node => node.Number != part && Within(snapshot, node, part)).ToList();
            // A field inside this one, or around it (a combobox and its input), is the same field.
            var others = inPart.Where(node => IsField(node) && node.Number != field.Number && !Within(snapshot, node, field.Number) && !Within(snapshot, field, node.Number)).ToList();
            var text = inPart.FirstOrDefault(node => node.Number < field.Number && node.Text is { Length: > 0 } && Clean(node.Text).Length > 0 && !places.IsIcon(node) && !Own(node));
            if (others.Count == 0)
            {
                if (text is not null)
                    return Clean(text.Text!);
                continue;
            }

            // Other fields in the part: it is this field's row only when the row is small and has one label before
            // its first field (a date and its time under "Start date"). The first field is the row's; a later one is
            // named after the row and what it says of itself. A wider part is a section, and its heading no label.
            if (others.Count > 2)
                return null;
            // The row's inputs may share a wrapper of their own, with the label one element further up.
            if (text is null)
                continue;
            if (others.Any(other => other.Number < text.Number))
                return null;
            if (others.All(other => other.Number > field.Number))
                return Clean(text.Text!);
            return field.Attribute("placeholder") is { Length: > 0 } own ? $"{Clean(text.Text!)}: {Clean(own)}" : null;
        }

        return null;
    }

    /// <summary>What follows the name of a button that is the header of a section of a form.</summary>
    public const string SectionMark = " (section)";

    // What a button's name is followed by: that it is off, or that it is the header of a section.
    private static string Mark(ScrapeSnapshot snapshot, ScrapeNode node, PlaceSigns places)
        => places.IsSection(node) ? SectionMark
            : OnlyAnIcon(snapshot, node, places) ? IconMark
            : node.Attributes.ContainsKey("disabled") || node.Attribute("aria-disabled") == "true" ? " (disabled)" : string.Empty;

    /// <summary>What follows the name of a button that shows nothing but an icon written as a word (<c>arrow_back</c>): the word is the icon's, not a label.</summary>
    public const string IconMark = " (icon)";

    // The button says nothing itself, and every text in it is an icon's.
    private static bool OnlyAnIcon(ScrapeSnapshot snapshot, ScrapeNode node, PlaceSigns places)
    {
        if (node.Text is { Length: > 0 } || node.Name is { Length: > 0 })
            return false;
        var texts = snapshot.Nodes.Where(below => below.Number != node.Number && below.Text is { Length: > 0 } && Within(snapshot, below, node.Number)).ToList();
        return texts.Count > 0 && texts.All(places.IsIcon);
    }

    private static bool InControl(ScrapeSnapshot snapshot, ScrapeNode node, IReadOnlyList<ControlSign>? signs)
        => signs is { Count: > 0 } && ControlSign.At(signs, snapshot, node) is { Sign.IsHolder: false };

    private static bool InToolbar(ScrapeSnapshot snapshot, ScrapeNode node, PlaceSigns places)
    {
        for (var at = node.Number; at > 0; at = snapshot[at].Parent)
        {
            if (places.IsToolbar(snapshot[at]))
                return true;
        }

        return false;
    }

    // A row is a tr with cells (a header's row has th, and is the columns). The count first, then the first rows.
    private static IReadOnlyList<string> RowsOf(ScrapeSnapshot snapshot, IReadOnlyList<ScrapeNode> scope)
    {
        var rows = scope.Where(node => node.Type == "tr" || node.Role == "row")
            .Select(row => scope.Where(cell => cell.Parent == row.Number && (cell.Type == "td" || cell.Role is "cell" or "gridcell")).ToList())
            .Where(cells => cells.Count > 0)
            .ToList();
        if (rows.Count == 0)
        {
            return [];
        }

        string Cell(ScrapeNode cell) => Clean(cell.Text is { Length: > 0 } own ? own : snapshot.TextWithin(cell) ?? string.Empty);
        return [rows.Count == 1 ? "1 row" : $"{rows.Count} rows", .. rows.Take(3).Select(cells => Shorten(string.Join(" · ", cells.Select(Cell).Where(text => text.Length > 0)), 200))];
    }

    // An id a framework counted out (input-28, list-603) says nothing about the field.
    private static string? Given(string? id)
        => id is { Length: > 0 } && !(id.Contains('-', StringComparison.Ordinal) && char.IsAsciiDigit(id[^1])) ? id : null;

    internal static bool IsField(ScrapeNode node)
        => node.Type is "textarea" or "select" || node.Role == "combobox"
            || (node.Type == "input" && node.Attribute("type") is not ("hidden" or "button" or "submit"));

    private static bool IsButton(ScrapeNode node)
        => node.Type == "button" || node.Role == "button"
            || (node.Type == "input" && node.Attribute("type") is "button" or "submit")
            || (node.Type == "a" && Classes(node).Any(name => name is "btn" or "button" || name.EndsWith("-btn", StringComparison.Ordinal)));

    private static bool IsMessage(ScrapeNode node)
        => node.Role is "alert" or "status"
            || Classes(node).Any(name => name.EndsWith("message", StringComparison.Ordinal) || name.EndsWith("error", StringComparison.Ordinal) || name.Contains("snack", StringComparison.Ordinal) && name.Contains("content", StringComparison.Ordinal));

    // Where a link goes on the page's own site, without what changes per visit (a cache-busting query); null for a
    // link that goes nowhere (javascript:, #) or to another site.
    private static string? Target(string location, string? href)
    {
        if (string.IsNullOrWhiteSpace(href) || href == "#" || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate(location, UriKind.Absolute, out var page) || !Uri.TryCreate(page, href, out var target)
            || !string.Equals(target.Host, page.Host, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // A record's id is part of where a link goes (?id=12); a parameter that only defeats the cache (_=1a2b) is not.
        var query = string.Join('&', target.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Where(part => !part.StartsWith("_=", StringComparison.Ordinal)));
        return target.AbsolutePath + (query.Length > 0 ? "?" + query : string.Empty) + target.Fragment;
    }

    private static string[] Classes(ScrapeNode node)
        => node.Attribute("class")?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];

    private static bool Within(ScrapeSnapshot snapshot, ScrapeNode node, int ancestor)
    {
        for (var at = node.Number; at > 0; at = snapshot[at].Parent)
        {
            if (at == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> Texts(IEnumerable<ScrapeNode> nodes, Func<ScrapeNode, string> text)
        => [.. nodes.Select(text).Where(value => value.Trim(' ', '*').Length > 0).Distinct(StringComparer.Ordinal).Take(Most).Select(value => Shorten(value, 80))];

    private static string Clean(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Replace("​", string.Empty, StringComparison.Ordinal).Trim();

    private static string Shorten(string text, int most) => text.Length <= most ? text : text[..most] + "…";

    /// <summary>The facts as lines of text, a kind per line; a kind with nothing is left out.</summary>
    public string ToText()
    {
        var lines = new List<string> { $"{Location}  ({Title})" };
        if (Dialog is not null)
        {
            lines.Add($"A dialog is open, and these are its facts: {Dialog}");
        }

        void Add(string name, IReadOnlyList<string> values)
        {
            if (values.Count > 0)
            {
                lines.Add($"{name}: {string.Join(" | ", values)}");
            }
        }

        Add("Headings", Headings);
        Add("Buttons", Buttons);
        Add("Tabs", Tabs);
        Add("Fields", [.. Fields.Select(field => $"{field.Label} [{field.Kind}{(field.Required ? ", required" : string.Empty)}{(field.Disabled ? ", disabled" : string.Empty)}]")]);
        Add("Marked required", MarkedRequired);
        Add("Messages", Messages);
        Add("Columns", Columns);
        Add("Rows", Rows);
        Add("Options", Options);
        Add("Links", Links);
        return string.Join('\n', lines);
    }
}
