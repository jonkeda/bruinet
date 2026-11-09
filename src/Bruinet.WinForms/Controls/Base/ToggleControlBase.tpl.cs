using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls;

/// <summary>
/// Base class for WinForms controls with a checked state (a check box, a toggle button, a radio button):
/// Toggle, Check, Uncheck, SetChecked. Each acts once through the element's pattern (Toggle, or
/// SelectionItem for a radio button) and confirms the state took.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ToggleControlBase<TScope> : ClickableControlBase<TScope>, IToggleControlObject<TScope>
    where TScope : IWinFormsScope<TScope>
{
    protected ToggleControlBase(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected ToggleControlBase(IWinFormsScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    #region Core Methods

    /// <summary>Whether the control is checked: its Toggle state, else (a radio button) whether it is selected.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsCheckedCore(IWinFormsElement? element) => element == null ? null : element.Checked ?? element.Selected;

    /// <summary>
    /// Sets the checked state with one action, and confirms it took. Does nothing when it is
    /// already in that state.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="checked">The state to set; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetCheckedCore(IWinFormsElement element, bool? @checked, int? timeoutMs = null)
    {
        if (@checked is not { } value || IsCheckedCore(element) == value)
        {
            return;
        }

        ActAndConfirm(
            value ? "Check" : "Uncheck",
            element.Click,
            () => IsCheckedCore(element), isChecked => isChecked == value,
            value ? "checked" : "unchecked", timeoutMs);
    }

    /// <summary>Flips the checked state with one action, and confirms it changed.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ToggleCore(IWinFormsElement element, int? timeoutMs = null)
    {
        SetCheckedCore(element, IsCheckedCore(element) != true, timeoutMs);
    }

    #endregion

    #region Hand-written Convenience Members

    /// <summary>Checks the control. An alias for <c>SetChecked(true)</c>.</summary>
    public TScope Check(int? timeoutMs = null) => SetChecked(true, timeoutMs);

    /// <summary>Unchecks the control. An alias for <c>SetChecked(false)</c>.</summary>
    public TScope Uncheck(int? timeoutMs = null) => SetChecked(false, timeoutMs);

    #endregion
}
