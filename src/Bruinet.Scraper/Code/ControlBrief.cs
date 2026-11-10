using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Bruinet.Core.Scraping;
using Bruinet.Core.Utilities;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;

namespace Bruinet.Scraper.Code;

/// <summary>A brief for making one control: its name, and the text the agent gets.</summary>
/// <param name="Name">The control's class name.</param>
/// <param name="Text">The brief: task, facts, hints.</param>
/// <param name="Cut">Whether the text was cut at <see cref="ControlBrief.MaxCharacters"/>.</param>
public sealed record ControlBriefText(string Name, string Text, bool Cut);

/// <summary>
/// What the agent gets to make a control from a recorded pattern:
/// the <b>task</b> (where to write, what to read and nothing else), the <b>facts</b> (where it occurs, an
/// example outline, its parts and in how many occurrences each is found) and the <b>hints</b>
/// (<see cref="ControlKind"/>'s reading: a base, members). The model decides; the hints help it.
/// </summary>
public static partial class ControlBrief
{
    /// <summary>The most characters a brief has (about 6k tokens); the rest is a tool call away.</summary>
    public const int MaxCharacters = 24_000;

    private const int ExampleLines = 80;
    private const int MaxParts = 30;
    private const int MaxPages = 10;
    private const int NodesPerPage = 8;
    private const int PartSources = 20;
    private const int MaxVariants = 10;
    private const int RowLines = 30;

    private static readonly string[] FactAttributes =
        ["type", "readonly", "disabled", "placeholder", "name", "for", "href", "tabindex", "contenteditable", "aria-haspopup", "aria-expanded", "aria-owns", "aria-controls", "aria-checked", "aria-pressed", "aria-selected", "aria-label", "aria-labelledby", "aria-disabled", "aria-live"];

    /// <summary>The brief for the pattern with <paramref name="signature"/>.</summary>
    /// <param name="project">The scraped project.</param>
    /// <param name="signature">The pattern's signature (<see cref="NodeSignature"/>).</param>
    /// <param name="variant">Which variant of the pattern (<see cref="ControlKind.Variant"/>); every occurrence when null.</param>
    /// <param name="lifted">Whether the control starts at the pattern's node lifted to its root (<see cref="Pattern.Lifted"/>).</param>
    /// <param name="name">The control's name; the pattern's own when null.</param>
    /// <param name="header">Makes the scraped header for a page's node.</param>
    /// <param name="bruinetRoot">The Bruinet repository, to name the files to read by path; null when the project is outside it.</param>
    public static ControlBriefText Write(ScrapeProject project, string signature, string? variant, bool lifted, string? name, Func<string, int, string> header, string? bruinetRoot)
    {
        var index = project.Index();
        var all = index.Pages
            .Select(p => (p.Key, Snapshot: index.Read(p.Latest)))
            .SelectMany(p => p.Snapshot.Nodes
                .Where(n => n.Kind == ScrapeNodeKind.Element && NodeSignature.Of(p.Snapshot, n) == signature)
                .Select(n => (Inner: n, Root: lifted ? PatternDetector.RootOf(p.Snapshot, n) : n))
                .DistinctBy(n => n.Root.Number)
                .Select(n => new Occurrence(p.Key, p.Snapshot, n.Root, ControlKind.Variant(p.Snapshot, n.Root), n.Inner)))
            .ToArray();
        var occurrences = all.Where(o => variant is null || o.Variant == variant).ToArray();
        if (occurrences.Length == 0)
            throw new ScraperProblem($"No recorded page has the pattern '{signature}'{(variant is null ? "" : $" as a {variant}")} on its newest capture.");

        // The occurrence with the most a test reads or acts on shows the most: an unlabelled one says less.
        var example = occurrences.MaxBy(o => ControlKind.Below(o.Snapshot, o.Root).Count)!;
        var readings = ControlKind.Below(example.Snapshot, example.Root);
        var interactive = readings.Where(r => r.Reading.Core != PartCore.Readable).ToArray();
        var cores = interactive.Select(r => r.Reading.Core).Distinct().ToArray();
        // Rows: children of one signature, two or more, in the occurrences (a list's items, a table's rows).
        var rows = Rows(occurrences);
        // The config's ## Components names it: the person's word wins over the reading below.
        var rule = project.Config.Components.FirstOrDefault(r => PatternDetector.Matches(example.Snapshot, r.Match).Contains(example.Root));
        var hint = rule is not null ? GivenHint(rule.Kind) : rows is not null ? Collection : cores.Length switch
        {
            0 => Hints[PartCore.Readable],
            1 => Hints[cores[0]],
            _ => Component,
        };
        var className = name is { Length: > 0 } ? name : project.Config.Components.FirstOrDefault(r => PatternDetector.Matches(occurrences[0].Snapshot, r.Match).Contains(occurrences[0].Root))?.Name ?? PatternName(project, signature, variant, lifted);
        var pages = occurrences.Select(o => o.Page).Distinct(StringComparer.Ordinal).Count();

        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"## {className} ({occurrences.Length}× on {Plural(pages, "page")})\n\n");

