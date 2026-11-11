using System.Globalization;
using System.Text;

namespace Bruinet.Cli.Selection;

/// <summary>
/// Where a run's time went, from its timings (<c>timings-*.csv</c>, <c>BRUINET_TIMINGS</c>): the host's start, time by
/// kind, per test, and the slowest spans of each kind. Spans nest (a step holds its calls), so the kinds' sums overlap;
/// a test's "outside" is its time less its binding, scenario and report spans, the part nothing names yet.
/// </summary>
/// <param name="Rows">Every row of the files but their clock rows.</param>
internal sealed record TimingsReport(IReadOnlyList<TimingRow> Rows)
{
    /// <summary>The report's file, beside the timings.</summary>
    public const string FileName = "timings.md";

    /// <summary>The rows of the timings files.</summary>
    public static TimingsReport Read(IEnumerable<string> csvFiles)
    {
        var rows = new List<TimingRow>();
        foreach (var csv in csvFiles)
        {
            using var reader = new StreamReader(csv);
            var header = reader.ReadLine()?.Split(';') ?? [];
            int Column(string name) => Array.IndexOf(header, name) is var at and >= 0 ? at : throw new FormatException($"{csv} has no {name} column: not a timings file.");
            var (run, head, test, kind, name, page, control, start, duration) =
                (Column("Run"), Column("Head"), Column("Test"), Column("Kind"), Column("Name"), Column("Page"), Column("Control"), Column("StartMs"), Column("DurationMs"));
            while (reader.ReadLine() is { } line)
            {
                var cells = line.Split(';');
                if (cells.Length < header.Length || cells[kind] == "clock")
                    continue;
                rows.Add(new TimingRow(cells[run], cells[head], cells[test], cells[kind], cells[name], cells[page], cells[control],
                    long.Parse(cells[start], CultureInfo.InvariantCulture), long.Parse(cells[duration], CultureInfo.InvariantCulture)));
            }
        }

        return new TimingsReport(rows);
    }

    /// <summary>The report as Markdown, listing <paramref name="top"/> of the slowest per kind.</summary>
    public string ToMarkdown(int top)
    {
        var text = new StringBuilder();
        void Line(string line = "") => text.Append(line).Append('\n');

        Line("# Timings");
        Line();
        Line($"Runs: {string.Join(", ", Rows.Select(row => row.Run).Distinct().Order(StringComparer.Ordinal))}; "
            + $"heads: {string.Join(", ", Rows.Select(row => row.Head).Distinct().Order(StringComparer.Ordinal))}; rows: {Rows.Count}.");

        Line();
        Line("## The host's start");
        Line();
        Line("| Span | s |");
        Line("| --- | --- |");
        foreach (var span in Rows.Where(row => row.Kind is "host" or "fixture").OrderBy(row => row.StartMs))
            Line($"| {span.Kind} {span.Name} | {Seconds(span.DurationMs)} |");

        Line();
        Line("## By kind");
        Line();
        Line("| Kind | Count | Summed s |");
        Line("| --- | --- | --- |");
        foreach (var group in Rows.GroupBy(row => row.Kind).OrderByDescending(group => Sum(group)))
            Line($"| {group.Key} | {group.Count()} | {Seconds(Sum(group))} |");

        Line();
        Line("## Per test");
        Line();
        Line("| Test | Whole s | Bind s | Lifecycle s | Steps s | Calls s | Page loads s | Arrange s | Outside s |");
        Line("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var test in Rows.Where(row => row.Test.Length > 0).GroupBy(row => row.Test).OrderByDescending(test => Of(test, "test")))
        {
            var whole = Of(test, "test");
            // With a scenario, what its spans name; without one, what its steps and calls do.
            var outside = Of(test, "scenario") > 0
                ? whole - Of(test, "bind") - Of(test, "scenario") - Of(test, "report")
                : whole - Of(test, "step") - Of(test, "call");
            Line($"| {test.Key} | {Seconds(whole)} | {Seconds(Of(test, "bind"))} | {Seconds(Of(test, "lifecycle"))} | {Seconds(Of(test, "step"))} | {Seconds(Of(test, "call"))} | {Seconds(Of(test, "navigate"))} | {Seconds(Of(test, "arrange"))} | {Seconds(outside)} |");
        }

        foreach (var kind in new[] { "bind", "lifecycle", "navigate", "arrange", "step", "call" })
        {
            var slow = Rows.Where(row => row.Kind == kind).OrderByDescending(row => row.DurationMs).Take(top).ToList();
            if (slow.Count == 0)
                continue;
            Line();
            Line($"## The slowest {kind}");
            Line();
            Line("| ms | Name | Page | Control | Test |");
            Line("| --- | --- | --- | --- | --- |");
            foreach (var row in slow)
                Line($"| {row.DurationMs.ToString(CultureInfo.InvariantCulture)} | {row.Name} | {row.Page} | {row.Control} | {row.Test} |");
        }

        return text.ToString();
    }

    private static long Sum(IEnumerable<TimingRow> rows) => rows.Sum(row => row.DurationMs);

    private static long Of(IEnumerable<TimingRow> rows, string kind) => Sum(rows.Where(row => row.Kind == kind));

    private static string Seconds(long ms) => (ms / 1000.0).ToString("0.0", CultureInfo.InvariantCulture);
}

/// <summary>One row of a timings file: a control call or a span.</summary>
internal sealed record TimingRow(string Run, string Head, string Test, string Kind, string Name, string Page, string Control, long StartMs, long DurationMs);
