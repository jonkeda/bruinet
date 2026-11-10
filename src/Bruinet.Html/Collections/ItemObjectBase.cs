using Bruinet.Core.Collections;
using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Collections;

/// <summary>
/// One row of a collection, as a scope for the controls inside it: Core's
/// <see cref="ItemObjectBase{TContext, TElement, TCollection, TSelf}"/> on the Html types.
/// </summary>
/// <typeparam name="TSelf">The row's own type.</typeparam>
public abstract class ItemObjectBase<TSelf> : ItemObjectBase<IHtmlTestContext, IHtmlElement, IHtmlCollection, TSelf>, IHtmlScope<TSelf>
    where TSelf : ItemObjectBase<TSelf>
{
    /// <summary>The row of <paramref name="key"/>, found at <paramref name="position"/> when it was made.</summary>
    protected ItemObjectBase(IHtmlCollection collection, ItemKey key, int position)
        : base(collection, key, position)
    {
    }

    /// <inheritdoc />
    public override IHtmlTestContext Context => Collection.Context;

    /// <inheritdoc />
    public IHtmlPage? Page => Collection.Page;

    /// <inheritdoc />
    protected override IHtmlElement? TryFindIn(IHtmlElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<IHtmlElement> FindAllIn(IHtmlElement root, Locator locator) => root.FindElements(locator);
}
