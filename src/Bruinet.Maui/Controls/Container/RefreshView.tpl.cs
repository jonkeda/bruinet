using Bruinet.Maui.Containers;

namespace Bruinet.Maui.Controls.Container;

/// <summary>
/// MAUI RefreshView control: a container that refreshes its content when pulled down.
/// </summary>
/// <remarks>
/// <b>Not addressable on Windows.</b> RefreshView maps to the WinUI <c>RefreshContainer</c>,
/// whose automation peer cannot be overridden without collapsing the UIA tree. On Windows, drive
/// the bound command instead.
/// </remarks>
/// <typeparam name="TParent">The parent scope type.</typeparam>
/// <typeparam name="TSelf">The concrete container type.</typeparam>
public partial class RefreshView<TParent, TSelf> : ContainerObjectBase<TParent, TSelf>,
    IRefreshableControlObject<TSelf>
    where TParent : IMauiScope<TParent>
    where TSelf : RefreshView<TParent, TSelf>
{
    /// <summary>
    /// Creates a new refresh view control within the specified scope.
    /// </summary>
    public RefreshView(IMauiScope<TParent> parentScope, Locator locator)
        : base(parentScope, locator)
    {
    }

    /// <summary>
    /// Creates a new refresh view control using the scope's default locator strategy.
    /// </summary>
    public RefreshView(IMauiScope<TParent> parentScope, string locatorValue)
        : base(parentScope, locatorValue)
    {
    }

    #region Core Methods (Element-Aware, No Logging)

    /// <summary>Whether the container reports itself enabled.</summary>
    /// <param name="element">The container's root, or null when absent.</param>
    /// <returns>The enabled state, or null when absent.</returns>
    [AbsenceTolerant]
    protected virtual bool? IsEnabledCore(IMauiElement? element) => element?.Enabled;


    /// <summary>
    /// Performs the pull-to-refresh gesture.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void PullToRefreshCore(IMauiElement element, int? timeoutMs = null)
        => element.PerformGesture(MauiGesture.SwipeDown);

    /// <summary>
    /// Reads the refreshing state from the pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element (may be null).</param>
    /// <returns>The view's IsRefreshing where the head reads it (MauiHeadless does); null when it cannot tell.</returns>
    [AbsenceTolerant]
    protected virtual bool? IsRefreshingCore(IMauiElement? element)
        => bool.TryParse(element?.GetAttribute("IsRefreshing"), out var refreshing) ? refreshing : null;

    #endregion

    #region Hand-written Convenience Members

    /// <summary>
    /// Asserts the control is refreshing.
    /// </summary>
    public TSelf AssertRefreshing(string? message, int? timeoutMs = null)
        => AssertRefreshing(true, message, timeoutMs);

    #endregion
}

/// <summary>
/// A <see cref="RefreshView{TParent, TSelf}"/> for use where no view-specific subclass is needed.
/// </summary>
/// <typeparam name="TParent">The parent scope type.</typeparam>
public sealed partial class RefreshView<TParent> : RefreshView<TParent, RefreshView<TParent>>
    where TParent : IMauiScope<TParent>
{
    /// <summary>Creates a RefreshView container within the specified scope.</summary>
    public RefreshView(IMauiScope<TParent> parentScope, Locator locator)
        : base(parentScope, locator)
    {
    }

    /// <summary>Creates a RefreshView container using the scope's default locator strategy.</summary>
    public RefreshView(IMauiScope<TParent> parentScope, string locatorValue)
        : base(parentScope, locatorValue)
    {
    }
}
