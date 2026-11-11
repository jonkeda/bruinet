namespace Bruinet.Core.Calls;

/// <summary>
/// A driver's short wait below a call, for the effect of what it just did (a dropdown that opens,
/// a shell that settles): one confirmation (R3), its window cut to what the running call has left
/// (<see cref="CallBudget.Within"/>). A wait with a budget of its own below a call would break the
/// call's one budget (R1).
/// </summary>
public static class Settle
{
    /// <summary>Polls <paramref name="condition"/> until it holds, within the window; false when it never did.</summary>
    /// <param name="condition">One look; an exception is a "not yet", kept for the caller's message.</param>
    /// <param name="windowMs">The wait's own window, outside a call.</param>
    /// <param name="pollingIntervalMs">The pause between looks.</param>
    public static bool Until(Func<bool> condition, int windowMs, int pollingIntervalMs = 50)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return Confirmer.Run(condition, held => held, CallBudget.Within(windowMs), pollingIntervalMs).IsConfirmed;
    }

    /// <summary>
    /// Reads until <paramref name="done"/> holds for what was read, within the window, and returns the last read either
    /// way, for the caller to judge. A last look that threw throws again: the caller asked, and that was the answer.
    /// </summary>
    /// <param name="read">One look.</param>
    /// <param name="done">Whether the look is the one waited for.</param>
    /// <param name="windowMs">The wait's own window, outside a call.</param>
    /// <param name="pollingIntervalMs">The pause between looks.</param>
    public static T? Read<T>(Func<T?> read, Func<T?, bool> done, int windowMs, int pollingIntervalMs = 50)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(done);
        var confirmation = Confirmer.Run(read, done, CallBudget.Within(windowMs), pollingIntervalMs);
        if (!confirmation.IsConfirmed && confirmation.LastError is { } error)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(error);
        }

        return confirmation.LastValue;
    }

    /// <summary>
    /// Reads until two looks in a row agree (a position that has stopped moving), within the window; false when it
    /// was still moving at the end. A last look that threw throws again.
    /// </summary>
    /// <param name="read">One look.</param>
    /// <param name="windowMs">The wait's own window, outside a call.</param>
    /// <param name="pollingIntervalMs">The pause between looks.</param>
    /// <param name="comparer">How two looks are compared; the type's own equality when null.</param>
    public static bool Still<T>(Func<T> read, int windowMs, int pollingIntervalMs = 50, IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        comparer ??= EqualityComparer<T>.Default;
        var looked = false;
        T previous = default!;
        var confirmation = Confirmer.Run<T>(read, current =>
        {
            var same = looked && comparer.Equals(previous, current!);
            previous = current!;
            looked = true;
            return same;
        }, CallBudget.Within(windowMs), pollingIntervalMs);
        if (!confirmation.IsConfirmed && confirmation.LastError is { } error)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(error);
        }

        return confirmation.IsConfirmed;
    }
}
