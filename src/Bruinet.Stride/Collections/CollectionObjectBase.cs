using Bruinet.Core.Collections;
using Bruinet.Core.Locators;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Collections;

/// <summary>
/// A list of item rows that hands out typed row objects: Core's
/// <see cref="CollectionObjectBase{TContext, TElement, TParentScope, TSelf, TItem}"/> on the Stride types,
/// with where Stride's rows are, how a row's key is read, and how its lists scroll.
/// </summary>
/// <typeparam name="TParent">The scope the collection is declared in.</typeparam>
/// <typeparam name="TSelf">The collection type, for fluent chains.</typeparam>
/// <typeparam name="TItem">The row type.</typeparam>
public abstract class CollectionObjectBase<TParent, TSelf, TItem>
    : CollectionObjectBase<IStrideTestContext, IStrideElement, IStrideScope<TParent>, TSelf, TItem>, IStrideContainer<TParent, TSelf>, IStrideCollection
    where TParent : IStrideScope<TParent>
    where TSelf : CollectionObjectBase<TParent, TSelf, TItem>
    where TItem : ItemObjectBase<TItem>
{
    /// <summary>The list found by <paramref name="locator"/>; its rows are its children.</summary>
    protected CollectionObjectBase(IStrideScope<TParent> parent, Locator locator)
        : base(parent, locator)
    {
    }

    /// <summary>The list whose AutomationId is <paramref name="automationId"/>.</summary>
    protected CollectionObjectBase(IStrideScope<TParent> parent, string automationId)
        : this(parent, Locator.ByAutomationId(automationId))
    {
    }

    /// <summary>The element inside a row whose text is the row's key; null to use the row's own text.</summary>
    protected virtual Locator? ItemKeyLocator => null;

    /// <inheritdoc />
    public override IStrideTestContext Context => ParentScope.Context;

    /// <inheritdoc />
    public TParent Parent => ParentScope.Self;

    /// <inheritdoc />
    public IStridePage? Page => ParentScope.Page;

    /// <inheritdoc />
    protected override ItemKey KeyOf(IStrideElement row, int position) => ItemKeys.Of(row, position, ItemKeyLocator);

    /// <inheritdoc />
    protected override IReadOnlyList<IStrideElement> TryGetItemRoots() => TryFindRootElement()?.Children() ?? [];

    /// <inheritdoc />
    protected override bool IsShown(IStrideElement root) => root.Visible;

    /// <inheritdoc />
    protected override IStrideElement? TryFindIn(IStrideElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<IStrideElement> FindAllIn(IStrideElement root, Locator locator) => root.FindElements(locator);
}
