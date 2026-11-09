using Bruinet.Core.Calls;
using Bruinet.Core.Composition;
using Bruinet.Core.Scopes;

namespace Bruinet.NativeAndroid.Pages;

// Test composition and UAT find NativeAndroid pages by Core's ITestPage, which INativeAndroidPage
// derives from.

/// <summary>
/// One screen of the app. Its readiness (design 4): the app in front, then the page's activity and
/// its ready view, then the busy signal.
/// </summary>
/// <typeparam name="TSelf">The page's own type.</typeparam>
public abstract class PageObjectBase<TSelf> : ObjectBase, INativeAndroidPage<TSelf>
    where TSelf : PageObjectBase<TSelf>
{
    /// <summary>
    /// The resource id of the view an app shows while it is busy (design 4, ?N3). Present and
    /// shown: busy. Absent or gone: idle.
    /// </summary>
    public const string BusyViewName = "bruinet_busy";

    // One id lookup: it is on every attempt of every call on the page (measured, 2026-10-07).
    private static readonly Locator BusyView = Locator.ById(BusyViewName);

    private readonly INativeAndroidTestContext _context;

    protected PageObjectBase(INativeAndroidTestContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public override INativeAndroidTestContext Context => _context;

    /// <inheritdoc />
    public TSelf Self => (TSelf)this;

    /// <inheritdoc />
    public virtual string Name => GetType().Name;

    /// <inheritdoc />
    public virtual string? Activity => null;

    /// <inheritdoc />
    protected override string ScopeName => Name;

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => _context.DefaultLocatorStrategy;

    /// <inheritdoc />
    public INativeAndroidPage? Page => this;

    /// <summary>A view that shows the page is there; null when the activity is enough.</summary>
    protected virtual Locator? ReadyLocator => null;

    /// <summary>The toolbar title's view; null when the page's title is its activity.</summary>
    protected virtual Locator? TitleLocator => null;

    /// <summary>Whether the page reads the app's busy view (<see cref="BusyViewName"/>).</summary>
    protected virtual bool ReadsBusySignal => true;

    /// <summary>More views that mean the page is busy while shown: a progress bar, a refresh spinner.</summary>
    protected virtual IReadOnlyList<Locator> BusyIndicators => [];

    /// <summary>Whether the page is shown now: its activity and its ready view. The override point.</summary>
    public virtual bool IsLoaded()
    {
        if (Activity is { } activity && _context.Driver.CurrentActivity is var current
            && !string.Equals(current, activity, StringComparison.Ordinal)
            && current?.EndsWith(activity.TrimStart('.'), StringComparison.Ordinal) != true)
        {
            return false;
        }

        return ReadyLocator is not { } ready || _context.TryFindElement(ready)?.Visible == true;
    }

    /// <summary>
    /// One probe of the page (design 4): the app in front, then loaded, then busy.
    /// </summary>
    public virtual ScopeReadiness ProbeReadiness()
    {
        if (_context.ProbeReadiness() is { IsReady: false } app)
        {
            return app;
        }

        if (!IsLoaded())
        {
            return new ScopeReadiness(Name, ScopeReadinessState.NotLoaded,
                Activity is null ? null : $"activity '{_context.Driver.CurrentActivity}', expected '{Activity}'");
        }

        if (ReadsBusySignal && _context.TryFindElement(BusyView)?.Visible == true)
        {
            return new ScopeReadiness(Name, ScopeReadinessState.Busy, $"the app shows '{BusyViewName}'");
        }

        foreach (var indicator in BusyIndicators)
        {
            if (_context.TryFindElement(indicator)?.Visible == true)
            {
                return new ScopeReadiness(Name, ScopeReadinessState.Busy, $"'{indicator}' is shown");
            }
        }

        return ScopeReadiness.Ready(Name);
    }

    /// <inheritdoc />
    public bool IsReady() => ProbeReadiness().IsReady;

    /// <inheritdoc />
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => RunFirst(timeoutMs, outcomes);

    /// <inheritdoc />
    public bool WaitReady(int? timeoutMs = null)
        => RunProbe(() => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(), timeoutMs ?? _context.Timeouts.PageLoad);

    /// <inheritdoc />
    public bool WaitLoaded(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
        {
            return true;
        }

        return RunProbe(
            () => IsLoaded() == expected.Value ? Observation.Done() : Observation.Pending(),
            timeoutMs ?? _context.Timeouts.PageLoad);
    }

    /// <inheritdoc />
    public void AssertLoaded(bool? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
        {
            return;
        }

        RunProbe(
            () => IsLoaded() == expected.Value ? Observation.Done() : Observation.Pending(),
            timeoutMs ?? _context.Timeouts.PageLoad,
            readiness => new ScopeNotReadyException(
                readiness,
                message ?? $"Expected page '{Name}' {(expected.Value ? "to be loaded" : "not to be loaded")} but it is {(expected.Value ? "not" : "still loaded")}."));
    }

    /// <inheritdoc />
    public bool IsBusy() => ProbeReadiness().IsBusy;

    /// <inheritdoc />
    public bool WaitBusy(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
        {
            return true;
        }

        return RunProbe(() =>
        {
            var readiness = ProbeReadiness();
            return readiness.IsLoaded && readiness.IsBusy == expected.Value
                ? Observation.Done()
                : Observation.ScopeNotReady(readiness);
        }, timeoutMs ?? _context.Timeouts.PageLoad);
    }

    /// <inheritdoc />
    public void AssertIdle(string? message = null, int? timeoutMs = null)
    {
        var budget = timeoutMs ?? _context.Timeouts.PageLoad;
        RunProbe(
            () => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(),
            budget,
            readiness => new ScopeNotReadyException(
                readiness,
                message ?? $"Page '{Name}' did not become ready within {budget} ms. Last readiness state: {readiness.State}" +
                    (readiness.Detail is null ? "." : $" ({readiness.Detail}).")));
    }

    /// <inheritdoc />
    public virtual string? GetTitle()
        => TitleLocator is { } title ? _context.TryFindElement(title)?.Text : _context.Driver.CurrentActivity;

    /// <inheritdoc />
    public bool WaitTitle(string? expected, int? timeoutMs = null)
    {
        if (expected == null)
        {
            return true;
        }

        return RunProbe(
            () => GetTitle() == expected ? Observation.Done() : Observation.Pending(),
            timeoutMs ?? _context.Timeouts.DefaultWait);
    }

    /// <inheritdoc />
    public void AssertTitle(string? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
        {
            return;
        }

        RunProbe(
            () => GetTitle() == expected ? Observation.Done() : Observation.Pending(),
            timeoutMs ?? _context.Timeouts.DefaultWait,
            _ =>
            {
                var actual = GetTitle();
                return new AssertionException(
                    message ?? $"Expected page title '{expected}' but got '{actual ?? "(null)"}'.", expected, actual);
            });
    }

    /// <inheritdoc />
    public void TakeScreenshot(string? filename = null)
        => _context.SaveScreenshot(filename ?? $"{Name}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

    /// <inheritdoc />
    public INativeAndroidElement? TryFindElement(Locator locator) => _context.TryFindElement(locator);

    /// <inheritdoc />
    public INativeAndroidElement FindElement(Locator locator) => _context.FindElement(locator);

    /// <inheritdoc />
    public IReadOnlyList<INativeAndroidElement> FindElements(Locator locator) => _context.FindElements(locator);
}
