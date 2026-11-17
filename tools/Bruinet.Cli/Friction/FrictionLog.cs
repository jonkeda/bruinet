using System.Globalization;
using System.Text;

namespace Bruinet.Cli.Friction;

/// <summary>
/// A suite's friction log (<c>.testing/400-FrictionLog.md</c>): a table of
/// <c>| # | Date | Stage | What happened | Cost | Workaround | Change | Where |</c>. A row is added at the table's
/// end with the next number, so nobody counts, and no script is written for it.
/// </summary>
internal static class FrictionLog
{
    public const string FileName = "400-FrictionLog.md";

    /// <summary>The log of the suite a folder is in: <c>.testing/400-FrictionLog.md</c> here or in a folder above; null when none.</summary>
    public static string? Find(string from)
    {
        for (var folder = new DirectoryInfo(from); folder is not null; folder = folder.Parent)
        {
            var log = Path.Combine(folder.FullName, ".testing", FileName);
            if (File.Exists(log))
                return log;
        }

        return null;
    }

    /// <summary>The number the next row gets: one more than the highest a row of the log starts with.</summary>
    public static int NextNumber(string markdown)
        => markdown.Split('\n').Select(Number).DefaultIfEmpty(0).Max() + 1;

    /// <summary>A row of the log; a cell's line ends become spaces and its bars are escaped.</summary>
    public static string Row(int number, DateOnly date, string stage, string what, string cost, string workaround, string change, string where)
        => $"| {number} | {date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} | {Cell(stage)} | {Cell(what)} | {Cell(cost)} | {Cell(workaround)} | {Cell(change)} | {Cell(where)} |";

    /// <summary>The log with a row added after its last row, in the log's own line ends; the row's number.</summary>
    /// <exception cref="FormatException">The log has no table to add to.</exception>
    public static (string Markdown, int Number) Add(string markdown, DateOnly date, string stage, string what, string cost, string workaround, string change, string where)
    {
        var lineEnd = markdown.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        var last = lines.FindLastIndex(line => line.TrimStart().StartsWith('|'));
        if (last < 0)
            throw new FormatException("The friction log has no table: it starts with | # | Date | Stage | What happened | Cost | Workaround | Change | Where |.");
        var number = NextNumber(markdown);
        lines.Insert(last + 1, Row(number, date, stage, what, cost, workaround, change, where));
        return (new StringBuilder().AppendJoin(lineEnd, lines).ToString(), number);
    }

    private static int Number(string line)
    {
        var cells = line.Split('|');
        return cells.Length > 2 && int.TryParse(cells[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    private static string Cell(string text)
        => string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Replace("|", "\\|", StringComparison.Ordinal).Trim() is { Length: > 0 } cell ? cell : "None";
}