        text.Append("### Task\n");
        var file = Path.Combine(project.Folder, project.Config.ControlsFolder, className + (hint.Template ? CodeGate.TemplateExtension : ".cs"));
        text.Append(CultureInfo.InvariantCulture, $"Write:      {file}   (new file{(hint.Template ? $"; check_code generates {className}.gen.cs from it" : "; a container is written by hand, not generated")})\n");
        text.Append(CultureInfo.InvariantCulture, $"Namespace:  {ControlsNamespace(project)}\n");
        text.Append(CultureInfo.InvariantCulture, $"First line: {header(example.Page, example.Inner.Number)}\n");
        text.Append(CultureInfo.InvariantCulture, $"Class:      {hint.Declaration(className)}\n");
        text.Append("Read only:  ").Append(string.Join("\n            ", ReadOnly(hint, bruinetRoot))).Append('\n');
        text.Append("Check:      check_code (generates the template's members, then builds the project)\n\n");

        text.Append("### Facts\n");
        text.Append(CultureInfo.InvariantCulture, $"Signature: {signature}{(lifted ? $"; the control starts higher, at the root that also holds its label ({example.Root.Type}{string.Concat(Classes(example.Root).Take(3).Select(c => "." + c))}), and its parts are relative to that root" : "")}\n");
        var variants = all.GroupBy(o => o.Variant, StringComparer.Ordinal).OrderByDescending(g => g.Count()).ToArray();
        if (variants.Length > 1)
        {
            text.Append("Variants of the pattern, by what is inside (what a test does with it):\n");
            foreach (var v in variants.Take(MaxVariants))
                text.Append(CultureInfo.InvariantCulture, $"  {v.Count(),4}×  {v.Key,-22} e.g. {v.First().Page} node {v.First().Root.Number}{(v.Key == variant ? "   ← this brief" : "")}\n");
            if (variants.Length > MaxVariants)
                text.Append(CultureInfo.InvariantCulture, $"  +{variants.Length - MaxVariants} more variants, {variants.Skip(MaxVariants).Sum(v => v.Count())}×\n");
            text.Append(variant is null ? "This brief is for all of them: one control, or say why to split it.\n" : "This brief is for the marked one; the model may still make one control for several, and say why.\n");
        }
        text.Append("Where: ").Append(Where(occurrences)).Append('\n');
        text.Append(CultureInfo.InvariantCulture, $"\nExample ({example.Page}, node {example.Root.Number}, with its parent):\n");
        text.Append(Outline(example.Snapshot, example.Root, ExampleLines));
        text.Append(Owned(example));
        text.Append(Clicks(project, occurrences));
        if (rows is not null)
        {
            text.Append(CultureInfo.InvariantCulture, $"\nRows: {rows.Signature}, per occurrence: {string.Join(", ", rows.PerOccurrence.Take(MaxPages))}\n");
            text.Append(CultureInfo.InvariantCulture, $"Row example ({rows.Example.Page}, node {rows.Example.Root.Number}):\n");
            text.Append(Outline(rows.Example.Snapshot, rows.Example.Root, RowLines));
            text.Append(CultureInfo.InvariantCulture, $"\nRow parts (relative to a row), found in how many of the {rows.All.Count} rows:\n");
            text.Append(Parts(rows.Example, rows.All, project.Config.TestIds));
            text.Append("Row keys (what tells one row from another): ").Append(Keys(rows.All, project.Config.TestIds)).Append('\n');
        }
        else
        {
            text.Append(CultureInfo.InvariantCulture, $"\nParts (relative to the control's root), found in how many of the {occurrences.Length}:\n");
            text.Append(Parts(example, occurrences, project.Config.TestIds));
        }

