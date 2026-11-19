using Bruinet.Core.Utilities;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Projects;

/// <summary>
/// How a control is recognized on a page: one row of a control library's <c>control-signs.md</c>, or of a project's
/// <c>## Control signs</c>. A field whose own element, or an element around it, shows the sign is that control.
/// </summary>
/// <param name="Control">The control's class (<c>VSelect</c>), or what it would be called when it is not made.</param>
/// <param name="Sign">
/// What shows it: an element's tag, classes and attributes written as a selector with no spaces
/// (<c>.v-input.v-select</c>, <c>b7-rich-text-editor</c>, <c>input[type=file]</c>). It may end in
/// <c>:has(…)</c> for an element that holds another (<c>.v-text-field:has(i:text(event))</c>: a text field with
/// the calendar icon in it); <c>:text(…)</c> there is that element's own text.
/// </param>
/// <param name="Made">Whether the control exists; false for one to make.</param>
/// <param name="Library">Where the control is, or would go.</param>
public sealed record ControlSign(string Control, string Sign, bool Made, string Library)
{
    /// <summary>The name of a library's file of signs, in its project folder.</summary>
    public const string FileName = "control-signs.md";

    /// <summary>How far above a field its sign is looked for: a library wraps an input in a few elements.</summary>
    public const int Reach = 6;

    /// <summary>How far above a field its holder is looked for: a row or a card lies further out than the control.</summary>
    public const int HolderReach = 14;

    /// <summary>
    /// True for a row of <c>## Field holders</c>: the sign is not a control's but that of what holds a field with its
    /// label (a form's row, a card). A suite finds a field through its holder, so a field in another holder needs
    /// another member.
    /// </summary>
    public bool IsHolder { get; init; }

    /// <summary>The holder a field is in: the nearest element around it, within <see cref="HolderReach"/>, that shows a holder's sign; null when none.</summary>
    public static ControlSign? HolderOf(IReadOnlyList<ControlSign> signs, ScrapeSnapshot snapshot, ScrapeNode node)
    {
        ArgumentNullException.ThrowIfNull(signs);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(node);
        var holders = signs.Where(sign => sign.IsHolder).ToList();
        if (holders.Count == 0)
            return null;
        var level = 0;
        for (var at = node; level <= HolderReach; at = snapshot[at.Parent], level++)
        {
            if (at.Kind == ScrapeNodeKind.Element && holders.FirstOrDefault(holder => holder._compound.Value.Self.Matches(at) && (holder._compound.Value.Holds is not { } holds || Below(snapshot, at, Reach).Any(holds.Matches))) is { } found)
                return found;
            if (at.Parent == 0)
                break;
        }

        return null;
    }

    private readonly Lazy<(Compound Self, Compound? Holds)> _compound = new(() =>
    {
        var has = Sign.IndexOf(":has(", StringComparison.Ordinal);
        return has < 0 ? (Compound.Parse(Sign), null) : (Compound.Parse(Sign[..has]), Compound.Parse(Sign[(has + 5)..].TrimEnd(')') + (Sign.Contains(":text(", StringComparison.Ordinal) ? ")" : "")));
    });

    /// <summary>Reads the rows of a table with the columns Control, Sign and State (<c>made</c>, or anything else for one to make).</summary>
    /// <exception cref="FormatException">A row has no control or no sign, or its sign has a space in it.</exception>
    public static IReadOnlyList<ControlSign> Read(IEnumerable<MarkdownTableRow> rows, string library)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var signs = new List<ControlSign>();
        foreach (var row in rows)
        {
            var holder = row.Cells.ContainsKey("Holder");
            var control = row.Cells.GetValueOrDefault(holder ? "Holder" : "Control", "").Trim().Trim('`');
            var sign = row.Cells.GetValueOrDefault("Sign", "").Trim().Trim('`');
            if (control.Length == 0 || sign.Length == 0)
                throw new FormatException($"Line {row.LineNumber}: a control sign needs a Control and a Sign.");
            if (sign.Contains(' ', StringComparison.Ordinal))
                throw new FormatException($"Line {row.LineNumber}: a sign is one element ('.v-input.v-select'), not a path: '{sign}'.");
            signs.Add(new ControlSign(control, sign, row.Cells.GetValueOrDefault("State", "made").Trim().Equals("made", StringComparison.OrdinalIgnoreCase), library) { IsHolder = holder });
        }

