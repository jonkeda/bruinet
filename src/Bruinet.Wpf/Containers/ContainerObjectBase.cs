using Bruinet.Core.Calls;
using Bruinet.Core.Scopes;

namespace Bruinet.Wpf.Containers;

/// <summary>
/// A scope inside another scope, found by its own root element: a group box, a panel, a dialog, a
/// tab's content. Its readiness asks the parent first, then checks its root, then its own content.
/// </summary>
/// <typeparam name="TParent">The scope it is declared in.</typeparam>
/// <typeparam name="TScope">The container's own type.</typeparam>
public abstract class ContainerObjectBase<TParent, TScope> : ObjectBase, IWpfContainer<TParent, TScope>
    where TParent : IWpfScope<TParent>
    where TScope : IWpfContainer<TParent, TScope>
{
    private readonly IWpfScope<TParent> _parentScope;
    private readonly Locator _locator;

    protected ContainerObjectBase(IWpfScope<TParent> parentScope, Locator locator)
    {
        _parentScope = parentScope ?? throw new ArgumentNullException(nameof(parentScope));
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
    }

    protected ContainerObjectBase(IWpfScope<TParent> parentScope, string automationId)
        : this(parentScope, Locator.ByAutomationId(automationId))
    {
    }

    /// <inheritdoc />
    public override IWpfTestContext Context => _parentScope.Context;

    /// <inheritdoc />
    public TParent Parent => _parentScope.Self;

    /// <inheritdoc />
    public abstract TScope Self { get; }

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => _parentScope.DefaultLocatorStrategy;

    /// <inheritdoc />
    public IWpfPage? Page => _parentScope.Page;

    /// <summary>The container's locator in its parent.</summary>
    protected Locator Locator => _locator;

    /// <inheritdoc />
    protected override string ScopeName => $"{GetType().Name}('{_locator}')";

    /// <summary>
    /// Whether the parent's readiness is on this container's path. False for a scope shown over
    /// its parent (a dialog over a busy page), which must be usable while the parent is not.
    /// </summary>
    protected virtual bool AsksParent => true;

    /// <summary>
    /// The container's own content check, once its root is there: a spinner gone, rows loaded.
    /// Ready by default.
    /// </summary>
    protected virtual ScopeReadiness ProbeContentReadiness(IWpfElement root) => ContentReady();

    /// <summary>A ready answer from this container.</summary>
    protected ScopeReadiness ContentReady() => ScopeReadiness.Ready(ScopeName);

    /// <summary>A "content not ready" answer from this container.</summary>
    protected ScopeReadiness ContentNotReady(string? detail = null)
        => new(ScopeName, ScopeReadinessState.ContentNotReady, detail);

    /// <summary>The root element, found in one attempt, or null. Override to find it another way.</summary>
    protected virtual IWpfElement? TryFindRootElement() => _parentScope.TryFindElement(_locator);

    /// <inheritdoc />
    public ScopeReadiness ProbeReadiness()
    {
        if (AsksParent && _parentScope.ProbeReadiness() is { IsReady: false } parent)
        {
            return parent;
        }

        return TryFindRootElement() is { } root
            ? ProbeContentReadiness(root)
            : new ScopeReadiness(ScopeName, ScopeReadinessState.MissingRoot);
    }

    /// <inheritdoc />
    public bool IsReady() => ProbeReadiness().IsReady;

    /// <inheritdoc />
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => RunFirst(timeoutMs, outcomes);

    /// <inheritdoc />
    public bool WaitReady(int? timeoutMs = null)
        => RunProbe(() => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(), timeoutMs ?? DefaultTimeoutMs);

    #region State

    /// <summary>Whether the container's root element exists, now. <see cref="WaitExists"/> waits.</summary>
    public bool Exists() => TryFindRootElement() != null;

    /// <summary>Whether the container's root element is shown, now; null when it does not exist.</summary>
    public bool? IsVisible() => TryFindRootElement()?.Visible;

    /// <summary>Waits until the container's existence matches <paramref name="expected"/>; null skips.</summary>
    public bool WaitExists(bool? expected = true, int? timeoutMs = null)
        => WaitRoot(expected, root => root != null, timeoutMs, nameof(WaitExists));

    /// <summary>Waits until the container's visibility matches <paramref name="expected"/>; null skips.</summary>
    public bool WaitVisible(bool? expected = true, int? timeoutMs = null)
        => WaitRoot(expected, root => root?.Visible == true, timeoutMs, nameof(WaitVisible));

    /// <summary>Asserts the container's existence, returning the container so a chain stays inside it.</summary>
    public TScope AssertExists(bool? expected = true, string? message = null, int? timeoutMs = null)
        => WaitRoot(expected, root => root != null, timeoutMs, nameof(AssertExists))
            ? Self
            : throw new ControlAssertionException(message
                ?? $"Expected container {(expected == true ? "to exist" : "not to exist")}. Locator: {_locator}");

    /// <summary>Asserts the container's visibility, returning the container so a chain stays inside it.</summary>
    public TScope AssertVisible(bool? expected = true, string? message = null, int? timeoutMs = null)
        => WaitRoot(expected, root => root?.Visible == true, timeoutMs, nameof(AssertVisible))
            ? Self
            : throw new ControlAssertionException(message ?? $"Expected container visibility '{expected}'. Locator: {_locator}");

    // One call (R1): each attempt asks the parent first (R4), then reads the root once.
    private bool WaitRoot(bool? expected, Func<IWpfElement?, bool> state, int? timeoutMs, string caller)
    {
        if (expected is not { } wanted)
        {
            return true;
        }

        return RunProbe(
            () => (AsksParent ? ScopeGate.Check(_parentScope.ProbeReadiness()) : null)
                ?? (state(TryFindRootElement()) == wanted ? Observation.Done() : Observation.Pending()),
            timeoutMs ?? DefaultTimeoutMs,
            caller: caller);
    }

    #endregion

    /// <inheritdoc />
    public IWpfElement? TryFindElement(Locator locator) => TryFindRootElement()?.TryFindElement(locator);

    /// <inheritdoc />
    public IWpfElement FindElement(Locator locator) => TryFindElement(locator) ?? throw new ElementNotFoundException(locator);

    /// <inheritdoc />
    public IReadOnlyList<IWpfElement> FindElements(Locator locator) => TryFindRootElement()?.FindElements(locator) ?? [];
}
