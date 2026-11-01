using System.Text.RegularExpressions;
using Bruinet.Core.Scraping;

namespace Bruinet.Scraper.Html;

/// <summary>
/// For a recorded Html node: the Bruinet control that fits it and the locator to find it by, in the
/// preference order of the scrape-pages skill (a test id, the accessible name or text, a stable id, a
/// stable class; a position last). A suggestion, not a decision: the agent and the user pick.
/// </summary>
public static partial class ControlAdvisor
{
    private static readonly HashSet<string> Headings = new(StringComparer.Ordinal) { "h1", "h2", "h3", "h4", "h5", "h6" };

    /// <summary>The nodes a page object would name: what is acted on, read, or a frame, with their suggestions.</summary>
    /// <param name="snapshot">The capture.</param>
    /// <param name="testIds">The test-id attributes, preferred first (the config's).</param>
    /// <param name="blazor">Whether the app is Blazor: the <c>Bruinet.Blazor</c> controls are preferred.</param>
    /// <param name="root">Only this node's subtree, or 0 for the page.</param>
    public static IReadOnlyList<ControlSuggestion> Suggest(ScrapeSnapshot snapshot, IReadOnlyList<string> testIds, bool blazor, int root = 0)
    {
        var within = root == 0 ? null : new HashSet<int>(Subtree(snapshot, root));
        var documents = Documents(snapshot);
        var suggestions = snapshot.Nodes
            .Where(n => within is null || within.Contains(n.Number))
            // An option is its select's: SelectControl picks it by text or value.
            .Where(n => n.Type != "option" && ControlOf(snapshot, n, blazor) is not null && (IsActedOn(n) || HasTestId(n, testIds) || Headings.Contains(n.Type) || n.Frame is not null))
            .Select(n => Suggest(snapshot, n, testIds, blazor, documents))
            .ToList();

        // One page object holds them all: a name used twice gets a number (Search, Search2).
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < suggestions.Count; i++)
        {
            var name = suggestions[i].Name;
            used[name] = used.TryGetValue(name, out var count) ? count + 1 : 1;
            if (used[name] > 1)
                suggestions[i] = suggestions[i] with { Name = name + used[name] };
        }

