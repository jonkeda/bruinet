using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Containers;

/// <summary>
/// A scope inside another scope, found by its own root element: Core's
/// <see cref="Bruinet.Core.Scopes.ContainerObjectBase{TContext, TElement, TParentScope, TScope}"/> on the WinForms types.
/// </summary>
/// <typeparam name="TParent">The scope it is declared in.</typeparam>
/// <typeparam name="TScope">The container's own type.</typeparam>
public abstract class ContainerObjectBase<TParent, TScope>
    : Bruinet.Core.Scopes.ContainerObjectBase<IWinFormsTestContext, IWinFormsElement, IWinFormsScope<TParent>, TScope>, IWinFormsContainer<TParent, TScope>
    where TParent : IWinFormsScope<TParent>
    where TScope : IWinFormsContainer<TParent, TScope>
{
    /// <summary>The container found by <paramref name="locator"/> in <paramref name="parentScope"/>.</summary>
    protected ContainerObjectBase(IWinFormsScope<TParent> parentScope, Locator locator)
        : base(parentScope, locator)
    {
    }

    /// <summary>The container whose AutomationId is <paramref name="automationId"/> in <paramref name="parentScope"/>.</summary>
    protected ContainerObjectBase(IWinFormsScope<TParent> parentScope, string automationId)
        : this(parentScope, Locator.ByAutomationId(automationId))
    {
    }

    /// <inheritdoc />
    public override IWinFormsTestContext Context => ParentScope.Context;

    /// <inheritdoc />
    public TParent Parent => ParentScope.Self;

    /// <inheritdoc />
    public IWinFormsPage? Page => ParentScope.Page;

    /// <inheritdoc />
    protected override bool IsShown(IWinFormsElement root) => root.Visible;

    /// <inheritdoc />
    protected override IWinFormsElement? TryFindIn(IWinFormsElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<IWinFormsElement> FindAllIn(IWinFormsElement root, Locator locator) => root.FindElements(locator);
}
