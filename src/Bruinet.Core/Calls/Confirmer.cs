using Bruinet.Core.Exceptions;
using Bruinet.Core.Utilities;

namespace Bruinet.Core.Calls;

/// <summary>
/// The wait for an action's effect, behind <c>Confirm</c> on controls and scopes.
/// </summary>
public static class Confirmer
{
    public static Confirmation<T> Run<T>(Func<T?> read, Func<T?, bool> done, int budgetMs, int pollingIntervalMs)
    {
        var deadline = Deadline.In(budgetMs);

        // The confirmation is a phase with its own budget (R3). Driver reads below it take their
        // timeout from the running context (CallBudget), so it must be this phase's, not the
        // resolve phase's, which the action may have left all but spent.
        var outer = AttemptContext.Current;
        AttemptContext.Current = new AttemptContext(deadline, animationMs: 0);
        try
        {
            return Poll(read, done, deadline, pollingIntervalMs);
        }
        finally
        {
            AttemptContext.Current = outer;
        }
    }

    private static Confirmation<T> Poll<T>(Func<T?> read, Func<T?, bool> done, Deadline deadline, int pollingIntervalMs)
    {
        T? last = default;
        Exception? lastError = null;

        while (true)
        {
            try
            {
                // Each poll asks afresh: an answer kept by the enclosing attempt would hide the change waited for.
                using (AttemptMemo.Begin())
                {
                    last = read();
                }

                lastError = null;
                if (done(last))
                {
                    return new Confirmation<T>(ConfirmationResult.Confirmed, last, null);
                }
            }
            catch (StaleElementException error)
            {
                return new Confirmation<T>(ConfirmationResult.Replaced, last, error);
            }
            catch (Exception error) when (!Poller.IsFatal(error))
            {
                lastError = error;
            }

            var remaining = deadline.RemainingMs;
            if (remaining <= 0)
            {
                return new Confirmation<T>(ConfirmationResult.NotConfirmed, last, lastError);
            }

            WaitHelper.Pause(Math.Max(1, Math.Min(pollingIntervalMs, remaining)));
        }
    }
}
