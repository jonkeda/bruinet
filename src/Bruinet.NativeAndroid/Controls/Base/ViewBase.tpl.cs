using Bruinet.Core.Configuration;
using Bruinet.Core.Calls;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Core.Scopes;
using Bruinet.Core.Utilities;
using Bruinet.NativeAndroid.Interfaces;
using System.Runtime.CompilerServices;

namespace Bruinet.NativeAndroid.Controls;

/// <summary>
/// Base class for every native Android control: the Is/Wait/Assert pattern with fluent chaining.
/// A control finds its view within its scope (page, container, dialog or row).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ViewBase<TScope> : ObjectBase, IControlObject<TScope>, IControlHost<INativeAndroidElement>
    where TScope : INativeAndroidScope<TScope>
{
    private readonly INativeAndroidScope<TScope> _scope;

    protected ViewBase(INativeAndroidScope<TScope> scope, Locator locator)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Locator = locator ?? throw new ArgumentNullException(nameof(locator));
    }

    protected ViewBase(INativeAndroidScope<TScope> scope, string resourceIdOrXPath)
        : this(scope, ResolveLocator(resourceIdOrXPath))
    {
    }

    protected Locator Locator { get; }

    protected TScope ContainingScope => _scope.Self;

    public override INativeAndroidTestContext Context => _scope.Context;

    /// <summary>The view, found in one attempt, or null.</summary>
    protected INativeAndroidElement? TryFindElement() => _scope.TryFindElement(Locator);

    /// <summary>The view, found in one attempt.</summary>
    /// <exception cref="ElementNotFoundException">It is not there.</exception>
    protected INativeAndroidElement FindElement() => TryFindElement() ?? throw NotFound();

    /// <summary>The error for a control that could not be found.</summary>
    protected virtual ElementNotFoundException NotFound() => new(Locator);

    /// <summary>
    /// Whether a call that does not find the control scrolls the screen to bring it into the tree.
    /// UiAutomator2 leaves views outside the screen out of it. True on a page; a control in a list
    /// row or a dialog does not scroll, since that would move its own scope.
    /// </summary>
    protected virtual bool ScrollsToFind => _scope is INativeAndroidPage;

    #region Calls

    // Every public member is one call (.docs/contracts/call-model.md, R1), run by Core's
    // ControlCalls: one log pair, one budget, one poll, an action once after it (R0). This control
    // is its host: the view, the scope and the settings. The Run* names and signatures are the
    // generator's (R8), so its output compiles on every stack.

    private ControlCalls<INativeAndroidElement>? _calls;

    private ControlCalls<INativeAndroidElement> Calls => _calls ??= new(this);

    /// <summary>
    /// What is left of the running call's budget, for a wait below the call (a scroll); the
    /// default wait outside a call.
    /// </summary>
    protected int CallRemainingMs => CallBudget.RemainingOr(DefaultTimeoutMs);

    Locator IControlHost<INativeAndroidElement>.Locator => Locator;

    ControlCall IControlHost<INativeAndroidElement>.NewCall()
        => new(Context.Logger, Context.Name, Context.NearMiss, _scope.Page?.GetType().Name ?? "Unknown", Locator.Value);

    int IControlHost<INativeAndroidElement>.DefaultTimeoutMs => DefaultTimeoutMs;

    int IControlHost<INativeAndroidElement>.PollingIntervalMs => PollingIntervalMs;

    int IControlHost<INativeAndroidElement>.AnimationMs => Context.Timeouts.Animation.ToMs();

    ScopeReadiness IControlHost<INativeAndroidElement>.ProbeScope() => _scope.ProbeReadiness();

    INativeAndroidElement? IControlHost<INativeAndroidElement>.Locate(AttemptContext attempt) => FindOrScrollTo(attempt);

    INativeAndroidElement? IControlHost<INativeAndroidElement>.TryFind() => TryFindElement();

    bool? IControlHost<INativeAndroidElement>.IsVisible(INativeAndroidElement element) => IsVisibleCore(element);

    void IControlHost<INativeAndroidElement>.ScrollIntoView(INativeAndroidElement element, int timeoutMs) => element.ScrollIntoView(timeoutMs);

    string IControlHost<INativeAndroidElement>.ScrollKey(INativeAndroidElement element) => Locator.ToString();

    string? IControlHost<INativeAndroidElement>.InstanceKey(INativeAndroidElement element) => element.InstanceKey;

    bool IControlHost<INativeAndroidElement>.ChecksVisibilityBeforeAction(INativeAndroidElement element) => true;

    void IControlHost<INativeAndroidElement>.EnsureReadyForAction(INativeAndroidElement element) => EnsureReadyForActionCore(element);

    ElementNotFoundException IControlHost<INativeAndroidElement>.NotFound() => NotFound();

    // A control below the fold is not in the tree: scroll it in, throttled as a scroll into view is.
    private INativeAndroidElement? FindOrScrollTo(AttemptContext attempt)
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

    protected bool RunWaitWithElement<T>(T? expected, Func<INativeAndroidElement, bool> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => expected == null || Calls.WaitWithElement(coreOperation, timeoutMs, caller ?? nameof(RunWaitWithElement));

    /// <summary>
    /// Polls <paramref name="until"/> on the value <paramref name="read"/> gives, until it holds.
    /// The last value read is in the call's log when it never did.
    /// </summary>
    protected bool RunWaitForValue(Func<INativeAndroidElement, string?> read, Func<string?, bool> until,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => Calls.WaitForValue(read, until, timeoutMs, caller ?? nameof(RunWaitForValue));

    /// <summary>Polls a predicate that is meaningful when the view is absent.</summary>
    protected bool RunWaitWithOptionalElement<T>(T? expected, Func<INativeAndroidElement?, bool> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => expected == null || Calls.WaitWithOptionalElement(coreOperation, timeoutMs, caller ?? nameof(RunWaitWithOptionalElement));

    /// <summary>Asserts a value that is meaningful when the view is absent.</summary>
    protected TScope RunAssertWithOptionalElement<T>(T? expected,
        Func<INativeAndroidElement?, T?> getActual, Func<T?, T?, bool> compare,
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
    protected TScope RunDoWithElement(Action<INativeAndroidElement> coreOperation,
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
                + $"it reads '{confirmation.LastValue}'. The action ran once and is not repeated.",
                lastError));
        }
    }

    /// <summary>
    /// Checks any readiness this control needs before it is acted on. Throw
    /// <see cref="ElementNotReadyException"/> when it is not ready; the call asks again.
    /// </summary>
    protected virtual void EnsureReadyForActionCore(INativeAndroidElement element)
    {
    }

    protected TScope RunSetWithElement<T>(T? value, Action<INativeAndroidElement> coreOperation,
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
    protected T? RunGetWithOptionalElement<T>(Func<INativeAndroidElement?, T> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => Calls.GetWithOptionalElement(coreOperation, timeoutMs, caller ?? nameof(RunGetWithOptionalElement));

    protected T? RunGetWithElement<T>(Func<INativeAndroidElement, T> coreOperation,
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
    protected TScope RunAssertWithElement<T>(T? expected, Func<INativeAndroidElement, T?> getActual,
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
    /// A plain string is a resource id (with or without its package prefix); one that starts with
    /// <c>/</c> or <c>(</c> is an XPath.
    /// </summary>
    protected static Locator ResolveLocator(string resourceIdOrXPath)
    {
        if (string.IsNullOrWhiteSpace(resourceIdOrXPath))
        {
            throw new ArgumentNullException(nameof(resourceIdOrXPath));
        }

        return resourceIdOrXPath.StartsWith('/') || resourceIdOrXPath.StartsWith('(')
            ? Locator.ByXPath(resourceIdOrXPath)
            : Locator.ById(resourceIdOrXPath);
    }

    #region Keys

    /// <summary>Types keys into the control.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="keys">The keys to send.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SendKeysCore(INativeAndroidElement element, string keys, int? timeoutMs = null)
    {
        element.SendKeys(keys);
    }

    #endregion

    #region Visible

    /// <summary>Whether the control is shown; null when it is not there.</summary>
    /// <param name="element">The pre-found view, or null.</param>
    [AbsenceTolerant]
    protected virtual bool? IsVisibleCore(INativeAndroidElement? element)
    {
        return element?.Visible;
    }

    #endregion

    #region Enabled

    /// <summary>Throws <see cref="ElementNotReadyException"/> (Disabled) when the view is not enabled.</summary>
    protected virtual void EnsureEnabledCore(INativeAndroidElement element)
    {
        if (IsEnabledCore(element) != true)
        {
            throw new ElementNotReadyException(Locator, NotReadyReason.Disabled, "not enabled");
        }
    }

    /// <summary>Whether the control is enabled.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual bool? IsEnabledCore(INativeAndroidElement? element)
    {
        return element?.Enabled;
    }

    #endregion

    #region Exists

    // Hand-written, as on MAUI and Html: IsExists answers bool, not bool?, and never fails on absence.
    private static bool? IsExistsBase(INativeAndroidElement? element) => element != null;

    /// <summary>Whether the control is on the screen now. Does not wait (R7).</summary>
    public bool IsExists()
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
    protected virtual string? GetTextCore(INativeAndroidElement element)
    {
        return element.Text;
    }

    /// <summary>The text now, or <c>null</c> when the control is not there. Does not wait (R7).</summary>
    public string? TryGetText() => TryFindElement() is { } element ? GetTextCore(element) : null;

    /// <summary>Waits until the text satisfies <paramref name="until"/> (a count above zero, a value that changed).</summary>
    public bool WaitText(Func<string?, bool> until, int? timeoutMs = null)
        => RunWaitForValue(element => GetTextCore(element), until, timeoutMs);

    #endregion

    #region Content description

    /// <summary>The control's content description: what TalkBack reads for it.</summary>
    /// <param name="element">The pre-found view.</param>
    [GenerateComparisons(Comparison.EqualTo | Comparison.Contains)]
    protected virtual string? GetContentDescriptionCore(INativeAndroidElement element)
    {
        return element.ContentDescription;
    }

    #endregion

    #region Attributes

    /// <summary>A named UiAutomator2 attribute of the control.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="name">The attribute's name.</param>
    protected virtual string? GetAttributeCore(INativeAndroidElement element, string? name)
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
    protected virtual void ScrollIntoViewCore(INativeAndroidElement element)
    {
        element.ScrollIntoView(CallRemainingMs);
    }

    #endregion
}
