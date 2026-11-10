using Bruinet.Core.Locators;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Containers;

/// <summary>
/// A scope inside another scope, found by its own root element: Core's
/// <see cref="Bruinet.Core.Scopes.ContainerObjectBase{TContext, TElement, TParentScope, TScope}"/> on the Stride types.
/// </summary>
/// <typeparam name="TParent">The scope it is declared in.</typeparam>
/// <typeparam name="TScope">The container's own type.</typeparam>
public abstract class ContainerObjectBase<TParent, TScope>
    : Bruinet.Core.Scopes.ContainerObjectBase<IStrideTestContext, IStrideElement, IStrideScope<TParent>, TScope>, IStrideContainer<TParent, TScope>
    where TParent : IStrideScope<TParent>
    where TScope : IStrideContainer<TParent, TScope>
{
    /// <summary>The container found by <paramref name="locator"/> in <paramref name="parentScope"/>.</summary>
    protected ContainerObjectBase(IStrideScope<TParent> parentScope, Locator locator)
        : base(parentScope, locator)
    {
    }

    /// <summary>The container whose AutomationId is <paramref name="automationId"/> in <paramref name="parentScope"/>.</summary>
    protected ContainerObjectBase(IStrideScope<TParent> parentScope, string automationId)
        : this(parentScope, Locator.ByAutomationId(automationId))
    {
    }

    /// <inheritdoc />
    public override IStrideTestContext Context => ParentScope.Context;

    /// <inheritdoc />
    public TParent Parent => ParentScope.Self;

    /// <inheritdoc />
    public IStridePage? Page => ParentScope.Page;

    /// <inheritdoc />
    protected override bool IsShown(IStrideElement root) => root.Visible;

    /// <inheritdoc />
    protected override IStrideElement? TryFindIn(IStrideElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<IStrideElement> FindAllIn(IStrideElement root, Locator locator) => root.FindElements(locator);
}
