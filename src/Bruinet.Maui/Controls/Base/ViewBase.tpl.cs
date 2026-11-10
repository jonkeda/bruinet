using Bruinet.Core.Configuration;
using Bruinet.Core.Calls;
using Bruinet.Core.Utilities;
using System.Runtime.CompilerServices;

namespace Bruinet.Maui.Controls.Base;

/// <summary>
/// Base class for all MAUI controls implementing the Is/Wait/Assert pattern with fluent chaining.
/// Controls find elements within their scope (page, container, or list item).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent method chaining.</typeparam>
public abstract partial class ViewBase<TScope> : IElementObject<TScope>, IControlHost<IMauiElement>
    where TScope : IMauiScope<TScope>
{
    private readonly IMauiScope<TScope> _mauiScope;

    /// <summary>
    /// Creates a new control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator used to find the control element.</param>
    protected ViewBase(IMauiScope<TScope> scope, Locator locator)
    {
        Locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _mauiScope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    /// <summary>
    /// Creates a new control within the specified scope using a string locator value.
    /// Uses the scope's DefaultLocatorStrategy to create the locator.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID, name).</param>
    protected ViewBase(IMauiScope<TScope> scope, string locatorValue)
        : this(scope, new Locator(scope?.DefaultLocatorStrategy ?? LocatorStrategy.AutomationId, locatorValue))
    {
        if (string.IsNullOrEmpty(locatorValue))
            throw new ArgumentNullException(nameof(locatorValue));
    }

    /// <summary>
    /// The locator that finds this control within its scope.
    /// </summary>
    protected Locator Locator { get; }

    /// <summary>
    /// The page this control belongs to, or null when its scope is not in a page.
    /// </summary>
    protected IMauiPage? Page => _mauiScope.Page;

    /// <summary>
    /// Gets the containing scope for fluent chaining.
    /// </summary>
    protected TScope ContainingScope => _mauiScope.Self;

    /// <summary>
    /// Gets the MAUI-typed scope for element finding operations.
    /// </summary>
    protected IMauiScope<TScope> MauiScope => _mauiScope;

    /// <summary>
    /// Gets the MAUI test context.
    /// </summary>
    protected IMauiTestContext Context => _mauiScope.Context;

    /// <summary>
    /// Gets the default timeout in milliseconds.
    /// </summary>
    protected int DefaultTimeoutMs => Context.Timeouts.DefaultWait.ToMs();

    /// <summary>
    /// Gets the polling interval in milliseconds.
    /// </summary>
    protected int PollingIntervalMs => Context.Timeouts.PollingInterval.ToMs();

    #region Calls

    // Every public member is one call (.docs/contracts/call-model.md, R1), run by Core's
    // ControlCalls: one log pair, one budget, one poll, an action once after it (R0). This control
    // is its host: the element, the scope and the settings. The Run* names and signatures are the
    // generator's (R8), so its output compiles on every stack.

    private ControlCalls<IMauiElement>? _calls;

    private ControlCalls<IMauiElement> Calls => _calls ??= new(this);

    /// <summary>
    /// What is left of the running call's budget, for a wait below the call (a scroll); the
    /// default wait outside a call.
    /// </summary>
    protected int CallRemainingMs => CallBudget.RemainingOr(DefaultTimeoutMs);

    Locator IControlHost<IMauiElement>.Locator => Locator;

    ControlCall IControlHost<IMauiElement>.NewCall()
        => new(Context.Logger, Context.Name, Context.NearMiss, Page?.GetType().Name ?? "Unknown", Locator.Value);

    int IControlHost<IMauiElement>.DefaultTimeoutMs => DefaultTimeoutMs;

    int IControlHost<IMauiElement>.PollingIntervalMs => PollingIntervalMs;

    int IControlHost<IMauiElement>.AnimationMs => Context.Timeouts.Animation.ToMs();

    ScopeReadiness IControlHost<IMauiElement>.ProbeScope() => _mauiScope.ProbeReadiness();

    bool IControlHost<IMauiElement>.ScopeIsAbsent() => _mauiScope is Containers.IAbsentScope absent && absent.IsAbsent();

    IMauiElement? IControlHost<IMauiElement>.Locate(AttemptContext attempt) => Locate(attempt);

    IMauiElement? IControlHost<IMauiElement>.TryFind() => TryFindElement();

    // The check before a read or an action. Where the driver finds only what is shown (Android), being
    // found already answers it, without a round trip; IsVisible and AssertVisible still ask
    // (.my/android-performance/design-fewer-reads.md, D3).
    bool? IControlHost<IMauiElement>.IsVisible(IMauiElement element)
        => element is IShownWhenFound { IsShownWhenFound: true } || IsVisibleCore(element) == true;

    void IControlHost<IMauiElement>.ScrollIntoView(IMauiElement element, int timeoutMs) => element.ScrollIntoView(timeoutMs);

    string IControlHost<IMauiElement>.ScrollKey(IMauiElement element) => element.InstanceKey ?? Locator.ToString();

    string? IControlHost<IMauiElement>.InstanceKey(IMauiElement element) => element.InstanceKey;

    bool IControlHost<IMauiElement>.ChecksVisibilityBeforeAction(IMauiElement element) => true;

    void IControlHost<IMauiElement>.EnsureReadyForAction(IMauiElement element)
    {
        EnsureNotUnderADialog(element);
        EnsureReadyForActionCore(element);
    }

    /// <summary>
    /// On Android a control found is a control counted as visible, also under a dialog: the tap
    /// then lands on the dialog, and the action reads as "accepted and did not take it". So before
    /// an action, a control outside an open dialog's bounds is not ready, "covered by a dialog",
    /// and the call keeps asking. One dialog lookup an action; a control inside the dialog passes.
    /// </summary>
    private void EnsureNotUnderADialog(IMauiElement element)
    {
        if (Context.Driver?.FoundMeansVisible != true)
        {
            return;
        }

        IMauiElement? dialog;
        try
        {
            dialog = Context.AppElement.TryFindActiveDialog();
        }
        catch (Exception error) when (error is NotSupportedException or RouteUnavailableException)
        {
            return;
        }

        if (dialog is not null
            && !dialog.Rect.Contains(Bruinet.Core.Interfaces.ElementGeometryExtensions.CenterOf(element.Rect)))
        {
            throw new ElementNotReadyException(Locator, NotReadyReason.Other, "covered by a dialog");
        }
    }

    ElementNotFoundException IControlHost<IMauiElement>.NotFound() => NotFound();

    /// <summary>
    /// Runs an operation once as one call: waits for the page, then runs it, with one log pair.
    /// </summary>
    protected TResult Run<TValue, TResult>(
        string action,
        TValue? value,
        Func<TResult> operation,
        int? timeoutMs = null)
        => Calls.Run(action, value?.ToString(), operation, timeoutMs);

    protected bool RunWait(Func<bool> operation, int? timeoutMs = null,
        [CallerMemberName] string? caller = null)
        => Calls.Wait(operation, timeoutMs, caller ?? nameof(RunWait));

    protected bool RunWaitWithElement<T>(T? expected, Func<IMauiElement, bool> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => expected == null || Calls.WaitWithElement(coreOperation, timeoutMs, caller ?? nameof(RunWaitWithElement));

    /// <summary>
    /// Polls <paramref name="until"/> on the value <paramref name="read"/> gives, until it holds.
    /// The last value read is in the call's log when it never did.
    /// </summary>
    protected bool RunWaitForValue(Func<IMauiElement, string?> read, Func<string?, bool> until,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => Calls.WaitForValue(read, until, timeoutMs, caller ?? nameof(RunWaitForValue));

    /// <summary>Polls a predicate that is meaningful when the element is absent.</summary>
    protected bool RunWaitWithOptionalElement<T>(T? expected, Func<IMauiElement?, bool> coreOperation,
        int? timeoutMs = null, Func<IMauiElement?>? resolve = null,
        [CallerMemberName] string? caller = null)
        => expected == null || Calls.WaitWithOptionalElement(coreOperation, timeoutMs, caller ?? nameof(RunWaitWithOptionalElement), resolve);

    /// <summary>Asserts a value that is meaningful when the element is absent.</summary>
    protected TScope RunAssertWithOptionalElement<T>(T? expected,
        Func<IMauiElement?, T?> getActual, Func<T?, T?, bool> compare,
        string? message = null, int? timeoutMs = null, Func<IMauiElement?>? resolve = null,
        [CallerMemberName] string? caller = null)
    {
        if (expected != null)
        {
            Calls.AssertWithOptionalElement(expected, getActual, compare, message, timeoutMs,
                caller ?? nameof(RunAssertWithOptionalElement), resolve);
        }

        return ContainingScope;
    }

    /// <summary>Runs an operation as one call: waits for the scope, then runs it once.</summary>
    protected TScope RunDo(Action operation, int? timeoutMs = null,
        [CallerMemberName] string? caller = null)
    {
        Calls.Run(caller ?? nameof(RunDo), null, () =>
        {
            operation();
            return true;
        }, timeoutMs);
        return ContainingScope;
    }

    /// <summary>Whether an action waits for the element to be visible before it runs.</summary>
    protected virtual bool RequiresVisibilityForAction => true;

    /// <summary>An action as one call: polls until the control is ready to be acted on, then acts once.</summary>
    protected TScope RunDoWithElement(Action<IMauiElement> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
    {
        Calls.Act(coreOperation, RequiresVisibilityForAction, timeoutMs, caller ?? nameof(RunDoWithElement));
        return ContainingScope;
    }

    /// <summary>
    /// Waits, inside an action, for the effect of what the action has already done. Never repeats
    /// the action (R0).
    /// </summary>
    protected Confirmation<T> Confirm<T>(Func<T?> read, Func<T?, bool> done, int? timeoutMs)
        => Calls.Confirm(read, done, timeoutMs);

    /// <summary>
    /// Checks any readiness this control needs before it is acted on. Throw
    /// <see cref="ElementNotReadyException"/> when it is not ready; the call asks again.
    /// </summary>
    protected virtual void EnsureReadyForActionCore(IMauiElement element)
    {
    }

    protected TScope RunSetWithElement<T>(T? value, Action<IMauiElement> coreOperation,
         int? timeoutMs = null, [CallerMemberName] string? caller = null)
    {
        if (value != null)
        {
            Calls.Act(coreOperation, ensureVisible: true, timeoutMs, caller ?? nameof(RunSetWithElement), value.ToString());
        }

        return ContainingScope;
    }

    /// <summary>
    /// Reads a value that is meaningful when the element is absent: once, after the scope is
    /// ready, with the element resolved optionally.
    /// </summary>
    protected T? RunGetWithOptionalElement<T>(Func<IMauiElement?, T> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => Calls.GetWithOptionalElement(coreOperation, timeoutMs, caller ?? nameof(RunGetWithOptionalElement));

    protected T? RunGetWithElement<T>(Func<IMauiElement, T> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => Calls.GetWithElement(coreOperation, timeoutMs, caller ?? nameof(RunGetWithElement));

    /// <summary>Run assertion with custom comparison function.</summary>
    protected TScope RunAssert<T>(T? expected, Func<T?> getActual,
        Func<T?, T?, bool> compare, string? message = null,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
    {
        if (expected != null)
        {
            Calls.Assert(expected, getActual, compare, message, timeoutMs, caller ?? nameof(RunAssert));
        }

        return ContainingScope;
    }

    /// <summary>An assertion as one call: finds the element again on every attempt, then compares.</summary>
    protected TScope RunAssertWithElement<T>(T? expected, Func<IMauiElement, T?> getActual,
        Func<T?, T?, bool> compare, string? message = null,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
    {
        if (expected != null)
        {
            Calls.AssertWithElement(expected, getActual, compare, message, timeoutMs, caller ?? nameof(RunAssertWithElement));
        }

        return ContainingScope;
    }

    #endregion

    #region Element Finding

    /// <summary>
    /// Tries to find the element within the scope.
    /// </summary>
    /// <returns>The element if found, null otherwise.</returns>
    protected virtual IMauiElement? TryFindElement()
    {
        return _mauiScope.TryFindElement(Locator);
    }

    /// <summary>
    /// Whether a lookup that finds nothing should scroll to look.
    /// </summary>
    protected enum ScrollLookup
    {
        /// <summary>A plain lookup.</summary>
        None,

        /// <summary>
        /// A plain lookup, then one sweep of the scope's scrolling element. Inside a poll, the
        /// sweep happens on the first tick only.
        /// </summary>
        Once,
    }

    /// <summary>
    /// Resolves the element, scrolling to look for it if the plain lookup finds nothing.
    /// </summary>
    /// <param name="lookup">Whether to scroll to look.</param>
    /// <returns>The element, or null when it is genuinely not on the page.</returns>
    protected IMauiElement? TryFindElement(ScrollLookup lookup)
    {
        var element = TryFindElement();
        if (element != null || lookup == ScrollLookup.None || !_mauiScope.AllowsScrollLookup)
        {
            return element;
        }

        return SweepThenFindInScope();
    }

    /// <summary>
    /// The element to scroll when looking for this control: the scope's scroller, or the app,
    /// which lets the platform pick the scrolling container on screen.
    /// </summary>
    private IMauiElement? ScrollingElement() => _mauiScope.ScrollingRoot ?? Context.AppElement;

    /// <summary>
    /// Sweeps the scroller for the locator, then looks again in this control's own scope.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On Android and iOS the sweep only brings the element into view; whether it belongs to the
    /// scope is the scoped lookup's to say. The scroller is the page's, so its search runs
    /// page-wide, and handing its result back found a page's title "inside" a form that has none:
    /// there the plain lookup misses whatever is below the fold, and every miss sweeps.
    /// </para>
    /// <para>
    /// Windows keeps the sweep's own result. Off-screen elements are in its tree, so a sweep there
    /// is rare, and some parts are reached only through it: a MediaElement's transport controls are
    /// not in the element's own subtree, and confining the sweep failed six of its tests.
    /// </para>
    /// </remarks>
    private IMauiElement? SweepThenFindInScope()
    {
        var swept = ScrollingElement()?.TryFindByScrolling(Locator);
        if (swept is null || Context.Driver?.OmitsOffscreenElements != true)
        {
            return swept;
        }

        return TryFindElement();
    }

    /// <summary>
    /// A resolver for a polling helper, applying <paramref name="lookup"/>.
    /// </summary>
    /// <param name="lookup">Whether to scroll to look.</param>
    /// <returns>A resolver to hand to a polling helper.</returns>
    protected Func<IMauiElement?> Resolver(ScrollLookup lookup)
    {
        if (lookup == ScrollLookup.None)
        {
            return () => TryFindElement();
        }

        var swept = false;
        return () =>
        {
            if (swept)
            {
                return TryFindElement();
            }

            swept = true;
            return TryFindElement(ScrollLookup.Once);
        };
    }

    /// <summary>
    /// Finds the element in one attempt, sweeping the scope's scroller once if the plain lookup
    /// finds nothing, or throws <see cref="NotFound"/>.
    /// </summary>
    /// <returns>The element.</returns>
    /// <exception cref="ElementNotFoundException">Thrown when element is not found.</exception>
    protected IMauiElement FindElement()
        => TryFindElement()
           ?? (_mauiScope.AllowsScrollLookup ? SweepThenFindInScope() : null)
           ?? throw NotFound();

    /// <summary>
    /// The error for a control that could not be found.
    /// </summary>
    protected virtual ElementNotFoundException NotFound()
        => _mauiScope.DescribeMiss(Locator) ?? new ElementNotFoundException(Locator);

    /// <summary>
    /// The lookup of one attempt: a plain lookup, then a sweep of the scroller when the scope
    /// allows one and the call has not swept within the last Animation interval (F3).
    /// </summary>
    private IMauiElement? Locate(AttemptContext attempt)
    {
        var element = TryFindElement();
        if (element != null || !_mauiScope.AllowsScrollLookup || !attempt.MaySweep())
        {
            return element;
        }

        return SweepThenFindInScope();
    }

    #endregion

    #region Visible

    /// <summary>
    /// Checks if element is visible using pre-found element.
    /// No stale element handling - element is found once at operation start.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <returns>True if visible, false otherwise.</returns>
    [AbsenceTolerant]
    protected virtual bool? IsVisibleCore(IMauiElement? element)
    {
        return element?.Visible;
    }

    /// <summary>
    /// Checks whether the element can be seen at all, scrolling to it when it is not already
    /// on screen.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <returns>True when visible, scrolling to it first if needed; null when absent.</returns>
    [AbsenceTolerant]
    [SkipGeneration("Hand-written below: the trio resolves with ScrollLookup.Once, which the generated trio cannot express.")]
    protected virtual bool? IsVisibleAfterScrollCore(IMauiElement? element)
    {
        if (element == null)
        {
            return null;
        }

        if (IsVisibleCore(element) == true)
        {
            return true;
        }

        ScrollIntoViewCore(element);
        return IsVisibleCore(element);
    }

    /// <summary>Whether the user could see the control, scrolling to it if needed.</summary>
    /// <returns>True when visible after scrolling; false when not, or absent.</returns>
    public bool? IsVisibleAfterScroll()
        => IsVisibleAfterScrollCore(TryFindElement(ScrollLookup.Once)) == true;

    /// <summary>Waits until <see cref="IsVisibleAfterScroll"/> matches <paramref name="expected"/>.</summary>
    public bool WaitVisibleAfterScroll(bool? expected = true, int? timeoutMs = null)
        => RunWaitWithOptionalElement(expected,
            element => IsVisibleAfterScrollCore(element) == expected!.Value,
            timeoutMs, Resolver(ScrollLookup.Once));

    /// <summary>Asserts <see cref="IsVisibleAfterScroll"/>, returning the scope for chaining.</summary>
    public TScope AssertVisibleAfterScroll(bool? expected = true, string? message = null, int? timeoutMs = null)
        => RunAssertWithOptionalElement(expected,
            IsVisibleAfterScrollCore, (actual, expected1) => actual == expected1,
            message ?? $"Expected VisibleAfterScroll to be '{expected}'. Locator: {Locator}", timeoutMs,
            Resolver(ScrollLookup.Once));

    #endregion

    #region Enabled

    /// <summary>
    /// Checks if element is enabled using pre-found element.
    /// No stale element handling - element is found once at operation start.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <returns>True if enabled, false otherwise.</returns>
    protected virtual bool? IsEnabledCore(IMauiElement? element)
    {
        return element?.Enabled;
    }

    #endregion

    #region Exists

    protected virtual bool? IsExistsBase(IMauiElement? element)
    {
        return element != null;
    }

    /// <summary>
    /// Whether the control is on the page, scrolling to it if it is not on screen.
    /// </summary>
    public bool Exists()
    {
        return IsExistsBase(TryFindElement(ScrollLookup.Once)) == true;
    }

    /// <summary>
    /// Waits until the element's presence matches <paramref name="expected"/>.
    /// </summary>
    public bool WaitExists(bool? expected = true, int? timeoutMs = null)
    {
        return RunWaitWithOptionalElement(expected,
            element => IsExistsBase(element) == expected!.Value,
            timeoutMs, Resolver(ScrollLookup.Once));
    }

    /// <summary>
    /// Asserts the element's presence, returning the scope for chaining.
    /// </summary>
    public TScope AssertExists(bool? expected = true, string? message = null, int? timeoutMs = null)
    {
        return RunAssertWithOptionalElement(expected,
             IsExistsBase, (actual, expected1) => (actual == expected1),
            message ?? $"Expected Exists to be '{expected}'. Locator: {Locator}", timeoutMs,
            Resolver(ScrollLookup.Once));
    }

    #endregion
    #region Attributes

    /// <summary>
    /// Reads a named attribute straight from the platform.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="name">The platform's own attribute name.</param>
    /// <returns>The value, or null when absent - or unsupported.</returns>
    protected virtual string? GetAttributeCore(IMauiElement element, string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        return element.GetAttribute(name);
    }

    /// <summary>Waits until the attribute <paramref name="name"/> satisfies <paramref name="until"/>.</summary>
    public bool WaitAttribute(string name, Func<string?, bool> until, int? timeoutMs = null)
        => RunWaitForValue(element => GetAttributeCore(element, name), until, timeoutMs);

    #endregion

    #region ScrollIntoView

    /// <summary>
    /// Core scroll implementation. Uses element's ScrollIntoView method.
    /// </summary>
    /// <param name="element">The element to scroll into view.</param>
    protected virtual void ScrollIntoViewCore(IMauiElement element) => element.ScrollIntoView(CallRemainingMs);

    #endregion

}
