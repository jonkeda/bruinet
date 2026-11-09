using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls;

/// <summary>
/// Base class for WinForms controls that can be clicked: Click through the element's pattern (Invoke,
/// Toggle, SelectionItem, ExpandCollapse); DoubleClick, RightClick, Hover and LongPress with the
/// pointer, through <c>PhysicalInput</c>, since UI Automation has no pattern for them (AD-005).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ClickableControlBase<TScope> : ViewBase<TScope>, IClickableControlObject<TScope>
    where TScope : IWinFormsScope<TScope>
{
    protected ClickableControlBase(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected ClickableControlBase(IWinFormsScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    // A disabled control is not acted on; the call asks again within its budget (R0).
    protected override void EnsureReadyForActionCore(IWinFormsElement element) => EnsureEnabledCore(element);

    #region Core Methods

    /// <summary>Clicks the control through its pattern.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ClickCore(IWinFormsElement element, int? timeoutMs = null)
    {
        element.Click();
    }

    /// <summary>Double-clicks the control with the pointer.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void DoubleClickCore(IWinFormsElement element, int? timeoutMs = null)
    {
        element.DoubleClick();
    }

    /// <summary>Right-clicks the control with the pointer.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void RightClickCore(IWinFormsElement element, int? timeoutMs = null)
    {
        element.RightClick();
    }

    /// <summary>Moves the pointer over the control.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void HoverCore(IWinFormsElement element, int? timeoutMs = null)
    {
        element.Hover();
    }

    /// <summary>Presses the control and holds it with the pointer.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="durationMs">How long to hold, in milliseconds; one second when null.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void LongPressCore(IWinFormsElement element, int? durationMs = null, int? timeoutMs = null)
    {
        element.LongPress(durationMs ?? 1000);
    }

    /// <summary>Whether the control can be clicked: shown and enabled.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsClickableCore(IWinFormsElement? element)
    {
        var isVisible = IsVisibleCore(element);
        var isEnabled = IsEnabledCore(element);

        if (isVisible == null || isEnabled == null)
            return null;

        return isVisible.Value && isEnabled.Value;
    }

    #endregion
}
