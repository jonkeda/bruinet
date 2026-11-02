using Bruinet.Core.Testing;

namespace Bruinet.Core.Logging;

/// <summary>A control call a step made: what it drove, how long it took, how often it tried.</summary>
/// <param name="Page">The page object.</param>
/// <param name="Control">The control.</param>
/// <param name="Action">What was done or asked (<c>Tap</c>, <c>GetText</c>, <c>Exists</c>).</param>
/// <param name="AtMs">When it started, on <see cref="Timing.NowMs"/>'s clock.</param>
/// <param name="DurationMs">How long it took: a wait shows as a long call.</param>
/// <param name="Attempts">How many tries it took.</param>
/// <param name="Result"><c>success</c> or <c>error</c>.</param>
public sealed record StepTraceCall(string Page, string Control, string Action, long AtMs, int DurationMs, int Attempts, string Result);

/// <summary>
/// The control calls of each step (AI8), kept as <see cref="StepLog"/> keeps what the app said: under the scope they ran
/// in, a few thousand in all, so a scenario's trace can say what each of its steps drove. <c>BRUINET_TRACE=off</c>
/// keeps none; <c>all</c> asks for a trace of every scenario, a pass too.
/// </summary>
public static class StepTrace
{
    /// <summary><c>off</c> keeps nothing; <c>all</c> traces passes too; anything else traces failures.</summary>
    public const string Variable = "BRUINET_TRACE";

    private const int Kept = 2000;

    private static readonly object Gate = new();
    private static readonly LinkedList<(TestScope Scope, StepTraceCall Call)> Calls = [];

    /// <summary>Whether calls are kept: not with <c>BRUINET_TRACE=off</c>.</summary>
    public static bool Enabled { get; } = !string.Equals(Environment.GetEnvironmentVariable(Variable), "off", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether every scenario is traced, a pass too, with the page read after each step.</summary>
    public static bool All { get; } = string.Equals(Environment.GetEnvironmentVariable(Variable), "all", StringComparison.OrdinalIgnoreCase);

    /// <summary>Keeps a call under the scope it runs in.</summary>
    /// <param name="call">The call.</param>
    public static void Add(StepTraceCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (!Enabled)
        {
            return;
        }

        var scope = TestScope.Current;
        lock (Gate)
        {
            Calls.AddLast((scope, call));
            if (Calls.Count > Kept)
            {
                Calls.RemoveFirst();
            }
        }
    }

    /// <summary>The calls made in <paramref name="step"/> (the very scope, not one alike), oldest first.</summary>
    /// <param name="step">The step's scope, <see cref="TestScope.Current"/> while it ran.</param>
    public static IReadOnlyList<StepTraceCall> Of(TestScope step)
    {
        ArgumentNullException.ThrowIfNull(step);
        lock (Gate)
        {
            return [.. Calls.Where(item => ReferenceEquals(item.Scope, step)).Select(item => item.Call)];
        }
    }
}
