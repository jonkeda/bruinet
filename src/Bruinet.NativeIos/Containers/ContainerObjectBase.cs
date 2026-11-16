using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Containers;

/// <summary>
/// A scope inside another scope, found by its own root element: Core's
/// <see cref="Bruinet.Core.Scopes.ContainerObjectBase{TContext, TElement, TParentScope, TScope}"/> on the NativeIos types.
/// </summary>
/// <typeparam name="TParent">The scope it is declared in.</typeparam>
/// <typeparam name="TScope">The container's own type.</typeparam>
public abstract class ContainerObjectBase<TParent, TScope>
    : Bruinet.Core.Scopes.ContainerObjectBase<INativeIosTestContext, INativeIosElement, INativeIosScope<TParent>, TScope>, INativeIosContainer<TParent, TScope>
    where TParent : INativeIosScope<TParent>
    where TScope : INativeIosContainer<TParent, TScope>
{
    /// <summary>The container found by <paramref name="locator"/> in <paramref name="parentScope"/>.</summary>
    protected ContainerObjectBase(INativeIosScope<TParent> parentScope, Locator locator)
        : base(parentScope, locator)
    {
    }

    /// <summary>The container whose accessibility identifier is <paramref name="identifier"/> in <paramref name="parentScope"/>.</summary>
    protected ContainerObjectBase(INativeIosScope<TParent> parentScope, string identifier)
        : this(parentScope, Locator.ById(identifier))
    {
    }

    /// <inheritdoc />
    public override INativeIosTestContext Context => ParentScope.Context;

    /// <inheritdoc />
    public TParent Parent => ParentScope.Self;

    /// <inheritdoc />
    public INativeIosPage? Page => ParentScope.Page;

    /// <inheritdoc />
    protected override bool IsShown(INativeIosElement root) => root.Visible;

    /// <inheritdoc />
    protected override INativeIosElement? TryFindIn(INativeIosElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<INativeIosElement> FindAllIn(INativeIosElement root, Locator locator) => root.FindElements(locator);
}
