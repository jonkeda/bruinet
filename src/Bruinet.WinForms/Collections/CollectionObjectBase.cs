using Bruinet.Core.Collections;
using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Collections;

/// <summary>
/// A list of item rows that hands out typed row objects: Core's
/// <see cref="CollectionObjectBase{TContext, TElement, TParentScope, TSelf, TItem}"/> on the WinForms types,
/// with where WinForms's rows are, how a row's key is read, and how its lists scroll.
/// </summary>
/// <typeparam name="TParent">The scope the collection is declared in.</typeparam>
/// <typeparam name="TSelf">The collection type, for fluent chains.</typeparam>
/// <typeparam name="TItem">The row type.</typeparam>
public abstract class CollectionObjectBase<TParent, TSelf, TItem>
    : CollectionObjectBase<IWinFormsTestContext, IWinFormsElement, IWinFormsScope<TParent>, TSelf, TItem>, IWinFormsContainer<TParent, TSelf>, IWinFormsCollection
    where TParent : IWinFormsScope<TParent>
    where TSelf : CollectionObjectBase<TParent, TSelf, TItem>
    where TItem : ItemObjectBase<TItem>
{
    /// <summary>A list's rows: its list items.</summary>
    protected static readonly Locator ListItems = Locator.ByControlType("ListItem");

    /// <summary>A data grid's rows.</summary>
    protected static readonly Locator DataItems = Locator.ByControlType("DataItem");

    private readonly Locator _itemLocator;

    /// <summary>The list found by <paramref name="locator"/>, whose rows match <paramref name="itemLocator"/> inside it (<see cref="ListItems"/> by default).</summary>
    protected CollectionObjectBase(IWinFormsScope<TParent> parent, Locator locator, Locator? itemLocator = null)
        : base(parent, locator)
    {
        _itemLocator = itemLocator ?? ListItems;
    }

    /// <summary>The list whose automationId is <paramref name="automationId"/>.</summary>
    protected CollectionObjectBase(IWinFormsScope<TParent> parent, string automationId, Locator? itemLocator = null)
        : this(parent, Locator.ByAutomationId(automationId), itemLocator)
    {
    }

    /// <summary>The element inside a row whose text is the row's key; null to use the stack's fallbacks.</summary>
    protected virtual Locator? ItemKeyLocator => null;

    /// <inheritdoc />
    public override IWinFormsTestContext Context => ParentScope.Context;

    /// <inheritdoc />
    public TParent Parent => ParentScope.Self;

    /// <inheritdoc />
    public IWinFormsPage? Page => ParentScope.Page;

    /// <inheritdoc />
    protected override ItemKey KeyOf(IWinFormsElement row, int position) => ItemKeys.Of(row, position, ItemKeyLocator);

    /// <inheritdoc />
    protected override IReadOnlyList<IWinFormsElement> TryGetItemRoots() => FindElements(_itemLocator);

    /// <inheritdoc />
    protected override bool ScrollsToFind => true;

    /// <inheritdoc />
    protected override bool ScrollStep(IWinFormsElement list, bool onwards) => list.ScrollPage(onwards);

    /// <inheritdoc />
    protected override bool IsShown(IWinFormsElement root) => root.Visible;

    /// <inheritdoc />
    protected override IWinFormsElement? TryFindIn(IWinFormsElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<IWinFormsElement> FindAllIn(IWinFormsElement root, Locator locator) => root.FindElements(locator);
}
