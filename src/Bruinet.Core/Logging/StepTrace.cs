using System.Runtime.CompilerServices;
using Bruinet.Core.Testing;

namespace Bruinet.Core.Logging;

/// <summary>A control call a step made: what it drove, how long it took, how often it tried.</summary>
/// <param name="Page">The page object, or the container's scope, the control is on.</param>
/// <param name="Control">The control.</param>
/// <param name="Action">What was done or asked (<c>Tap</c>, <c>GetText</c>, <c>Exists</c>).</param>
/// <param name="AtMs">When it started, on <see cref="Timing.NowMs"/>'s clock.</param>
/// <param name="DurationMs">How long it took: a wait shows as a long call.</param>
/// <param name="Attempts">How many tries it took.</param>
/// <param name="Result"><c>success</c> or <c>error</c>.</param>
public sealed record StepTraceCall(string Page, string Control, string Action, long AtMs, int DurationMs, int Attempts, string Result);

/// <summary>
/// The control calls of each step (AI8): kept with the step's scope, so a scenario's trace can say what each of its steps
/// drove, however many other tests run beside it. A call made outside a step (a fixture's start, a page read for the
/// trace) is no step's and is not kept. Every scenario that passes or fails is traced; <c>BRUINET_TRACE=off</c> keeps
/// none; <c>all</c> also reads the page after every step.
/// </summary>
public static class StepTrace
{
    /// <summary><c>off</c> keeps nothing; <c>all</c> reads the page after every step; anything else after a failed one.</summary>
    public const string Variable = "BRUINET_TRACE";

    private const int KeptPerStep = 500;

    private static readonly ConditionalWeakTable<TestScope, List<StepTraceCall>> Steps = new();
    private static string? _mode = Environment.GetEnvironmentVariable(Variable);

    /// <summary>Whether calls are kept: not with <c>BRUINET_TRACE=off</c>.</summary>
    public static bool Enabled => !string.Equals(_mode, "off", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the page is read after every step for the trace, not only after a failed one.</summary>
    public static bool All => string.Equals(_mode, "all", StringComparison.OrdinalIgnoreCase);

    /// <summary>The calls made in <paramref name="step"/> (the very scope, not one alike), oldest first; the first 500.</summary>
    /// <param name="step">The step's scope, <see cref="TestScope.Current"/> while it ran.</param>
    public static IReadOnlyList<StepTraceCall> Of(TestScope step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (!Steps.TryGetValue(step, out var calls))
        {
            return [];
        }

        lock (calls)
        {
            return [.. calls];
        }
    }

    // A call, under the step it runs in; nothing outside a step, nothing when off, and nothing made before both are known.
    internal static void Add(string page, string control, string action, long atMs, int durationMs, int attempts, string result)
    {
        var scope = TestScope.Current;
        if (scope.Step is null || !Enabled)
        {
            return;
        }

        var calls = Steps.GetValue(scope, _ => []);
        lock (calls)
        {
            if (calls.Count < KeptPerStep)
            {
                calls.Add(new StepTraceCall(page, control, action, atMs, durationMs, attempts, result));
            }
        }
    }

    // For tests: the mode as BRUINET_TRACE would set it, until disposed.
    internal static IDisposable Use(string? mode)
    {
        var before = _mode;
        _mode = mode;
        return new Restore(() => _mode = before);
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
