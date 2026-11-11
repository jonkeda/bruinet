using Bruinet.Core.Exceptions;
using Bruinet.Core.Scopes;

namespace Bruinet.Core.Calls;

/// <summary>
/// The readiness step every attempt of a call starts with, and the failure it produces.
/// </summary>
public static class ScopeGate
{
    /// <summary>
    /// One probe of a scope: null when it is ready, otherwise the observation to report.
    /// </summary>
    /// <param name="readiness">What the scope's probe found.</param>
    /// <exception cref="ScopeNotReadyException">
    /// The scope is misconfigured (a busy signal that is missing or unreadable). No amount of
    /// waiting fixes that, so the call fails at once (R0).
    /// </exception>
    public static Observation? Check(ScopeReadiness readiness)
    {
        if (readiness.IsReady)
        {
            return null;
        }

        if (readiness.IsConfigurationError)
        {
            throw Misconfigured(readiness);
        }

        return Observation.ScopeNotReady(readiness);
    }

    /// <summary>The failure of a scope that is misconfigured, whatever call asked.</summary>
    public static ScopeNotReadyException Misconfigured(ScopeReadiness readiness)
        => new(readiness, $"Scope '{readiness.ScopeName}' is misconfigured ({readiness.State}): {readiness.Detail}");

    /// <summary>The failure of a call whose scope never became ready.</summary>
    /// <param name="readiness">What the last probe found.</param>
    /// <param name="caller">The public member.</param>
    /// <param name="subject">"control 'AutomationId:X'" or "container 'AutomationId:Y'".</param>
    /// <param name="budgetMs">The call's budget.</param>
    /// <param name="locator">The control's locator, kept on the exception for a tool to read whole.</param>
    public static ScopeNotReadyException NotReady(ScopeReadiness readiness, string caller, string subject, int budgetMs, Bruinet.Core.Locators.Locator? locator = null)
        => new(readiness,
            $"Scope '{readiness.ScopeName}' did not become ready for {caller} on {subject} within {budgetMs} ms. " +
            $"Last readiness state: {readiness.State}" +
            (readiness.Detail is null ? "." : $" ({readiness.Detail}).") +
            (Hint(readiness.State) is { } hint ? " " + hint : string.Empty))
        {
            Locator = locator,
        };

    // One closing line per kind of not ready: what to look at next (review 01, proposal 6).
    private static string? Hint(ScopeReadinessState state) => state switch
    {
        ScopeReadinessState.NotLoaded => "Is the app on this page? Its ready element did not show: the step before may have gone elsewhere.",
        ScopeReadinessState.MissingRoot or ScopeReadinessState.StaleRoot => "Its root element was not there: the container may be closed, or not open yet.",
        ScopeReadinessState.Busy => "The page stayed busy: what keeps it busy (a spinner, a pending call) shows in the step's trace.",
        ScopeReadinessState.ContentNotReady => "Its content check did not hold: the data it waits for did not come.",
        _ => null,
    };
}
