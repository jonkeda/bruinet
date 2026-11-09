using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls;

/// <summary>
/// Base class for WPF controls that can be clicked: Click through the element's pattern (Invoke,
/// Toggle, SelectionItem, ExpandCollapse); DoubleClick, RightClick, Hover and LongPress with the
/// pointer, through <c>PhysicalInput</c>, since UI Automation has no pattern for them (AD-005).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ClickableControlBase<TScope> : ViewBase<TScope>, IClickableControlObject<TScope>
    where TScope : IWpfScope<TScope>
{
    protected ClickableControlBase(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected ClickableControlBase(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    // A disabled control is not acted on; the call asks again within its budget (R0).
    protected override void EnsureReadyForActionCore(IWpfElement element) => EnsureEnabledCore(element);

    #region Core Methods

    /// <summary>Clicks the control through its pattern.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ClickCore(IWpfElement element, int? timeoutMs = null)
    {
        element.Click();
    }

    /// <summary>Double-clicks the control with the pointer.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void DoubleClickCore(IWpfElement element, int? timeoutMs = null)
    {
        element.DoubleClick();
    }

    /// <summary>Right-clicks the control with the pointer.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void RightClickCore(IWpfElement element, int? timeoutMs = null)
    {
        element.RightClick();
    }

    /// <summary>Moves the pointer over the control.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void HoverCore(IWpfElement element, int? timeoutMs = null)
    {
        element.Hover();
    }

    /// <summary>Presses the control and holds it with the pointer.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="durationMs">How long to hold, in milliseconds; one second when null.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void LongPressCore(IWpfElement element, int? durationMs = null, int? timeoutMs = null)
    {
        element.LongPress(durationMs ?? 1000);
    }

    /// <summary>Whether the control can be clicked: shown and enabled.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsClickableCore(IWpfElement? element)
    {
        var isVisible = IsVisibleCore(element);
        var isEnabled = IsEnabledCore(element);

        if (isVisible == null || isEnabled == null)
            return null;

        return isVisible.Value && isEnabled.Value;
    }

    #endregion
}
