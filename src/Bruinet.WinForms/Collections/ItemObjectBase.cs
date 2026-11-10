using Bruinet.Core.Collections;
using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Collections;

/// <summary>
/// One row of a collection, as a scope for the controls inside it: Core's
/// <see cref="ItemObjectBase{TContext, TElement, TCollection, TSelf}"/> on the WinForms types.
/// </summary>
/// <typeparam name="TSelf">The row's own type.</typeparam>
public abstract class ItemObjectBase<TSelf> : ItemObjectBase<IWinFormsTestContext, IWinFormsElement, IWinFormsCollection, TSelf>, IWinFormsScope<TSelf>
    where TSelf : ItemObjectBase<TSelf>
{
    /// <summary>The row of <paramref name="key"/>, found at <paramref name="position"/> when it was made.</summary>
    protected ItemObjectBase(IWinFormsCollection collection, ItemKey key, int position)
        : base(collection, key, position)
    {
    }

    /// <inheritdoc />
    public override IWinFormsTestContext Context => Collection.Context;

    /// <inheritdoc />
    public IWinFormsPage? Page => Collection.Page;

    /// <inheritdoc />
    protected override IWinFormsElement? TryFindIn(IWinFormsElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<IWinFormsElement> FindAllIn(IWinFormsElement root, Locator locator) => root.FindElements(locator);
}
