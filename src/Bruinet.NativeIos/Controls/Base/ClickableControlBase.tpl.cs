using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls;

/// <summary>
/// Base class for native iOS controls that can be tapped: Click, DoubleClick, LongPress, and
/// RightClick as a long press (what opens a context menu on iOS).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ClickableControlBase<TScope> : ViewBase<TScope>, IClickableControlObject<TScope>
    where TScope : INativeIosScope<TScope>
{
    protected ClickableControlBase(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected ClickableControlBase(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }

    // A disabled control is not acted on; the call asks again within its budget (R0).
    protected override void EnsureReadyForActionCore(INativeIosElement element) => EnsureEnabledCore(element);

    #region Core Methods

    /// <summary>Taps the control.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ClickCore(INativeIosElement element, int? timeoutMs = null)
    {
        element.Click();
    }

    /// <summary>Taps the control twice.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void DoubleClickCore(INativeIosElement element, int? timeoutMs = null)
    {
        element.DoubleTap();
    }

    /// <summary>iOS's context click: a long press, which opens a context menu.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void RightClickCore(INativeIosElement element, int? timeoutMs = null)
    {
        element.LongPress();
    }

    /// <summary>An iPhone has no pointer to hover with: always throws.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <exception cref="RouteUnavailableException">Always.</exception>
    protected virtual void HoverCore(INativeIosElement element, int? timeoutMs = null)
    {
        throw new RouteUnavailableException($"Hover has no route on native iOS: a touch screen has no pointer. Control: {Locator}");
    }

    /// <summary>Presses the control and holds it.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="durationMs">How long to hold, in milliseconds; one second when null.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void LongPressCore(INativeIosElement element, int? durationMs = null, int? timeoutMs = null)
    {
        element.LongPress(durationMs ?? 1000);
    }

    /// <summary>Whether the control can be tapped: shown and enabled.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual bool? IsClickableCore(INativeIosElement? element)
    {
        var isVisible = IsVisibleCore(element);
        var isEnabled = IsEnabledCore(element);

        if (isVisible == null || isEnabled == null)
            return null;

        return isVisible.Value && isEnabled.Value;
    }

    #endregion
}
