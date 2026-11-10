using Bruinet.Core.Collections;
using Bruinet.Core.Locators;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Collections;

/// <summary>
/// One row of a collection, as a scope for the controls inside it: Core's
/// <see cref="ItemObjectBase{TContext, TElement, TCollection, TSelf}"/> on the Stride types.
/// </summary>
/// <typeparam name="TSelf">The row's own type.</typeparam>
public abstract class ItemObjectBase<TSelf> : ItemObjectBase<IStrideTestContext, IStrideElement, IStrideCollection, TSelf>, IStrideScope<TSelf>
    where TSelf : ItemObjectBase<TSelf>
{
    /// <summary>The row of <paramref name="key"/>, found at <paramref name="position"/> when it was made.</summary>
    protected ItemObjectBase(IStrideCollection collection, ItemKey key, int position)
        : base(collection, key, position)
    {
    }

    /// <inheritdoc />
    public override IStrideTestContext Context => Collection.Context;

    /// <inheritdoc />
    public IStridePage? Page => Collection.Page;

    /// <inheritdoc />
    protected override IStrideElement? TryFindIn(IStrideElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<IStrideElement> FindAllIn(IStrideElement root, Locator locator) => root.FindElements(locator);
}
