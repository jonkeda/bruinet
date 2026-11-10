using Bruinet.Core.Calls;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;

namespace Bruinet.Core.Scopes;

/// <summary>
/// A scope inside another scope, found by its own root element: a group box, a panel, a dialog, a
/// tab's content (public API review, proposal 5). Its readiness asks the parent first, then checks
/// its root, then its own content. Each stack's container base derives from this with its types and
/// says whether a root is shown and how to search inside it.
/// </summary>
/// <typeparam name="TContext">The stack's test context.</typeparam>
/// <typeparam name="TElement">The stack's element.</typeparam>
/// <typeparam name="TParentScope">The stack's scope the container is declared in.</typeparam>
/// <typeparam name="TScope">The container's own type.</typeparam>
public abstract class ContainerObjectBase<TContext, TElement, TParentScope, TScope> : ObjectBase<TContext>
    where TContext : ITestContext, IScope
    where TElement : class
    where TParentScope : IElementScope<TElement>
{
    /// <summary>The container found by <paramref name="locator"/> in <paramref name="parentScope"/>.</summary>
    protected ContainerObjectBase(TParentScope parentScope, Locator locator)
    {
        ParentScope = parentScope ?? throw new ArgumentNullException(nameof(parentScope));
        Locator = locator ?? throw new ArgumentNullException(nameof(locator));
    }

    /// <summary>The scope the container is declared in.</summary>
    protected TParentScope ParentScope { get; }

    /// <summary>The container's locator in its parent.</summary>
    protected Locator Locator { get; }

    /// <inheritdoc cref="IScope" />
    public abstract TScope Self { get; }

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => ParentScope.DefaultLocatorStrategy;

    /// <inheritdoc />
    protected override string ScopeName => $"{GetType().Name}('{Locator}')";

    /// <summary>
    /// Whether the parent's readiness is on this container's path. False for a scope shown over
    /// its parent (a dialog over a busy page), which must be usable while the parent is not.
    /// </summary>
    protected virtual bool AsksParent => true;

    /// <summary>
    /// The container's own content check, once its root is there: a spinner gone, rows loaded.
    /// Ready by default.
    /// </summary>
    protected virtual ScopeReadiness ProbeContentReadiness(TElement root) => ContentReady();

    /// <summary>A ready answer from this container.</summary>
    protected ScopeReadiness ContentReady() => ScopeReadiness.Ready(ScopeName);

    /// <summary>A "content not ready" answer from this container.</summary>
    protected ScopeReadiness ContentNotReady(string? detail = null)
        => new(ScopeName, ScopeReadinessState.ContentNotReady, detail);

    /// <summary>The root element, found in one attempt, or null. Override to find it another way.</summary>
    protected virtual TElement? TryFindRootElement() => ParentScope.TryFindElement(Locator);

    /// <inheritdoc />
    public ScopeReadiness ProbeReadiness()
    {
        if (AsksParent && ParentScope.ProbeReadiness() is { IsReady: false } parent)
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
    public bool? IsVisible() => TryFindRootElement() is { } root ? IsShown(root) : null;

    /// <summary>Waits until the container's existence matches <paramref name="expected"/>; null skips.</summary>
    public bool WaitExists(bool? expected = true, int? timeoutMs = null)
        => WaitRoot(expected, root => root != null, timeoutMs, nameof(WaitExists));

    /// <summary>Waits until the container's visibility matches <paramref name="expected"/>; null skips.</summary>
    public bool WaitVisible(bool? expected = true, int? timeoutMs = null)
        => WaitRoot(expected, root => root != null && IsShown(root), timeoutMs, nameof(WaitVisible));

    /// <summary>Asserts the container's existence, returning the container so a chain stays inside it.</summary>
    public TScope AssertExists(bool? expected = true, string? message = null, int? timeoutMs = null)
        => WaitRoot(expected, root => root != null, timeoutMs, nameof(AssertExists))
            ? Self
            : throw new ControlAssertionException(message
                ?? $"Expected container {(expected == true ? "to exist" : "not to exist")}. Locator: {Locator}");

    /// <summary>Asserts the container's visibility, returning the container so a chain stays inside it.</summary>
    public TScope AssertVisible(bool? expected = true, string? message = null, int? timeoutMs = null)
        => WaitRoot(expected, root => root != null && IsShown(root), timeoutMs, nameof(AssertVisible))
            ? Self
            : throw new ControlAssertionException(message ?? $"Expected container visibility '{expected}'. Locator: {Locator}");

    // One call (R1): each attempt asks the parent first (R4), then reads the root once.
    private bool WaitRoot(bool? expected, Func<TElement?, bool> state, int? timeoutMs, string caller)
    {
        if (expected is not { } wanted)
        {
            return true;
        }

        return RunProbe(
            () => (AsksParent ? ScopeGate.Check(ParentScope.ProbeReadiness()) : null)
                ?? (state(TryFindRootElement()) == wanted ? Observation.Done() : Observation.Pending()),
            timeoutMs ?? DefaultTimeoutMs,
            caller: caller);
    }

    #endregion

    /// <inheritdoc />
    public TElement? TryFindElement(Locator locator) => TryFindRootElement() is { } root ? TryFindIn(root, locator) : null;

    /// <inheritdoc />
    public TElement FindElement(Locator locator) => TryFindElement(locator) ?? throw new ElementNotFoundException(locator);

    /// <inheritdoc />
    public IReadOnlyList<TElement> FindElements(Locator locator) => TryFindRootElement() is { } root ? FindAllIn(root, locator) : [];

    /// <summary>Whether <paramref name="root"/> is shown.</summary>
    protected abstract bool IsShown(TElement root);

    /// <summary>The element <paramref name="locator"/> finds inside <paramref name="root"/>, or null.</summary>
    protected abstract TElement? TryFindIn(TElement root, Locator locator);

    /// <summary>Every element <paramref name="locator"/> finds inside <paramref name="root"/>.</summary>
    protected abstract IReadOnlyList<TElement> FindAllIn(TElement root, Locator locator);
}
