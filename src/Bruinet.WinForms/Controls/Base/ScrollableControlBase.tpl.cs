using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls;

/// <summary>
/// Base class for WinForms elements that scroll their own content (a scroll viewer, a list): ScrollPage,
/// ScrollToStart, ScrollToEnd, through the Scroll pattern. Named and element-relative; no coordinates.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ScrollableControlBase<TScope> : ViewBase<TScope>
    where TScope : IWinFormsScope<TScope>
{
    protected ScrollableControlBase(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected ScrollableControlBase(IWinFormsScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    #region Core Methods

    /// <summary>Scrolls the content one page down, or up when <paramref name="down"/> is false.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="down">Whether to show what is below.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ScrollPageCore(IWinFormsElement element, bool down, int? timeoutMs = null)
    {
        element.ScrollPage(down);
    }

    /// <summary>Scrolls to the top.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ScrollToStartCore(IWinFormsElement element, int? timeoutMs = null)
    {
        element.SetVerticalScrollPercent(0);
    }

    /// <summary>Scrolls to the bottom.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ScrollToEndCore(IWinFormsElement element, int? timeoutMs = null)
    {
        element.SetVerticalScrollPercent(100);
    }

    /// <summary>The vertical scroll position in percent (0 at the top, 100 at the bottom); null when it does not scroll.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual double? GetScrollPositionCore(IWinFormsElement? element) => element?.VerticalScrollPercent;

    #endregion
}
