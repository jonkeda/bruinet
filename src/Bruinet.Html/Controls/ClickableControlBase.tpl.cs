using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Controls;

/// <summary>
/// Base class for Html controls that can be clicked: Click, DoubleClick, RightClick, Hover,
/// LongPress.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ClickableControlBase<TScope> : ViewBase<TScope>, IClickableControlObject<TScope>
    where TScope : IHtmlScope<TScope>
{
    protected ClickableControlBase(IHtmlScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected ClickableControlBase(IHtmlScope<TScope> scope, string selectorOrId)
        : base(scope, selectorOrId)
    {
    }

    // A disabled control is not acted on; the call asks again within its budget (R0).
    protected override void EnsureReadyForActionCore(IHtmlElement element) => EnsureEnabledCore(element);

    #region Core Methods

    /// <summary>Clicks the control.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ClickCore(IHtmlElement element, int? timeoutMs = null)
    {
        element.Click();
    }

    /// <summary>Clicks the control at a point of its own box, in CSS pixels from its top-left corner.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="x">Pixels from the box's left edge.</param>
    /// <param name="y">Pixels from the box's top edge.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ClickAtCore(IHtmlElement element, double x, double y, int? timeoutMs = null)
    {
        element.ClickAt(x, y);
    }

    /// <summary>Double-clicks the control.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void DoubleClickCore(IHtmlElement element, int? timeoutMs = null)
    {
        element.DoubleClick();
    }

    /// <summary>Right-clicks the control.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void RightClickCore(IHtmlElement element, int? timeoutMs = null)
    {
        element.RightClick();
    }

    /// <summary>Moves the pointer over the control.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void HoverCore(IHtmlElement element, int? timeoutMs = null)
    {
        element.Hover();
    }

    /// <summary>Presses the control and holds it.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="durationMs">How long to hold, in milliseconds; one second when null.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void LongPressCore(IHtmlElement element, int? durationMs = null, int? timeoutMs = null)
    {
        element.LongPress(durationMs ?? 1000);
    }

    /// <summary>Whether the control can be clicked: visible and enabled.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsClickableCore(IHtmlElement? element)
    {
        var isVisible = IsVisibleCore(element);
        var isEnabled = IsEnabledCore(element);

        if (isVisible == null || isEnabled == null)
            return null;

        return isVisible.Value && isEnabled.Value;
    }

    #endregion
}
