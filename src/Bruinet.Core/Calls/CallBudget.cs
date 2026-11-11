namespace Bruinet.Core.Calls;

/// <summary>
/// What is left of the running call's budget, for a wait below the call: a driver passes it to
/// every platform wait, so no wait outlasts the caller's timeout (?A5, R2, R3).
/// </summary>
public static class CallBudget
{
    /// <summary>
    /// The running call's remaining budget, at least 1 ms, or <paramref name="outsideACallMs"/>
    /// when no call is running (a plain <c>Is*</c> member).
    /// </summary>
    public static int RemainingOr(int outsideACallMs)
        => AttemptContext.Current is { } call ? Math.Max(1, call.Deadline.RemainingMs) : outsideACallMs;

    /// <summary>
    /// A wait's own window, cut to what is left of the running call's budget: a driver's wait for
    /// a page to publish or a dropdown to open never outlasts the caller's timeout, and keeps its
    /// window outside a call. The window is scaled by <see cref="Configuration.TimeoutSettings.Scale"/>,
    /// as the budgets are, since a slow machine is where a fixed window bites (review 02, finding 8).
    /// </summary>
    public static int Within(int windowMs)
    {
        var scaled = Scaled(windowMs);
        return Math.Min(scaled, RemainingOr(scaled));
    }

    /// <summary>A fixed window scaled by <see cref="Configuration.TimeoutSettings.Scale"/>.</summary>
    /// <param name="windowMs">The window at scale 1.</param>
    public static int Scaled(int windowMs)
        => Configuration.TimeoutSettings.Scale == 1 || windowMs <= 0
            ? windowMs
            : (int)Math.Min(int.MaxValue, Math.Round(windowMs * Configuration.TimeoutSettings.Scale));
}
