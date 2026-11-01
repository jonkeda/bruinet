using System.Globalization;
using System.Text;
using System.Text.Json;
using Bruinet.Mocking.Data;

namespace Bruinet.Cli.Sql;

/// <summary>One test's window on the wall clock, from a run's timings (its <c>test</c> span).</summary>
internal sealed record TestWindow(string Test, DateTime Start, DateTime End);

/// <summary>
/// What one test sent: its own steps' commands and those of the arrange before it (its reset and seed); its objects are
/// its own steps', as a schema change would reach it.
/// </summary>
internal sealed record TestDatabase(
    string Test,
    double TestMs,
    double ArrangeMs,
    double DatabaseMs,
    double ArrangeDatabaseMs,
    int Commands,
    IReadOnlyList<string> Objects,
    IReadOnlyList<Repeat> Repeats)
{
    /// <summary>The database's share of the test's own steps; null without timings.</summary>
    public double? Share => TestMs > 0 ? DatabaseMs / TestMs : null;

    /// <summary>The database's share of the test with its arrange.</summary>
    public double? ShareWithArrange => TestMs + ArrangeMs > 0 ? (DatabaseMs + ArrangeDatabaseMs) / (TestMs + ArrangeMs) : null;
}

/// <summary>A command one test sent many times.</summary>
internal sealed record Repeat(string Text, int Count);

/// <summary>One object and the tests that touch it, directly or through a view or procedure.</summary>
internal sealed record ObjectFanOut(string Object, int Tests, double Share);

/// <summary>
/// <c>bruinet sql report</c>: the log joined to the tests, what each test spent in the database, and how far each
/// object reaches (.my/sql-replay/plan.md, 1.6). A command belongs to the test whose window it started in; one that
/// started between two tests belongs to the next one's arrange (xUnit makes and resets a test's fixture before its
/// window); one after the last window to none. By the start, not the end: a reset's last write can end within a
/// millisecond of the window opening, and the server's clock and this machine's differ by about that much. Without timings, the log's own test names are used, and there are no shares.
/// </summary>
internal sealed class SqlReport
{
    /// <summary>A command sent more often than this in one test is listed as a repeat.</summary>
    public const int RepeatThreshold = 10;

    private SqlReport(IReadOnlyList<TestDatabase> tests, IReadOnlyList<ObjectFanOut> objects, int unattributed, double unattributedMs, int entries)
    {
        Tests = tests;
        Objects = objects;
        Unattributed = unattributed;
        UnattributedMs = unattributedMs;
        Entries = entries;
    }

    public IReadOnlyList<TestDatabase> Tests { get; }

    public IReadOnlyList<ObjectFanOut> Objects { get; }

    public int Unattributed { get; }

    public double UnattributedMs { get; }

    public int Entries { get; }

