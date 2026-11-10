using System.Diagnostics.CodeAnalysis;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Code;

/// <summary>What a test does with an element: the interactive core it is, or text it reads.</summary>
public enum PartCore
{
    /// <summary>Typed into: an editable input, a textarea, <c>contenteditable</c>.</summary>
    Text,

    /// <summary>Picks from a list: <c>select</c>, a combobox, an element that pops a list up.</summary>
    Selector,

    /// <summary>On or off: a checkbox, a radio, a switch, <c>aria-checked</c>, <c>aria-pressed</c>.</summary>
    Toggle,

    /// <summary>A value on a scale: a slider, a spin button, <c>progress</c>.</summary>
    Range,

    /// <summary>A date or a time input.</summary>
    Date,

    /// <summary>Clicked: a button, a link, a menu item, a tab, an option.</summary>
    Clickable,

    /// <summary>Read: a label, a heading, a status, text.</summary>
    Readable,
}

/// <summary>An element's core and the facts it rests on.</summary>
/// <param name="Core">What a test does with it.</param>
/// <param name="Because">The attributes or tag that say so, such as <c>aria-haspopup=listbox</c>.</param>
public sealed record CoreReading(PartCore Core, string Because);

/// <summary>
/// Reads what an element is from what every page has (tags, roles, ARIA, input types, <c>readonly</c>,
/// <c>tabindex</c>, text), never from a UI library's class names, so it works for any framework and an
/// app's own controls. Its answers are hints for the model, given with the facts they rest on.
/// </summary>
public static class ControlKind
{
    private static readonly HashSet<string> TextTypes = new(StringComparer.OrdinalIgnoreCase) { "text", "email", "number", "search", "tel", "url", "password", "" };
    private static readonly HashSet<string> Headings = new(StringComparer.Ordinal) { "h1", "h2", "h3", "h4", "h5", "h6", "label", "legend", "caption", "th", "dt" };

    /// <summary>The element's core, or null when a test has nothing to do with it (a wrapper, a hidden input).</summary>
    [SuppressMessage("Maintainability", "CA1502", Justification = "Known debt (AD-033): split when it next changes.")]
    public static CoreReading? Of(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        if (node.Kind != ScrapeNodeKind.Element)
            return null;
        var type = (node.Attribute("type") ?? "").ToLowerInvariant();
        var popup = node.Attribute("aria-haspopup");
        switch (node.Type)
        {
            case "input" when type == "hidden":
                return null;
            case "input" when type is "checkbox" or "radio":
                return new(PartCore.Toggle, $"input type={type}");
            case "input" when type == "range":
                return new(PartCore.Range, "input type=range");
            case "input" when type is "date" or "time" or "datetime-local" or "month" or "week":
                return new(PartCore.Date, $"input type={type}");
            case "input" when type is "button" or "submit" or "reset" or "image":
                return new(PartCore.Clickable, $"input type={type}");
            case "input" when TextTypes.Contains(type):
                if (popup is not null && popup != "false")
                    return new(PartCore.Selector, $"input aria-haspopup={popup}");
                if (node.Role is "combobox")
                    return new(PartCore.Selector, "input role=combobox");
                if (PopupAbove(snapshot, node) is { } above)
                    return new(PartCore.Selector, $"input inside an element with {above}");
                return node.Attribute("readonly") is not null
                    ? new(PartCore.Readable, "readonly input")
                    : new(PartCore.Text, $"input type={(type.Length > 0 ? type : "text")}");
            case "textarea":
                return new(PartCore.Text, "textarea");
            case "select":
                return new(PartCore.Selector, "select");
            case "progress" or "meter":
                return new(PartCore.Range, node.Type);
        }

        // A button that pops a list up is a selector: the popup says more than the role.
        if (popup is not null && popup != "false" && popup != "dialog")
            return new(PartCore.Selector, $"aria-haspopup={popup}" + (node.Role is { } r ? $" on role={r}" : ""));

        switch (node.Role)
        {
            case "combobox" or "listbox":
                return new(PartCore.Selector, $"role={node.Role}");
            case "checkbox" or "switch" or "radio" or "menuitemcheckbox" or "menuitemradio":
                return new(PartCore.Toggle, $"role={node.Role}");
            case "slider" or "spinbutton":
                return new(PartCore.Range, $"role={node.Role}");
            case "textbox":
                return new(PartCore.Text, "role=textbox");
            case "button" or "link" or "menuitem" or "tab" or "option" or "treeitem":
                return new(PartCore.Clickable, $"role={node.Role}");
            case "status" or "alert" or "heading" or "tooltip":
                return new(PartCore.Readable, $"role={node.Role}");
        }

        if (node.Attribute("aria-checked") is { } checkedState)
            return new(PartCore.Toggle, $"aria-checked={checkedState}");
        if (node.Attribute("aria-pressed") is { } pressed)
            return new(PartCore.Toggle, $"aria-pressed={pressed}");
        if (node.Attribute("contenteditable") is "" or "true")
            return new(PartCore.Text, "contenteditable");
        if (node.Type is "button" or "summary")
            return new(PartCore.Clickable, node.Type);
        if (node.Type == "a" && node.Attribute("href") is not null)
            return new(PartCore.Clickable, "a href");
        if (node.Attribute("tabindex") is { } tab && int.TryParse(tab, out var index) && index >= 0)
            return new(PartCore.Clickable, $"tabindex={tab}");
        if (Headings.Contains(node.Type))
            return new(PartCore.Readable, node.Type);
        if (node.Attribute("aria-live") is { } live && live != "off")
            return new(PartCore.Readable, $"aria-live={live}");
        if (node.Text is { Length: > 0 } text && text.Trim('​', ' ').Length > 0)
            return new(PartCore.Readable, "text");
        return null;
    }

