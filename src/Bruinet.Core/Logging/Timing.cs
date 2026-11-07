using System.Diagnostics;
using System.Globalization;
using Bruinet.Core.Testing;

namespace Bruinet.Core.Logging;

/// <summary>
/// A run's timings (.my/test-selection/speed-plan.md, T1): one row per control call and per span of the work between
/// calls (the host's start, a fixture, a scenario's binding and lifecycle, a step, a page load, an arrangement), each
/// with its test, scenario and step (<see cref="TestScope"/>). Written only when <c>BRUINET_TIMINGS</c> names a folder:
/// <c>timings-&lt;run&gt;.csv</c> there, one file per test process. The same rows are the run's touch map: which test
/// used which page and control.
/// </summary>
public static class Timing
{
    /// <summary>The environment variable naming the folder the timings go to.</summary>
    public const string Variable = "BRUINET_TIMINGS";

    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    /// <summary>
    /// When the rows' clock read zero, UTC: each file's first row after the header (kind <c>clock</c>, the time as its
    /// name), so its rows can be joined with what another process logged on the wall clock, such as SQL Server's
    /// Extended Events (<c>bruinet sql report</c>).
    /// </summary>
    public static DateTime ZeroUtc { get; } = DateTime.UtcNow - Clock.Elapsed;
    private static readonly Lazy<TimingFile?> File = new(Open, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly string Run = $"{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}";

    /// <summary>Whether this run writes timings.</summary>
    public static bool Enabled { get; } = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable));

    /// <summary>
    /// A span of work, written when disposed: <paramref name="kind"/> says what sort (<c>host</c>, <c>fixture</c>,
    /// <c>bind</c>, <c>lifecycle</c>, <c>step</c>, <c>navigate</c>, <c>arrange</c>, <c>test</c>), <paramref name="name"/> which one.
    /// </summary>
    public static IDisposable Span(string kind, string name)
        => Enabled && File.Value is { } file ? file.Span(kind, name, TestScope.Current, () => Clock.ElapsedMilliseconds) : NoSpan.Instance;

    /// <summary>One control call, as <c>ControlCall</c> ends it.</summary>
    public static void Call(string page, string control, string action, long startMs, int durationMs, int attempts, string result)
    {
        // The step's trace (AI8) keeps it whether or not timings are written.
        StepTrace.Add(page, control, action, startMs, durationMs, attempts, result);
        if (Enabled)
        {
            File.Value?.Write(TestScope.Current, "call", action, page, control, action, startMs, durationMs, attempts, result);
        }
    }

    /// <summary>
    /// A control asked outside a call (<c>IsVisible</c>, <c>TryGetText</c>): no call row, but the touch map still needs to
    /// know the test reached it. A <c>query</c> row with no duration.
    /// </summary>
    public static void Query(string page, string control, string name)
    {
        if (Enabled)
        {
            File.Value?.Write(TestScope.Current, "query", name, page, control, name, Clock.ElapsedMilliseconds, 0, null, null);
        }
    }

    /// <summary>Milliseconds since the run began, the rows' <c>StartMs</c>.</summary>
    public static long NowMs => Clock.ElapsedMilliseconds;

    private static TimingFile? Open()
    {
        var folder = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        Directory.CreateDirectory(folder);
        var writer = new StreamWriter(Path.Combine(folder, $"timings-{Run}.csv"), append: false) { AutoFlush = true };
        var file = new TimingFile(writer, Run, WebHeads.Current.ToString().ToLowerInvariant(), ZeroUtc);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => file.Dispose();
        return file;
    }

    private sealed class NoSpan : IDisposable
    {
        public static readonly NoSpan Instance = new();

        public void Dispose()
        {
        }
    }
}

/// <summary>
/// The rows of one timings file: a header and the clock row when made, then a line per <see cref="Write"/>. Each line
/// is written whole, under a lock, so threads do not mix their rows.
/// </summary>
internal sealed class TimingFile : IDisposable
{
    internal const string Header = "Run;Head;Test;Scenario;Step;Kind;Name;Page;Control;Action;StartMs;DurationMs;Attempts;Result";

    private readonly object _gate = new();
    private readonly TextWriter _writer;
    private readonly string _run;
    private readonly string _head;

    internal TimingFile(TextWriter writer, string run, string head, DateTime zeroUtc)
    {
        _writer = writer;
        _run = run;
        _head = head;
        _writer.WriteLine(Header);
        _writer.WriteLine(string.Join(';', run, head, string.Empty, string.Empty, string.Empty,
            "clock", zeroUtc.ToString("O", CultureInfo.InvariantCulture), string.Empty, string.Empty, string.Empty, "0", "0", string.Empty, string.Empty));
    }

    internal void Write(TestScope scope, string kind, string name, string? page, string? control, string? action,
        long startMs, long durationMs, int? attempts, string? result)
    {
        var line = string.Join(';',
            _run, _head, Cell(scope.Test), Cell(scope.Scenario), Cell(scope.Step),
            kind, Cell(name), Cell(page), Cell(control), Cell(action),
            startMs.ToString(CultureInfo.InvariantCulture), durationMs.ToString(CultureInfo.InvariantCulture),
            attempts?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, Cell(result));
        lock (_gate)
        {
            _writer.WriteLine(line);
        }
    }

    // A span is written once, when first disposed; its duration is the clock's reading then, less its start.
    internal IDisposable Span(string kind, string name, TestScope scope, Func<long> nowMs)
        => new SpanScope(this, kind, name, scope, nowMs);

    public void Dispose()
    {
        lock (_gate)
        {
            _writer.Dispose();
        }
    }

    // One field: no separator, no line break, inside it.
    private static string Cell(string? value)
        => value is null ? string.Empty : value.Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ');

    private sealed class SpanScope(TimingFile file, string kind, string name, TestScope scope, Func<long> nowMs) : IDisposable
    {
        private readonly long _startMs = nowMs();
        private int _done;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0)
            {
                file.Write(scope, kind, name, null, null, null, _startMs, nowMs() - _startMs, null, null);
            }
        }
    }
}
