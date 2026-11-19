using Bruinet.Core.Utilities;

namespace Bruinet.Scraper.Code;

/// <summary>How a field of one control becomes a member: one row of <c>## Field members</c>.</summary>
/// <param name="Control">The control a sign named (<c>VTextField</c>); <c>*</c> for a field no sign fits.</param>
/// <param name="Kind">The field's kind the row is for (<c>number</c>); empty for any.</param>
/// <param name="Member">The member's code, with <c>{Self}</c>, <c>{Name}</c> and <c>{Label}</c>.</param>
public sealed record FieldMember(string Control, string Kind, string Member)
{
    /// <summary>The holder the row is for (<c>FormRow</c>): a member finds its field through it. Empty for a field in any holder or none.</summary>
    public string Holder { get; init; } = "";
}

/// <summary>
/// A suite's own way of writing what the Scraper drafts from a place: <c>draft-templates.md</c> beside
/// <c>scraper.config.md</c>. A suite models a form its own way (a row found by its label, a control found by a
/// locator), so the draft's shape is the suite's, not the tool's. Each <c>##</c> section is one template: a table
/// (<c>Field members</c>) or the first code block under the heading. A template is text with places in braces:
/// <c>{Self}</c> the class, <c>{Page}</c> the page a dialog is on, <c>{Namespace}</c>, <c>{Header}</c> the line
/// that says what it was drafted from, <c>{Summary}</c>, <c>{Members}</c>, <c>{Address}</c>, in a test <c>{Form}</c>, <c>{Opener}</c> and <c>{Module}</c>, and in a member
/// <c>{Name}</c> and <c>{Label}</c>.
/// </summary>
public sealed record DraftTemplates
{
    /// <summary>The file's name.</summary>
    public const string FileName = "draft-templates.md";

    /// <summary>How a field becomes a member, the first row that fits first.</summary>
    public IReadOnlyList<FieldMember> Fields { get; init; } = [];

    /// <summary>The code blocks by their section's heading (<c>Dialog</c>, <c>Form page</c>, <c>Button member</c>, <c>Row</c>, <c>Column member</c>, <c>Form test</c>).</summary>
    public IReadOnlyDictionary<string, string> Blocks { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A template by its heading.</summary>
    /// <exception cref="FormatException">The file has no such section with a code block.</exception>
    public string Block(string heading) => Blocks.TryGetValue(heading, out var block)
        ? block
        : throw new FormatException($"{FileName} has no '## {heading}' with a code block under it.");

    /// <summary>The member template for a field: the first row whose control and kind fit; null when none does.</summary>
    /// <param name="control">The field's control, or null when no sign named one.</param>
    /// <param name="kind">The field's kind.</param>
    /// <param name="holder">What holds the field, or null when nothing is known to.</param>
    public string? MemberFor(string? control, string kind, string? holder = null) =>
        Fields.FirstOrDefault(row => (row.Control == (control ?? "*")) && (row.Kind.Length == 0 || string.Equals(row.Kind, kind, StringComparison.OrdinalIgnoreCase))
            && (row.Holder.Length == 0 || string.Equals(row.Holder, holder, StringComparison.Ordinal)))?.Member;

    /// <summary>Reads the templates.</summary>
    public static DraftTemplates Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var fields = MarkdownTables.ReadSections(markdown).TryGetValue("Field members", out var table)
            ? table.Rows.Select(row => new FieldMember(Cell(row, "Control"), Cell(row, "Kind"), Cell(row, "Member")) { Holder = Cell(row, "Holder") }).Where(row => row.Control.Length > 0 && row.Member.Length > 0).ToArray()
            : [];

        var blocks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? heading = null;
        List<string>? block = null;
        foreach (var line in markdown.ReplaceLineEndings("\n").Split('\n'))
        {
            if (block is not null)
            {
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    if (heading is not null && !blocks.ContainsKey(heading))
                        blocks[heading] = string.Join('\n', block);
                    block = null;
                }
                else
                    block.Add(line);
            }
            else if (line.StartsWith("## ", StringComparison.Ordinal))
                heading = line[3..].Trim();
            else if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                block = [];
        }

        return new DraftTemplates { Fields = fields, Blocks = blocks };
    }

    private static string Cell(MarkdownTableRow row, string column) => row.Cells.GetValueOrDefault(column, "").Trim().Trim('`');
}
