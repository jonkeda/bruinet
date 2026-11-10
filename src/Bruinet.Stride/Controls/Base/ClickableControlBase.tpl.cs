using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Controls;

/// <summary>
/// Base class for Stride controls that can be clicked: Click raises the control's Click event in the
/// game, as a person's click does. The automation server has no route for a double click, a right
/// click, hover or a long press: those fail naming the gap.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ClickableControlBase<TScope> : ViewBase<TScope>, IClickableControlObject<TScope>
    where TScope : IStrideScope<TScope>
{
    protected ClickableControlBase(IStrideScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected ClickableControlBase(IStrideScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    // A disabled control is not acted on; the call asks again within its budget (R0).
    protected override void EnsureReadyForActionCore(IStrideElement element) => EnsureEnabledCore(element);

    #region Core Methods

    /// <summary>Clicks the control: its Click event, raised in the game.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ClickCore(IStrideElement element, int? timeoutMs = null)
    {
        element.Click();
    }

    /// <summary>The automation server has no double click: always throws.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void DoubleClickCore(IStrideElement element, int? timeoutMs = null)
    {
        throw new RouteUnavailableException($"DoubleClick has no route in the Stride automation server. Control: {Locator}");
    }

    /// <summary>The automation server has no right click: always throws.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void RightClickCore(IStrideElement element, int? timeoutMs = null)
    {
        throw new RouteUnavailableException($"RightClick has no route in the Stride automation server. Control: {Locator}");
    }

    /// <summary>The automation server has no pointer to hover with: always throws.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void HoverCore(IStrideElement element, int? timeoutMs = null)
    {
        throw new RouteUnavailableException($"Hover has no route in the Stride automation server. Control: {Locator}");
    }

    /// <summary>The automation server has no long press: always throws.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="durationMs">How long to hold, in milliseconds; one second when null.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void LongPressCore(IStrideElement element, int? durationMs = null, int? timeoutMs = null)
    {
        throw new RouteUnavailableException($"LongPress has no route in the Stride automation server. Control: {Locator}");
    }

    /// <summary>Whether the control can be clicked: shown and enabled.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsClickableCore(IStrideElement? element)
    {
        var isVisible = IsVisibleCore(element);
        var isEnabled = IsEnabledCore(element);

        if (isVisible == null || isEnabled == null)
            return null;

        return isVisible.Value && isEnabled.Value;
    }

    #endregion
}
