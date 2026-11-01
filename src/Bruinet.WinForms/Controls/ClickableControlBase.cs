namespace Bruinet.WinForms.Controls;

/// <summary>
/// Base class for clickable WinForms controls (buttons, links, etc.).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent method chaining.</typeparam>
public abstract class ClickableControlBase<TScope> : ControlBase<TScope>, IClickableControlObject<TScope>
    where TScope : IWinFormsScope<TScope>
{
    protected ClickableControlBase(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator) { }

    protected ClickableControlBase(IWinFormsScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue) { }

    #region Click Actions

    public virtual TScope Click(int? timeoutMs = null)
    {
        return Run("Click", e => ClickCore(e), timeoutMs: timeoutMs);
    }

    protected virtual void ClickCore(IWinFormsElement element)
    {
        element.Click();
    }

    public virtual TScope DoubleClick(int? timeoutMs = null)
    {
        return Run("DoubleClick", e => e.DoubleClick(), timeoutMs: timeoutMs);
    }

    public virtual TScope RightClick(int? timeoutMs = null)
    {
        return Run("RightClick", e => e.RightClick(), timeoutMs: timeoutMs);
    }

    public virtual TScope Hover(int? timeoutMs = null)
    {
        return Run("Hover", e => e.Hover(), timeoutMs: timeoutMs);
    }

    public virtual TScope LongPress(int? durationMs = null, int? timeoutMs = null)
    {
        return Run("LongPress", e => e.LongPress(durationMs ?? 1000), timeoutMs: timeoutMs);
    }

    #endregion

    #region Clickable State

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

    public bool WaitClickable(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => IsClickable() == expected, timeout);
    }

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
