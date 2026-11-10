using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Controls;

/// <summary>
/// A scope inside another scope, found by its own root element: Core's
/// <see cref="Bruinet.Core.Scopes.ContainerObjectBase{TContext, TElement, TParentScope, TScope}"/> on the Html types.
/// </summary>
/// <typeparam name="TParent">The scope it is declared in.</typeparam>
/// <typeparam name="TScope">The container's own type.</typeparam>
public abstract class ContainerBase<TParent, TScope>
    : Bruinet.Core.Scopes.ContainerObjectBase<IHtmlTestContext, IHtmlElement, IHtmlScope<TParent>, TScope>, IHtmlContainer<TParent, TScope>
    where TParent : IHtmlScope<TParent>
    where TScope : IHtmlContainer<TParent, TScope>
{
    /// <summary>The container found by <paramref name="locator"/> in <paramref name="parentScope"/>.</summary>
    protected ContainerBase(IHtmlScope<TParent> parentScope, Locator locator)
        : base(parentScope, locator)
    {
    }

    /// <inheritdoc />
    public override IHtmlTestContext Context => ParentScope.Context;

    /// <inheritdoc />
    public TParent Parent => ParentScope.Self;

    /// <inheritdoc />
    public IHtmlPage? Page => ParentScope.Page;

    /// <summary>The container's root element now; throws when it is not there.</summary>
    public IHtmlElement ContainerRoot => TryFindRootElement() ?? throw new Bruinet.Core.Exceptions.ElementNotFoundException(Locator);

    /// <inheritdoc />
    protected override bool IsShown(IHtmlElement root) => root.Visible;

    /// <inheritdoc />
    protected override IHtmlElement? TryFindIn(IHtmlElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<IHtmlElement> FindAllIn(IHtmlElement root, Locator locator) => root.FindElements(locator);
}
