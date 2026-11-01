namespace Bruinet.Core.Interfaces;

/// <summary>
/// Scrolling capability for scroll views, lists, and other scrollable containers.
/// Action methods return TScope for fluent method chaining.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public interface IScrollableControlObject<TScope> : IControlObject<TScope>
{
    /// <summary>
    /// Scroll to top of the scrollable area.
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope ScrollToTop(int? timeoutMs = null);

    /// <summary>
    /// Scroll to bottom of the scrollable area.
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope ScrollToEnd(int? timeoutMs = null);

    /// <summary>
    /// Scroll by specified amount (positive = down/right).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope ScrollBy(int deltaX, int deltaY, int? timeoutMs = null);

    /// <summary>
    /// Scroll to make element at locator visible.
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope ScrollTo(Locator locator, int? timeoutMs = null);

    /// <summary>
    /// Get vertical scroll position (0-100 percent).
    /// Returns null if element not found.
    /// </summary>
    double? GetScrollPosition(int? timeoutMs = null);

    /// <summary>
    /// Set vertical scroll position (0-100 percent).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope SetScrollPosition(double percent, int? timeoutMs = null);

    /// <summary>
    /// Wait until scroll position matches expected value.
    /// If expected is null, returns true immediately (skip).
    /// </summary>
    bool WaitScrollPosition(double? expected, double tolerance = 1.0, int? timeoutMs = null);

    /// <summary>
    /// Assert scroll position matches expected value.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertScrollPosition(double? expected, double tolerance = 1.0, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Check if more content is available to scroll down.
    /// Returns null if element not found.
    /// </summary>
    bool? CanScrollDown(int? timeoutMs = null);

    /// <summary>
    /// Check if can scroll up from current position.
    /// Returns null if element not found.
    /// </summary>
    bool? CanScrollUp(int? timeoutMs = null);
}
