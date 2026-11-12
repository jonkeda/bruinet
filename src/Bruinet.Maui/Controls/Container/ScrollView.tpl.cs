using Bruinet.Core.Configuration;
using Bruinet.Maui.Containers;

namespace Bruinet.Maui.Controls.Container;

/// <summary>
/// MAUI ScrollView container. Scopes searches to the view's own subtree and adds
/// scrolling.
/// </summary>
/// <remarks>
/// Scrolling methods report progress rather than throwing when the content cannot scroll further.
/// </remarks>
/// <typeparam name="TParent">The parent scope type (a page or another container).</typeparam>
/// <typeparam name="TSelf">The view type itself (self-referencing for fluent returns).</typeparam>
public partial class ScrollView<TParent, TSelf> : ContainerObjectBase<TParent, TSelf>,
    IScrollableControlObject<TSelf>
    where TParent : IMauiScope<TParent>
    where TSelf : ScrollView<TParent, TSelf>
{
    /// <summary>
    /// Creates a ScrollView container within the specified scope.
    /// </summary>
    public ScrollView(IMauiScope<TParent> parentScope, Locator locator)
        : base(parentScope, locator)
    {
    }

    /// <summary>
    /// Creates a ScrollView container using the scope's default locator strategy.
    /// </summary>
    public ScrollView(IMauiScope<TParent> parentScope, string locatorValue)
        : base(parentScope, locatorValue)
    {
    }

    /// <inheritdoc />
    /// <remarks>A scroll view is the element that scrolls for everything inside it.</remarks>
    public override IMauiElement? ScrollingRoot => TryGetContainerRoot();

    /// <inheritdoc />
    /// <remarks>
    /// A scroll view on screen in full still holds content past its edges: a child it misses is
    /// reached by scrolling it, as a flyout's is.
    /// </remarks>
    public override bool AllowsScrollLookup => true;

    #region Core Methods (Element-Aware, No Logging)

    /// <summary>Whether the scroll view reports itself enabled.</summary>
    /// <param name="element">The scroll view's root, or null when absent.</param>
    /// <returns>The enabled state, or null when absent.</returns>
    [AbsenceTolerant]
    protected virtual bool? IsEnabledCore(IMauiElement? element) => element?.Enabled;

    /// <summary>
    /// Scrolls one viewport toward the end of the content.
    /// </summary>
    /// <param name="element">The container's own element.</param>
    protected virtual void ScrollForwardCore(IMauiElement element)
        => ScrollHelper.StepForward(element);

    /// <summary>
    /// Scrolls one viewport back toward the start of the content.
    /// </summary>
    /// <param name="element">The container's own element.</param>
    protected virtual void ScrollBackCore(IMauiElement element)
        => ScrollHelper.StepBack(element);

    #endregion

    #region Hand-written Convenience Members

    /// <summary>
    /// Brings a descendant into view, scrolling if necessary.
    /// </summary>
    /// <param name="locator">Locator for the descendant, resolved within this container.</param>
    /// <returns>The container, for chaining.</returns>
    /// <remarks>
    /// One call: it waits for the scope chain, then scrolls once, within the call's budget. Does
    /// nothing when the element is not present; use
    /// <c>FindElement</c> first if absence should be an
    /// error.
    /// </remarks>
    /// <remarks>
    /// On Android and iOS a descendant below the fold is not in the tree, so the plain lookup
    /// misses it and the call used to do nothing; there the scroll view scrolls itself until the
    /// descendant shows (its root's <c>TryFindByScrolling</c>).
    /// </remarks>
    /// <remarks>
    /// The search covers the whole scroller, down then up, so its default budget is the page-load
    /// wait rather than the default wait: on a long Android form the swipes to the bottom alone
    /// used the default 5 s, and a row above the fold was never reached. A hit stops early.
    /// </remarks>
    /// <param name="timeoutMs">The call's budget; null for the page-load wait.</param>
    public TSelf ScrollTo(Locator locator, int? timeoutMs = null)
        => RunDo(() =>
        {
            var element = TryFindElement(locator);
            if (element is null && Context.Driver?.OmitsOffscreenElements == true)
            {
                ContainerRoot.TryFindByScrolling(locator)?.RevealClearOfEdges();
                return;
            }

            ScrollHelper.ScrollIntoView(element, CallRemainingMs);

            // A sliver at the bottom edge is in the tree without its children; move it clear.
            element?.RevealClearOfEdges();
        }, timeoutMs ?? Context.Timeouts.PageLoad.ToMs());

    /// <summary>
    /// Brings a descendant into view by automation id.
    /// </summary>
    /// <returns>The container, for chaining.</returns>
    /// <param name="automationId">The descendant's automation id.</param>
    /// <param name="timeoutMs">The call's budget; null for the page-load wait.</param>
    public TSelf ScrollTo(string automationId, int? timeoutMs = null)
        => ScrollTo(Locator.ByAutomationId(automationId), timeoutMs);

    #endregion

    #region Scroll position (IScrollableControlObject, R2-T115)

    // The head's scroll pattern (IMauiElement.ReadScrollPercent): Windows reads it; MauiHeadless has
    // no layout and Android no read yet (R2-T116), so there these answer null and the sets throw.

    /// <summary>Scrolls to the top of the content.</summary>
    /// <param name="timeoutMs">The call's budget; null for the default.</param>
    /// <returns>The scroll view, for chaining.</returns>
    public TSelf ScrollToTop(int? timeoutMs = null)
        => RunDoWithElement(element => element.SetVerticalScrollPercent(0), timeoutMs);

    /// <summary>Scrolls to the end of the content.</summary>
    /// <param name="timeoutMs">The call's budget; null for the default.</param>
    /// <returns>The scroll view, for chaining.</returns>
    public TSelf ScrollToEnd(int? timeoutMs = null)
        => RunDoWithElement(element => element.SetVerticalScrollPercent(100), timeoutMs);

    /// <summary>Scrolls by an amount in pixels, positive down; the horizontal amount is not used, a MAUI ScrollView here scrolls one way.</summary>
    /// <param name="deltaX">Not used.</param>
    /// <param name="deltaY">Pixels, positive toward the end.</param>
    /// <param name="timeoutMs">The call's budget; null for the default.</param>
    /// <returns>The scroll view, for chaining.</returns>
    public TSelf ScrollBy(int deltaX, int deltaY, int? timeoutMs = null)
        => RunDoWithElement(element =>
        {
            if (element.ReadScrollPercent() is not { Scrolls: true } scroll || element.Rect.Height <= 0)
            {
                element.SetVerticalScrollPercent(deltaY > 0 ? 100 : 0);
                return;
            }

            // The viewport is VerticalViewSize percent of the content; what lies past it is the range.
            var viewport = (double)element.Rect.Height;
            var range = viewport * 100 / scroll.VerticalViewSize - viewport;
            element.SetVerticalScrollPercent(scroll.Vertical + deltaY / range * 100);
        }, timeoutMs);

    /// <summary>The vertical position, 0 at the top and 100 at the end; null when absent or not read on this head.</summary>
    /// <param name="timeoutMs">Not used: a read is now.</param>
    public double? GetScrollPosition(int? timeoutMs = null)
        => TryFindElement()?.ReadScrollPercent()?.Vertical;

    /// <summary>Scrolls to a vertical position in percent.</summary>
    /// <param name="percent">0 at the top, 100 at the end.</param>
    /// <param name="timeoutMs">The call's budget; null for the default.</param>
    /// <returns>The scroll view, for chaining.</returns>
    public TSelf SetScrollPosition(double percent, int? timeoutMs = null)
        => RunDoWithElement(element => element.SetVerticalScrollPercent(percent), timeoutMs);

    /// <summary>Waits until the position is within <paramref name="tolerance"/> of <paramref name="expected"/>; true at once for null.</summary>
    /// <param name="expected">The position in percent; null skips.</param>
    /// <param name="tolerance">How far off it may be, in percent.</param>
    /// <param name="timeoutMs">The wait's budget; null for the default.</param>
    public bool WaitScrollPosition(double? expected, double tolerance = 1.0, int? timeoutMs = null)
        => RunWaitWithOptionalElement(expected,
            element => Near(element?.ReadScrollPercent()?.Vertical, expected, tolerance),
            timeoutMs);

    /// <summary>Asserts the position is within <paramref name="tolerance"/> of <paramref name="expected"/>; null skips.</summary>
    /// <param name="expected">The position in percent; null skips.</param>
    /// <param name="tolerance">How far off it may be, in percent.</param>
    /// <param name="message">The failure's message; null for the default.</param>
    /// <param name="timeoutMs">The assertion's budget; null for the default.</param>
    /// <returns>The scroll view, for chaining.</returns>
    public TSelf AssertScrollPosition(double? expected, double tolerance = 1.0, string? message = null, int? timeoutMs = null)
        => RunAssertWithOptionalElement(expected,
            element => element?.ReadScrollPercent()?.Vertical,
            (actual, wanted) => Near(actual, wanted, tolerance),
            message ?? $"Expected the scroll position to be {expected} (within {tolerance}). Locator: {Locator}", timeoutMs);

    /// <summary>Whether there is content below the viewport; null when absent or not read on this head.</summary>
    /// <param name="timeoutMs">Not used: a read is now.</param>
    public bool? CanScrollDown(int? timeoutMs = null)
        => TryFindElement()?.ReadScrollPercent() is { } scroll ? scroll.Scrolls && scroll.Vertical < 99.9 : null;

    /// <summary>Whether there is content above the viewport; null when absent or not read on this head.</summary>
    /// <param name="timeoutMs">Not used: a read is now.</param>
    public bool? CanScrollUp(int? timeoutMs = null)
        => TryFindElement()?.ReadScrollPercent() is { } scroll ? scroll.Scrolls && scroll.Vertical > 0.1 : null;

    private static bool Near(double? actual, double? expected, double tolerance)
        => actual is { } a && expected is { } e && Math.Abs(a - e) <= tolerance;

    #endregion
}

/// <summary>
/// A <see cref="ScrollView{TParent, TSelf}"/> for use where no view-specific subclass is
/// needed.
/// </summary>
/// <typeparam name="TParent">The parent scope type.</typeparam>
public sealed partial class ScrollView<TParent> : ScrollView<TParent, ScrollView<TParent>>
    where TParent : IMauiScope<TParent>
{
    /// <summary>
    /// Creates a ScrollView container within the specified scope.
    /// </summary>
    public ScrollView(IMauiScope<TParent> parentScope, Locator locator)
        : base(parentScope, locator)
    {
    }

    /// <summary>
    /// Creates a ScrollView container using the scope's default locator strategy.
    /// </summary>
    public ScrollView(IMauiScope<TParent> parentScope, string locatorValue)
        : base(parentScope, locatorValue)
    {
    }
}
