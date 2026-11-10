using Bruinet.Core.Collections;
using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Collections;

/// <summary>
/// One row of a collection, as a scope for the controls inside it: Core's
/// <see cref="ItemObjectBase{TContext, TElement, TCollection, TSelf}"/> on the Wpf types.
/// </summary>
/// <typeparam name="TSelf">The row's own type.</typeparam>
public abstract class ItemObjectBase<TSelf> : ItemObjectBase<IWpfTestContext, IWpfElement, IWpfCollection, TSelf>, IWpfScope<TSelf>
    where TSelf : ItemObjectBase<TSelf>
{
    /// <summary>The row of <paramref name="key"/>, found at <paramref name="position"/> when it was made.</summary>
    protected ItemObjectBase(IWpfCollection collection, ItemKey key, int position)
        : base(collection, key, position)
    {
    }

    /// <inheritdoc />
    public override IWpfTestContext Context => Collection.Context;

    /// <inheritdoc />
    public IWpfPage? Page => Collection.Page;

    /// <inheritdoc />
    protected override IWpfElement? TryFindIn(IWpfElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<IWpfElement> FindAllIn(IWpfElement root, Locator locator) => root.FindElements(locator);
}
