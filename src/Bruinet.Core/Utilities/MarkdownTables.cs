namespace Bruinet.Core.Utilities;

/// <summary>A pipe table read from markdown: its columns and rows.</summary>
/// <param name="Columns">The header cells, in order.</param>
/// <param name="Rows">The rows that have one cell per column.</param>
/// <param name="EndLineNumber">The 1-based line after the table's last row.</param>
public sealed record MarkdownTable(
    IReadOnlyList<string> Columns,
    IReadOnlyList<MarkdownTableRow> Rows,
    int EndLineNumber);

/// <summary>One row of a <see cref="MarkdownTable"/>.</summary>
/// <param name="Cells">The trimmed cells, by column name.</param>
/// <param name="LineNumber">The row's 1-based line.</param>
public sealed record MarkdownTableRow(IReadOnlyDictionary<string, string> Cells, int LineNumber);

/// <summary>
/// Reads the pipe tables of a markdown file by the <c>##</c> section they are in: the format of
/// <c>uat.config.md</c> and <c>apps.md</c>. No meaning is given to any section or column.
/// </summary>
public static class MarkdownTables
{
    /// <summary>The table of each <c>##</c> section, by section title; the last table wins.</summary>
    /// <param name="markdown">The markdown text.</param>
    public static IReadOnlyDictionary<string, MarkdownTable> ReadSections(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var lines = markdown
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        Dictionary<string, MarkdownTable> sections = new(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                currentSection = trimmed[3..].Trim();
                continue;
            }

            if (currentSection is null ||
                !IsTableRow(trimmed) ||
                i + 1 >= lines.Length ||
                !IsSeparatorRow(lines[i + 1].Trim()))
            {
                continue;
            }

            var columns = SplitRow(trimmed);
            i += 2;
            List<MarkdownTableRow> rows = [];
            while (i < lines.Length && IsTableRow(lines[i].Trim()))
            {
                var cells = SplitRow(lines[i].Trim());
                if (cells.Count == columns.Count)
                {
                    Dictionary<string, string> row = new(StringComparer.Ordinal);
                    for (var column = 0; column < columns.Count; column++)
                    {
                        row[columns[column]] = cells[column];
                    }

                    rows.Add(new MarkdownTableRow(row, i + 1));
                }

                i++;
            }

            sections[currentSection] = new MarkdownTable(columns, rows, i + 1);
            i--;
        }

        return sections;
    }

    /// <summary>Whether <paramref name="line"/> is a pipe-table row.</summary>
    public static bool IsTableRow(string line)
        => line.StartsWith('|') && line.EndsWith('|') && line.Count(x => x == '|') >= 2;

    /// <summary>Whether <paramref name="line"/> is the <c>| --- |</c> row under a header.</summary>
    public static bool IsSeparatorRow(string line)
        => IsTableRow(line) && SplitRow(line).All(cell =>
        {
            var trimmed = cell.Replace(" ", string.Empty, StringComparison.Ordinal);
            return trimmed.Length >= 3 && trimmed.All(x => x is '-' or ':');
        });

    /// <summary>The trimmed cells of a table row. <c>\|</c> is a pipe inside a cell, as in GitHub's tables.</summary>
    public static IReadOnlyList<string> SplitRow(string line)
    {
        var inner = line.Trim()[1..^1];
        if (!inner.Contains(@"\|", StringComparison.Ordinal))
        {
            return inner.Split('|').Select(x => x.Trim()).ToArray();
        }

        List<string> cells = [];
        System.Text.StringBuilder cell = new();
        for (var i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '\\' && i + 1 < inner.Length && inner[i + 1] == '|')
            {
                cell.Append('|');
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
