namespace Bruinet.Maui.Controls.Navigation;

/// <summary>
/// One tab in a <see cref="TabMenu{TParent}"/>.
/// </summary>
/// <remarks>
/// A tab is not one element: it is a surface holding a button and a caption, and which of
/// those carries the command or the text differs by platform. The tab is rooted at the
/// surface, so the item scopes both, and each member below reaches for the part that knows.
/// </remarks>
/// <typeparam name="TParent">The scope the tab menu belongs to.</typeparam>
public partial class TabItem<TParent> : Base.SelectableItemBase<TabMenu<TParent>, TabItem<TParent>>
    where TParent : IMauiScope<TParent>
{
    /// <summary>
    /// Creates a tab bound to a root the tab menu has already found.
    /// </summary>
    /// <param name="tabMenu">The owning tab menu.</param>
    /// <param name="itemRoot">The tab's root element.</param>
    /// <param name="index">The tab's zero-based position.</param>
    public TabItem(TabMenu<TParent> tabMenu, IMauiElement itemRoot, int index)
        : base(tabMenu, itemRoot, index)
    {
    }

    #region Core Methods (Element-Aware, No Logging)

    /// <summary>
    /// Invokes the tab's button surface, falling back to the tab itself.
    /// </summary>
    /// <param name="element">The tab's root element.</param>
    /// <param name="timeoutMs">Optional timeout.</param>
    protected override void ClickCore(IMauiElement element, int? timeoutMs = null)
    {
        var target = TabMenuMarkup.ButtonWithin(element) ?? element;

        target.Invoke();
    }

    /// <inheritdoc />
    protected override void EnsureClickableCore(IMauiElement element)
        => base.EnsureClickableCore(TabMenuMarkup.ButtonWithin(element) ?? element);

    /// <summary>
    /// Reads the tab's caption from its caption label, then its button, then the tab itself.
    /// </summary>
    /// <param name="element">The tab's root element.</param>
    protected override string? GetTextCore(IMauiElement element)
    {
        var caption = TabMenuMarkup.CaptionWithin(element)?.Text;
        if (!string.IsNullOrEmpty(caption))
            return caption;

        var button = TabMenuMarkup.ButtonWithin(element)?.Text;
        if (!string.IsNullOrEmpty(button))
            return button;

        return element.Text;
    }

    /// <summary>
    /// Whether this is the current tab.
    /// </summary>
    /// <param name="element">The tab's root element (may be null).</param>
    protected override bool? IsSelectedCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        return IsMarkedSelected(element)
               || IsMarkedSelected(TabMenuMarkup.ButtonWithin(element));
    }

    #endregion
}