    /// <summary>The cores in an element's subtree (itself included), each with the nodes that have it.</summary>
    public static IReadOnlyList<(ScrapeNode Node, CoreReading Reading)> Below(ScrapeSnapshot snapshot, ScrapeNode root)
    {
        var found = new List<(ScrapeNode, CoreReading)>();
        foreach (var node in Subtree(snapshot, root))
            if (Of(snapshot, node) is { } reading)
                found.Add((node, reading));
        return found;
    }

    /// <summary>
    /// What an occurrence of a pattern is for a test: the interactive cores in its subtree, in a fixed
    /// order, joined by <c>+</c> (<c>selector</c>, <c>text</c>, <c>clickable+text</c>), or <c>readable</c>.
    /// Occurrences of one pattern with different answers are variants: several controls.
    /// </summary>
    public static string Variant(ScrapeSnapshot snapshot, ScrapeNode root)
    {
        var cores = Below(snapshot, root).Select(b => b.Reading.Core).Where(c => c != PartCore.Readable).Distinct().Order().ToArray();
        // A selector's list items are part of it, not a second thing to do.
        if (cores.Contains(PartCore.Selector))
            cores = cores.Where(c => c != PartCore.Clickable).ToArray();
        return cores.Length == 0 ? "readable" : string.Join('+', cores.Select(c => c.ToString().ToLowerInvariant()));
    }

    /// <summary>The element and its descendants, in document order.</summary>
    public static IEnumerable<ScrapeNode> Subtree(ScrapeSnapshot snapshot, ScrapeNode root)
    {
        var below = new HashSet<int> { root.Number };
        yield return root;
        foreach (var node in snapshot.Nodes)
        {
            if (node.Number <= root.Number || !below.Contains(node.Parent))
                continue;
            below.Add(node.Number);
            yield return node;
        }
    }

    // An input inside a box that pops a list up (a select made of divs): the box, as a fact, or null.
    private static string? PopupAbove(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        var parent = node.Parent;
        for (var level = 0; level < 4 && parent > 0; level++)
        {
            var ancestor = snapshot[parent];
            if (ancestor.Attribute("aria-haspopup") is { } popup && popup != "false")
                return $"aria-haspopup={popup}";
            if (ancestor.Role is "combobox")
                return "role=combobox";
            parent = ancestor.Parent;
        }
        return null;
    }
}