        text.Append("\n### Hints\n");
        text.Append("What the elements are: ").Append(interactive.Length == 0 ? "nothing interactive; text to read" : string.Join("; ", interactive.Select(r => $"#{r.Node.Number} {r.Reading.Core.ToString().ToLowerInvariant()} ({r.Reading.Because})").Distinct().Take(8))).Append('\n');
        if (rule is not null)
            text.Append(CultureInfo.InvariantCulture, $"The config's ## Components names it: {rule.Kind}, {rule.Name} ({rule.Match.Kind}: {rule.Match.Value}). That wins.\n");
        text.Append("Kind: ").Append(hint.Kind).Append(". Base: ").Append(hint.Base).Append('\n');
        text.Append("Members a test needs: ").Append(hint.Members).Append('\n');
        text.Append("These are a reading of the facts, not an order: where you see it differently, say why and do that.\n");
        text.Append(CultureInfo.InvariantCulture, $"\nMore: control_brief for another pattern; get_page_snapshot(page, node) for any occurrence above.\n");

        var brief = text.ToString();
        return brief.Length <= MaxCharacters
            ? new ControlBriefText(className, brief, false)
            : new ControlBriefText(className, brief[..MaxCharacters] + "\n… cut at the brief's budget: get_page_snapshot for the rest.\n", true);
    }

    /// <summary>The namespace of the project's controls: its namespace with the pages folder swapped for the controls folder.</summary>
    public static string ControlsNamespace(ScrapeProject project)
    {
        var pages = "." + project.Config.PagesFolder.Replace('/', '.').Replace('\\', '.');
        var controls = project.Config.ControlsFolder.Replace('/', '.').Replace('\\', '.');
        return project.Namespace.EndsWith(pages, StringComparison.Ordinal)
            ? project.Namespace[..^pages.Length] + "." + controls
            : project.Namespace + "." + controls;
    }

    private sealed record Occurrence(string Page, ScrapeSnapshot Snapshot, ScrapeNode Root, string Variant, ScrapeNode Inner);

    private static string[] Classes(ScrapeNode node) => (node.Attribute("class") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private sealed record Hint(string Kind, string Base, string BaseFile, string Example, string Members, Func<string, string> Declaration, bool Template = true);

    private sealed record RowFacts(string Signature, Occurrence Example, IReadOnlyList<Occurrence> All, IReadOnlyList<string> PerOccurrence);

    // A row and the collection it is in, so a key need only be unique within its own collection.
    private static int Host(Occurrence row) => row.Root.Parent;

    private static Hint Simple(string kind, string @base, string example, string members) =>
        new(kind, $"{@base}<TScope>", $"src/Bruinet.Html/Controls/{@base}.tpl.cs", example, members,
            name => $"public partial class {name}<TScope> : {@base}<TScope> where TScope : IHtmlScope<TScope>   (usings: Bruinet.Core.Locators, Bruinet.Html.Controls, Bruinet.Html.Interfaces; constructors (scope, Locator) and (scope, string selectorOrId) as in the example)");

    private static readonly IReadOnlyDictionary<PartCore, Hint> Hints = new Dictionary<PartCore, Hint>
    {
        [PartCore.Text] = Simple("a simple control: text", "FocusableControlBase", "src/Bruinet.Html/Controls/Text/TextInputControl.tpl.cs", "SetText(text) and Clear() on the input; GetText()"),
        [PartCore.Selector] = Simple("a simple control: selector", "SelectorControlBase", "src/Bruinet.Html/Controls/Selection/SelectControl.tpl.cs", "SelectItem(text): open it, then the option with that text in the list it owns (see \"Lists it owns\"; a list drawn elsewhere is found from the page, by the id aria-owns names); GetSelectedText(); Open()"),
        [PartCore.Toggle] = Simple("a simple control: toggle", "ToggleControlBase", "src/Bruinet.Html/Controls/Toggle/CheckBoxControl.cs", "Check(), Uncheck(), IsChecked()"),
        [PartCore.Range] = Simple("a simple control: range", "RangeControlBase", "src/Bruinet.Html/Controls/Range/RangeInputControl.cs", "SetValue(value), GetValue()"),
        [PartCore.Date] = Simple("a simple control: date", "FocusableControlBase", "src/Bruinet.Html/Controls/DateTime/DateInputControl.tpl.cs", "SetDate(date), GetDate()"),
        [PartCore.Clickable] = Simple("a simple control: clickable", "ClickableControlBase", "src/Bruinet.Html/Controls/Buttons/ButtonControl.tpl.cs", "Click(); GetText()"),
        [PartCore.Readable] = Simple("a simple control: text to read", "ViewBase", "src/Bruinet.Html/Controls/Display/LabelControl.cs", "GetText(); a Get*Core per thing it shows"),
    };

    private static readonly Hint Component = new(
        "a component: several parts a test uses", "ContainerBase<TParent, TSelf>", "src/Bruinet.Html/Controls/ContainerBase.cs",
        "samples/Todo/tests/Bruinet.Samples.Todo.Web.UITests/Pages/Parts.cs",
        "a control per part (each with its own core, from the parts above), and members for what a test does with the whole",
        name => $"public sealed class {name}<TParent> : ContainerBase<TParent, {name}<TParent>> where TParent : IHtmlScope<TParent>   (as TodoStatus in the example: Self, a constructor taking the parent and a locator, a property per part)",
        Template: false);

    private static readonly Hint Collection = new(
        "a collection with rows", "CollectionObjectBase<TParent, TSelf, TItem> and ItemObjectBase<TItem>", "src/Bruinet.Html/Collections/CollectionObjectBase.cs",
        "samples/Todo/tests/Bruinet.Samples.Todo.Web.UITests/Pages/TodoListPage.cs",
        "a row class with a property per row part; on the collection, CreateItem and the ways a test finds a row (by a key or a part's text); the rows' locator",
        name => $"public sealed class {name}<TParent> : CollectionObjectBase<TParent, {name}<TParent>, {name}Row> where TParent : IHtmlScope<TParent>, and public sealed class {name}Row(IHtmlCollection rows, ItemKey key, int position) : ItemObjectBase<{name}Row>(rows, key, position)   (as TodoRows and TodoRow in the example)",
        Template: false);

    private const int MaxOptions = 10;
    private const int MaxClicks = 5;

    // A list the control owns or controls (aria-owns, aria-controls) that the page draws elsewhere, as a
    // select's options at the end of body: where it is, and its options; or that this capture has no such list.
    private static string Owned(Occurrence example)
    {
        var text = new StringBuilder();
        foreach (var node in ControlKind.Subtree(example.Snapshot, example.Root))
        {
            foreach (var attribute in new[] { "aria-owns", "aria-controls" })
            {
                foreach (var id in (node.Attribute(attribute) ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var list = example.Snapshot.Nodes.FirstOrDefault(n => n.Attribute("id") == id);
                    if (list is null)
                    {
                        text.Append(CultureInfo.InvariantCulture, $"#{node.Number} {attribute}={id}: not in this capture; the page draws it after a click (Explore's clicks below may show it).\n");
                        continue;
                    }
                    var options = ControlKind.Subtree(example.Snapshot, list)
                        .Where(n => n.Role is "option" or "menuitem" or "treeitem" or "menuitemcheckbox" or "menuitemradio" || n.Type is "option" or "li")
                        .ToArray();
                    var texts = options.Select(o => OwnText(example.Snapshot, o)).Where(t => t.Length > 0).Take(MaxOptions).Select(t => $"\"{Short(t, 30)}\"");
                    var inside = ControlKind.Subtree(example.Snapshot, example.Root).Any(n => n.Number == list.Number);
                    text.Append(CultureInfo.InvariantCulture, $"#{node.Number} {attribute}={id}: {Line(list)}{(inside ? "" : ", outside the control")}; {options.Length} options{(options.Length > 0 ? ": " + string.Join(", ", texts) : "")}\n");
                }
            }
        }
        return text.Length == 0 ? "" : "Lists it owns:\n" + text;
    }

    // An option's text: its own, else its descendants'.
    private static string OwnText(ScrapeSnapshot snapshot, ScrapeNode node) =>
        string.Join(' ', ControlKind.Subtree(snapshot, node).Select(n => n.Text?.Trim()).Where(t => t is { Length: > 0 })).Trim();

    // What Explore saw when it clicked something inside an occurrence: its reports' captures say which
    // label it clicked on which page; the capture before and after say what appeared.
    private static string Clicks(ScrapeProject project, IReadOnlyList<Occurrence> occurrences)
    {
        var facts = new List<string>();
        var byPage = occurrences.GroupBy(o => o.Page, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        foreach (var session in project.Sessions())
        {
            var report = Path.Combine(session.Folder, Explore.ExploreReport.FileName);
            if (!File.Exists(report) || !MarkdownTables.ReadSections(File.ReadAllText(report)).TryGetValue("Captures", out var captures))
                continue;
            foreach (var row in captures.Rows)
            {
                var via = ClickVia().Match(row.Cells.GetValueOrDefault("Via", ""));
                if (!via.Success || !int.TryParse(row.Cells.GetValueOrDefault("#", ""), CultureInfo.InvariantCulture, out var number))
                    continue;
                var (label, page) = (via.Groups["label"].Value, via.Groups["page"].Value);
                if (!byPage.TryGetValue(page, out var here))
                    continue;
                if (!here.Any(o => ControlKind.Subtree(o.Snapshot, o.Root).Any(n => Says(n, label))))
                    continue;
                var after = session.Events.FirstOrDefault(e => e.Number == number);
                var before = session.Events.Where(e => e.Page == page && e.Number < number).MaxBy(e => e.Number);
                var change = after is null || before is null ? "" : ": " + Change(session.ReadSnapshot(before), session.ReadSnapshot(after));
                facts.Add($"clicked '{label}' on {page} ({session.Id} capture #{number}){change}");
                if (facts.Count == MaxClicks)
                    break;
            }
            if (facts.Count == MaxClicks)
                break;
        }
        return facts.Count == 0 ? "" : "Explore's clicks inside it:\n" + string.Concat(facts.Select(f => "  " + f + "\n"));
    }

    private static bool Says(ScrapeNode node, string label) =>
        new[] { node.Text, node.Name, node.Attribute("aria-label"), node.Attribute("title") }.Any(t => t is not null && string.Equals(t.Trim(), label, StringComparison.OrdinalIgnoreCase));

    private static readonly string[] Popups = ["listbox", "option", "menu", "menuitem", "dialog", "alertdialog", "tooltip", "tabpanel", "grid", "tree"];

    // What a click made appear: popup roles that grew in number, and the nodes it added or took away.
    private static string Change(ScrapeSnapshot before, ScrapeSnapshot after)
    {
        int Count(ScrapeSnapshot s, string role) => s.Nodes.Count(n => n.Role == role || (role == "dialog" && n.Type == "dialog"));
        var appeared = Popups.Select(r => (Role: r, Delta: Count(after, r) - Count(before, r))).Where(d => d.Delta > 0).Select(d => $"role={d.Role} ×{d.Delta}").ToArray();
        var nodes = after.Nodes.Count - before.Nodes.Count;
        return (appeared.Length > 0 ? "appeared " + string.Join(", ", appeared) : "no list, menu or dialog appeared")
            + $"; {(nodes >= 0 ? "+" : "")}{nodes} nodes";
    }

    [GeneratedRegex(@"^click '(?<label>.*)' on (?<page>\S+)$")]
    private static partial Regex ClickVia();

    // The child signature that occurs two or more times under the most occurrences: the rows.
    private static RowFacts? Rows(IReadOnlyList<Occurrence> occurrences)
    {
        var counts = occurrences
            .Select(o => (Occurrence: o, Children: o.Snapshot.ChildrenOf(o.Root.Number).Where(c => c.Kind == ScrapeNodeKind.Element).GroupBy(c => NodeSignature.Of(o.Snapshot, c), StringComparer.Ordinal).Where(g => g.Count() >= 2).ToArray()))
            .ToArray();
        var signature = counts.SelectMany(c => c.Children.Select(g => g.Key)).GroupBy(s => s, StringComparer.Ordinal).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
        if (signature is null)
            return null;
        var all = counts.SelectMany(c => c.Children.Where(g => g.Key == signature).SelectMany(g => g.Select(n => new Occurrence(c.Occurrence.Page, c.Occurrence.Snapshot, n, ControlKind.Variant(c.Occurrence.Snapshot, n), n)))).ToArray();
        var example = all.MaxBy(r => ControlKind.Below(r.Snapshot, r.Root).Count)!;
        var per = counts.Select(c => $"{c.Occurrence.Page} node {c.Occurrence.Root.Number}: {c.Children.Where(g => g.Key == signature).Sum(g => g.Count())}").ToArray();
        return new RowFacts(signature, example, all, per);
    }

    // What tells rows apart: an attribute every row has with a value of its own, a numbered id, else a part's text.
    private static string Keys(IReadOnlyList<Occurrence> rows, IReadOnlyList<string> testIds)
    {
        var keys = new List<string>();
        var names = rows.SelectMany(r => r.Root.Attributes.Keys).Distinct(StringComparer.Ordinal)
            .Where(a => a is "id" or "key" || a.StartsWith("data-", StringComparison.Ordinal) || testIds.Contains(a));
        foreach (var name in names)
        {
            var values = rows.Select(r => r.Root.Attribute(name)).ToArray();
            var unique = rows.GroupBy(r => (r.Page, Host(r))).All(g => g.Select(r => r.Root.Attribute(name)).Distinct(StringComparer.Ordinal).Count() == g.Count());
            if (values.All(v => v is { Length: > 0 }) && unique)
                keys.Add($"{name} (unique on every row, e.g. {values[0]})");
        }
        if (keys.Count == 0)
            keys.Add("none on the row itself: a part's text (the first readable part above), or the position");
        return string.Join("; ", keys);
    }

    private static Hint GivenHint(string kind) => kind switch
    {
        "collection" => Collection,
        "component" => Component,
        _ => Hints[Enum.Parse<PartCore>(kind, ignoreCase: true)],
    };

    private static IEnumerable<string> ReadOnly(Hint hint, string? root)
    {
        string Path(string relative) => root is null ? relative + " (in the Bruinet repository)" : System.IO.Path.Combine(root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
        yield return Path(".github/skills/html-control/SKILL.md");
        yield return Path(hint.BaseFile) + "   (the base: its *Core methods)";
        yield return Path(hint.Example) + "   (a control of this kind)";
    }

    private static string PatternName(ScrapeProject project, string signature, string? variant, bool lifted)
    {
        var index = project.Index();
        var pages = index.Pages.Select(p => (p.Key, index.Read(p.Latest))).ToArray();
        var patterns = PatternDetector.Detect(pages, [], int.MaxValue, project.Config.TestIds).Where(p => p.Signature == signature && p.Lifted == lifted).ToArray();
        return (patterns.FirstOrDefault(p => p.Variant == variant) ?? patterns.FirstOrDefault())?.Name ?? "Part";
    }

    private static string Where(IReadOnlyList<Occurrence> occurrences)
    {
        var byPage = occurrences.GroupBy(o => o.Page, StringComparer.Ordinal).ToArray();
        var shown = byPage.Take(MaxPages).Select(g => $"{g.Key} (node {string.Join(", ", g.Take(NodesPerPage).Select(o => o.Root.Number))}{(g.Count() > NodesPerPage ? $", +{g.Count() - NodesPerPage}" : "")})");
        return string.Join("; ", shown) + (byPage.Length > MaxPages ? $"; +{byPage.Length - MaxPages} more pages" : "");
    }

    // The node's parent, then its subtree: every element, with the attributes that say what it is.
    private static string Outline(ScrapeSnapshot snapshot, ScrapeNode root, int maxLines)
    {
        var text = new StringBuilder();
        var depth = new Dictionary<int, int>();
        var start = 0;
        if (root.Parent > 0)
        {
            text.Append(Line(snapshot[root.Parent])).Append('\n');
            start = 1;
        }
        var lines = start;
        foreach (var node in ControlKind.Subtree(snapshot, root))
        {
            var level = node.Number == root.Number ? start : depth[node.Parent] + 1;
            depth[node.Number] = level;
            if (lines == maxLines)
            {
                text.Append("  … more: get_page_snapshot for the rest\n");
                break;
            }
            text.Append(' ', level * 2).Append(Line(node)).Append('\n');
            lines++;
        }
        return text.ToString();
    }

    private static string Line(ScrapeNode node)
    {
        if (node.Kind == ScrapeNodeKind.ShadowRoot)
            return $"#{node.Number} (shadow root)";
        var line = new StringBuilder().Append('#').Append(node.Number).Append(' ').Append(node.Type);
        if (node.Attribute("id") is { Length: > 0 } id)
            line.Append(" #").Append(id);
        if (node.Attribute("class") is { Length: > 0 } classes)
            line.Append(' ').Append(string.Concat(classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(6).Select(c => "." + c)));
        if (node.Role is { Length: > 0 } role)
            line.Append(" role=").Append(role);
        foreach (var key in FactAttributes)
            if (node.Attribute(key) is { } value)
                line.Append(' ').Append(key).Append(value.Length > 0 ? "=" + Short(value, 40) : "");
        foreach (var data in node.Attributes.Where(a => a.Key.StartsWith("data-", StringComparison.Ordinal)).Take(3))
            line.Append(' ').Append(data.Key).Append('=').Append(Short(data.Value, 30));
        if (node.Name is { Length: > 0 } name && node.Attribute("aria-label") is null)
            line.Append(" name=\"").Append(Short(name, 40)).Append('"');
        if (node.Text is { Length: > 0 } text && text.Trim('​', ' ').Length > 0)
            line.Append(" \"").Append(Short(text.Trim(), 50)).Append('"');
        return line.ToString();
    }

    // Each element a test reads or acts on, in the example and then in other occurrences (a part only some
    // have), with a locator relative to the pattern's node and in how many occurrences it finds something.
    private static string Parts(Occurrence example, IReadOnlyList<Occurrence> occurrences, IReadOnlyList<string> testIds)
    {
        var text = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var sources = new[] { example }.Concat(occurrences.Where(o => o != example).Take(PartSources - 1));
        foreach (var (occurrence, node, reading) in sources.SelectMany(o => ControlKind.Below(o.Snapshot, o.Root).Select(b => (o, b.Node, b.Reading))))
        {
            if (node.Number == occurrence.Root.Number)
                continue;
            if (Locator(node, testIds) is not { } locator || !seen.Add(locator))
                continue;
            if (seen.Count > MaxParts)
            {
                text.Append("  … more parts: get_page_snapshot for the rest\n");
                break;
            }
            var selector = SnapshotSelector.Parse(locator);
            var found = selector is null ? 0 : occurrences.Count(o => selector.Under(o.Snapshot, o.Root).Count > 0);
            var many = selector is null ? 0 : selector.Under(occurrence.Snapshot, occurrence.Root).Count;
            var facts = Line(node);
            var where = occurrence == example ? "" : $" (seen on {occurrence.Page}, node {node.Number})";
            text.Append(CultureInfo.InvariantCulture, $"  {locator,-36} {found,4} of {occurrences.Count,-4} {reading.Core.ToString().ToLowerInvariant()} ({reading.Because}){(many > 1 ? $", {many} in one occurrence" : "")}   {facts[(facts.IndexOf(' ', StringComparison.Ordinal) + 1)..]}{where}\n");
        }
        return seen.Count == 0 ? "  (none: nothing below the node is read or acted on)\n" : text.ToString();
    }

    // A CSS locator for a part, inside the pattern's node: a test id; else its tag with a class that says
    // what it is; an input's type; a role or an ARIA attribute; else the tag.
    private static string? Locator(ScrapeNode node, IReadOnlyList<string> testIds)
    {
        foreach (var testId in testIds)
            if (node.Attribute(testId) is { Length: > 0 } value)
                return $"[{testId}={Quote(value)}]";
        var naming = (node.Attribute("class") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(PatternDetector.IsNamingClass);
        if (naming is not null)
            return $"{node.Type}.{naming}";
        if (node.Type == "input")
            return node.Attribute("type") is { Length: > 0 } type ? $"input[type={type}]" : "input";
        if (node.Role is { Length: > 0 } role)
            return $"{node.Type}[role={role}]";
        if (node.Attribute("aria-haspopup") is not null)
            return $"{node.Type}[aria-haspopup]";
        return node.Type.Length > 0 ? node.Type : null;
    }

    private static string Quote(string value) => value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_') ? value : "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string Short(string value, int max) => value.Length <= max ? value : value[..max] + "…";

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
