using Bruinet.Core.Configuration;
using Bruinet.Core.Calls;
using Bruinet.Core.Exceptions;
using Bruinet.Maui.Interfaces;

namespace Bruinet.Maui.Testing;

/// <summary>
/// A test fixture's two navigation waits, as Bruinet members rather than loops of a fixture's own:
/// a pushed page arriving, and getting back to the app's root page between tests.
/// </summary>
public static class PageNavigation
{
    /// <summary>
    /// Waits, as one logged call, until the page a tap pushed over <paramref name="leaving"/> has
    /// arrived: the app names the top page and its root is in the tree, or, where the driver cannot
    /// name it (Appium), the page left has gone from the tree (a covered page drops out of it).
    /// Then lets the app settle. Not asserted: the test's first lookup has its own expectation and
    /// a better message.
    /// </summary>
    /// <param name="context">The test context.</param>
    /// <param name="leaving">The <c>AutomationId</c> of the page the tap left (the hub).</param>
    /// <param name="timeoutMs">The call's budget.</param>
    /// <returns>Whether the page arrived within the budget.</returns>
    public static bool WaitArrived(IMauiTestContext context, string leaving, int timeoutMs)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(leaving);
        var call = new ControlCall(context.Logger, context.Name, context.NearMiss, leaving, leaving);
        var arrived = call.Run(nameof(WaitArrived), null, timeoutMs, context.Timeouts.Animation.ToMs(),
            attempt => Poller.Until(_ => ArrivalOf(context, leaving), attempt, Math.Min(context.Timeouts.PollingInterval.ToMs(), ArrivalPollMs)),
            succeeded: met => met);

        try
        {
            context.Driver.IsIdle();
        }
        catch (NotSupportedException)
        {
            // An app mid-transition has nobody to ask.
        }

        return arrived;
    }

    // Every test opens a page, so its arrival is looked for often: the fixture's loop looked every
    // 25 ms, and the context's 100 ms cost a full mobile run about a minute.
    private const int ArrivalPollMs = 25;

    private static Observation ArrivalOf(IMauiTestContext context, string leaving)
    {
        string top;
        try
        {
            top = context.Driver.CurrentRoute();
        }
        catch (NotSupportedException)
        {
            return Observation.Pending("nothing on the bridge can name the top page yet");
        }

        if (string.IsNullOrEmpty(top))
        {
            return context.TryFindElement(Core.Locators.Locator.ByAutomationId(leaving)) is null
                ? Observation.Done()
                : Observation.Pending($"'{leaving}' still in the tree");
        }

        return top != leaving && context.TryFindElement(Core.Locators.Locator.ByAutomationId(top)) is not null
            ? Observation.Done()
            : Observation.Pending($"top page '{top}'");
    }

    /// <summary>
    /// Goes back to <paramref name="root"/>: one back when the app is not at its navigation root,
    /// then more while something visibly covers the root (a dialog, or a page with a way back),
    /// up to <paramref name="maxExtraBacks"/>. A root that is only late gets the whole wait first and
    /// one back after it, as a last resort for a cover nothing reports (a CommunityToolkit popup on
    /// Android is a modal page with no toolbar): a back on a root that is merely slow would close
    /// the app.
    /// </summary>
    /// <param name="context">The test context.</param>
    /// <param name="root">The app's root page.</param>
    /// <param name="timeoutMs">How long the root gets to show after each step.</param>
    /// <param name="forget">Drops a page's cached root after a back (a page object resolves its root once).</param>
    /// <param name="whenBackRefused">What to try when the driver refuses to go back; true when it went.</param>
    /// <param name="maxExtraBacks">Backs past the first for a covered root: a dialog, its page, one spare.</param>
    /// <returns>Whether the root shows, and the steps taken, for a failure's message.</returns>
    public static (bool AtRoot, IReadOnlyList<string> Steps) ReturnToRoot(
        IMauiTestContext context,
        IMauiPage root,
        int timeoutMs,
        Action? forget = null,
        Func<bool>? whenBackRefused = null,
        int maxExtraBacks = 3)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(root);
        var steps = new List<string>();

        if (context.Driver.IsAtNavigationRoot())
        {
            steps.Add("already at the navigation root");
        }
        else
        {
            Back(context, whenBackRefused, steps, "popped");
        }

        forget?.Invoke();

        var lastResortTaken = false;
        for (var extra = 0; extra <= maxExtraBacks; extra++)
        {
            // The common case first, in one look (not a call: a 1 ms call is a near-miss by
            // definition): the root is there and nothing needs asking.
            if (LoadedNow(root))
            {
                return (true, steps);
            }

            if (Covered(context) is { } cover && extra < maxExtraBacks)
            {
                // Something is over the root now: waiting would only wait it out.
                Back(context, whenBackRefused, steps, $"{cover}, so went back once more");
                forget?.Invoke();
                continue;
            }

            if (root.WaitLoaded(true, timeoutMs))
            {
                return (true, steps);
            }

            if (lastResortTaken || extra == maxExtraBacks)
            {
                break;
            }

            // Nothing reports a cover, and the root did not show in the whole wait.
            Back(context, whenBackRefused, steps, "the root did not show and nothing reported a cover, so went back once (last resort)");
            forget?.Invoke();
            lastResortTaken = true;
        }

        return (false, steps);
    }

    private static bool LoadedNow(IMauiPage root)
    {
        try
        {
            return root.IsLoaded();
        }
        catch (StaleElementException)
        {
            return false;
        }
    }

    // What visibly covers the root now, or null.
    private static string? Covered(IMauiTestContext context)
    {
        try
        {
            if (context.AppElement.TryFindActiveDialog() is not null)
            {
                return "a dialog was open";
            }
        }
        catch (Exception error) when (error is NotSupportedException or RouteUnavailableException)
        {
            // A driver without dialogs: only the navigation stack can say.
        }

        return context.Driver.IsAtNavigationRoot() ? null : "a page was still pushed";
    }

    private static void Back(IMauiTestContext context, Func<bool>? whenBackRefused, List<string> steps, string what)
    {
        try
        {
            context.Driver.NavigateBack();
            steps.Add(what);
        }
        catch (BruinetException refusal)
        {
            steps.Add($"the back was refused: {refusal.Message}");
            if (whenBackRefused is not null)
            {
                steps.Add(whenBackRefused() ? "went back another way" : "and no other way back");
            }
        }
    }
}
