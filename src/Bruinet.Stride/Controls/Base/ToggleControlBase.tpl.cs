using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Controls;

/// <summary>
/// Base class for Stride controls with a checked state (a toggle button): Toggle, Check, Uncheck,
/// SetChecked. Each clicks once (the game's Click event moves the button to its next state) and
/// confirms the state took.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ToggleControlBase<TScope> : ClickableControlBase<TScope>, IToggleControlObject<TScope>
    where TScope : IStrideScope<TScope>
{
    protected ToggleControlBase(IStrideScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected ToggleControlBase(IStrideScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    #region Core Methods

    /// <summary>Whether the toggle button is checked; null for an indeterminate one.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsCheckedCore(IStrideElement? element) => element?.Checked;

    /// <summary>
    /// Sets the checked state with one action, and confirms it took. Does nothing when it is
    /// already in that state.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="checked">The state to set; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetCheckedCore(IStrideElement element, bool? @checked, int? timeoutMs = null)
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
    protected virtual void ToggleCore(IStrideElement element, int? timeoutMs = null)
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
