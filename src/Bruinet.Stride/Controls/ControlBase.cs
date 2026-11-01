using Bruinet.Core.Abstractions.Controls;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Utilities;
using Bruinet.Stride.Communication;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Controls;

/// <summary>
/// Base class for all Stride UI control objects.
/// Implements the Is/Wait/Assert pattern with fluent TScope chaining.
/// Controls get ElementState snapshots via the automation pipe rather than live element handles.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent method chaining.</typeparam>
public abstract class ControlBase<TScope> : ControlObjectBase<TScope>, IControlObject<TScope>
    where TScope : IStrideScope<TScope>
{
    private readonly IStrideScope<TScope> _strideScope;

    protected ControlBase(IStrideScope<TScope> scope, string automationId)
        : base(new Locator(LocatorStrategy.AutomationId, automationId), scope)
    {
        _strideScope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    /// <summary>
    /// Gets the containing scope for fluent chaining.
    /// </summary>
    protected TScope ContainingScope => _strideScope.Self;

    /// <summary>
    /// Gets the Stride test context.
    /// </summary>
    protected IStrideTestContext Context => _strideScope.StrideContext;

    /// <summary>
    /// Gets the automation ID for this control.
    /// </summary>
    protected string AutomationId => Locator.Value;

    /// <summary>
    /// Gets the page name for logging.
    /// </summary>
    protected string PageName => Page?.Name ?? "";

    /// <summary>
    /// Gets the test name for logging.
    /// </summary>
    protected string TestName => ""; // Set via context if needed

    #region Element State Access

    /// <summary>
    /// Get current element state from game.
    /// </summary>
    protected ElementState GetState() => Context.GetElementState(AutomationId);

    /// <summary>
    /// Get element screen bounds.
    /// </summary>
    protected ElementBounds GetBounds() => GetState().Bounds;

    #endregion

    #region Polling

    protected bool Poll(Func<bool> condition, int timeoutMs)
    {
        return LegacyWaitHelper.WaitFor(condition, timeoutMs, Context.Timeouts.PollingInterval);
    }

    #endregion

    #region Is* Methods

    public bool IsExists() => GetState().Exists;

    public bool? IsVisible()
    {
        var state = GetState();
        if (!state.Exists)
            return null;
        return state.IsVisible;
    }

    public bool? IsEnabled()
    {
        var state = GetState();
        if (!state.Exists)
            return null;
        return state.IsEnabled;
    }

    public bool? IsClickable()
    {
        var state = GetState();
        if (!state.Exists)
            return null;
        return state.IsVisible && state.IsEnabled && state.IsHitTestVisible;
    }

    #endregion

    #region Wait* Methods

    public bool WaitExists(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        return Poll(() => IsExists() == expected.Value, timeoutMs ?? Context.Timeouts.DefaultWait);
    }

    public bool WaitVisible(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        return Poll(() => IsVisible() == expected.Value, timeoutMs ?? Context.Timeouts.DefaultWait);
    }

    public bool WaitEnabled(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        return Poll(() => IsEnabled() == expected.Value, timeoutMs ?? Context.Timeouts.DefaultWait);
    }

    public bool WaitClickable(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        return Poll(() => IsClickable() == expected.Value, timeoutMs ?? Context.Timeouts.DefaultWait);
    }

    public bool WaitText(string? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        return Poll(() => GetText() == expected, timeoutMs ?? Context.Timeouts.DefaultWait);
    }

    #endregion

    #region Assert* Methods

    public TScope AssertExists(bool? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;

        if (!WaitExists(expected, timeoutMs))
        {
            throw new AssertionException(
                message ?? $"Control '{AutomationId}' exists check failed. Expected: {expected}, Actual: {IsExists()}");
        }

        return ContainingScope;
    }

    public TScope AssertVisible(bool? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;

        if (!WaitVisible(expected, timeoutMs))
        {
            throw new AssertionException(
                message ?? $"Control '{AutomationId}' visibility check failed. Expected: {expected}, Actual: {IsVisible()}");
        }

        return ContainingScope;
    }

    public TScope AssertEnabled(bool? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;

        if (!WaitEnabled(expected, timeoutMs))
        {
            throw new AssertionException(
                message ?? $"Control '{AutomationId}' enabled check failed. Expected: {expected}, Actual: {IsEnabled()}");
        }

        return ContainingScope;
    }

    public TScope AssertClickable(bool? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;

        if (!WaitClickable(expected, timeoutMs))
        {
            var state = GetState();
            throw new AssertionException(
                message ?? $"Control '{AutomationId}' clickable check failed. Visible: {state.IsVisible}, Enabled: {state.IsEnabled}, HitTestVisible: {state.IsHitTestVisible}");
        }

        return ContainingScope;
    }

    #endregion

    #region Text

    public string? GetText(int? timeoutMs = null)
    {
        if (timeoutMs.HasValue)
            WaitExists(true, timeoutMs);
        return GetState().Text;
    }

    public TScope AssertText(string? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;

        if (!WaitText(expected, timeoutMs))
        {
            var actual = GetText();
            throw new AssertionException(
                message ?? $"Control '{AutomationId}' text mismatch. Expected: '{expected}', Actual: '{actual}'");
        }

        return ContainingScope;
    }

    public TScope AssertTextContains(string? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;

        var timeout = timeoutMs ?? Context.Timeouts.DefaultWait;
        var matched = Poll(() =>
        {
            var text = GetText();
            return text != null && text.Contains(expected, StringComparison.Ordinal);
        }, timeout);

        if (!matched)
        {
            var actual = GetText();
            throw new AssertionException(
                message ?? $"Control '{AutomationId}' text should contain '{expected}' but was '{actual}'");
        }

        return ContainingScope;
    }

    public string? GetAttribute(string name, int? timeoutMs)
    {
        // Stride doesn't have a general attribute concept — return state properties by name
        var state = GetState();
        return name.ToLowerInvariant() switch
        {
            "text" => state.Text,
            "name" => state.Name,
            "controltype" => state.ControlType,
            "automationid" => state.AutomationId,
            _ => null
        };
    }

    #endregion

    #region Logging Helpers

    protected void LogAction(string action, string? value = null)
    {
        Context.Logger.LogAction(TestName, PageName, AutomationId, action, value);
    }

    #endregion
}
