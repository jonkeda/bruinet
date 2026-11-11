using Bruinet.Core.Configuration;
using Bruinet.Core.Calls;
using Bruinet.Core.Scopes;
using Bruinet.Wpf.Controls;
using Bruinet.Wpf.Controls.Buttons;
using Bruinet.Wpf.Controls.Container;
using Bruinet.Wpf.Controls.Display;
using Bruinet.Wpf.Controls.Range;
using Bruinet.Wpf.Controls.Selection;
using Bruinet.Wpf.Controls.Text;
using Bruinet.Wpf.Controls.Toggle;

namespace Bruinet.Wpf.Pages;

// Test composition and UAT find WPF pages by Core's ITestPage, which IWpfPage derives from.

/// <summary>
/// One view of the WPF app. A WPF app usually has one window whose content changes, so a page is
/// known by an element it shows (<see cref="ReadyLocator"/>). Its readiness (design 4): the app
/// runs, then the page's ready element is shown, then no busy signal is shown.
/// </summary>
/// <typeparam name="TSelf">The page's own type.</typeparam>
public abstract class PageObjectBase<TSelf> : ObjectBase, IWpfPage<TSelf>
    where TSelf : PageObjectBase<TSelf>
{
    /// <summary>
    /// The AutomationId of the element an app shows while it is busy (design 4): present and shown
    /// is busy, absent or hidden is idle. Every page reads it; <see cref="BusyIndicators"/> adds more.
    /// </summary>
    public const string BusyAutomationId = "BruinetBusy";

    private static readonly Locator BusyElement = Locator.ByAutomationId(BusyAutomationId);

    private readonly IWpfTestContext _context;

    protected PageObjectBase(IWpfTestContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public override IWpfTestContext Context => _context;

    /// <inheritdoc />
    public TSelf Self => (TSelf)this;

    /// <inheritdoc />
    public virtual string Name => GetType().Name;

    /// <inheritdoc />
    protected override string ScopeName => Name;

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => _context.DefaultLocatorStrategy;

    /// <inheritdoc />
    public IWpfPage? Page => this;

    /// <summary>
    /// An element that shows the page is there; by default the one whose AutomationId is the
    /// page's <see cref="Name"/>. A page that names none of its own declares one of its controls.
    /// </summary>
    protected virtual Locator ReadyLocator => Locator.ByAutomationId(Name);

    /// <summary>Whether the page reads the app's busy element (<see cref="BusyAutomationId"/>).</summary>
    protected virtual bool ReadsBusySignal => true;

    /// <summary>More elements that mean the page is busy while shown: a spinner, a "Signing in..." text.</summary>
    protected virtual IReadOnlyList<Locator> BusyIndicators => [];

    /// <summary>Whether the page is shown now: its ready element is on screen. The override point.</summary>
    public virtual bool IsLoaded() => _context.TryFindElement(ReadyLocator)?.Visible == true;

    /// <summary>One probe of the page (design 4): the app runs, then loaded, then busy.</summary>
    public virtual ScopeReadiness ProbeReadiness()
    {
        if (_context.ProbeReadiness() is { IsReady: false } app)
        {
            return app;
        }

        if (!IsLoaded())
        {
            return new ScopeReadiness(Name, ScopeReadinessState.NotLoaded, $"'{ReadyLocator}' is not shown");
        }

        if (ReadsBusySignal && _context.TryFindElement(BusyElement)?.Visible == true)
        {
            return new ScopeReadiness(Name, ScopeReadinessState.Busy, $"the app shows '{BusyAutomationId}'");
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

    /// <summary>The page's title: the app window's title by default.</summary>
    public virtual string? GetTitle() => _context.Driver.Window.Name;

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
    public IWpfElement? TryFindElement(Locator locator) => _context.TryFindElement(locator);

    /// <inheritdoc />
    public IWpfElement FindElement(Locator locator) => _context.FindElement(locator);

    /// <inheritdoc />
    public IReadOnlyList<IWpfElement> FindElements(Locator locator) => _context.FindElements(locator);

    #region Factory methods

    // A control declared by a plain string is found by its AutomationId.

    protected Button<TSelf> Button(Locator locator) => new(this, locator);
    protected Button<TSelf> Button(string automationId) => new(this, automationId);
    protected CheckBox<TSelf> CheckBox(Locator locator) => new(this, locator);
    protected CheckBox<TSelf> CheckBox(string automationId) => new(this, automationId);
    protected RadioButton<TSelf> RadioButton(Locator locator) => new(this, locator);
    protected RadioButton<TSelf> RadioButton(string automationId) => new(this, automationId);
    protected ToggleButton<TSelf> ToggleButton(Locator locator) => new(this, locator);
    protected ToggleButton<TSelf> ToggleButton(string automationId) => new(this, automationId);
    protected ComboBox<TSelf> ComboBox(Locator locator) => new(this, locator);
    protected ComboBox<TSelf> ComboBox(string automationId) => new(this, automationId);
    protected ListBox<TSelf> ListBox(Locator locator) => new(this, locator);
    protected ListBox<TSelf> ListBox(string automationId) => new(this, automationId);
    protected TabItem<TSelf> TabItem(Locator locator) => new(this, locator);
    protected TabItem<TSelf> TabItem(string automationId) => new(this, automationId);
    protected Label<TSelf> Label(Locator locator) => new(this, locator);
    protected Label<TSelf> Label(string automationId) => new(this, automationId);
    protected TextBox<TSelf> TextBox(Locator locator) => new(this, locator);
    protected TextBox<TSelf> TextBox(string automationId) => new(this, automationId);
    protected PasswordBox<TSelf> PasswordBox(Locator locator) => new(this, locator);
    protected PasswordBox<TSelf> PasswordBox(string automationId) => new(this, automationId);
    protected ProgressBar<TSelf> ProgressBar(Locator locator) => new(this, locator);
    protected ProgressBar<TSelf> ProgressBar(string automationId) => new(this, automationId);
    protected Slider<TSelf> Slider(Locator locator) => new(this, locator);
    protected Slider<TSelf> Slider(string automationId) => new(this, automationId);
    protected ScrollView<TSelf> ScrollView(Locator locator) => new(this, locator);
    protected ScrollView<TSelf> ScrollView(string automationId) => new(this, automationId);
    protected Expander<TSelf> Expander(Locator locator) => new(this, locator);
    protected Expander<TSelf> Expander(string automationId) => new(this, automationId);
    protected TreeViewItem<TSelf> TreeViewItem(Locator locator) => new(this, locator);
    protected TreeViewItem<TSelf> TreeViewItem(string automationId) => new(this, automationId);

    #endregion
}
