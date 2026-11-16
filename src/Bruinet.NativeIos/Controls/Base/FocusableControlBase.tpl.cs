using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls;

/// <summary>
/// Base class for native iOS controls that take focus: Focus, IsFocused, and Blur as hiding
/// the keyboard.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class FocusableControlBase<TScope> : ClickableControlBase<TScope>, IFocusableControlObject<TScope>
    where TScope : INativeIosScope<TScope>
{
    protected FocusableControlBase(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected FocusableControlBase(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }

    #region Core Methods

    /// <summary>Taps the control to focus it, and confirms it has focus.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void FocusCore(INativeIosElement element, int? timeoutMs = null)
    {
        if (element.Focused)
        {
            return;
        }

        ActAndConfirm(
            "Focus", element.Click,
            () => IsFocusedCore(element), focused => focused == true,
            "focus", timeoutMs);
    }

    /// <summary>
    /// Hides the keyboard. XCUITest has no route to move focus away, and the field may keep it,
    /// so this confirms the keyboard is gone, not that focus moved.
    /// </summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void BlurCore(INativeIosElement element, int? timeoutMs = null)
    {
        ActAndConfirm(
            "Blur", Context.Driver.HideKeyboard,
            () => (bool?)Context.Driver.IsKeyboardShown, shown => shown == false,
            "the keyboard hidden", timeoutMs);
    }

    /// <summary>Whether the control has focus.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual bool? IsFocusedCore(INativeIosElement? element)
        => element?.Focused;

    #endregion
}
