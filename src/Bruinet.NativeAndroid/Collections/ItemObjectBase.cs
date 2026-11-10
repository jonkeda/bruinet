using Bruinet.Core.Collections;
using Bruinet.Core.Locators;
using Bruinet.NativeAndroid.Interfaces;

namespace Bruinet.NativeAndroid.Collections;

/// <summary>
/// One row of a collection, as a scope for the controls inside it: Core's
/// <see cref="ItemObjectBase{TContext, TElement, TCollection, TSelf}"/> on the NativeAndroid types.
/// </summary>
/// <typeparam name="TSelf">The row's own type.</typeparam>
public abstract class ItemObjectBase<TSelf> : ItemObjectBase<INativeAndroidTestContext, INativeAndroidElement, INativeAndroidCollection, TSelf>, INativeAndroidScope<TSelf>
    where TSelf : ItemObjectBase<TSelf>
{
    /// <summary>The row of <paramref name="key"/>, found at <paramref name="position"/> when it was made.</summary>
    protected ItemObjectBase(INativeAndroidCollection collection, ItemKey key, int position)
        : base(collection, key, position)
    {
    }

    /// <inheritdoc />
    public override INativeAndroidTestContext Context => Collection.Context;

    /// <inheritdoc />
    public INativeAndroidPage? Page => Collection.Page;

    /// <inheritdoc />
    protected override INativeAndroidElement? TryFindIn(INativeAndroidElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<INativeAndroidElement> FindAllIn(INativeAndroidElement root, Locator locator) => root.FindElements(locator);
}