        return signs;
    }

    /// <summary>Reads a library's file of signs: every table in it with a Control and a Sign column.</summary>
    public static IReadOnlyList<ControlSign> ReadFile(string path, string library)
        => [.. MarkdownTables.ReadSections(File.ReadAllText(path)).Where(section => section.Key != PlaceSigns.Section).SelectMany(section => Read(section.Value.Rows.Where(row => row.Cells.ContainsKey("Sign")), library))];

    /// <summary>
    /// The first sign an element shows, on itself or on an element around it within <see cref="Reach"/>; null when
    /// none. The order of the signs decides: the more particular control stands before the plainer one.
    /// </summary>
    public static ControlSign? Of(IReadOnlyList<ControlSign> signs, ScrapeSnapshot snapshot, ScrapeNode node) => At(signs, snapshot, node)?.Sign;

    /// <summary>
    /// As <see cref="Of"/>, with the element that shows the sign: where the control starts. What lies in it is the
    /// control's own (a select's chosen value), not a label beside it.
    /// </summary>
    public static (ControlSign Sign, ScrapeNode Root)? At(IReadOnlyList<ControlSign> signs, ScrapeSnapshot snapshot, ScrapeNode node)
    {
        ArgumentNullException.ThrowIfNull(signs);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(node);
        signs = [.. signs.Where(sign => !sign.IsHolder)];
        if (signs.Count == 0)
            return null;
        var around = new List<ScrapeNode>();
        for (var at = node; around.Count <= Reach; at = snapshot[at.Parent])
        {
            if (at.Kind == ScrapeNodeKind.Element)
                around.Add(at);
            if (at.Parent == 0)
                break;
        }

        foreach (var sign in signs)
        {
            if (around.FirstOrDefault(at => sign._compound.Value.Self.Matches(at) && (sign._compound.Value.Holds is not { } holds || Below(snapshot, at, Reach).Any(holds.Matches))) is { } root)
                return (sign, root);
        }

        return null;
    }

    private static IEnumerable<ScrapeNode> Below(ScrapeSnapshot snapshot, ScrapeNode node, int depth)
    {
        foreach (var child in snapshot.ChildrenOf(node.Number))
        {
            yield return child;
            if (depth > 1)
            {
                foreach (var below in Below(snapshot, child, depth - 1))
                    yield return below;
            }
        }
    }

    // One element as a selector names it: a tag, classes, attributes.
    private sealed record Compound(string? Tag, IReadOnlyList<string> Classes, IReadOnlyList<(string Name, string? Value)> Attributes)
    {
        public string? Text { get; init; }

        public static Compound Parse(string sign)
        {
            var text = sign.IndexOf(":text(", StringComparison.Ordinal);
            if (text >= 0)
                return Parse(sign[..text]) with { Text = sign[(text + 6)..].TrimEnd(')') };
            var classes = new List<string>();
            var attributes = new List<(string, string?)>();
            string? tag = null;
            var at = 0;
            while (at < sign.Length)
            {
                var next = sign.IndexOfAny(['.', '['], at + 1);
                if (sign[at] == '[')
                    next = sign.IndexOf(']', at) + 1;
                if (next <= at)
                    next = sign.Length;
                var part = sign[at..next];
                if (part[0] == '.')
                    classes.Add(part[1..]);
                else if (part[0] == '[')
                {
                    var pair = part.Trim('[', ']').Split('=', 2);
                    attributes.Add((pair[0], pair.Length > 1 ? pair[1].Trim('\'', '"') : null));
                }
                else
                    tag = part;
                at = next;
            }

            return new Compound(tag, classes, attributes);
        }

        public bool Matches(ScrapeNode node)
        {
            if (Tag is not null && !string.Equals(node.Type, Tag, StringComparison.OrdinalIgnoreCase))
                return false;
            if (Text is not null && !string.Equals(node.Text?.Trim(), Text, StringComparison.Ordinal))
                return false;
            if (Classes.Count > 0)
            {
                var has = node.Attribute("class")?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
                if (!Classes.All(name => has.Contains(name, StringComparer.Ordinal)))
                    return false;
            }

            return Attributes.All(attribute => attribute.Name == "role" && node.Role is not null
                ? attribute.Value is null || node.Role == attribute.Value
                : node.Attributes.TryGetValue(attribute.Name, out var value) && (attribute.Value is null || value == attribute.Value));
        }
    }
}
