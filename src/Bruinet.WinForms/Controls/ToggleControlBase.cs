namespace Bruinet.WinForms.Controls;

/// <summary>
/// Base class for toggle WinForms controls (CheckBox, RadioButton).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent method chaining.</typeparam>
public abstract class ToggleControlBase<TScope> : ClickableControlBase<TScope>, IToggleControlObject<TScope>
    where TScope : IWinFormsScope<TScope>
{
    protected ToggleControlBase(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator) { }

    protected ToggleControlBase(IWinFormsScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue) { }

    #region Checked State

    public virtual bool? IsChecked()
    {
        try
        {
            var element = TryFindElement();
            if (element == null)
                return null;
            return IsCheckedCore(element);
        }
        catch
        {
            return null;
        }
    }

    protected virtual bool IsCheckedCore(IWinFormsElement element)
    {
        return element.Selected;
    }

    public bool WaitChecked(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => IsChecked() == expected.Value, timeout);
    }

    public TScope AssertChecked(bool? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;
        if (!WaitChecked(expected, timeoutMs))
        {
            var actual = IsChecked();
            throw new AssertionException(
                message ?? $"Expected '{AutomationId}' checked={expected} but was {actual}",
                expected, actual, AutomationId);
        }
        return ContainingScope;
    }

    #endregion

    #region Toggle Actions

    public virtual TScope Toggle(int? timeoutMs = null)
    {
        return Run("Toggle", e => ToggleCore(e), timeoutMs: timeoutMs);
    }

    protected virtual void ToggleCore(IWinFormsElement element)
    {
        element.Click();
    }

    public virtual TScope SetChecked(bool? value, int? timeoutMs = null)
    {
        if (value == null)
            return ContainingScope;
        return value.Value ? Check(timeoutMs) : Uncheck(timeoutMs);
    }

    public virtual TScope Check(int? timeoutMs = null)
    {
        if (IsChecked() != true)
        {
            Toggle(timeoutMs);
        }
        return ContainingScope;
    }

    public virtual TScope Uncheck(int? timeoutMs = null)
    {
        if (IsChecked() == true)
        {
            Toggle(timeoutMs);
        }
        return ContainingScope;
    }

    #endregion
}
