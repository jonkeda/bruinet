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
public partial class ScrollView<TParent, TSelf> : ContainerObjectBase<TParent, TSelf>
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
        }, timeoutMs ?? Context.Timeouts.PageLoad);

    /// <summary>
    /// Brings a descendant into view by automation id.
    /// </summary>
    /// <returns>The container, for chaining.</returns>
    /// <param name="automationId">The descendant's automation id.</param>
    /// <param name="timeoutMs">The call's budget; null for the page-load wait.</param>
    public TSelf ScrollTo(string automationId, int? timeoutMs = null)
        => ScrollTo(Locator.ByAutomationId(automationId), timeoutMs);

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
