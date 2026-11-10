using Bruinet.Core.Locators;
using Bruinet.NativeAndroid.Interfaces;

namespace Bruinet.NativeAndroid.Containers;

/// <summary>
/// A scope inside another scope, found by its own root element: Core's
/// <see cref="Bruinet.Core.Scopes.ContainerObjectBase{TContext, TElement, TParentScope, TScope}"/> on the NativeAndroid types.
/// </summary>
/// <typeparam name="TParent">The scope it is declared in.</typeparam>
/// <typeparam name="TScope">The container's own type.</typeparam>
public abstract class ContainerObjectBase<TParent, TScope>
    : Bruinet.Core.Scopes.ContainerObjectBase<INativeAndroidTestContext, INativeAndroidElement, INativeAndroidScope<TParent>, TScope>, INativeAndroidContainer<TParent, TScope>
    where TParent : INativeAndroidScope<TParent>
    where TScope : INativeAndroidContainer<TParent, TScope>
{
    /// <summary>The container found by <paramref name="locator"/> in <paramref name="parentScope"/>.</summary>
    protected ContainerObjectBase(INativeAndroidScope<TParent> parentScope, Locator locator)
        : base(parentScope, locator)
    {
    }

    /// <summary>The container whose resource id is <paramref name="resourceId"/> in <paramref name="parentScope"/>.</summary>
    protected ContainerObjectBase(INativeAndroidScope<TParent> parentScope, string resourceId)
        : this(parentScope, Locator.ById(resourceId))
    {
    }

    /// <inheritdoc />
    public override INativeAndroidTestContext Context => ParentScope.Context;

    /// <inheritdoc />
    public TParent Parent => ParentScope.Self;

    /// <inheritdoc />
    public INativeAndroidPage? Page => ParentScope.Page;

    /// <inheritdoc />
    protected override bool IsShown(INativeAndroidElement root) => root.Visible;

    /// <inheritdoc />
    protected override INativeAndroidElement? TryFindIn(INativeAndroidElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<INativeAndroidElement> FindAllIn(INativeAndroidElement root, Locator locator) => root.FindElements(locator);
}
