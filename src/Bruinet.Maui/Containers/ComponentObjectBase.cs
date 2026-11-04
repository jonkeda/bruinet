using Bruinet.Maui.Controls.Display;

namespace Bruinet.Maui.Containers;

/// <summary>
/// Base class for component objects: composite containers rooted at an element rather than at
/// the driver, hosting typed child controls (e.g., buttons, entries, labels, spinners)
/// and encapsulating component-level interactions.
/// </summary>
/// <typeparam name="TParent">The parent scope type (a page or another container).</typeparam>
/// <typeparam name="TSelf">The component type itself (self-referencing for fluent returns).</typeparam>
public abstract class ComponentObjectBase<TParent, TSelf>
    : ContainerObjectBase<TParent, TSelf>
    where TParent : IMauiScope<TParent>
    where TSelf : ComponentObjectBase<TParent, TSelf>
{
    /// <summary>
    /// Creates a component container within the given parent scope using a locator.
    /// </summary>
    /// <param name="parentScope">The parent scope (page or container).</param>
    /// <param name="locator">The locator for the component's root element.</param>
    protected ComponentObjectBase(IMauiScope<TParent> parentScope, Locator locator)
        : base(parentScope, locator)
    {
    }

    /// <summary>
    /// Creates a component container within the given parent scope using a string locator value.
    /// </summary>
    /// <param name="parentScope">The parent scope (page or container).</param>
    /// <param name="locatorValue">The locator value (e.g. automation ID).</param>
    protected ComponentObjectBase(IMauiScope<TParent> parentScope, string locatorValue)
        : base(parentScope, locatorValue)
    {
    }

    private IMauiElement? _foundRoot;
    private string? _foundAutomationId;

    /// <inheritdoc />
    // A recycling list hands a live element to another row; see call-model.md, Scopes.
    protected override bool IsCachedRootValid(IMauiElement root)
        => base.IsCachedRootValid(root) && StillCarriesItsId(root);

    /// <inheritdoc />
    protected override IMauiElement FindContainerRootElement()
    {
        var root = base.FindContainerRootElement();
        _foundRoot = root;
        _foundAutomationId = root.AutomationId;
        return root;
    }

    // A root found by an override (a stand-in part) or without an id has nothing to compare with.
    private bool StillCarriesItsId(IMauiElement root)
        => !ReferenceEquals(root, _foundRoot)
           || string.IsNullOrEmpty(_foundAutomationId)
           || Bruinet.Core.Calls.AttemptMemo.Remember(root, nameof(IMauiElement.AutomationId), () => root.AutomationId)
               == _foundAutomationId;

    /// <summary>
    /// Gets a typed child <see cref="ActivityIndicator{TSelf}"/> scoped to this component.
    /// </summary>
    public ActivityIndicator<TSelf> ActivityIndicator(string automationId) => new(Self, automationId);
}
