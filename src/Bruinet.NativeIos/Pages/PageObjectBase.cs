using Bruinet.Core.Configuration;
using Bruinet.Core.Calls;
using Bruinet.Core.Composition;
using Bruinet.Core.Scopes;

namespace Bruinet.NativeIos.Pages;

// Test composition and UAT find NativeIos pages by Core's ITestPage, which INativeIosPage
// derives from.

/// <summary>
/// One screen of the app. Its readiness (design 4): the app in front, then the page's ready
/// element, then the busy signal. iOS has no activity: the ready element is what tells pages apart.
/// </summary>
/// <typeparam name="TSelf">The page's own type.</typeparam>
public abstract class PageObjectBase<TSelf> : ObjectBase, INativeIosPage<TSelf>
    where TSelf : PageObjectBase<TSelf>
{
    /// <summary>
    /// The accessibility identifier of the element an app shows while it is busy (design 4, ?N3).
    /// Present and shown: busy. Absent or gone: idle. The same name as Android's resource id.
    /// </summary>
    public const string BusyViewName = "bruinet_busy";

    // One identifier lookup: it is on every attempt of every call on the page.
    private static readonly Locator BusyView = Locator.ById(BusyViewName);

    private static readonly Locator NavigationBar = Locator.ByClassName("XCUIElementTypeNavigationBar");

    private readonly INativeIosTestContext _context;

    protected PageObjectBase(INativeIosTestContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public override INativeIosTestContext Context => _context;

    /// <inheritdoc />
    public TSelf Self => (TSelf)this;

    /// <inheritdoc />
    public virtual string Name => GetType().Name;

    /// <inheritdoc />
    protected override string ScopeName => Name;

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => _context.DefaultLocatorStrategy;

    /// <inheritdoc />
    public INativeIosPage? Page => this;

    /// <summary>An element that shows the page is there; null when the app in front is enough.</summary>
    protected virtual Locator? ReadyLocator => null;

    /// <summary>The title's element; null when the page's title is its navigation bar's.</summary>
    protected virtual Locator? TitleLocator => null;

    /// <summary>Whether the page reads the app's busy element (<see cref="BusyViewName"/>).</summary>
    protected virtual bool ReadsBusySignal => true;

    /// <summary>More elements that mean the page is busy while shown: a progress view, a refresh spinner.</summary>
    protected virtual IReadOnlyList<Locator> BusyIndicators => [];

    /// <summary>Whether the page is shown now: its ready element. The override point.</summary>
    public virtual bool IsLoaded()
        => ReadyLocator is not { } ready || _context.TryFindElement(ready)?.Visible == true;

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
                ReadyLocator is { } ready ? $"'{ready}' is not shown" : null);
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
        => RunProbe(() => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(), timeoutMs ?? _context.Timeouts.PageLoad.ToMs());

    /// <inheritdoc />
    public bool WaitLoaded(bool? expected = true, int? timeoutMs = null)
    {
        if (expected == null)
        {
            return true;
        }

        return RunProbe(
            () => IsLoaded() == expected.Value ? Observation.Done() : Observation.Pending(),
            timeoutMs ?? _context.Timeouts.PageLoad.ToMs());
    }

    /// <inheritdoc />
    public void AssertLoaded(bool? expected = true, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
        {
            return;
        }

        RunProbe(
            () => IsLoaded() == expected.Value ? Observation.Done() : Observation.Pending(),
            timeoutMs ?? _context.Timeouts.PageLoad.ToMs(),
            readiness => new ScopeNotReadyException(
                readiness,
                message ?? $"Expected page '{Name}' {(expected.Value ? "to be loaded" : "not to be loaded")} but it is {(expected.Value ? "not" : "still loaded")}."));
    }

    /// <inheritdoc />
    public bool IsBusy() => ProbeReadiness().IsBusy;

    /// <inheritdoc />
    public bool WaitBusy(bool? expected = true, int? timeoutMs = null)
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
        }, timeoutMs ?? _context.Timeouts.PageLoad.ToMs());
    }

    /// <inheritdoc />
    public void AssertIdle(string? message = null, int? timeoutMs = null)
    {
        var budget = timeoutMs ?? _context.Timeouts.PageLoad.ToMs();
        RunProbe(
            () => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(),
            budget,
            readiness => new ScopeNotReadyException(
                readiness,
                message ?? $"Page '{Name}' did not become ready within {budget} ms. Last readiness state: {readiness.State}" +
                    (readiness.Detail is null ? "." : $" ({readiness.Detail}).")));
    }

    /// <inheritdoc />
    /// <remarks>XCUITest names a navigation bar by its title.</remarks>
    public virtual string? GetTitle()
        => TitleLocator is { } title
            ? _context.TryFindElement(title)?.Text
            : _context.TryFindElement(NavigationBar)?.GetAttribute("name");

    /// <inheritdoc />
    public bool WaitTitle(string? expected, int? timeoutMs = null)
    {
        if (expected == null)
        {
            return true;
        }

        return RunProbe(
            () => GetTitle() == expected ? Observation.Done() : Observation.Pending(),
            timeoutMs ?? _context.Timeouts.DefaultWait.ToMs());
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
            timeoutMs ?? _context.Timeouts.DefaultWait.ToMs(),
            _ =>
            {
                var actual = GetTitle();
                return new ControlAssertionException(
                    message ?? $"Expected page title '{expected}'.", expected, actual);
            });
    }

    /// <inheritdoc />
    public void TakeScreenshot(string? filename = null)
        => _context.SaveScreenshot(filename ?? $"{Name}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

    /// <inheritdoc />
    public INativeIosElement? TryFindElement(Locator locator) => _context.TryFindElement(locator);

    /// <inheritdoc />
    public INativeIosElement FindElement(Locator locator) => _context.FindElement(locator);

    /// <inheritdoc />
    public IReadOnlyList<INativeIosElement> FindElements(Locator locator) => _context.FindElements(locator);
}
