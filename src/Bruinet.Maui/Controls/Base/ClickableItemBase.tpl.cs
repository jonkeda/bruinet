using Bruinet.Maui.Containers;

namespace Bruinet.Maui.Controls.Base;

/// <summary>
/// Base class for a collection item that behaves like a leaf control: it is clicked, it
/// says something, and it can be disabled.
/// </summary>
/// <remarks>
/// Existence and visibility come from <see cref="ContainerObjectBase{TParent, TSelf}"/>,
/// answered against the item's root.
/// </remarks>
/// <typeparam name="TCollection">The owning collection.</typeparam>
/// <typeparam name="TSelf">The item type itself (self-referencing for fluent returns).</typeparam>
public abstract partial class ClickableItemBase<TCollection, TSelf>
    : ItemObjectBase<TCollection, TSelf>
    where TCollection : IMauiScope<TCollection>, IItemRootProvider
    where TSelf : ClickableItemBase<TCollection, TSelf>
{
    /// <summary>
    /// Creates an item bound to an already-found root element.
    /// </summary>
    /// <param name="collection">The owning collection.</param>
    /// <param name="itemRoot">The item's root element.</param>
    /// <param name="index">The item's zero-based position.</param>
    protected ClickableItemBase(TCollection collection, IMauiElement itemRoot, int index)
        : base(collection, itemRoot, index)
    {
    }

    #region Core Methods (Element-Aware, No Logging)

    /// <summary>
    /// Clicks the item's own root element.
    /// </summary>
    /// <param name="element">The item's root element.</param>
    /// <param name="timeoutMs">Optional timeout.</param>
    protected virtual void ClickCore(IMauiElement element, int? timeoutMs = null)
    {
        // Invoked: a plain item is activated, the way a menu entry is. An item that is chosen
        // from a group rather than activated overrides this - see SelectableItemBase.
        element.Invoke();
    }

    /// <summary>
    /// Reads the item's caption: its text, or failing that its accessibility name.
    /// </summary>
    /// <param name="element">The item's root element.</param>
    [GenerateComparisons(Comparison.EqualTo | Comparison.Contains | Comparison.Empty)]
    protected virtual string? GetTextCore(IMauiElement element)
    {
        var text = element.Text;

        return string.IsNullOrEmpty(text) ? element.Name : text;
    }

    /// <summary>
    /// Whether the item is enabled.
    /// </summary>
    /// <param name="element">The item's root element (may be null).</param>
    protected virtual bool? IsEnabledCore(IMauiElement? element) => element?.Enabled;

    #endregion

    #region Guards

    /// <summary>
    /// Throws when the item is not enabled.
    /// </summary>
    /// <param name="element">The item's root element.</param>
    protected virtual void EnsureEnabledCore(IMauiElement element)
    {
        if (IsEnabledCore(element) != true)
        {
            throw new ElementNotReadyException(Locator, NotReadyReason.Disabled, $"item {Index}");
        }
    }

    /// <summary>
    /// Throws when the item cannot be acted on.
    /// </summary>
    /// <param name="element">The item's root element.</param>
    protected virtual void EnsureClickableCore(IMauiElement element) => EnsureEnabledCore(element);

    /// <inheritdoc />
    protected override void EnsureReadyForActionCore(IMauiElement root) => EnsureClickableCore(root);

    #endregion
}
