using Bruinet.Core.Collections;
using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Collections;

/// <summary>
/// One row of a collection, as a scope for the controls inside it: Core's
/// <see cref="ItemObjectBase{TContext, TElement, TCollection, TSelf}"/> on the NativeIos types.
/// </summary>
/// <typeparam name="TSelf">The row's own type.</typeparam>
public abstract class ItemObjectBase<TSelf> : ItemObjectBase<INativeIosTestContext, INativeIosElement, INativeIosCollection, TSelf>, INativeIosScope<TSelf>
    where TSelf : ItemObjectBase<TSelf>
{
    /// <summary>The row of <paramref name="key"/>, found at <paramref name="position"/> when it was made.</summary>
    protected ItemObjectBase(INativeIosCollection collection, ItemKey key, int position)
        : base(collection, key, position)
    {
    }

    /// <inheritdoc />
    public override INativeIosTestContext Context => Collection.Context;

    /// <inheritdoc />
    public INativeIosPage? Page => Collection.Page;

    /// <inheritdoc />
    protected override INativeIosElement? TryFindIn(INativeIosElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<INativeIosElement> FindAllIn(INativeIosElement root, Locator locator) => root.FindElements(locator);
}
