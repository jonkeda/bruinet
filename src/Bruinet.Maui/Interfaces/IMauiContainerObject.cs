namespace Bruinet.Maui.Interfaces;

/// <summary>
/// A MAUI container object: a scope rooted at an element, holding controls and other
/// containers, with an explicit way back out to its parent.
/// </summary>
/// <remarks>
/// Unlike <see cref="IMauiContainer{TParent, TSelf}"/> (which a control implements as a
/// side effect of scoping), a container object is a peer of a page object. Its own
/// members return <typeparamref name="TSelf"/>, so a chain stays inside the container
/// until <see cref="Parent"/> is called.
/// </remarks>
/// <typeparam name="TParent">The parent scope type (a page or another container).</typeparam>
/// <typeparam name="TSelf">The container type itself (self-referencing).</typeparam>
public interface IMauiContainerObject<TParent, TSelf>
    : IMauiScope<TSelf>
    where TParent : IMauiScope<TParent>
    where TSelf : IMauiContainerObject<TParent, TSelf>
{
    /// <summary>The element all of this container's lookups are scoped to.</summary>
    IMauiElement ContainerRoot { get; }

    /// <summary>Forgets the cached root, so the next lookup finds it again.</summary>
    void InvalidateCache();

    /// <summary>
    /// The parent scope. Chain <c>.Parent.Parent</c> to walk out to the root page.
    /// </summary>
    TParent Parent { get; }
}

/// <summary>
/// A MAUI container whose root element is supplied rather than located, so its children
/// resolve within that subtree. Collection rows are the motivating case.
/// </summary>
/// <typeparam name="TCollection">The owning collection scope.</typeparam>
/// <typeparam name="TSelf">The item type itself (self-referencing).</typeparam>
public interface IMauiItemObject<TCollection, TSelf>
    : IMauiContainerObject<TCollection, TSelf>
    where TCollection : IMauiScope<TCollection>
    where TSelf : IMauiItemObject<TCollection, TSelf>
{
    /// <summary>The item's position in its collection when it was created.</summary>
    int Index { get; }

    /// <summary>Which item the row holds, recorded when it was created; the row is found again by it.</summary>
    ItemKey Key { get; }
}

/// <summary>
/// A MAUI collection object: a container that also hands out typed items.
/// </summary>
/// <typeparam name="TParent">The parent scope type.</typeparam>
/// <typeparam name="TSelf">The collection type itself (self-referencing).</typeparam>
/// <typeparam name="TItem">The item container type.</typeparam>
public interface IMauiCollectionObject<TParent, TSelf, TItem>
    : IMauiContainerObject<TParent, TSelf>
    where TParent : IMauiScope<TParent>
    where TSelf : IMauiCollectionObject<TParent, TSelf, TItem>
    where TItem : IMauiItemObject<TSelf, TItem>
{
    /// <summary>The item at <paramref name="index"/>, waiting for it to appear; throws when it does not.</summary>
    TItem Item(int index, int? timeoutMs = null);

    /// <summary>The item at <paramref name="index"/>, or null when there is none now.</summary>
    TItem? TryItem(int index);

    /// <summary>How many items are materialized now.</summary>
    int GetItemCount();

    /// <summary>The materialized items.</summary>
    IEnumerable<TItem> Items { get; }
}
