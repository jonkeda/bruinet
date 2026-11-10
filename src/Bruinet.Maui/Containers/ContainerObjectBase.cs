using Bruinet.Core.Configuration;
using System.Runtime.CompilerServices;
using Bruinet.Core.Calls;
using Bruinet.Core.Utilities;

namespace Bruinet.Maui.Containers;

/// <summary>
/// Shared behavior for scopes rooted at an element rather than at the driver.
/// </summary>
/// <remarks>
/// <para>
/// Pages and child containers both inherit this behavior. Parent navigation belongs only
/// to <see cref="ContainerObjectBase{TParent, TSelf}"/>.
/// </para>
/// <para>
/// By default, searches are scoped strictly to <see cref="ContainerRoot"/>: when a child is not
/// found within the container, the search does <b>not</b> fall back to the parent scope.
/// Container scoping means elements must be within the container. A component whose platform
/// deliberately flattens its children may override the element-finding members explicitly.
/// </para>
/// </remarks>
/// <typeparam name="TSelf">The container type itself (self-referencing for fluent returns).</typeparam>
/// <typeparam name="TSetResult">The scope returned by generated set operations.</typeparam>
public abstract class RootedScopeBase<TSelf, TSetResult>
    : ObjectBase, IMauiScope<TSelf>, IControlHost<IMauiElement>, IRootCache, IAbsentScope
    where TSelf : RootedScopeBase<TSelf, TSetResult>
{
    bool IAbsentScope.IsAbsent() => IsAbsentScope();

    /// <summary>Whether this scope's root is absent from a parent chain that is there; never for a page.</summary>
    private protected virtual bool IsAbsentScope() => false;

    private IMauiElement? _cachedRoot;
    private bool _rootCacheValid;

    /// <summary>
    /// The locator that finds this scope's root element.
    /// </summary>
    protected abstract Locator Locator { get; }

    /// <inheritdoc />
    public TSelf Self => (TSelf)this;

    /// <inheritdoc />
    public abstract IMauiPage? Page { get; }

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => LocatorStrategy.AutomationId;

    /// <summary>
    /// Whether this container keeps its resolved root between operations.
    /// </summary>
    protected virtual bool CacheContainerRoot => true;

    /// <summary>Checks whether a cached root still represents this scope.</summary>
    /// <remarks>
    /// The alive rule (R6), asked once per attempt: the scope chain re-checks a root as each child
    /// resolves, and on Android each check is a round trip (AttemptMemo).
    /// </remarks>
    protected virtual bool IsCachedRootValid(IMauiElement root)
    {
        // A bounds read of this root in this attempt already proved it alive (design-fewer-reads, D4).
        if (!Bruinet.Core.Calls.AttemptMemo.Knows(root, nameof(MauiElementExtensions.HasUsableBounds)))
        {
            _ = Bruinet.Core.Calls.AttemptMemo.Remember(root, nameof(IMauiElement.InstanceKey), () => root.InstanceKey);
        }

        return true;
    }

    #region Container root

    /// <summary>The container's root element, found on first use and kept until <see cref="InvalidateCache"/>.</summary>
    public IMauiElement ContainerRoot
    {
        get
        {
            if (!CacheContainerRoot)
                return FindContainerRootElement();

            if (_rootCacheValid && _cachedRoot != null)
            {
                try
                {
                    if (IsCachedRootValid(_cachedRoot))
                    {
                        return _cachedRoot;
                    }

                    InvalidateCache();
                }
                catch (StaleElementException)
                {
                    InvalidateCache();
                }
            }

            _cachedRoot = FindContainerRootElement();
            _rootCacheValid = true;
            return _cachedRoot;
        }
    }

    /// <summary>
    /// Finds the container's root element. Override to search elsewhere, for example a
    /// popup window for a dialog that lives outside the normal scope chain.
    /// </summary>
    /// <exception cref="ElementNotFoundException">Thrown when the root is not found.</exception>
    protected abstract IMauiElement FindContainerRootElement();

    /// <summary>Forgets the root element, so the next use finds it again.</summary>
    public void InvalidateCache()
    {
        _rootCacheValid = false;
        _cachedRoot = null;
    }

    /// <summary>
    /// Gets this container's own element, or null when it is absent.
    /// </summary>
    protected IMauiElement? TryFindElement() => TryGetContainerRoot();

    /// <summary>
    /// Gets the container root without throwing when it is absent.
    /// </summary>
    protected IMauiElement? TryGetContainerRoot()
    {
        try
        {
            return ContainerRoot;
        }
        catch (ElementNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Runs <paramref name="read"/> against the root. When the root turns out to be gone, forgets
    /// it, finds it again once, and runs <paramref name="read"/> on the new one.
    /// </summary>
    /// <param name="read">What to do with the root.</param>
    /// <param name="whenAbsent">The answer when there is no root.</param>
    /// <returns>What <paramref name="read"/> returned, or <paramref name="whenAbsent"/>.</returns>
    protected T WithRoot<T>(Func<IMauiElement, T> read, T whenAbsent)
    {
        var root = TryGetContainerRoot();
        if (root == null)
            return whenAbsent;

        try
        {
            return read(root);
        }
        catch (StaleElementException)
        {
            InvalidateCache();

            root = TryGetContainerRoot();
            return root == null ? whenAbsent : read(root);
        }
    }

    #endregion

    #region Typed children (scoped to the container root)

    /// <summary>
    /// A child control of this container, as a control object rather than a raw element.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The typed counterpart to <see cref="FindElement"/>, which returns a raw
    /// <c>IMauiElement</c>.
    /// </para>
    /// <para>
    /// Prefer declaring a named property on the container - <c>public Label&lt;ProductRow&gt; Name
    /// =&gt; new(this, "ProductNameLabel");</c>. Use this for children not worth naming.
    /// </para>
    /// </remarks>
    /// <typeparam name="TControl">The control type to resolve the child as.</typeparam>
    /// <param name="automationId">The child's automation id.</param>
    public TControl Child<TControl>(string automationId)
        where TControl : Controls.Base.ViewBase<TSelf>
    {
        ArgumentException.ThrowIfNullOrEmpty(automationId);

        return (TControl)Activator.CreateInstance(
            typeof(TControl),
            (IMauiScope<TSelf>)(TSelf)this,
            automationId)!;
    }

    /// <summary>A child <see cref="Controls.Display.Label{TScope}"/> of this container.</summary>
    public Controls.Display.Label<TSelf> Label(string automationId)
        => new((TSelf)this, automationId);

    /// <summary>A child <see cref="Controls.Buttons.Button{TScope}"/> of this container.</summary>
    public Controls.Buttons.Button<TSelf> Button(string automationId)
        => new((TSelf)this, automationId);

    /// <summary>A child <see cref="Controls.Text.Entry{TScope}"/> of this container.</summary>
    public Controls.Text.Entry<TSelf> Entry(string automationId)
        => new((TSelf)this, automationId);

    /// <summary>A child <see cref="Controls.Toggle.CheckBox{TScope}"/> of this container.</summary>
    public Controls.Toggle.CheckBox<TSelf> CheckBox(string automationId)
        => new((TSelf)this, automationId);

    #endregion

    #region Element finding (scoped to the container root)

    /// <inheritdoc />
    /// <remarks>
    /// None for a page or a plain scope: the driver picks the scrolling element on screen.
    /// A container passes its parent's answer on, and a container that scrolls itself answers
    /// with its own root.
    /// </remarks>
    public virtual IMauiElement? ScrollingRoot => null;

    /// <inheritdoc />
    public virtual bool AllowsScrollLookup => true;

    /// <inheritdoc />
    /// <remarks>
    /// One attempt, and no readiness check: a lookup answers what is there now. A call's attempt
    /// asks the scope chain first (<see cref="ProbeReadiness"/>), so a scope that is not ready is
    /// reported as that, not as a missing child.
    /// </remarks>
    public virtual IMauiElement? TryFindElement(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);

        // Not found within the container is null. Do NOT fall back to the parent scope -
        // container scoping means elements must be within the container.
        return WithRoot(root => root.TryFindElement(locator), null);
    }

    /// <inheritdoc />
    public virtual IMauiElement FindElement(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);

        return TryFindElement(locator) ?? throw DescribeMiss(locator);
    }

    /// <inheritdoc />
    public virtual ElementNotFoundException DescribeMiss(Locator locator)
        => new($"Element not found within {ScopeName}. Child locator: {locator}");

    /// <inheritdoc />
    public virtual IReadOnlyList<IMauiElement> FindElements(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);

        return WithRoot(root => root.FindElements(locator), []);
    }

    #endregion

    #region Readiness

    /// <summary>The scope's name in readiness answers and messages.</summary>
    protected virtual string ScopeName => $"{GetType().Name.Split('`')[0]} '{Locator}'";

    /// <inheritdoc />
    /// <remarks>
    /// The parent first (<see cref="ProbeParentReadiness"/>), returned unchanged when it is not
    /// ready; then this scope's root; then <see cref="ProbeContentReadiness"/> on it. A root that
    /// turns out to be gone is found once more; gone again, the answer is
    /// <see cref="ScopeReadinessState.StaleRoot"/>.
    /// </remarks>
    public virtual ScopeReadiness ProbeReadiness()
    {
        var parent = ProbeParentReadiness();
        return parent.IsReady ? ProbeOwnReadiness(rootReacquired: false) : parent;
    }

    /// <inheritdoc cref="IScope.IsReady"/>
    public bool IsReady() => ProbeReadiness().IsReady;

    /// <summary>
    /// The readiness this scope inherits: its parent's when it asks the parent, otherwise ready.
    /// </summary>
    protected virtual ScopeReadiness ProbeParentReadiness() => ScopeReadiness.Ready(ScopeName);

    /// <summary>
    /// What an attempt of this scope's own members checks first: the parent chain for a
    /// container (its own root is what the attempt then looks for), the whole probe for a page.
    /// </summary>
    protected virtual ScopeReadiness ProbeCallReadiness() => ProbeParentReadiness();

    /// <summary>
    /// This scope's own readiness beyond "the root is there": one attempt, no waiting.
    /// </summary>
    /// <param name="root">The scope's root, just found or checked.</param>
    protected virtual ScopeReadiness ProbeContentReadiness(IMauiElement root) => ContentReady();

    /// <summary>The answer of a scope that is ready.</summary>
    protected ScopeReadiness ContentReady() => ScopeReadiness.Ready(ScopeName);

    /// <summary>The answer of a scope whose content is not ready yet.</summary>
    /// <param name="detail">What it is waiting for, for the message.</param>
    protected ScopeReadiness ContentNotReady(string? detail = null)
        => new(ScopeName, ScopeReadinessState.ContentNotReady, detail);

    private ScopeReadiness ProbeOwnReadiness(bool rootReacquired)
    {
        try
        {
            var root = TryGetContainerRoot();
            if (root == null)
            {
                return new ScopeReadiness(
                    ScopeName,
                    rootReacquired ? ScopeReadinessState.StaleRoot : ScopeReadinessState.MissingRoot,
                    RootReacquired: rootReacquired);
            }

            var answer = ProbeContentReadiness(root);
            return rootReacquired ? answer with { RootReacquired = true } : answer;
        }
        catch (StaleElementException)
        {
            if (rootReacquired)
            {
                return new ScopeReadiness(ScopeName, ScopeReadinessState.StaleRoot, RootReacquired: true);
            }

            InvalidateCache();
            return ProbeOwnReadiness(rootReacquired: true);
        }
    }

    /// <summary>The budget <see cref="WaitReady"/> uses when the caller gives none.</summary>
    protected virtual int DefaultReadyTimeoutMs => DefaultTimeoutMs;

    /// <inheritdoc />
    /// <remarks>One call: polls <see cref="ProbeReadiness"/>. A misconfigured scope fails at once.</remarks>
    public virtual bool WaitReady(int? timeoutMs = null)
        => RunProbe(() => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(),
            timeoutMs ?? DefaultReadyTimeoutMs);

    /// <inheritdoc />
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes)
        => FirstOutcome.Wait(Call, Context.ProbeReadiness, outcomes, Budget(timeoutMs), AnimationMs, PollingIntervalMs);

    /// <summary>
    /// A member of the scope itself as one call: polls <paramref name="probe"/> with no readiness
    /// step in front, because the probe is about the scope's own state.
    /// </summary>
    /// <param name="probe">One attempt.</param>
    /// <param name="budgetMs">The call's budget.</param>
    /// <param name="onTimeout">
    /// The failure to throw when the budget runs out, given the last readiness; null to answer
    /// false instead.
    /// </param>
    /// <param name="caller">The public member.</param>
    /// <returns>True when the probe reported done within the budget.</returns>
    private protected bool RunProbe(Func<Observation> probe, int budgetMs,
        Func<ScopeReadiness, Exception>? onTimeout = null,
        [CallerMemberName] string? caller = null)
        => Call.Run(caller ?? nameof(RunProbe), null, budgetMs, AnimationMs, context =>
        {
            if (Poll(context, _ => probe()))
            {
                return true;
            }

            if (context.Log.Last is { Kind: ObservationKind.Failed, Error: { } error })
            {
                var failure = context.Log.Unexpected(error, ScopeName, budgetMs);
                if (ReferenceEquals(failure, error))
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
                }

                throw failure;
            }

            return onTimeout == null ? false : throw onTimeout(ProbeReadiness());
        }, succeeded: met => met);

    #endregion

    #region State

    /// <summary>
    /// Whether the container's root element exists, now. <see cref="WaitExists"/> waits.
    /// </summary>
    public bool IsExists() => TryGetContainerRoot() != null;

    /// <summary>
    /// Whether the container's root element is visible, now; null when it does not exist.
    /// <see cref="WaitVisible"/> waits.
    /// </summary>
    public bool? IsVisible() => TryGetContainerRoot()?.Visible;

    /// <summary>
    /// Waits until the container's existence matches <paramref name="expected"/>.
    /// A null expectation is a skip and returns true.
    /// </summary>
    /// <remarks>One call; each attempt checks the parent chain first.</remarks>
    public bool WaitExists(bool? expected, int? timeoutMs = null)
        => RunWaitWithOptionalElement(expected, root => (root != null) == expected!.Value, timeoutMs);

    /// <summary>
    /// Waits until the container's visibility matches <paramref name="expected"/>.
    /// A null expectation is a skip and returns true.
    /// </summary>
    /// <remarks>One call; each attempt checks the parent chain first.</remarks>
    public bool WaitVisible(bool? expected, int? timeoutMs = null)
        => RunWaitWithOptionalElement(expected, root => root?.Visible == expected!.Value, timeoutMs);

    /// <summary>
    /// Asserts the container's existence, returning the container so a chain stays inside it.
    /// </summary>
    public TSelf AssertExists(bool? expected = true, string? message = null, int? timeoutMs = null)
        => RunAssertWithOptionalElement(
            expected,
            root => (bool?)(root != null),
            (actual, wanted) => actual == wanted,
            message ?? $"Expected container {(expected == true ? "to exist" : "not to exist")}. Locator: {Locator}",
            timeoutMs);

    /// <summary>
    /// Asserts the container's visibility, returning the container so a chain stays inside it.
    /// </summary>
    public TSelf AssertVisible(bool? expected = true, string? message = null, int? timeoutMs = null)
        => RunAssertWithOptionalElement(
            expected,
            root => root?.Visible,
            (actual, wanted) => actual == wanted,
            message ?? $"Expected container visibility '{expected}'. Locator: {Locator}",
            timeoutMs);

    #endregion

    #region Run helpers

    // Mirrors the helper surface on the control base, but returning TSelf instead of the
    // parent scope. Identical names and shapes let the generator emit one body for both
    // hierarchies. The element these operate on is the container root, never checked for
    // visibility.
    //
    // Every public member is one call (call-model contract, R1), run by Core's ControlCalls as a
    // control's are; this scope is its host. The collection's own polls (an item, a scroll) use
    // Call, Poll, RootAttempt and Failure below.

    private ControlCalls<IMauiElement>? _calls;

    private ControlCalls<IMauiElement> Calls => _calls ??= new(this);

    private protected ControlCall Call => new(Context.Logger, Context.Name, Context.NearMiss, PageName, ControlId);

    private protected int Budget(int? timeoutMs) => timeoutMs ?? DefaultTimeoutMs;

    private protected int AnimationMs => Context.Timeouts.Animation.ToMs();

    /// <summary>
    /// What is left of the running call's budget, for a wait below the call (a scroll); the
    /// default wait outside a call. Nothing inside a call starts a budget of its own (R2, R3).
    /// </summary>
    protected int CallRemainingMs => CallBudget.RemainingOr(DefaultTimeoutMs);

    Locator IControlHost<IMauiElement>.Locator => Locator;

    ControlCall IControlHost<IMauiElement>.NewCall() => Call;

    int IControlHost<IMauiElement>.DefaultTimeoutMs => DefaultTimeoutMs;

    int IControlHost<IMauiElement>.PollingIntervalMs => PollingIntervalMs;

    int IControlHost<IMauiElement>.AnimationMs => AnimationMs;

    ScopeReadiness IControlHost<IMauiElement>.ProbeScope() => ProbeCallReadiness();

    IMauiElement? IControlHost<IMauiElement>.Locate(AttemptContext attempt) => TryGetContainerRoot();

    IMauiElement? IControlHost<IMauiElement>.TryFind() => TryGetContainerRoot();

    bool? IControlHost<IMauiElement>.IsVisible(IMauiElement element) => element.Visible;

    void IControlHost<IMauiElement>.ScrollIntoView(IMauiElement element, int timeoutMs) => element.ScrollIntoView(timeoutMs);

    string IControlHost<IMauiElement>.ScrollKey(IMauiElement element) => Locator.ToString();

    string? IControlHost<IMauiElement>.InstanceKey(IMauiElement element) => element.InstanceKey;

    bool IControlHost<IMauiElement>.ChecksVisibilityBeforeAction(IMauiElement element) => false;

    void IControlHost<IMauiElement>.EnsureReadyForAction(IMauiElement element) => EnsureReadyForActionCore(element);

    ElementNotFoundException IControlHost<IMauiElement>.NotFound() => RootNotFound();

    string IControlHost<IMauiElement>.Noun => "container";

    bool IControlHost<IMauiElement>.ChecksVisibility => false;

    void IControlHost<IMauiElement>.OnStale() => InvalidateCache();

    /// <summary>The failure of a phase that ran out of budget, from what it last saw.</summary>
    private protected Exception Failure(AttemptContext attempt, string caller, int budgetMs)
        => Calls.Failure(attempt, caller, budgetMs);

    /// <summary>
    /// The error for this scope's root not found, in the words of whatever it is looked up in.
    /// Builds the message only: it does not look again.
    /// </summary>
    protected virtual ElementNotFoundException RootNotFound() => new(Locator);

    /// <summary>One attempt against the root: the scope chain, then the root, then <paramref name="body"/>.</summary>
    private protected Observation RootAttempt(Func<IMauiElement, Observation> body)
        => Calls.Attempt(AttemptContext.Current ?? throw new InvalidOperationException("A root attempt runs inside a call."),
            ensureVisible: false, body);

    /// <summary>A poll of <paramref name="attempt"/> as one call; false when it never reported Done.</summary>
    private protected bool Poll(AttemptContext context, Func<AttemptContext, Observation> attempt,
        Func<Observation, bool>? stop = null)
        => Poller.Until(attempt, context, PollingIntervalMs, stop);

    /// <summary>
    /// Waits, inside a Core method, for the effect of an action the method has already done on the
    /// root.
    /// </summary>
    /// <param name="read">Reads the value.</param>
    /// <param name="done">Whether the value is the one waited for.</param>
    /// <param name="timeoutMs">Maximum time to wait; null for the default.</param>
    /// <returns>How the wait ended, with the last value and error.</returns>
    protected Confirmation<T> Confirm<T>(Func<T?> read, Func<T?, bool> done, int? timeoutMs)
        => Calls.Confirm(read, done, timeoutMs);

    /// <summary>Polls an arbitrary condition.</summary>
    protected bool RunWait(Func<bool> operation, int? timeoutMs = null,
        [CallerMemberName] string? caller = null)
        => Calls.Wait(operation, timeoutMs, caller ?? nameof(RunWait));

    /// <summary>Polls a condition evaluated against the container root.</summary>
    protected bool RunWaitWithElement<T>(T? expected, Func<IMauiElement, bool> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => expected == null || Calls.WaitWithElement(coreOperation, timeoutMs, caller ?? nameof(RunWaitWithElement));

    /// <summary>Runs an action once, as one call, after the scope chain is ready.</summary>
    protected TSelf RunDo(Action operation, int? timeoutMs = null,
        [CallerMemberName] string? caller = null)
    {
        Calls.Run(caller ?? nameof(RunDo), null, () =>
        {
            operation();
            return true;
        }, timeoutMs);
        return Self;
    }

    /// <summary>Runs an action against the container root, once, as one call.</summary>
    protected TSelf RunDoWithElement(Action<IMauiElement> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
    {
        Calls.Act(coreOperation, ensureVisible: false, timeoutMs, caller ?? nameof(RunDoWithElement));
        return Self;
    }

    /// <summary>Reads a value from the container root.</summary>
    protected T? RunGetWithElement<T>(Func<IMauiElement, T> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => Calls.GetWithElement(coreOperation, timeoutMs, caller ?? nameof(RunGetWithElement), ensureVisible: false);

    /// <summary>
    /// Reads a value that is meaningful when the container root is absent: once, with the root
    /// resolved optionally.
    /// </summary>
    protected T? RunGetWithOptionalElement<T>(Func<IMauiElement?, T> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => Calls.GetWithOptionalElement(coreOperation, timeoutMs, caller ?? nameof(RunGetWithOptionalElement));

    /// <summary>Sets a value on the container, returning the parent scope.</summary>
    protected TSetResult RunSetWithElement<T>(T? value, Action<IMauiElement> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
    {
        if (value != null)
        {
            Calls.Act(coreOperation, ensureVisible: false, timeoutMs, caller ?? nameof(RunSetWithElement), value.ToString());
        }

        return SetResult;
    }

    /// <summary>The scope returned by a generated set operation.</summary>
    protected abstract TSetResult SetResult { get; }

    /// <summary>
    /// Polls a predicate that is meaningful when the container root is absent.
    /// </summary>
    protected bool RunWaitWithOptionalElement<T>(T? expected,
        Func<IMauiElement?, bool> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => expected == null || Calls.WaitWithOptionalElement(coreOperation, timeoutMs, caller ?? nameof(RunWaitWithOptionalElement));

    /// <summary>
    /// Asserts a value that is meaningful when the container root is absent.
    /// </summary>
    protected TSelf RunAssertWithOptionalElement<T>(T? expected,
        Func<IMauiElement?, T?> getActual, Func<T?, T?, bool> compare,
        string? message = null, int? timeoutMs = null,
        [CallerMemberName] string? caller = null)
    {
        if (expected != null)
        {
            Calls.AssertWithOptionalElement(expected, getActual, compare, message, timeoutMs,
                caller ?? nameof(RunAssertWithOptionalElement));
        }

        return Self;
    }

    /// <summary>Asserts a value, returning the container for chaining.</summary>
    protected TSelf RunAssert<T>(T? expected, Func<T?> getActual,
        Func<T?, T?, bool> compare, string? message = null,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
    {
        if (expected != null)
        {
            Calls.Assert(expected, getActual, compare, message, timeoutMs, caller ?? nameof(RunAssert));
        }

        return Self;
    }

    /// <summary>Asserts a value read from the container root.</summary>
    protected TSelf RunAssertWithElement<T>(T? expected, Func<IMauiElement, T?> getActual,
        Func<T?, T?, bool> compare, string? message = null,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
    {
        if (expected != null)
        {
            Calls.AssertWithElement(expected, getActual, compare, message, timeoutMs, caller ?? nameof(RunAssertWithElement));
        }

        return Self;
    }

    /// <summary>
    /// What an action or a set on this scope requires of its root beyond being there: one check,
    /// inside the call's poll, throwing <see cref="ElementNotReadyException"/> when not met.
    /// </summary>
    /// <param name="root">The root, just found.</param>
    protected virtual void EnsureReadyForActionCore(IMauiElement root)
    {
    }

    #endregion

    #region Logging identity

    private string PageName => Page?.GetType().Name ?? "Unknown";
    private string ControlId => Locator.Value;

    #endregion
}

/// <summary>
/// A rooted scope with an explicit parent. Pages inherit <see cref="RootedScopeBase{TSelf, TSetResult}"/>
/// directly because a root page has no parent relationship.
/// </summary>
public abstract class ContainerObjectBase<TParent, TSelf>
    : RootedScopeBase<TSelf, TParent>, IMauiContainerObject<TParent, TSelf>
    where TParent : IMauiScope<TParent>
    where TSelf : ContainerObjectBase<TParent, TSelf>
{
    private readonly IMauiScope<TParent> _parentScope;
    private readonly Locator _locator;

    /// <summary>Creates a container within the given parent scope.</summary>
    protected ContainerObjectBase(IMauiScope<TParent> parentScope, Locator locator)
    {
        _parentScope = parentScope ?? throw new ArgumentNullException(nameof(parentScope));
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
    }

    /// <summary>Creates a container using the parent scope's default locator strategy.</summary>
    protected ContainerObjectBase(IMauiScope<TParent> parentScope, string locatorValue)
        : this(parentScope,
               new Locator(
                   parentScope?.DefaultLocatorStrategy ?? LocatorStrategy.AutomationId,
                   locatorValue ?? throw new ArgumentNullException(nameof(locatorValue))))
    {
        if (locatorValue.Length == 0)
            throw new ArgumentException("Locator value must not be empty.", nameof(locatorValue));
    }

    protected override Locator Locator => _locator;

    /// <inheritdoc />
    public TParent Parent => _parentScope.Self;

    /// <inheritdoc />
    public override IMauiTestContext Context => _parentScope.Context;

    /// <inheritdoc />
    public override IMauiPage? Page => _parentScope.Page;

    /// <inheritdoc />
    /// <remarks>
    /// Whatever scrolls for the scope this container sits in: a <c>Border</c> inside a
    /// <c>ScrollView</c> is scrolled by the <c>ScrollView</c>.
    /// </remarks>
    public override IMauiElement? ScrollingRoot => _parentScope.ScrollingRoot;

    /// <inheritdoc />
    /// <remarks>
    /// A container inside a row is confined as the row is. A container shown whole holds all its
    /// children in the tree, so a child it misses is not below the fold: no sweep, which would
    /// also leave the page scrolled away from it.
    /// </remarks>
    public override bool AllowsScrollLookup
        => _parentScope.AllowsScrollLookup
           && !(OnATouchPlatform && TryGetContainerRoot() is { } root && Context.Driver!.IsWhollyInView(root));

    /// <remarks>
    /// On Android and iOS a container below the fold is not in the tree at all - UiAutomator2 leaves
    /// out what the user cannot see - so a miss there scrolls to it, as a control's lookup does. The
    /// DockLayout at the foot of the CommunityToolkit page was "not found" otherwise. Windows keeps an
    /// off-screen element in the tree, so a miss there is an answer and costs no sweep.
    /// </remarks>
    protected override IMauiElement FindContainerRootElement()
        => _parentScope.TryFindElement(Locator)
           ?? (OnATouchPlatform
               && _parentScope.AllowsScrollLookup
               && (_parentScope.ScrollingRoot ?? Context.AppElement).TryFindByScrolling(Locator) is not null
               // The sweep brings it into view; the parent's own lookup says whether it is the
               // parent's (the scroller's search is page-wide).
               ? _parentScope.TryFindElement(Locator)
               : null)
           ?? throw RootNotFound();

    private bool OnATouchPlatform => Context.Driver?.OmitsOffscreenElements == true;

    /// <inheritdoc />
    protected override ElementNotFoundException RootNotFound()
        => _parentScope.DescribeMiss(Locator) ?? new ElementNotFoundException(Locator);

    /// <summary>
    /// Whether this container inherits its parent's readiness. False for a scope shown over its
    /// parent (a popup, a dialog), which is usable while the page under it is busy or gone.
    /// </summary>
    protected virtual bool AsksParent => true;

    /// <inheritdoc />
    protected override ScopeReadiness ProbeParentReadiness()
        => AsksParent ? _parentScope.ProbeReadiness() : ContentReady();

    private protected override bool IsAbsentScope()
    {
        if (!AsksParent)
        {
            return false;
        }

        var parent = _parentScope.ProbeReadiness();
        if (parent.IsReady)
        {
            return TryGetContainerRoot() is null;
        }

        return parent.State == ScopeReadinessState.MissingRoot && _parentScope is IAbsentScope absent && absent.IsAbsent();
    }

    protected override TParent SetResult => Parent;

    // Element-object members, so a container can declare a control capability
    // (IRefreshableControlObject, ISwipeableControlObject) the way a view does.

    // R9: timeoutMs is unused - it exists only because Core's IControlObject declares it, and a
    // container that declares a control capability implements that interface. Core is unchanged by
    // this work; see .docs/decisions/ad-010-maui-ahead-of-core.md.
    /// <summary>Reads a named attribute from the container's root, in the platform's own vocabulary, now.</summary>
    public string? GetAttribute(string name, int? timeoutMs = null)
        => TryGetContainerRoot()?.GetAttribute(name);
}

/// <summary>A scope that can tell it is absent from a page that is there (D11).</summary>
internal interface IAbsentScope
{
    bool IsAbsent();
}
