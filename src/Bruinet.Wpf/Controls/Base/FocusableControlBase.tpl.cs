using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls;

/// <summary>
/// Base class for WPF controls that take the keyboard focus: Focus, IsFocused, and Blur as moving
/// the focus to the window. UI Automation's SetFocus moves it; no key is pressed.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class FocusableControlBase<TScope> : ClickableControlBase<TScope>, IFocusableControlObject<TScope>
    where TScope : IWpfScope<TScope>
{
    protected FocusableControlBase(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected FocusableControlBase(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    #region Core Methods

    /// <summary>Gives the control the keyboard focus, and confirms it has it.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void FocusCore(IWpfElement element, int? timeoutMs = null)
    {
        if (element.Focused)
        {
            return;
        }

        ActAndConfirm(
            "Focus", element.Focus,
            () => IsFocusedCore(element), focused => focused == true,
            "focus", timeoutMs);
    }

    /// <summary>Moves the keyboard focus to the app's window, and confirms the control lost it.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void BlurCore(IWpfElement element, int? timeoutMs = null)
    {
        if (!element.Focused)
        {
            return;
        }

        ActAndConfirm(
            "Blur", Context.Driver.Window.Focus,
            () => IsFocusedCore(element), focused => focused == false,
            "no focus", timeoutMs);
    }

    /// <summary>Whether the control has the keyboard focus.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsFocusedCore(IWpfElement? element)
        => element?.Focused;

    #endregion
}
