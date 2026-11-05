using Bruinet.Core.Interfaces;
using Bruinet.Maui.Containers;
using Bruinet.Maui.Interfaces;

namespace Bruinet.Maui.Testing;

/// <summary>
/// Recovers app navigation state by unwinding pages until a registered root state is reached.
/// </summary>
public class NavigationRecovery
{
    private const int ProbeIntervalMs = 50;

    private readonly IMauiTestContext _context;
    private readonly IScreenshotService? _screenshotService;
    private readonly HashSet<INavigablePage> _navigablePages = new();
    private readonly List<Func<bool>> _rootChecks = [];
    private readonly int _settleTimeoutMs;
    private readonly int _postNavigationDelayMs;

    /// <param name="context">The test context.</param>
    /// <param name="screenshotService">Captures a screenshot when recovery fails.</param>
    /// <param name="settleTimeoutMs">How long to wait for a known state after each step back.</param>
    /// <param name="postNavigationDelayMs">How long to wait for a known state after going back from a page no registration recognises.</param>
    public NavigationRecovery(
        IMauiTestContext context,
        IScreenshotService? screenshotService = null,
        int settleTimeoutMs = 2000,
        int postNavigationDelayMs = 300)
    {
        _context = context;
        _screenshotService = screenshotService;
        _settleTimeoutMs = settleTimeoutMs;
        _postNavigationDelayMs = postNavigationDelayMs;
    }

    /// <summary>
    /// Registers pages that can be detected and left during recovery.
    /// </summary>
    /// <param name="pages">Navigable page instances; probed with a zero timeout.</param>
    public void RegisterPages(params INavigablePage[] pages)
    {
        foreach (var page in pages)
        {
            _navigablePages.Add(page);
        }
    }

    /// <summary>
    /// Registers a root-state predicate.
    /// </summary>
    /// <param name="isRoot">
    /// Answers now, without waiting: recovery polls it together with the other checks.
    /// </param>
    public void RegisterRootCheck(Func<bool> isRoot)
    {
        _rootChecks.Add(isRoot);
    }

    /// <summary>
    /// Attempts to return to a registered root state.
    /// </summary>
    /// <param name="maxAttempts">Maximum number of unwind attempts.</param>
    /// <returns>True when root state is reached.</returns>
    public bool TryReturnToRoot(int maxAttempts = 5)
    {
        // A page's root kept from an earlier test is usually gone. On iOS, asking a removed element
        // anything takes XCTest about 2.2 s before it answers "stale"; a fresh lookup that finds
        // nothing takes about 0.2 s.
        foreach (var page in _navigablePages)
        {
            ForgetRoot(page);
        }

        INavigablePage? departed = null;
        var settleMs = 0;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var (atRoot, currentPage) = WaitForKnownState(departed, settleMs);
            if (atRoot)
            {
                return true;
            }

            if (currentPage != null)
            {
                if (!currentPage.TryLeave())
                {
                    _context.NavigateBack();
                }

                departed = currentPage;
                settleMs = _settleTimeoutMs;
            }
            else
            {
                _context.NavigateBack();
                departed = null;
                settleMs = _postNavigationDelayMs;
            }
        }

        var recovered = WaitForKnownState(departed, settleMs).AtRoot;
        if (!recovered)
        {
            _screenshotService?.Capture("NavigationRecovery", nameof(TryReturnToRoot), "recovery_failed_after_max_attempts");
        }

        return recovered;
    }

    // A page being left can still answer during its exit transition, so it does not count until it is gone.
    private (bool AtRoot, INavigablePage? Page) WaitForKnownState(INavigablePage? departed, int budgetMs)
    {
        var deadline = Environment.TickCount64 + Bruinet.Core.Calls.CallBudget.Within(budgetMs);
        while (true)
        {
            if (departed != null)
            {
                // The page being left is removed during this wait: its kept root would turn stale.
                ForgetRoot(departed);
            }

            if (departed == null || !departed.IsLoaded(0))
            {
                if (IsAtRoot())
                {
                    return (true, null);
                }

                var page = _navigablePages.FirstOrDefault(candidate => candidate.IsLoaded(0));
                if (page != null)
                {
                    return (false, page);
                }
            }

            if (Environment.TickCount64 >= deadline)
            {
                return (false, null);
            }

            Thread.Sleep(ProbeIntervalMs);
        }
    }

    private static void ForgetRoot(INavigablePage page)
    {
        if (page is IRootCache cache)
        {
            cache.InvalidateCache();
        }
    }

    private bool IsAtRoot() => _rootChecks.Any(rootCheck => rootCheck());
}
