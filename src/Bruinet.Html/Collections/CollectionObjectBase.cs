using Bruinet.Core.Collections;
using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Collections;

/// <summary>
/// A list of item rows that hands out typed row objects: Core's
/// <see cref="CollectionObjectBase{TContext, TElement, TParentScope, TSelf, TItem}"/> on the Html types,
/// with where Html's rows are, how a row's key is read, and how its lists scroll.
/// </summary>
/// <typeparam name="TParent">The scope the collection is declared in.</typeparam>
/// <typeparam name="TSelf">The collection type, for fluent chains.</typeparam>
/// <typeparam name="TItem">The row type.</typeparam>
public abstract class CollectionObjectBase<TParent, TSelf, TItem>
    : CollectionObjectBase<IHtmlTestContext, IHtmlElement, IHtmlScope<TParent>, TSelf, TItem>, IHtmlContainer<TParent, TSelf>, IHtmlCollection
    where TParent : IHtmlScope<TParent>
    where TSelf : CollectionObjectBase<TParent, TSelf, TItem>
    where TItem : ItemObjectBase<TItem>
{
    private readonly Locator _itemLocator;

    /// <summary>The collection found by <paramref name="locator"/>, whose rows match <paramref name="itemLocator"/> inside it.</summary>
    protected CollectionObjectBase(IHtmlScope<TParent> parent, Locator locator, Locator itemLocator)
        : base(parent, locator)
    {
        _itemLocator = itemLocator ?? throw new ArgumentNullException(nameof(itemLocator));
    }

    /// <inheritdoc />
    public override IHtmlTestContext Context => ParentScope.Context;

    /// <inheritdoc />
    public TParent Parent => ParentScope.Self;

    /// <inheritdoc />
    public IHtmlPage? Page => ParentScope.Page;

    /// <summary>The collection's root element now; throws when it is not there.</summary>
    public IHtmlElement ContainerRoot => TryFindRootElement() ?? throw new Bruinet.Core.Exceptions.ElementNotFoundException(Locator);

    /// <inheritdoc />
    /// <remarks>
    /// A keyed row is found by one lookup of its <c>data-item-key</c>, not by reading each row's key:
    /// that costs a driver call per row, and a long list then outlasts the call's budget.
    /// </remarks>
    public override IHtmlElement? TryGetItemRoot(ItemKey key)
        => key.Kind == ItemKeyKind.Logical
            ? TryFindRootElement()?.TryFindElement(KeySelector(key.Value))
            : base.TryGetItemRoot(key);

    /// <summary>The row whose <c>data-item-key</c> is <paramref name="key"/> now, or <c>null</c>.</summary>
    /// <remarks>
    /// One lookup by the key's selector, as every later read of the row is. The row's position is not
    /// looked up, so it is <c>-1</c>.
    /// </remarks>
    public override TItem? TryItem(string key)
    {
        var itemKey = ItemKey.Logical(key);
        return TryGetItemRoot(itemKey) is null ? null : CreateItem(itemKey, -1);
    }

    // The row whose data-item-key is the key, as a CSS selector.
    private static Locator KeySelector(string key)
        => Locator.ByCss($"[{ItemKeys.Attribute}=\"{key.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"]");

    /// <inheritdoc />
    protected override ItemKey KeyOf(IHtmlElement row, int position) => ItemKeys.Of(row, position);

    /// <inheritdoc />
    protected override IReadOnlyList<IHtmlElement> TryGetItemRoots() => FindElements(_itemLocator);

    /// <inheritdoc />
    protected override bool IsShown(IHtmlElement root) => root.Visible;

    /// <inheritdoc />
    protected override IHtmlElement? TryFindIn(IHtmlElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<IHtmlElement> FindAllIn(IHtmlElement root, Locator locator) => root.FindElements(locator);
}
