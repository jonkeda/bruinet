using Bruinet.Core.Collections;
using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Collections;

/// <summary>
/// A list of item rows that hands out typed row objects: Core's
/// <see cref="CollectionObjectBase{TContext, TElement, TParentScope, TSelf, TItem}"/> on the Wpf types,
/// with where Wpf's rows are, how a row's key is read, and how its lists scroll.
/// </summary>
/// <typeparam name="TParent">The scope the collection is declared in.</typeparam>
/// <typeparam name="TSelf">The collection type, for fluent chains.</typeparam>
/// <typeparam name="TItem">The row type.</typeparam>
public abstract class CollectionObjectBase<TParent, TSelf, TItem>
    : CollectionObjectBase<IWpfTestContext, IWpfElement, IWpfScope<TParent>, TSelf, TItem>, IWpfContainer<TParent, TSelf>, IWpfCollection
    where TParent : IWpfScope<TParent>
    where TSelf : CollectionObjectBase<TParent, TSelf, TItem>
    where TItem : ItemObjectBase<TItem>
{
    /// <summary>A list's rows: its list items.</summary>
    protected static readonly Locator ListItems = Locator.ByControlType("ListItem");

    /// <summary>A data grid's rows.</summary>
    protected static readonly Locator DataItems = Locator.ByControlType("DataItem");

    private readonly Locator _itemLocator;

    /// <summary>The list found by <paramref name="locator"/>, whose rows match <paramref name="itemLocator"/> inside it (<see cref="ListItems"/> by default).</summary>
    protected CollectionObjectBase(IWpfScope<TParent> parent, Locator locator, Locator? itemLocator = null)
        : base(parent, locator)
    {
        _itemLocator = itemLocator ?? ListItems;
    }

    /// <summary>The list whose automationId is <paramref name="automationId"/>.</summary>
    protected CollectionObjectBase(IWpfScope<TParent> parent, string automationId, Locator? itemLocator = null)
        : this(parent, Locator.ByAutomationId(automationId), itemLocator)
    {
    }

    /// <summary>The element inside a row whose text is the row's key; null to use the stack's fallbacks.</summary>
    protected virtual Locator? ItemKeyLocator => null;

    /// <inheritdoc />
    public override IWpfTestContext Context => ParentScope.Context;

    /// <inheritdoc />
    public TParent Parent => ParentScope.Self;

    /// <inheritdoc />
    public IWpfPage? Page => ParentScope.Page;

    /// <inheritdoc />
    protected override ItemKey KeyOf(IWpfElement row, int position) => ItemKeys.Of(row, position, ItemKeyLocator);

    /// <inheritdoc />
    protected override IReadOnlyList<IWpfElement> TryGetItemRoots() => FindElements(_itemLocator);

    /// <inheritdoc />
    protected override bool ScrollsToFind => true;

    /// <inheritdoc />
    protected override bool ScrollStep(IWpfElement list, bool onwards) => list.ScrollPage(onwards);

    /// <inheritdoc />
    protected override bool IsShown(IWpfElement root) => root.Visible;

    /// <inheritdoc />
    protected override IWpfElement? TryFindIn(IWpfElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<IWpfElement> FindAllIn(IWpfElement root, Locator locator) => root.FindElements(locator);
}
