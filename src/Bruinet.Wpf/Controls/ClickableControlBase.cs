namespace Bruinet.Wpf.Controls;

/// <summary>
/// Base class for clickable WPF controls (buttons, links, etc.).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent method chaining.</typeparam>
public abstract class ClickableControlBase<TScope> : ControlBase<TScope>, IClickableControlObject<TScope>
    where TScope : IWpfScope<TScope>
{
    /// <summary>
    /// Creates a new clickable control with the specified scope and locator.
    /// </summary>
    protected ClickableControlBase(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator) { }

    /// <summary>
    /// Creates a new clickable control using the scope's default locator strategy.
    /// </summary>
    protected ClickableControlBase(IWpfScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue) { }

    #region Click Actions

    /// <inheritdoc />
    public virtual TScope Click(int? timeoutMs = null)
    {
        return Run("Click", e => ClickCore(e), timeoutMs: timeoutMs);
    }

    /// <summary>
    /// Core click implementation. Override in derived classes for custom click behavior.
    /// </summary>
    protected virtual void ClickCore(IWpfElement element)
    {
        element.Click();
    }

    /// <inheritdoc />
    public virtual TScope DoubleClick(int? timeoutMs = null)
    {
        return Run("DoubleClick", e => e.DoubleClick(), timeoutMs: timeoutMs);
    }

    /// <inheritdoc />
    public virtual TScope RightClick(int? timeoutMs = null)
    {
        return Run("RightClick", e => e.RightClick(), timeoutMs: timeoutMs);
    }

    /// <inheritdoc />
    public virtual TScope Hover(int? timeoutMs = null)
    {
        return Run("Hover", e => e.Hover(), timeoutMs: timeoutMs);
    }

    /// <inheritdoc />
    public virtual TScope LongPress(int? durationMs = null, int? timeoutMs = null)
    {
        return Run("LongPress", e => e.LongPress(durationMs ?? 1000), timeoutMs: timeoutMs);
    }

    #endregion

    #region Clickable State

    /// <inheritdoc />
    public virtual bool? IsClickable()
    {
        try
        {
            var element = TryFindElement();
            if (element == null)
                return null;
            return element.Visible && element.Enabled;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public bool WaitClickable(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => IsClickable() == expected, timeout);
    }

    /// <inheritdoc />
    public TScope AssertClickable(bool? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;
        if (!WaitClickable(expected, timeoutMs))
        {
            var actual = IsClickable();
            throw new AssertionException(
                message ?? $"Expected '{AutomationId}' clickable={expected} but was {actual}",
                expected, actual, AutomationId);
        }
        return ContainingScope;
    }

    #endregion
}