    /// <summary>The report of <paramref name="entries"/>, joined to <paramref name="windows"/> when there are any.</summary>
    public static SqlReport Of(IEnumerable<SqlLogEntry> entries, IReadOnlyList<TestWindow> windows, SchemaSnapshot? schema)
    {
        var ordered = windows.OrderBy(window => window.Start).ToList();
        var byTest = new Dictionary<string, (List<SqlLogEntry> Own, List<SqlLogEntry> Arrange)>(StringComparer.Ordinal);
        var all = entries.ToList();
        var unattributed = new List<SqlLogEntry>();
        foreach (var entry in all)
        {
            var (test, arrange) = ordered.Count > 0 ? Attribute(entry, ordered) : (entry.Test, false);
            if (test is null)
            {
                unattributed.Add(entry);
                continue;
            }

            if (!byTest.TryGetValue(test, out var lists))
            {
                byTest[test] = lists = ([], []);
            }

            (arrange ? lists.Arrange : lists.Own).Add(entry);
        }

        var tests = new List<TestDatabase>();
        var names = ordered.Count > 0 ? ordered.Select(window => window.Test).Distinct().ToList() : byTest.Keys.Order(StringComparer.Ordinal).ToList();
        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];
            var (own, arrange) = byTest.TryGetValue(name, out var lists) ? lists : ([], []);
            var window = ordered.FirstOrDefault(w => w.Test == name);
            var testMs = window is null ? 0 : (window.End - window.Start).TotalMilliseconds;
            var arrangeMs = window is null ? 0 : ArrangeMs(window, ordered, arrange);
            // The test's own steps only: a reset touches every table it clears, and would make every object reach every test.
            var named = own.SelectMany(entry => entry.Objects ?? []);
            // With a schema, only its objects: a name in the text that isn't one (an alias, a system procedure) doesn't count.
            var objects = (schema is null
                    ? named.Distinct(StringComparer.OrdinalIgnoreCase)
                    : schema.Expand(named).Where(name => schema.Objects.ContainsKey(name)))
                .Order(StringComparer.Ordinal)
                .ToList();
            var repeats = own.Where(entry => entry.Text is not null)
                .GroupBy(entry => entry.Text!, StringComparer.Ordinal)
                .Where(group => group.Count() > RepeatThreshold)
                .Select(group => new Repeat(group.Key, group.Count()))
                .OrderByDescending(repeat => repeat.Count)
                .ToList();
            tests.Add(new TestDatabase(name, testMs, arrangeMs, own.Sum(entry => entry.Ms), arrange.Sum(entry => entry.Ms), own.Count + arrange.Count, objects, repeats));
        }

        var testCount = Math.Max(1, tests.Count);
        var fanOut = tests.SelectMany(test => test.Objects.Select(item => (item, test.Test)))
            .GroupBy(pair => pair.item, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ObjectFanOut(group.Key, group.Select(pair => pair.Test).Distinct().Count(), group.Select(pair => pair.Test).Distinct().Count() / (double)testCount))
            .OrderByDescending(item => item.Tests)
            .ThenBy(item => item.Object, StringComparer.Ordinal)
            .ToList();
        return new SqlReport(tests, fanOut, unattributed.Count, unattributed.Sum(entry => entry.Ms), all.Count);
    }

    /// <summary>The test windows of a run's timings files: each file's <c>clock</c> row and its <c>test</c> spans.</summary>
    public static IReadOnlyList<TestWindow> Windows(IEnumerable<string> csvFiles)
    {
        var windows = new List<TestWindow>();
        foreach (var file in csvFiles)
        {
            using var reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            var header = reader.ReadLine()?.Split(';') ?? [];
            int Column(string name) => Array.IndexOf(header, name);
            var (test, scenario, kind, name, start, duration) = (Column("Test"), Column("Scenario"), Column("Kind"), Column("Name"), Column("StartMs"), Column("DurationMs"));
            DateTime? zero = null;
            while (reader.ReadLine() is { } line)
            {
                var cells = line.Split(';');
                if (cells.Length < header.Length)
                {
                    continue;
                }

                if (cells[kind] == "clock")
                {
                    zero = DateTime.Parse(cells[name], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
                }
                else if (cells[kind] == "test" && zero is { } at && (scenario >= 0 && cells[scenario].Length > 0 ? cells[scenario] : cells[test]) is { Length: > 0 } owner)
                {
                    // A scenario's span is named by its scenario: every scenario of a suite runs in the one xUnit method.
                    var from = at.AddMilliseconds(long.Parse(cells[start], CultureInfo.InvariantCulture));
                    windows.Add(new TestWindow(owner, from, from.AddMilliseconds(long.Parse(cells[duration], CultureInfo.InvariantCulture))));
                }
            }
        }

        // The innermost windows only: the xUnit method's span wraps the scenario's, which is the test a command belongs to.
        return windows
            .Where(outer => !windows.Any(inner => !ReferenceEquals(inner, outer) && inner.Start >= outer.Start && inner.End <= outer.End && (inner.Start > outer.Start || inner.End < outer.End)))
            .ToList();
    }

    /// <summary>The map of each object to the tests that touch it, for selecting tests from a schema change.</summary>
    public object TouchMap() => new
    {
        objects = Tests.SelectMany(test => test.Objects.Select(item => (item, test.Test)))
            .GroupBy(pair => pair.item, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.Test).Distinct().Order(StringComparer.Ordinal).ToArray()),
    };

    public string ToJson() => JsonSerializer.Serialize(new
    {
        entries = Entries,
        unattributed = Unattributed,
        unattributedMs = UnattributedMs,
        suite = Suite(),
        tests = Tests.Select(test => new
        {
            test.Test,
            test.TestMs,
            test.ArrangeMs,
            test.DatabaseMs,
            test.ArrangeDatabaseMs,
            test.Commands,
            share = test.Share,
            shareWithArrange = test.ShareWithArrange,
            test.Objects,
            test.Repeats,
        }),
        objects = Objects,
    }, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    /// <summary>The report for a person: the suite's shares, the fan-out, the repeats, and the tests one by one.</summary>
    public string ToMarkdown()
    {
        var suite = Suite();
        var text = new StringBuilder();
        text.AppendLine("# SQL log report").AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"{Tests.Count} tests, {Entries} commands, {Objects.Count} objects touched. {Unattributed} commands ({UnattributedMs:0} ms) fell outside every test.").AppendLine();
        text.AppendLine("## The database's share").AppendLine();
        if (suite.TestsWithTime == 0)
        {
            text.AppendLine("No timings were given, so there are no shares: run the suite with `BRUINET_TIMINGS` set and pass `--timings`.").AppendLine();
        }
        else
        {
            text.AppendLine("| Share | Median | p90 |").AppendLine("| --- | --- | --- |");
            text.AppendLine(CultureInfo.InvariantCulture, $"| The test's own steps | {suite.Median:P1} | {suite.P90:P1} |");
            text.AppendLine(CultureInfo.InvariantCulture, $"| With its arrange (reset, seed) | {suite.MedianWithArrange:P1} | {suite.P90WithArrange:P1} |").AppendLine();
            text.AppendLine(CultureInfo.InvariantCulture, $"The database took {suite.DatabaseMs:0} ms of the tests' {suite.TestMs:0} ms, and {suite.ArrangeDatabaseMs:0} ms of their arrange's {suite.ArrangeMs:0} ms.").AppendLine();
        }

        text.AppendLine("## How far each object reaches").AppendLine();
        if (Objects.Count > 0)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"An object reaches a median {Percentile(Objects.Select(item => item.Share), 0.5):P1} of the tests, p90 {Percentile(Objects.Select(item => item.Share), 0.9):P1}.").AppendLine();
            text.AppendLine("| Object | Tests | Share |").AppendLine("| --- | --- | --- |");
            foreach (var item in Objects.Take(20))
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"| `{item.Object}` | {item.Tests} | {item.Share:P0} |");
            }

            text.AppendLine();
        }

        var repeats = Tests.SelectMany(test => test.Repeats.Select(repeat => (test.Test, repeat))).ToList();
        if (repeats.Count > 0)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"## Commands sent more than {RepeatThreshold} times in one test").AppendLine();
            text.AppendLine("| Test | Times | Command |").AppendLine("| --- | --- | --- |");
            foreach (var (test, repeat) in repeats.OrderByDescending(pair => pair.repeat.Count).Take(20))
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"| {test} | {repeat.Count} | `{Short(repeat.Text)}` |");
            }

            text.AppendLine();
        }

        text.AppendLine("## Tests").AppendLine();
        text.AppendLine("| Test | Test ms | Database ms | Share | Arrange ms | Its database ms | Commands | Objects |").AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var test in Tests)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"| {test.Test} | {test.TestMs:0} | {test.DatabaseMs:0.0} | {Percent(test.Share)} | {test.ArrangeMs:0} | {test.ArrangeDatabaseMs:0.0} | {test.Commands} | {test.Objects.Count} |");
        }

        return text.ToString();
    }

    private Suite Suite()
    {
        var timed = Tests.Where(test => test.TestMs > 0).ToList();
        return new Suite(
            timed.Count,
            Percentile(timed.Select(test => test.Share!.Value), 0.5),
            Percentile(timed.Select(test => test.Share!.Value), 0.9),
            Percentile(timed.Select(test => test.ShareWithArrange!.Value), 0.5),
            Percentile(timed.Select(test => test.ShareWithArrange!.Value), 0.9),
            timed.Sum(test => test.TestMs),
            timed.Sum(test => test.DatabaseMs),
            timed.Sum(test => test.ArrangeMs),
            timed.Sum(test => test.ArrangeDatabaseMs));
    }

    // The test the entry started in, or the next test's arrange when it started between two windows; null after the last.
    private static (string? Test, bool Arrange) Attribute(SqlLogEntry entry, List<TestWindow> windows)
    {
        var start = entry.Start;
        for (var i = 0; i < windows.Count; i++)
        {
            if (start <= windows[i].End && start >= windows[i].Start)
            {
                return (windows[i].Test, false);
            }

            if (start < windows[i].Start && (i == 0 || start > windows[i - 1].End))
            {
                return (windows[i].Test, true);
            }
        }

        return (null, false);
    }

    // The gap before the test, back to the window before it; the first test's back to its earliest arrange command.
    private static double ArrangeMs(TestWindow window, List<TestWindow> windows, List<SqlLogEntry> arrange)
    {
        var index = windows.IndexOf(window);
        var from = index > 0 ? windows[index - 1].End : arrange.Count > 0 ? arrange.Min(entry => entry.Start) : window.Start;
        return Math.Max(0, (window.Start - from).TotalMilliseconds);
    }

    private static double Percentile(IEnumerable<double> values, double fraction)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0 : sorted[(int)Math.Clamp(Math.Ceiling(fraction * sorted.Count) - 1, 0, sorted.Count - 1)];
    }

    private static string Percent(double? value) => value is { } share ? share.ToString("P1", CultureInfo.InvariantCulture) : "-";

    private static string Short(string text) => (text.Length > 90 ? text[..90] + "..." : text).Replace("|", "\\|", StringComparison.Ordinal);
}

internal sealed record Suite(
    int TestsWithTime,
    double Median,
    double P90,
    double MedianWithArrange,
    double P90WithArrange,
    double TestMs,
    double DatabaseMs,
    double ArrangeMs,
    double ArrangeDatabaseMs);
