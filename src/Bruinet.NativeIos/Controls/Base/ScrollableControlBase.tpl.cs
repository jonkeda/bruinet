using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls;

/// <summary>
/// Base class for native iOS views that scroll their own content (a scroll view, a nested
/// scroll view): Scroll, ScrollToStart, ScrollToEnd. Named and element-relative; no coordinates.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ScrollableControlBase<TScope> : ViewBase<TScope>
    where TScope : INativeIosScope<TScope>
{
    protected ScrollableControlBase(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected ScrollableControlBase(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }

    #region Core Methods

    /// <summary>Scrolls the content once, the finger moving in <paramref name="direction"/>.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="direction">The way the finger moves: <c>Up</c> shows what is below.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ScrollCore(INativeIosElement element, GestureDirection direction, int? timeoutMs = null)
    {
        element.Scroll(direction);
    }

    /// <summary>Scrolls to the top, until the content does not move, within the call's budget.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ScrollToStartCore(INativeIosElement element, int? timeoutMs = null)
    {
        ScrollToEdge(element, GestureDirection.Down);
    }

    /// <summary>Scrolls to the bottom, until the content does not move, within the call's budget.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ScrollToEndCore(INativeIosElement element, int? timeoutMs = null)
    {
        ScrollToEdge(element, GestureDirection.Up);
    }

    /// <summary>
    /// Pulls the content down once, from its top, to start its refresh control (<c>UIRefreshControl</c>).
    /// The refresh is the app's: wait for its effect, or for the page's busy signal.
    /// </summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void PullToRefreshCore(INativeIosElement element, int? timeoutMs = null)
    {
        element.Swipe(GestureDirection.Down, 0.6);
    }

    // Each scroll is a gesture that moved the content; it stops at the edge or when the call's
    // budget is spent, never by a pause.
    private void ScrollToEdge(INativeIosElement element, GestureDirection direction)
    {
        var deadline = Environment.TickCount64 + CallRemainingMs;
        while (element.Scroll(direction, 1.0) && Environment.TickCount64 < deadline)
        {
        }
    }

    #endregion
}
