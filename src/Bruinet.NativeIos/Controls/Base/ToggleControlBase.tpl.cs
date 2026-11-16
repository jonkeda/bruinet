using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls;

/// <summary>
/// Base class for native iOS controls with an on or off state (a switch): Toggle, Check, Uncheck,
/// SetChecked. Each taps once and confirms the state took.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ToggleControlBase<TScope> : ClickableControlBase<TScope>, IToggleControlObject<TScope>
    where TScope : INativeIosScope<TScope>
{
    protected ToggleControlBase(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected ToggleControlBase(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }

    #region Core Methods

    /// <summary>Whether the control is checked; null when it is not there or cannot be checked.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual bool? IsCheckedCore(INativeIosElement? element) => element?.Checked;

    /// <summary>
    /// Sets the checked state with one tap, and confirms it took. Does nothing when it is already
    /// in that state.
    /// </summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="checked">The state to set; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetCheckedCore(INativeIosElement element, bool? @checked, int? timeoutMs = null)
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

    /// <summary>Flips the checked state with one tap, and confirms it changed.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ToggleCore(INativeIosElement element, int? timeoutMs = null)
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
