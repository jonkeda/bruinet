using System.Runtime.CompilerServices;
using Bruinet.Core.Calls;
using Bruinet.Core.Scopes;

namespace Bruinet.Wpf;

/// <summary>
/// Base class for WPF page objects, containers, rows and controls.
/// </summary>
public abstract class ObjectBase
{
    /// <summary>The test context the object belongs to.</summary>
    public abstract IWpfTestContext Context { get; }

    protected int DefaultTimeoutMs => Context.Timeouts.DefaultWait;

    protected int PollingIntervalMs => Context.Timeouts.PollingInterval;

    /// <summary>The name this object's own calls are logged under.</summary>
    protected virtual string ScopeName => GetType().Name;

    /// <summary>
    /// A scope-level wait as one call (R1): polls <paramref name="probe"/> until it reports Done or
    /// the budget runs out. Answers false when it ran out, or throws what
    /// <paramref name="fail"/> builds from the last readiness seen.
    /// </summary>
    private protected bool RunProbe(
        Func<Observation> probe,
        int budgetMs,
        Func<ScopeReadiness, Exception>? fail = null,
        [CallerMemberName] string? caller = null)
    {
        caller ??= nameof(RunProbe);
        return new ControlCall(Context.Logger, Context.Name, Context.NearMiss, ScopeName, ScopeName).Run(caller, null, budgetMs, Context.Timeouts.Animation, attempt =>
        {
            if (Poller.Until(_ => probe(), attempt, PollingIntervalMs))
            {
                return true;
            }

            var last = attempt.Log.Last;
            if (fail != null)
            {
                throw fail(last.Scope ?? new ScopeReadiness(ScopeName, ScopeReadinessState.NotLoaded, last.ToString()));
            }

            return last.Kind == ObservationKind.Failed && last.Error is { } error
                ? throw attempt.Log.Unexpected(error, $"'{ScopeName}'", budgetMs)
                : false;
        }, succeeded: ok => ok);
    }

    /// <summary>
    /// A scope's <c>WaitFirst</c> as one call: polls <paramref name="outcomes"/> and answers the
    /// index of the first that holds, or -1. Each attempt checks only that the app is reachable.
    /// </summary>
    private protected int RunFirst(int? timeoutMs, Func<bool>[] outcomes)
        => FirstOutcome.Wait(
            new ControlCall(Context.Logger, Context.Name, Context.NearMiss, ScopeName, ScopeName),
            Context.ProbeReadiness, outcomes, timeoutMs ?? DefaultTimeoutMs, Context.Timeouts.Animation, PollingIntervalMs);
}
