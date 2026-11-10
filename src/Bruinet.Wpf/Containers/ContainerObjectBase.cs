using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Containers;

/// <summary>
/// A scope inside another scope, found by its own root element: Core's
/// <see cref="Bruinet.Core.Scopes.ContainerObjectBase{TContext, TElement, TParentScope, TScope}"/> on the Wpf types.
/// </summary>
/// <typeparam name="TParent">The scope it is declared in.</typeparam>
/// <typeparam name="TScope">The container's own type.</typeparam>
public abstract class ContainerObjectBase<TParent, TScope>
    : Bruinet.Core.Scopes.ContainerObjectBase<IWpfTestContext, IWpfElement, IWpfScope<TParent>, TScope>, IWpfContainer<TParent, TScope>
    where TParent : IWpfScope<TParent>
    where TScope : IWpfContainer<TParent, TScope>
{
    /// <summary>The container found by <paramref name="locator"/> in <paramref name="parentScope"/>.</summary>
    protected ContainerObjectBase(IWpfScope<TParent> parentScope, Locator locator)
        : base(parentScope, locator)
    {
    }

    /// <summary>The container whose AutomationId is <paramref name="automationId"/> in <paramref name="parentScope"/>.</summary>
    protected ContainerObjectBase(IWpfScope<TParent> parentScope, string automationId)
        : this(parentScope, Locator.ByAutomationId(automationId))
    {
    }

    /// <inheritdoc />
    public override IWpfTestContext Context => ParentScope.Context;

    /// <inheritdoc />
    public TParent Parent => ParentScope.Self;

    /// <inheritdoc />
    public IWpfPage? Page => ParentScope.Page;

    /// <inheritdoc />
    protected override bool IsShown(IWpfElement root) => root.Visible;

    /// <inheritdoc />
    protected override IWpfElement? TryFindIn(IWpfElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<IWpfElement> FindAllIn(IWpfElement root, Locator locator) => root.FindElements(locator);
}
