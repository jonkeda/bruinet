using Bruinet.Maui.Containers;

namespace Bruinet.Maui.Controls.Navigation;

/// <summary>
/// A MAUI Shell's flyout: a collection of <see cref="ShellFlyoutItem{TParent}"/>, with the
/// affordance that reveals them.
/// </summary>
/// <remarks>
/// A flyout is open when its host is visible and holds items: Windows keeps a closed pane's
/// items in the tree, and Android's host is always visible.
/// </remarks>
/// <typeparam name="TParent">The scope the shell belongs to.</typeparam>
public partial class ShellFlyout<TParent>
    : CollectionObjectBase<TParent, ShellFlyout<TParent>, ShellFlyoutItem<TParent>>
    where TParent : IMauiScope<TParent>
{
    /// <summary>Creates the flyout within the given scope.</summary>
    /// <param name="scope">The scope the shell belongs to.</param>
    /// <param name="chrome">Where the platform draws Shell, from <see cref="IMauiTestContext.ShellChrome"/>.</param>
    public ShellFlyout(IMauiScope<TParent> scope, ShellChromeLocators chrome)
        : base(scope,
               chrome.FlyoutHost,
               ItemStrategy.ByLocator(chrome.FlyoutItem),
               (flyout, itemRoot, index) => new ShellFlyoutItem<TParent>(flyout, itemRoot, index))
    {
    }

    /// <summary>
    /// The flyout's root is resolved afresh every time.
    /// </summary>
    protected override bool CacheContainerRoot => false;

    /// <inheritdoc />
    /// <remarks>The flyout scrolls its own items: its host is the scroller.</remarks>
    public override IMauiElement? ScrollingRoot => TryGetContainerRoot();

    /// <inheritdoc />
    /// <remarks>
    /// On, although the app root it sits in turns sweeps off: a long flyout holds items below the
    /// fold, and its host is the one scroller they can be in.
    /// </remarks>
    public override bool AllowsScrollLookup => true;

    /// <summary>
    /// Opens the flyout, and does nothing when it is already open.
    /// </summary>
    /// <remarks>
    /// Uses the app's verb where declared, and the platform's opener otherwise.
    /// </remarks>
    /// <param name="timeoutMs">Optional timeout.</param>
    /// <returns>The flyout, for chaining.</returns>
    public ShellFlyout<TParent> Open(int? timeoutMs = null)
    {
        if (IsOpen() == true)
            return Self;

        Context.AppElement.OpenFlyout();
        InvalidateCache();
        WaitOpen(true, timeoutMs);

        return Self;
    }

    /// <summary>
    /// Dismisses the flyout without choosing anything in it, and does nothing when it is
    /// already shut.
    /// </summary>
    /// <remarks>
    /// Call this in fixture cleanup: an open flyout leaves an overlay that the next test would
    /// click into.
    /// </remarks>
    /// <param name="timeoutMs">Optional timeout.</param>
    /// <returns>The flyout, for chaining.</returns>
    public ShellFlyout<TParent> Close(int? timeoutMs = null)
    {
        if (IsOpen() != true)
            return Self;

        Context.AppElement.CloseFlyout();
        InvalidateCache();
        WaitOpen(false, timeoutMs);

        return Self;
    }

    #region Core Methods (Element-Aware, No Logging)

    /// <summary>
    /// Whether the flyout is showing its items.
    /// </summary>
    /// <param name="element">The flyout's item host (may be null).</param>
    [AbsenceTolerant]
    protected virtual bool? IsOpenCore(IMauiElement? element)
    {
        // Asked of the app where it can say; null means count the pane's items instead.
        if (Context.AppElement.IsFlyoutOpen is { } reported)
        {
            return reported;
        }

        if (element == null)
            return false;

        return element.Visible && TryGetItemRoots().Count > 0;
    }

    #endregion
}
