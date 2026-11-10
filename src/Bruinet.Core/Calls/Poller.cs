using Bruinet.Core.Exceptions;
using Bruinet.Core.Utilities;

namespace Bruinet.Core.Calls;

/// <summary>
/// The one retry loop of a call.
/// </summary>
public static class Poller
{
    /// <summary>How close to the end of the budget a driver's timeout still counts as the budget running out.</summary>
    private const int BudgetSlopMs = 10;

    /// <summary>
    /// Runs <paramref name="attempt"/> until it reports <see cref="ObservationKind.Done"/> or the
    /// deadline passes.
    /// </summary>
    /// <param name="attempt">The work performed on each poll tick.</param>
    /// <param name="context">The attempt context passed to <paramref name="attempt"/>.</param>
    /// <param name="pollingIntervalMs">Pause between attempts, in milliseconds.</param>
    /// <param name="stop">
    /// Whether an observation ends the loop early, as a final answer that is not Done: the end of a
    /// list a search has scrolled through. Null never stops early.
    /// </param>
    /// <returns>Whether an attempt reported Done.</returns>
    public static bool Until(Func<AttemptContext, Observation> attempt, AttemptContext context, int pollingIntervalMs,
        Func<Observation, bool>? stop = null)
    {
        while (true)
        {
            Observation observation;
            try
            {
                // Readiness answers are kept for this attempt only (AttemptMemo). The first attempt
                // may also use what an enclosing attempt read; a retry reads afresh.
                using (AttemptMemo.Begin(inherit: context.Log.Attempts == 0))
                {
                    observation = attempt(context);
                }
            }
            catch (TimeoutException) when (context.Deadline.RemainingMs <= BudgetSlopMs && context.Log.Attempts > 0)
            {
                // The budget ran out in the middle of this attempt: a driver call below it was
                // given what was left (?A5) and used it up. That is not something the attempt saw,
                // so the call is reported by the last attempt that completed. The driver's timer
                // is its own, and fires a few milliseconds before this deadline's clock does.
                return false;
            }
            catch (Exception error) when (!IsFatal(error))
            {
                observation = Observation.Failed(error);
            }

            context.Log.Add(observation, context.Deadline.ElapsedMs);

            if (observation.Kind == ObservationKind.Done)
            {
                return true;
            }

            if (stop?.Invoke(observation) == true)
            {
                return false;
            }

            var remaining = context.Deadline.RemainingMs;
            if (remaining <= 0)
            {
                return false;
            }

            WaitHelper.Pause(Math.Max(1, Math.Min(pollingIntervalMs, remaining)));
        }
    }

    /// <summary>Whether <paramref name="error"/> must end the call at once rather than be retried.</summary>
    public static bool IsFatal(Exception error) => error is AppUnavailableException
        or RouteUnavailableException
        or IEndsCall
        or ScopeNotReadyException { Readiness.IsConfigurationError: true };
}
