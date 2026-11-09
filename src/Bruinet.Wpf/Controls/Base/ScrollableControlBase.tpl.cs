using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls;

/// <summary>
/// Base class for WPF elements that scroll their own content (a scroll viewer, a list): ScrollPage,
/// ScrollToStart, ScrollToEnd, through the Scroll pattern. Named and element-relative; no coordinates.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ScrollableControlBase<TScope> : ViewBase<TScope>
    where TScope : IWpfScope<TScope>
{
    protected ScrollableControlBase(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected ScrollableControlBase(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    #region Core Methods

    /// <summary>Scrolls the content one page down, or up when <paramref name="down"/> is false.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="down">Whether to show what is below.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ScrollPageCore(IWpfElement element, bool down, int? timeoutMs = null)
    {
        element.ScrollPage(down);
    }

    /// <summary>Scrolls to the top.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ScrollToStartCore(IWpfElement element, int? timeoutMs = null)
    {
        element.SetVerticalScrollPercent(0);
    }

    /// <summary>Scrolls to the bottom.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ScrollToEndCore(IWpfElement element, int? timeoutMs = null)
    {
        element.SetVerticalScrollPercent(100);
    }

    /// <summary>The vertical scroll position in percent (0 at the top, 100 at the bottom); null when it does not scroll.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual double? GetScrollPositionCore(IWpfElement? element) => element?.VerticalScrollPercent;

    #endregion
}
