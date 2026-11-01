using Bruinet.Core.Exceptions;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Utilities;
using Bruinet.Stride.Controls;
using Bruinet.Stride.Infrastructure;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Pages;

/// <summary>
/// Base class for Stride page objects with fluent method chaining support.
/// Uses CRTP (Curiously Recurring Template Pattern) for strongly-typed fluent returns.
/// Implements IStrideScope so pages can be used as scopes for child controls.
/// </summary>
/// <typeparam name="TSelf">The concrete page type (CRTP pattern).</typeparam>
public abstract class PageObjectBase<TSelf> : IStrideScope<TSelf>, IPageObject
    where TSelf : PageObjectBase<TSelf>
{
    private readonly IStrideTestContext _context;

    protected PageObjectBase(IStrideTestContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    #region IStrideScope Implementation

    public TSelf Self => (TSelf)this;

    public IStrideTestContext StrideContext => _context;

    #endregion

    #region IPageObject Implementation

    public virtual string Name => GetType().Name;

    /// <summary>
    /// Automation ID for the page root element (empty if page has no root element).
    /// </summary>
    public virtual string AutomationId => string.Empty;

    public LocatorStrategy DefaultLocatorStrategy => LocatorStrategy.AutomationId;

    public IPageObject? Page => this;

    public virtual bool IsLoaded(int? timeoutMs = null)
    {
        if (!string.IsNullOrEmpty(AutomationId))
            return _context.ElementExists(AutomationId) && _context.ElementIsVisible(AutomationId);
        return true;
    }

    public virtual bool IsReady(int? timeoutMs = null)
    {
        return IsLoaded(timeoutMs) && !_context.IsGameBusy();
    }

    public virtual bool WaitReady(int? timeoutMs = null)
    {
        var timeout = timeoutMs ?? _context.Timeouts.PageLoad;
        return LegacyWaitHelper.WaitFor(() => IsReady(), timeout, _context.Timeouts.PollingInterval);
    }

    public virtual bool WaitLoaded(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;

        var timeout = timeoutMs ?? _context.Timeouts.PageLoad;
        return LegacyWaitHelper.WaitFor(
            () => IsLoaded() == expected.Value,
            timeout,
            _context.Timeouts.PollingInterval);
    }

    public virtual void AssertLoaded(bool? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return;

        if (!WaitLoaded(expected, timeoutMs))
        {
            var actual = IsLoaded();
            throw new PageLoadException(
                message ?? $"Expected page '{Name}' {(expected.Value ? "to be loaded" : "not to be loaded")} but loaded state is {actual}.");
        }
    }

    public virtual string? GetTitle(int? timeoutMs = null) => Name;

    public bool WaitTitle(string? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? _context.Timeouts.DefaultWait;
        return LegacyWaitHelper.WaitFor(() => GetTitle() == expected, timeout, _context.Timeouts.PollingInterval);
    }

    public void AssertTitle(string? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return;

        if (!WaitTitle(expected, timeoutMs))
        {
            var actual = GetTitle();
            throw new PageLoadException(
                message ?? $"Expected page title '{expected}' but got '{actual ?? "(null)"}'.");
        }
    }

    public virtual void TakeScreenshot(string? filename = null, int? timeoutMs = null)
    {
        var path = filename ?? $"{Name}_{DateTime.Now:yyyyMMdd_HHmmss}";
        _context.SaveScreenshot(path);
    }

    #endregion

    #region Control Factory Methods

    protected Button<TSelf> Button(string automationId) => new(this, automationId);
    protected TextBlock<TSelf> TextBlock(string automationId) => new(this, automationId);
    protected EditText<TSelf> EditText(string automationId) => new(this, automationId);
    protected CheckBox<TSelf> CheckBox(string automationId) => new(this, automationId);
    protected ToggleButton<TSelf> ToggleButton(string automationId) => new(this, automationId);
    protected Slider<TSelf> Slider(string automationId) => new(this, automationId);
    protected ProgressBar<TSelf> ProgressBar(string automationId) => new(this, automationId);
    protected ListBox<TSelf> ListBox(string automationId) => new(this, automationId);
    protected ComboBox<TSelf> ComboBox(string automationId) => new(this, automationId);
    protected Image<TSelf> Image(string automationId) => new(this, automationId);
    protected Panel<TSelf> Panel(string automationId) => new(this, automationId);

    #endregion

    #region Input Helpers

    protected void PressKey(VirtualKey key) => _context.PressKey(key);
    protected void HoldKey(VirtualKey key, int durationMs) => _context.HoldKey(key, durationMs);
    protected void PressEscape() => PressKey(VirtualKey.Escape);
    protected void PressEnter() => PressKey(VirtualKey.Enter);
    protected void PressTab() => PressKey(VirtualKey.Tab);

    #endregion

    #region Wait Helpers

    protected bool WaitFor(Func<bool> condition, int? timeoutMs = null)
    {
        var timeout = timeoutMs ?? _context.Timeouts.DefaultWait;
        return LegacyWaitHelper.WaitFor(condition, timeout, _context.Timeouts.PollingInterval);
    }

    #endregion
}
