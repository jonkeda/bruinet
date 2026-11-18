using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bruinet.Cli.Suite;

/// <summary>
/// A suite's page checklist (<c>.testing/180-Pages.md</c>): a row per page object, written from the suite's source.
/// Only the last column, Owed, is a person's; writing the checklist again keeps it, by the page's class.
/// </summary>
internal static class PagesChecklist
{
    public const string FileName = "180-Pages.md";

    private const string Header = "| Module | Subject | Page | Kind | Address | Fields | Buttons | Tabs | Rows | Specs | Tests | Map | Gaps | Owed |";

    private static readonly Regex PageCell = new(@"^`(\w+)`", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>The suite a folder is in: the folder here or above that has a <c>.testing</c> folder; null when none.</summary>
    public static string? FindSuite(string from)
    {
        for (var folder = new DirectoryInfo(from); folder is not null; folder = folder.Parent)
        {
            if (Directory.Exists(Path.Combine(folder.FullName, ".testing")))
                return folder.FullName;
        }

        return null;
    }

    /// <summary>What each page's row of a written checklist says in Owed, by the page's class.</summary>
    public static Dictionary<string, string> Owed(string markdown)
    {
        var owed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in markdown.Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith('|')))
        {
            var cells = Cells(line);
            if (cells.Count == 14 && PageCell.Match(cells[2]) is { Success: true } page && cells[13].Length > 0)
                owed[page.Groups[1].Value] = cells[13];
        }

        return owed;
    }

    /// <summary>The checklist's Markdown, in line feeds.</summary>
    /// <param name="pages">The suite's pages.</param>
    /// <param name="owed">What a person wrote per page, by its class.</param>
    /// <param name="dropped">The classes that had a note and are pages no more.</param>
    public static string Write(SuitePages pages, IReadOnlyDictionary<string, string> owed, out IReadOnlyList<string> dropped)
    {
        var known = pages.Pages.Select(page => page.Page).ToHashSet(StringComparer.Ordinal);
        dropped = [.. owed.Keys.Where(page => !known.Contains(page)).Order(StringComparer.Ordinal)];
        var text = new StringBuilder();
        text.Append("# The suite's pages\n\n");
        text.Append("Every page object of the suite, and what its source shows of it. Written by `bruinet suite pages --write`;\n");
        text.Append("write it again after a page is added or changed. Only the last column, **Owed**, is written by a person, and is\n");
        text.Append("kept. `bruinet suite pages --check` says whether this document is still what the source says.\n\n");
        text.Append("## Counts\n\n| What | Count |\n| --- | --- |\n");
        text.Append(Invariant($"| Pages | {pages.Pages.Count} |\n"));
        foreach (var kind in pages.Pages.GroupBy(page => string.Join(" + ", page.Kinds), StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
            text.Append(Invariant($"| Kind: {kind.Key} | {kind.Count()} |\n"));
        text.Append(Invariant($"| Pages with a gap | {pages.Pages.Count(page => page.Gaps.Count > 0)} |\n"));
        foreach (var gap in pages.Pages.SelectMany(page => page.Gaps).Select(Bare).GroupBy(gap => gap, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
            text.Append(Invariant($"| Gap: {gap.Key} | {gap.Count()} |\n"));
        text.Append(Invariant($"| Pages with something owed | {pages.Pages.Count(page => owed.ContainsKey(page.Page))} |\n"));
        text.Append(Invariant($"| Fields, buttons, tabs with a name | {pages.Pages.Sum(page => page.Fields)}, {pages.Pages.Sum(page => page.Buttons)}, {pages.Pages.Sum(page => page.Tabs)} |\n"));
        text.Append(Invariant($"| Lists with a typed row | {pages.Pages.Sum(page => page.Rows.Count)} |\n"));
        if (pages.MapRows > 0)
            text.Append(Invariant($"| Addresses in the map | {pages.MapRows}, of which {pages.Unmade.Count} without a page |\n"));

        text.Append("\n## Pages\n\n").Append(Header).Append('\n');
        text.Append("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |\n");
        foreach (var page in pages.Pages)
        {
            text.Append("| ").AppendJoin(" | ",
                Cell(page.Module),
                Cell(page.Subject),
                page.PageName is null ? $"`{page.Page}`" : $"`{page.Page}` \"{Escape(page.PageName)}\"",
                string.Join(" + ", page.Kinds),
                page.Address is null ? string.Empty : $"`{page.Address}`",
                Number(page.Fields),
                Number(page.Buttons),
                Number(page.Tabs),
                Cell(string.Join(", ", page.Rows)),
                Cell(string.Join(", ", page.Specs)),
                Cell(string.Join(", ", page.Tests)),
                page.InMap ? "yes" : string.Empty,
                Cell(string.Join("; ", page.Gaps)),
                owed.GetValueOrDefault(page.Page, string.Empty)).Append(" |\n");
        }

        if (pages.MapRows > 0)
        {
            text.Append("\n## In the map, without a page\n\n");
            if (pages.Unmade.Count == 0)
            {
                text.Append("None: every address of the map has a page.\n");
            }
            else
            {
                text.Append("| Address | Where the map has it |\n| --- | --- |\n");
                foreach (var address in pages.Unmade)
                    text.Append(Invariant($"| `{address.Address}` | {Cell(address.Row)} |\n"));
            }
        }

        text.Append("\n## Reading it\n\n");
        text.Append("- **Address**: empty when the page has none of its own: a test reaches it from another page, or by a record's id.\n");
        text.Append("- **Kind**: `list` has a table or typed rows, `form` has fields, `view` has neither.\n");
        text.Append("- **Fields, Buttons, Tabs**: members of that kind a step can name, the page's own, inherited, and of its parts in the same module.\n");
        text.Append("- **Rows**: each list's row type and its number of columns.\n");
        text.Append("- **Gaps**: `by text` a member's phrase takes a text; `no typed row` a table without a row type; `nothing named` no field, button, tab or row; `no spec, no test` nothing visits the page.\n");
        return text.ToString();
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static string Number(int count) => count == 0 ? string.Empty : count.ToString(CultureInfo.InvariantCulture);

    private static string Cell(string text) => Escape(text);

    private static string Escape(string text) => text.Replace("|", "\\|", StringComparison.Ordinal);

    // A gap without its count: "by text (2)" is "by text".
    private static string Bare(string gap) => gap.IndexOf(" (", StringComparison.Ordinal) is var at and > 0 ? gap[..at] : gap;

    // A table row's cells; an escaped bar is part of its cell.
    private static List<string> Cells(string line)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var inner = line.Trim().Trim('|');
        for (var i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '\\' && i + 1 < inner.Length && inner[i + 1] == '|')
            {
                cell.Append("\\|");
                i++;
            }
            else if (inner[i] == '|')
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else
            {
                cell.Append(inner[i]);
            }
        }

        cells.Add(cell.ToString().Trim());
        return cells;
    }
}