        // A node in a frame is declared on the frame's scope: its member names the frame's.
        var names = suggestions.ToDictionary(x => x.Node, x => x.Name);
        return suggestions.Select(x => x.Frame is { } host && names.TryGetValue(host, out var scope) ? x with { Scope = scope } : x).ToArray();
    }

    /// <summary>The suggestion for one node.</summary>
    public static ControlSuggestion Suggest(ScrapeSnapshot snapshot, ScrapeNode node, IReadOnlyList<string> testIds, bool blazor)
        => Suggest(snapshot, node, testIds, blazor, Documents(snapshot));

    private static ControlSuggestion Suggest(ScrapeSnapshot snapshot, ScrapeNode node, IReadOnlyList<string> testIds, bool blazor, int[] documents)
    {
        var (control, ns) = ControlOf(snapshot, node, blazor) ?? ("LabelControl", "Bruinet.Html.Controls.Display");
        var (locator, stable, why) = LocatorOf(snapshot, node, testIds, documents);
        var frame = documents[node.Number];
        return new ControlSuggestion(node.Number, Name(node, testIds, TextOf(snapshot, node)), control, ns, locator, stable, why)
        {
            Frame = frame == 0 ? null : frame,
            Scope = frame == 0 ? null : Name(snapshot[frame], testIds, null),
            Core = Code.ControlKind.Of(snapshot, node) is { } reading ? $"{reading.Core.ToString().ToLowerInvariant()} ({reading.Because})" : null,
        };
    }

    private static (string Control, string Namespace)? ControlOf(ScrapeSnapshot snapshot, ScrapeNode node, bool blazor)
    {
        (string, string)? Html(string control, string ns) => blazor && BlazorControls.Contains(control) ? (control, "Bruinet.Blazor.Controls") : (control, "Bruinet.Html.Controls." + ns);
        var type = node.Attribute("type")?.ToLowerInvariant();
        return node.Type switch
        {
            "button" => Html("ButtonControl", "Buttons"),
            "input" when type is "button" or "submit" or "reset" => Html("ButtonControl", "Buttons"),
            "input" when type is "checkbox" => Html("CheckBoxControl", "Toggle"),
            "input" when type is "radio" => Html("RadioButtonControl", "Toggle"),
            "input" when type is "date" or "datetime-local" => Html("DateInputControl", "DateTime"),
            "input" when type is "time" => Html("TimeInputControl", "DateTime"),
            "input" when type is "range" => Html("RangeInputControl", "Range"),
            "input" when type is "hidden" or "file" => null,
            "input" => Html("TextInputControl", "Text"),
            "textarea" => Html("TextAreaControl", "Text"),
            "select" => Html("SelectControl", "Selection"),
            "a" when node.Attribute("href") is not null => Html("LinkControl", "Buttons"),
            "table" => Html("TableControl", "Collection"),
            "ul" or "ol" when snapshot.ChildrenOf(node.Number).Count() >= 2 => Html("ListControl", "Collection"),
            "progress" => Html("ProgressControl", "Display"),
            "iframe" or "frame" => ("FrameScope", "Bruinet.Html.Controls.Container"),
            "img" when blazor => ("ImageControl", "Bruinet.Blazor.Controls"),
            "video" when blazor => ("VideoControl", "Bruinet.Blazor.Controls"),
            "audio" when blazor => ("AudioControl", "Bruinet.Blazor.Controls"),
            _ when node.Role is "tablist" => Html("TabContainerControl", "Container"),
            _ when node.Role is "button" => Html("ButtonControl", "Buttons"),
            _ when node.Role is "checkbox" or "switch" => Html("CheckBoxControl", "Toggle"),
            _ when node.Text is { Length: > 0 } || Headings.Contains(node.Type) => Html("LabelControl", "Display"),
            _ => null,
        };
    }

    // The Bruinet.Blazor controls: a Blazor project uses these where they exist (they know Blazor's binding).
    private static readonly HashSet<string> BlazorControls = new(StringComparer.Ordinal)
    {
        "ButtonControl", "CheckBoxControl", "DateInputControl", "LinkControl", "ListControl", "ProgressControl", "RadioButtonControl",
        "RangeInputControl", "SelectControl", "TabContainerControl", "TableControl", "TextAreaControl", "TextInputControl", "TimeInputControl",
    };

    private const int MaxLabel = 50;

    private static (string Locator, bool Stable, string Why) LocatorOf(ScrapeSnapshot snapshot, ScrapeNode node, IReadOnlyList<string> testIds, int[] documents)
    {
        var document = documents[node.Number];
        bool Unique(Func<ScrapeNode, bool> match) => snapshot.Nodes.Count(n => documents[n.Number] == document && match(n)) == 1;
        // :has-text, which ByLinkText and the text locators map to, matches a part of the text, any case:
        // "Links" also finds "Broken Links - Images".
        static bool Has(string? text, string part) => text?.Contains(part, StringComparison.OrdinalIgnoreCase) == true;
        // A label is short; a text this long is content (an ad, a teaser) that changes.
        static bool Label(string? text) => text is { Length: > 0 and <= MaxLabel };

        foreach (var attribute in testIds)
        {
            if (node.Attribute(attribute) is { Length: > 0 } id && Unique(n => n.Attribute(attribute) == id))
                return (attribute == "data-testid" ? $"Locator.ByDataTestId({Quote(id)})" : $"Locator.ByCss({Quote($"[{attribute}='{id}']")})", true, $"its {attribute}");
        }
        if (node.Attribute("id") is { Length: > 0 } elementId && IsStableId(elementId) && Unique(n => n.Attribute("id") == elementId))
            return ($"Locator.ById({Quote(elementId)})", true, "its id");
        if (node.Name is { Length: > 0 } name && Unique(n => n.Name == name && n.Type == node.Type))
            return ($"Locator.ByCss({Quote($"{node.Type}[aria-label='{name}']")})", true, "its accessible name");
        if (node.Attribute("name") is { Length: > 0 } formName && Unique(n => n.Attribute("name") == formName))
            return ($"Locator.ByName({Quote(formName)})", true, "its name attribute");
        if (node.Type == "a" && TextOf(snapshot, node) is { } linkText && Label(linkText) && Unique(n => n.Type == "a" && Has(TextOf(snapshot, n), linkText)))
            return ($"Locator.ByLinkText({Quote(linkText)})", true, "its link text");
        if (node.Type is "button" && TextOf(snapshot, node) is { } buttonText && Label(buttonText) && Unique(n => n.Type == "button" && Has(TextOf(snapshot, n), buttonText)))
            return ($"Locator.ByCss({Quote($"button:has-text('{buttonText}')")})", true, "its text");
        if (node.Attribute("placeholder") is { Length: > 0 } placeholder && Unique(n => n.Attribute("placeholder") == placeholder))
            return ($"Locator.ByCss({Quote($"{node.Type}[placeholder='{placeholder}']")})", true, "its placeholder");
        if (Headings.Contains(node.Type) && node.Text is { Length: > 0 } heading && Unique(n => n.Type == node.Type && Has(n.Text, heading)))
            return ($"Locator.ByCss({Quote($"{node.Type}:has-text('{heading}')")})", true, "its text");
        var stableClass = node.Attribute("class")?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(c => IsStableClass(c) && Unique(n => HasClass(n, c) && n.Type == node.Type));
        if (stableClass is not null)
            return ($"Locator.ByCss({Quote($"{node.Type}.{stableClass}")})", true, "a class only it has");
        return ($"Locator.ByCss({Quote(Path(snapshot, node, testIds, documents))})", false, "nothing stable: its position; ask for a test id in the app");
    }

    // A button's or link's text is often in a child (a span in a UI library's button).
    private static string? TextOf(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        if (node.Text is { Length: > 0 } || node.Type is not ("button" or "a"))
            return node.Text;
        foreach (var child in snapshot.ChildrenOf(node.Number))
        {
            if (child.Text is { Length: > 0 } text)
                return text;
            foreach (var grandchild in snapshot.ChildrenOf(child.Number))
                if (grandchild.Text is { Length: > 0 } deeper)
                    return deeper;
        }
        return null;
    }

    // The app's own test id says what the element is best; then what the user sees.
    private static string Name(ScrapeNode node, IReadOnlyList<string> testIds, string? text)
    {
        // The first source with a word in it: a button's text can be only "+" (then its data-action says it).
        var sources = testIds.Select(node.Attribute)
            .Append(node.Name).Append(text).Append(node.Attribute("placeholder")).Append(node.Attribute("name")).Append(node.Attribute("id")).Append(node.Attribute("title"))
            .Concat(node.Attributes.Where(a => a.Key.StartsWith("data-", StringComparison.Ordinal)).OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => (string?)a.Value))
            .Append(node.Type);
        foreach (var source in sources)
        {
            if (string.IsNullOrEmpty(source))
                continue;
            // Split at camelCase humps too: FirstNameInput stays FirstNameInput, not Firstnameinput.
            var name = string.Concat(Explore.DangerWords.Words(source).Select(w => char.ToUpperInvariant(w[0]) + w[1..]).Take(4));
            if (name.Length > 0)
                return char.IsDigit(name[0]) ? "Element" + name : name;
        }
        return "Element";
    }

    private static bool IsActedOn(ScrapeNode node) =>
        node.Type is "button" or "input" or "textarea" or "select" or "a" or "iframe" or "table" or "progress"
        || node.Role is "button" or "checkbox" or "switch" or "tablist" or "link";

    private static bool HasTestId(ScrapeNode node, IReadOnlyList<string> testIds) => testIds.Any(t => node.Attribute(t) is { Length: > 0 });

    private static bool HasClass(ScrapeNode node, string name) =>
        node.Attribute("class")?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(name, StringComparer.Ordinal) == true;

    // An id with a long number or a GUID in it is made per render (react-123, ng-scope-456).
    private static bool IsStableId(string id) => !UnstableId().IsMatch(id);

    private static bool IsStableClass(string name) => !UnstableClass().IsMatch(name);

    // The frame document each node is in: the nearest iframe above it, or 0 for the page. A parent
    // comes before its children, so one pass in order does it.
    private static int[] Documents(ScrapeSnapshot snapshot)
    {
        var documents = new int[snapshot.Nodes.Count + 1];
        foreach (var node in snapshot.Nodes)
            documents[node.Number] = node.Parent == 0 ? 0 : snapshot[node.Parent].Frame is not null ? node.Parent : documents[node.Parent];
        return documents;
    }

    // The node's position, from its nearest ancestor that has a test id or a stable id of its own,
    // so a position is as short, and as local, as the page allows.
    private static string Path(ScrapeSnapshot snapshot, ScrapeNode node, IReadOnlyList<string> testIds, int[] documents)
    {
        var parts = new List<string>();
        for (var n = node; n.Parent != 0 && snapshot[n.Parent].Frame is null && n.Type is not "body"; n = snapshot[n.Parent])
        {
            var siblings = snapshot.ChildrenOf(n.Parent).Where(s => s.Type == n.Type).ToList();
            parts.Add(siblings.Count > 1 ? $"{n.Type}:nth-of-type({siblings.IndexOf(n) + 1})" : n.Type);
            var parent = snapshot[n.Parent];
            if (Anchor(snapshot, parent, testIds, documents) is { } anchor)
            {
                parts.Add(anchor);
                break;
            }
        }
        parts.Reverse();
        return string.Join(" > ", parts);
    }

    private static string? Anchor(ScrapeSnapshot snapshot, ScrapeNode node, IReadOnlyList<string> testIds, int[] documents)
    {
        bool Unique(Func<ScrapeNode, bool> match) => snapshot.Nodes.Count(n => documents[n.Number] == documents[node.Number] && match(n)) == 1;
        foreach (var attribute in testIds)
            if (node.Attribute(attribute) is { Length: > 0 } id && Unique(n => n.Attribute(attribute) == id))
                return $"[{attribute}='{id}']";
        return node.Attribute("id") is { Length: > 0 } elementId && IsStableId(elementId) && Unique(n => n.Attribute("id") == elementId)
            ? $"#{elementId}"
            : null;
    }

    private static IEnumerable<int> Subtree(ScrapeSnapshot snapshot, int root)
    {
        yield return root;
        foreach (var child in snapshot.ChildrenOf(root))
            foreach (var number in Subtree(snapshot, child.Number))
                yield return number;
    }

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    // Ids made per render: long numbers, GUIDs, and the prefixes UI libraries number their ids with.
    [GeneratedRegex(@"\d{3,}|[0-9a-f]{8}-[0-9a-f]{4}|^(react|ng|ember|mui|radix|mat-input|el-id)-|^v-\d|^:r|^(mudinput|popovercontent)")]
    private static partial Regex UnstableId();

    [GeneratedRegex(@"^(css|jss|sc|emotion)-|^_|\d{3,}|^(ng|is|has)-|active|selected|open|hover|focus|disabled")]
    private static partial Regex UnstableClass();
}

/// <summary>A node's suggested member: its node, a name, the control type and namespace, the locator expression, and whether that locator is stable and why.</summary>
public sealed record ControlSuggestion(int Node, string Name, string Control, string Namespace, string Locator, bool Stable, string Why)
{
    /// <summary>For a node in a frame: the frame's host node (its <c>iframe</c>).</summary>
    public int? Frame { get; init; }

    /// <summary>For a node in a frame: the frame's member, the scope to declare this one on (<c>new(Frame, …)</c>).</summary>
    public string? Scope { get; init; }

    /// <summary>
    /// What a test does with it and the facts that say so (<see cref="Code.ControlKind"/>), such as
    /// <c>selector (aria-haspopup=listbox)</c>; null for nothing interactive or read. Where it differs from
    /// <see cref="ControlSuggestion.Control"/> (a <c>LabelControl</c> for a selector made of divs), no
    /// Bruinet control fits: a control to make.
    /// </summary>
    public string? Core { get; init; }
}
