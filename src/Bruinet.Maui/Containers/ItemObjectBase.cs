namespace Bruinet.Maui.Containers;

/// <summary>
/// Base class for a collection item: a container whose root element is supplied rather
/// than located.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes item scoping real. Because the row's root is already known, its
/// children resolve within that subtree, so an item template can use the same automation
/// ids on every row - the normal MAUI authoring style - without the rows becoming
/// ambiguous.
/// </para>
/// <para>
/// A row records which item it holds (<see cref="Key"/>) when it is created. A row whose element
/// has gone is found again by that key, never by where it happened to be; a row whose element now
/// holds another item reports <see cref="ScopeReadinessState.ItemChanged"/>, so a call never acts
/// on the wrong item.
/// </para>
/// <para>
/// MAUI keeps its own row base rather than Core's (public API review, proposal 5, rule 6): a MAUI row
/// keeps its element and checks that it still holds its item, where the other stacks find the row
/// again by key on every lookup.
/// </para>
/// </remarks>
/// <typeparam name="TCollection">The owning collection.</typeparam>
/// <typeparam name="TSelf">The item type itself (self-referencing for fluent returns).</typeparam>
public abstract class ItemObjectBase<TCollection, TSelf>
    : ContainerObjectBase<TCollection, TSelf>, IMauiItemObject<TCollection, TSelf>
    where TCollection : IMauiScope<TCollection>, IItemRootProvider
    where TSelf : ItemObjectBase<TCollection, TSelf>
{
    private IMauiElement _itemRoot;

    /// <summary>
    /// Creates an item bound to an already-found root element.
    /// </summary>
    /// <param name="collection">The owning collection.</param>
    /// <param name="itemRoot">The item's root element.</param>
    /// <param name="index">The item's zero-based position.</param>
    protected ItemObjectBase(TCollection collection, IMauiElement itemRoot, int index)
        : base(collection, Locator.ByAutomationId($"[item {index}]"))
    {
        _itemRoot = itemRoot ?? throw new ArgumentNullException(nameof(itemRoot));
        Index = index;
        Key = collection.KeyOf(itemRoot, index);
    }

    /// <inheritdoc />
    public int Index { get; }

    /// <summary>Which item this row holds, recorded when it was created.</summary>
    public ItemKey Key { get; }

    /// <inheritdoc />
    protected override string ScopeName => $"{GetType().Name.Split('`')[0]} [{Key}]";

    /// <inheritdoc />
    /// <remarks>
    /// Never: a sweep searches the whole list, and every row carries the same ids, so it would find
    /// another row's element.
    /// </remarks>
    public override bool AllowsScrollLookup => false;

    /// <inheritdoc />
    protected override bool IsCachedRootValid(IMauiElement root) => IsUsable(root) && Key.IsHeldBy(root);

    /// <summary>
    /// Returns the supplied root while it still holds this item, and otherwise finds the item again
    /// by its <see cref="Key"/>.
    /// </summary>
    protected override IMauiElement FindContainerRootElement()
    {
        if (IsUsable(_itemRoot) && Key.IsHeldBy(_itemRoot))
        {
            return _itemRoot;
        }

        var refreshed = Parent.TryGetItemRoot(Key) ?? throw RootNotFound();

        _itemRoot = refreshed;
        return _itemRoot;
    }

    /// <inheritdoc />
    protected override ElementNotFoundException RootNotFound()
        => new($"Item [{Key}] is no longer in its collection, or not realized now.");

    /// <inheritdoc />
    protected override ScopeReadiness ProbeContentReadiness(IMauiElement root)
    {
        if (Key.IsHeldBy(root))
        {
            return ContentReady();
        }

        InvalidateCache();
        return new ScopeReadiness(ScopeName, ScopeReadinessState.ItemChanged,
            $"the row's element now holds another item than [{Key}]");
    }

    /// <summary>
    /// Whether the row's element is still there (the alive rule, R6) and still has a size.
    /// </summary>
    /// <remarks>
    /// One read: the bounds of a removed element fail as stale on every head, so a separate
    /// liveness read (<c>InstanceKey</c>, a round trip of its own on Android) said nothing more
    /// (.my/android-performance, P2).
    /// </remarks>
    private static bool IsUsable(IMauiElement element)
    {
        try
        {
            var rect = Bruinet.Core.Calls.AttemptMemo.Remember(element, nameof(IMauiElement.Rect), () => element.Rect);
            return rect.Width > 0 && rect.Height > 0;
        }
        catch (StaleElementException)
        {
            return false;
        }
    }
}

/// <summary>
/// Lets an item record which item it holds, and find its root again by that record after
/// virtualization invalidates it.
/// Implemented by collections; separated from the collection interface so
/// <see cref="ItemObjectBase{TCollection, TSelf}"/> can constrain on it without
/// naming the item type and creating a circular constraint.
/// </summary>
public interface IItemRootProvider : IItemRootSource<IMauiElement>
{
    /// <summary>
    /// The strongest key the platform offers for the row at <paramref name="position"/> whose
    /// element is <paramref name="itemRoot"/>.
    /// </summary>
    ItemKey KeyOf(IMauiElement itemRoot, int position);

}
