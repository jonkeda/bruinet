using Bruinet.Core.Configuration;
using Bruinet.Core.Calls;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Core.Scopes;
using Bruinet.Core.Utilities;
using Bruinet.NativeIos.Interfaces;
using System.Runtime.CompilerServices;

namespace Bruinet.NativeIos.Controls;

/// <summary>
/// Base class for every native iOS control: the Is/Wait/Assert pattern with fluent chaining.
/// A control finds its view within its scope (page, container, alert or row).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ViewBase<TScope> : ObjectBase, IControlObject<TScope>, IControlHost<INativeIosElement>
    where TScope : INativeIosScope<TScope>
{
    private readonly INativeIosScope<TScope> _scope;

    protected ViewBase(INativeIosScope<TScope> scope, Locator locator)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Locator = locator ?? throw new ArgumentNullException(nameof(locator));
    }

    protected ViewBase(INativeIosScope<TScope> scope, string identifierOrXPath)
        : this(scope, ResolveLocator(identifierOrXPath))
    {
    }

    protected Locator Locator { get; }

    protected TScope ContainingScope => _scope.Self;

    public override INativeIosTestContext Context => _scope.Context;

    /// <summary>The view, found in one attempt, or null.</summary>
    protected INativeIosElement? TryFindElement() => _scope.TryFindElement(Locator);

    /// <summary>The view, found in one attempt.</summary>
    /// <exception cref="ElementNotFoundException">It is not there.</exception>
    protected INativeIosElement FindElement() => TryFindElement() ?? throw NotFound();

    /// <summary>The error for a control that could not be found.</summary>
    protected virtual ElementNotFoundException NotFound() => new(Locator);

    /// <summary>
    /// Whether a call that does not find the control scrolls the screen to bring it into the tree.
    /// XCUITest leaves cells outside the screen out of it. True on a page; a control in a list
    /// row or a dialog does not scroll, since that would move its own scope.
    /// </summary>
    protected virtual bool ScrollsToFind => _scope is INativeIosPage;

    #region Calls

    // Every public member is one call (.docs/contracts/call-model.md, R1), run by Core's
    // ControlCalls: one log pair, one budget, one poll, an action once after it (R0). This control
    // is its host: the view, the scope and the settings. The Run* names and signatures are the
    // generator's (R8), so its output compiles on every stack.

    private ControlCalls<INativeIosElement>? _calls;

    private ControlCalls<INativeIosElement> Calls => _calls ??= new(this);

    /// <summary>
    /// What is left of the running call's budget, for a wait below the call (a scroll); the
    /// default wait outside a call.
    /// </summary>
    protected int CallRemainingMs => CallBudget.RemainingOr(DefaultTimeoutMs);

    Locator IControlHost<INativeIosElement>.Locator => Locator;

    ControlCall IControlHost<INativeIosElement>.NewCall()
        => new(Context.Logger, Context.Name, Context.NearMiss, _scope.Page?.GetType().Name ?? "Unknown", Locator.Value);

    int IControlHost<INativeIosElement>.DefaultTimeoutMs => DefaultTimeoutMs;

    int IControlHost<INativeIosElement>.PollingIntervalMs => PollingIntervalMs;

    int IControlHost<INativeIosElement>.AnimationMs => Context.Timeouts.Animation.ToMs();

    ScopeReadiness IControlHost<INativeIosElement>.ProbeScope() => _scope.ProbeReadiness();

    INativeIosElement? IControlHost<INativeIosElement>.Locate(AttemptContext attempt) => FindOrScrollTo(attempt);

    INativeIosElement? IControlHost<INativeIosElement>.TryFind() => TryFindElement();

    bool? IControlHost<INativeIosElement>.IsVisible(INativeIosElement element) => IsVisibleCore(element);

    void IControlHost<INativeIosElement>.ScrollIntoView(INativeIosElement element, int timeoutMs) => element.ScrollIntoView(timeoutMs);

    string IControlHost<INativeIosElement>.ScrollKey(INativeIosElement element) => Locator.ToString();

    string? IControlHost<INativeIosElement>.InstanceKey(INativeIosElement element) => element.InstanceKey;

    bool IControlHost<INativeIosElement>.ChecksVisibilityBeforeAction(INativeIosElement element) => true;

    void IControlHost<INativeIosElement>.EnsureReadyForAction(INativeIosElement element) => EnsureReadyForActionCore(element);

    ElementNotFoundException IControlHost<INativeIosElement>.NotFound() => NotFound();

    string? IControlHost<INativeIosElement>.ScopeName => _scope.Page?.GetType().Name;

    string? IControlHost<INativeIosElement>.MemberName => Bruinet.Core.Calls.ControlMembers.NameOf(_scope.Page, Locator);

    IReadOnlyList<string> IControlHost<INativeIosElement>.ShownControls => Bruinet.Core.Calls.ControlMembers.ShownOf(_scope.Page);

    // A control below the fold is not in the tree: scroll it in, throttled as a scroll into view is.
    private INativeIosElement? FindOrScrollTo(AttemptContext attempt)
    {
        if (TryFindElement() is { } element)
        {
            return element;
        }

        return ScrollsToFind && attempt.MayScrollIntoView(Locator.ToString()) && Context.Driver.TryScrollIntoView(Locator)
            ? TryFindElement()
            : null;
    }

    protected bool RunWait(Func<bool> operation, int? timeoutMs = null,
        [CallerMemberName] string? caller = null)
        => Calls.Wait(operation, timeoutMs, caller ?? nameof(RunWait));

    protected bool RunWaitWithElement<T>(T? expected, Func<INativeIosElement, bool> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => expected == null || Calls.WaitWithElement(coreOperation, timeoutMs, caller ?? nameof(RunWaitWithElement));

    /// <summary>
    /// Polls <paramref name="until"/> on the value <paramref name="read"/> gives, until it holds.
    /// The last value read is in the call's log when it never did.
    /// </summary>
    protected bool RunWaitForValue(Func<INativeIosElement, string?> read, Func<string?, bool> until,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => Calls.WaitForValue(read, until, timeoutMs, caller ?? nameof(RunWaitForValue));

    /// <summary>Polls a predicate that is meaningful when the view is absent.</summary>
    protected bool RunWaitWithOptionalElement<T>(T? expected, Func<INativeIosElement?, bool> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => expected == null || Calls.WaitWithOptionalElement(coreOperation, timeoutMs, caller ?? nameof(RunWaitWithOptionalElement));

    /// <summary>Asserts a value that is meaningful when the view is absent.</summary>
    protected TScope RunAssertWithOptionalElement<T>(T? expected,
        Func<INativeIosElement?, T?> getActual, Func<T?, T?, bool> compare,
        string? message = null, int? timeoutMs = null, [CallerMemberName] string? caller = null)
    {
        if (expected != null)
        {
            Calls.AssertWithOptionalElement(expected, getActual, compare, message, timeoutMs,
                caller ?? nameof(RunAssertWithOptionalElement));
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

    /// <summary>Whether an action waits for the view to be visible before it runs.</summary>
    protected virtual bool RequiresVisibilityForAction => true;

    /// <summary>An action as one call: polls until the control is ready to be acted on, then acts once.</summary>
    protected TScope RunDoWithElement(Action<INativeIosElement> coreOperation,
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
    /// Inside an action: runs <paramref name="act"/> on the view once, then confirms its effect
    /// with a budget of its own (R3). An effect that never shows fails the call, naming what was
    /// read; the action is never repeated (R0).
    /// </summary>
    /// <param name="action">The action's name, for the message ("SetText", "Check").</param>
    /// <param name="act">The action.</param>
    /// <param name="read">Reads the effect, typically from the view just acted on.</param>
    /// <param name="done">Whether the value read is the effect asked for.</param>
    /// <param name="expected">What was asked for, in words, for the message.</param>
    /// <param name="timeoutMs">The confirmation's budget; the caller's timeout, or the default.</param>
    protected void ActAndConfirm<T>(string action, Action act, Func<T?> read, Func<T?, bool> done,
        string expected, int? timeoutMs)
    {
        act();
        var confirmation = Confirm(read, done, timeoutMs);
        if (!confirmation.IsConfirmed)
        {
            throw confirmation.Failure(Locator, action, lastError => new InvalidOperationException(
                $"The view '{Locator}' accepted {action} and did not take it: expected {expected}, "
                + $"it reads '{Readable(confirmation.LastValue)}'. The action ran once and is not repeated.",
                lastError));
        }
    }


    // A list read (a select's selections) names its items, not its type.
    private static string? Readable(object? value)
        => value is System.Collections.IEnumerable items and not string
            ? "[" + string.Join(", ", items.Cast<object?>()) + "]"
            : value?.ToString();

    /// <summary>
    /// Checks any readiness this control needs before it is acted on. Throw
    /// <see cref="ElementNotReadyException"/> when it is not ready; the call asks again.
    /// </summary>
    protected virtual void EnsureReadyForActionCore(INativeIosElement element)
    {
    }

    protected TScope RunSetWithElement<T>(T? value, Action<INativeIosElement> coreOperation,
         int? timeoutMs = null, [CallerMemberName] string? caller = null)
    {
        if (value != null)
        {
            Calls.Act(coreOperation, ensureVisible: true, timeoutMs, caller ?? nameof(RunSetWithElement), value.ToString());
        }

        return ContainingScope;
    }

    /// <summary>
    /// Reads a value that is meaningful when the view is absent: once, after the scope is
    /// ready, with the view resolved optionally.
    /// </summary>
    protected T? RunGetWithOptionalElement<T>(Func<INativeIosElement?, T> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => Calls.GetWithOptionalElement(coreOperation, timeoutMs, caller ?? nameof(RunGetWithOptionalElement));

    protected T? RunGetWithElement<T>(Func<INativeIosElement, T> coreOperation,
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

    /// <summary>An assertion as one call: finds the view again on every attempt, then compares.</summary>
    protected TScope RunAssertWithElement<T>(T? expected, Func<INativeIosElement, T?> getActual,
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

    /// <summary>
    /// A plain string is an accessibility identifier; one that starts with <c>/</c> or <c>(</c> is
    /// an XPath.
    /// </summary>
    protected static Locator ResolveLocator(string identifierOrXPath)
    {
        if (string.IsNullOrWhiteSpace(identifierOrXPath))
        {
            throw new ArgumentNullException(nameof(identifierOrXPath));
        }

        return identifierOrXPath.StartsWith('/') || identifierOrXPath.StartsWith('(')
            ? Locator.ByXPath(identifierOrXPath)
            : Locator.ById(identifierOrXPath);
    }

    #region Keys

    /// <summary>Types keys into the control.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="keys">The keys to send.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SendKeysCore(INativeIosElement element, string keys, int? timeoutMs = null)
    {
        element.SendKeys(keys);
    }

    #endregion

    #region Visible

    /// <summary>Whether the control is shown; null when it is not there.</summary>
    /// <param name="element">The pre-found view, or null.</param>
    [AbsenceTolerant]
    protected virtual bool? IsVisibleCore(INativeIosElement? element)
    {
        return element?.Visible;
    }

    #endregion

    #region Enabled

    /// <summary>Throws <see cref="ElementNotReadyException"/> (Disabled) when the view is not enabled.</summary>
    protected virtual void EnsureEnabledCore(INativeIosElement element)
    {
        if (IsEnabledCore(element) != true)
        {
            throw new ElementNotReadyException(Locator, NotReadyReason.Disabled);
        }
    }

    /// <summary>Whether the control is enabled.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual bool? IsEnabledCore(INativeIosElement? element)
    {
        return element?.Enabled;
    }

    #endregion

    #region Exists

    // Hand-written, as on MAUI and Html: Exists answers bool, not bool?, and never fails on absence.
    private static bool? IsExistsBase(INativeIosElement? element) => element != null;

    /// <summary>Whether the control is on the screen now. Does not wait (R7).</summary>
    public bool Exists()
    {
        return IsExistsBase(TryFindElement()) == true;
    }

    /// <summary>Waits until the control's presence matches <paramref name="expected"/>.</summary>
    public bool WaitExists(bool? expected = true, int? timeoutMs = null)
    {
        return RunWaitWithOptionalElement(expected,
            element => IsExistsBase(element) == expected!.Value, timeoutMs);
    }

    /// <summary>Asserts the control's presence, returning the scope for chaining.</summary>
    public TScope AssertExists(bool? expected = true, string? message = null, int? timeoutMs = null)
    {
        return RunAssertWithOptionalElement(expected,
            IsExistsBase, (actual, expected1) => actual == expected1,
            message ?? $"Expected Exists to be '{expected}'. Locator: {Locator}", timeoutMs);
    }

    #endregion

    #region Text

    /// <summary>The control's text.</summary>
    /// <param name="element">The pre-found view.</param>
    [GenerateComparisons(Comparison.EqualTo | Comparison.Contains | Comparison.StartsWith
        | Comparison.EndsWith | Comparison.Empty)]
    protected virtual string? GetTextCore(INativeIosElement element)
    {
        return element.Text;
    }

    /// <summary>The text now, or <c>null</c> when the control is not there. Does not wait (R7).</summary>
    public string? TryGetText() => TryFindElement() is { } element ? GetTextCore(element) : null;

    /// <summary>Waits until the text satisfies <paramref name="until"/> (a count above zero, a value that changed).</summary>
    public bool WaitText(Func<string?, bool> until, int? timeoutMs = null)
        => RunWaitForValue(element => GetTextCore(element), until, timeoutMs);

    #endregion

    #region Accessibility label

    /// <summary>The control's accessibility label: what VoiceOver reads for it.</summary>
    /// <param name="element">The pre-found view.</param>
    [GenerateComparisons(Comparison.EqualTo | Comparison.Contains)]
    protected virtual string? GetAccessibilityLabelCore(INativeIosElement element)
    {
        return element.Label;
    }

    #endregion

    #region Attributes

    /// <summary>A named XCUITest attribute of the control (<c>value</c>, <c>hittable</c>, <c>traits</c>).</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="name">The attribute's name.</param>
    protected virtual string? GetAttributeCore(INativeIosElement element, string? name)
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
    /// Scrolls the view onto the screen, as one call. Scrolling is how the view becomes visible,
    /// so this call does not wait for visibility first.
    /// </summary>
    public TScope ScrollIntoView(int? timeoutMs = null)
    {
        Calls.GetWithElement(element =>
        {
            ScrollIntoViewCore(element);
            return true;
        }, timeoutMs, nameof(ScrollIntoView), ensureVisible: false);
        return ContainingScope;
    }

    /// <summary>Scrolls the view onto the screen, with what is left of the call's budget.</summary>
    /// <param name="element">The pre-found view.</param>
    [SkipGeneration("Hand-written above: scrolling is how the view becomes visible, so the call does not wait for visibility first.")]
    protected virtual void ScrollIntoViewCore(INativeIosElement element)
    {
        element.ScrollIntoView(CallRemainingMs);
    }

    #endregion
}
