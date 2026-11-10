using Bruinet.Core.Configuration;
using Bruinet.Core.Calls;
using Bruinet.Core.Scopes;
using Bruinet.Stride.Controls.Buttons;
using Bruinet.Stride.Controls.Display;
using Bruinet.Stride.Controls.Range;
using Bruinet.Stride.Controls.Text;
using Bruinet.Stride.Controls.Toggle;

namespace Bruinet.Stride.Pages;

// Test composition and UAT find Stride pages by Core's ITestPage, which IStridePage derives from.

/// <summary>
/// One view of the game's UI: a HUD, a menu, an overlay. It is known by an element it shows
/// (<see cref="ReadyLocator"/>). Its readiness (design 4): the game is ready, then the page's ready
/// element is shown, then the game is not busy and no busy indicator of the page is shown.
/// </summary>
/// <typeparam name="TSelf">The page's own type.</typeparam>
public abstract class PageObjectBase<TSelf> : ObjectBase, IStridePage<TSelf>
    where TSelf : PageObjectBase<TSelf>
{
    private readonly IStrideTestContext _context;

    protected PageObjectBase(IStrideTestContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public override IStrideTestContext Context => _context;

    /// <inheritdoc />
    public TSelf Self => (TSelf)this;

    /// <inheritdoc />
    public virtual string Name => GetType().Name;

    /// <inheritdoc />
    protected override string ScopeName => Name;

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => _context.DefaultLocatorStrategy;

    /// <inheritdoc />
    public IStridePage? Page => this;

    /// <summary>An element that shows the page is there (its root panel); by default the one named as the page.</summary>
    protected virtual Locator ReadyLocator => Locator.ByAutomationId(Name);

    /// <summary>More elements that mean the page is busy while shown: a loading text, a spinner.</summary>
    protected virtual IReadOnlyList<Locator> BusyIndicators => [];

    /// <summary>Whether the page is shown now: its ready element is. The override point.</summary>
    public virtual bool IsLoaded() => _context.TryFindElement(ReadyLocator)?.Visible == true;

    /// <summary>One probe of the page (design 4): the game ready, then loaded, then not busy.</summary>
    public virtual ScopeReadiness ProbeReadiness()
    {
        if (_context.ProbeReadiness() is { IsReady: false } game)
        {
            return game;
        }

        if (!IsLoaded())
        {
            return new ScopeReadiness(Name, ScopeReadinessState.NotLoaded, $"'{ReadyLocator}' is not shown");
        }

        if (_context.Driver.IsGameBusy())
        {
            return new ScopeReadiness(Name, ScopeReadinessState.Busy, "the game says it is busy");
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

    /// <summary>The page's title: its name, since a game's view has no title of its own.</summary>
    public virtual string? GetTitle() => Name;

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
                    message ?? $"Expected page title '{expected}' but got '{actual ?? "(null)"}'.", expected, actual);
            });
    }

    /// <inheritdoc />
    public void TakeScreenshot(string? filename = null)
        => _context.SaveScreenshot(filename ?? $"{Name}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

    /// <inheritdoc />
    public IStrideElement? TryFindElement(Locator locator) => _context.TryFindElement(locator);

    /// <inheritdoc />
    public IStrideElement FindElement(Locator locator) => _context.FindElement(locator);

    /// <inheritdoc />
    public IReadOnlyList<IStrideElement> FindElements(Locator locator) => _context.FindElements(locator);

    #region Keys

    /// <summary>Presses a key in the game's own input (one frame or more).</summary>
    protected void PressKey(VirtualKey key) => _context.PressKey(key);

    /// <summary>Holds a key down in the game's own input for <paramref name="durationMs"/>.</summary>
    protected void HoldKey(VirtualKey key, int durationMs) => _context.HoldKey(key, durationMs);

    #endregion

    #region Factory methods

    // A control declared by a plain string is found by its Name.

    protected Button<TSelf> Button(Locator locator) => new(this, locator);
    protected Button<TSelf> Button(string automationId) => new(this, automationId);
    protected ToggleButton<TSelf> ToggleButton(Locator locator) => new(this, locator);
    protected ToggleButton<TSelf> ToggleButton(string automationId) => new(this, automationId);
    protected EditText<TSelf> EditText(Locator locator) => new(this, locator);
    protected EditText<TSelf> EditText(string automationId) => new(this, automationId);
    protected TextBlock<TSelf> TextBlock(Locator locator) => new(this, locator);
    protected TextBlock<TSelf> TextBlock(string automationId) => new(this, automationId);
    protected Slider<TSelf> Slider(Locator locator) => new(this, locator);
    protected Slider<TSelf> Slider(string automationId) => new(this, automationId);
    protected ImageElement<TSelf> Image(Locator locator) => new(this, locator);
    protected ImageElement<TSelf> Image(string automationId) => new(this, automationId);

    #endregion
}
