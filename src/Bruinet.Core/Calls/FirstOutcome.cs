using Bruinet.Core.Scopes;

namespace Bruinet.Core.Calls;

/// <summary>
/// The body of every stack's <c>WaitFirst</c>: one call that polls several answers about now and
/// reports the first that holds.
/// </summary>
/// <remarks>
/// Each attempt checks only that the app is reachable, never the scope the call stands in: an
/// outcome such as "the sheet is gone" must be able to hold while that scope, or its parent, is not.
/// </remarks>
internal static class FirstOutcome
{
    /// <summary>The member name every stack logs the call under.</summary>
    public const string Caller = "WaitFirst";

    /// <summary>Runs the wait as one call on <paramref name="call"/>.</summary>
    /// <param name="call">The scope's call, for the log.</param>
    /// <param name="appReadiness">One probe of whether the app is reachable.</param>
    /// <param name="outcomes">Answers about now; none may wait itself.</param>
    /// <param name="budgetMs">The whole budget.</param>
    /// <param name="animationMs">The scroll throttle interval.</param>
    /// <param name="pollingIntervalMs">The pause between attempts.</param>
    /// <returns>The index of the first outcome that held, or -1 when none did within the budget.</returns>
    public static int Wait(
        ControlCall call,
        Func<ScopeReadiness> appReadiness,
        Func<bool>[] outcomes,
        int budgetMs,
        int animationMs,
        int pollingIntervalMs)
    {
        ArgumentNullException.ThrowIfNull(appReadiness);
        ArgumentNullException.ThrowIfNull(outcomes);
        if (outcomes.Length == 0)
            throw new ArgumentException("Give at least one outcome.", nameof(outcomes));

        return call.Run(Caller, $"{outcomes.Length} outcomes", budgetMs, animationMs, context =>
        {
            var held = -1;
            var met = Poller.Until(_ =>
            {
                if (ScopeGate.Check(appReadiness()) is { } notReady)
                    return notReady;

                held = Array.FindIndex(outcomes, outcome => outcome());
                return held >= 0 ? Observation.Done() : Observation.Pending("no outcome held");
            }, context, pollingIntervalMs);

            if (!met && context.Log.Last is { Kind: ObservationKind.Failed, Error: { } error })
            {
                var failure = context.Log.Unexpected(error, Caller, budgetMs);
                if (ReferenceEquals(failure, error))
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
                throw failure;
            }

            return met ? held : -1;
        }, succeeded: index => index >= 0);
    }
}
