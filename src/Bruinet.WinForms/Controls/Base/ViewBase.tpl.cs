using Bruinet.Core.Configuration;
using Bruinet.Core.Calls;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Core.Scopes;
using Bruinet.Core.Utilities;
using Bruinet.WinForms.Interfaces;
using System.Runtime.CompilerServices;

namespace Bruinet.WinForms.Controls;

/// <summary>
/// Base class for every WinForms control: the Is/Wait/Assert pattern with fluent chaining. A control
/// finds its element within its scope (page, container, dialog or row).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ViewBase<TScope> : ObjectBase, IControlObject<TScope>, IControlHost<IWinFormsElement>
    where TScope : IWinFormsScope<TScope>
{
    private readonly IWinFormsScope<TScope> _scope;

    protected ViewBase(IWinFormsScope<TScope> scope, Locator locator)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Locator = locator ?? throw new ArgumentNullException(nameof(locator));
    }

    protected ViewBase(IWinFormsScope<TScope> scope, string automationId)
        : this(scope, ResolveLocator(automationId))
    {
    }

    protected Locator Locator { get; }

    protected TScope ContainingScope => _scope.Self;

    public override IWinFormsTestContext Context => _scope.Context;

    /// <summary>The element, found in one attempt, or null.</summary>
    protected IWinFormsElement? TryFindElement() => _scope.TryFindElement(Locator);

    /// <summary>The element, found in one attempt.</summary>
    /// <exception cref="ElementNotFoundException">It is not there.</exception>
    protected IWinFormsElement FindElement() => TryFindElement() ?? throw NotFound();

    /// <summary>The error for a control that could not be found.</summary>
    protected virtual ElementNotFoundException NotFound() => new(Locator);

    #region Calls

    // Every public member is one call (.docs/contracts/call-model.md, R1), run by Core's
    // ControlCalls: one log pair, one budget, one poll, an action once after it (R0). This control
    // is its host: the element, the scope and the settings. The Run* names and signatures are the
    // generator's (R8), so its output compiles on every stack.

    private ControlCalls<IWinFormsElement>? _calls;

    private ControlCalls<IWinFormsElement> Calls => _calls ??= new(this);

    /// <summary>
    /// What is left of the running call's budget, for a wait below the call (a scroll); the
    /// default wait outside a call.
    /// </summary>
    protected int CallRemainingMs => CallBudget.RemainingOr(DefaultTimeoutMs);

    Locator IControlHost<IWinFormsElement>.Locator => Locator;

    ControlCall IControlHost<IWinFormsElement>.NewCall()
        => new(Context.Logger, Context.Name, Context.NearMiss, _scope.Page?.GetType().Name ?? "Unknown", Locator.Value);

    int IControlHost<IWinFormsElement>.DefaultTimeoutMs => DefaultTimeoutMs;

    int IControlHost<IWinFormsElement>.PollingIntervalMs => PollingIntervalMs;

    int IControlHost<IWinFormsElement>.AnimationMs => Context.Timeouts.Animation.ToMs();

    ScopeReadiness IControlHost<IWinFormsElement>.ProbeScope() => _scope.ProbeReadiness();

    // WinForms keeps a control that is scrolled away in the tree: a lookup finds it, and the call scrolls it
    // into view (ScrollItem) before it acts. A list in virtual mode is a collection, which scrolls to find.
    IWinFormsElement? IControlHost<IWinFormsElement>.Locate(AttemptContext attempt) => TryFindElement();

    IWinFormsElement? IControlHost<IWinFormsElement>.TryFind() => TryFindElement();

    bool? IControlHost<IWinFormsElement>.IsVisible(IWinFormsElement element) => IsVisibleCore(element);

    void IControlHost<IWinFormsElement>.ScrollIntoView(IWinFormsElement element, int timeoutMs) => element.ScrollIntoView(timeoutMs);

    string IControlHost<IWinFormsElement>.ScrollKey(IWinFormsElement element) => Locator.ToString();

    string? IControlHost<IWinFormsElement>.InstanceKey(IWinFormsElement element) => element.InstanceKey;

    bool IControlHost<IWinFormsElement>.ChecksVisibilityBeforeAction(IWinFormsElement element) => true;

    void IControlHost<IWinFormsElement>.EnsureReadyForAction(IWinFormsElement element) => EnsureReadyForActionCore(element);

    ElementNotFoundException IControlHost<IWinFormsElement>.NotFound() => NotFound();

    string? IControlHost<IWinFormsElement>.ScopeName => _scope.Page?.GetType().Name;

    string? IControlHost<IWinFormsElement>.MemberName => Bruinet.Core.Calls.ControlMembers.NameOf(_scope.Page, Locator);

    IReadOnlyList<string> IControlHost<IWinFormsElement>.ShownControls => Bruinet.Core.Calls.ControlMembers.ShownOf(_scope.Page);

    protected bool RunWait(Func<bool> operation, int? timeoutMs = null,
        [CallerMemberName] string? caller = null)
        => Calls.Wait(operation, timeoutMs, caller ?? nameof(RunWait));

    protected bool RunWaitWithElement<T>(T? expected, Func<IWinFormsElement, bool> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => expected == null || Calls.WaitWithElement(coreOperation, timeoutMs, caller ?? nameof(RunWaitWithElement));

    /// <summary>
    /// Polls <paramref name="until"/> on the value <paramref name="read"/> gives, until it holds.
    /// The last value read is in the call's log when it never did.
    /// </summary>
    protected bool RunWaitForValue(Func<IWinFormsElement, string?> read, Func<string?, bool> until,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => Calls.WaitForValue(read, until, timeoutMs, caller ?? nameof(RunWaitForValue));

    /// <summary>Polls a predicate that is meaningful when the element is absent.</summary>
    protected bool RunWaitWithOptionalElement<T>(T? expected, Func<IWinFormsElement?, bool> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => expected == null || Calls.WaitWithOptionalElement(coreOperation, timeoutMs, caller ?? nameof(RunWaitWithOptionalElement));

    /// <summary>Asserts a value that is meaningful when the element is absent.</summary>
    protected TScope RunAssertWithOptionalElement<T>(T? expected,
        Func<IWinFormsElement?, T?> getActual, Func<T?, T?, bool> compare,
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

    /// <summary>Whether an action waits for the element to be visible before it runs.</summary>
    protected virtual bool RequiresVisibilityForAction => true;

    /// <summary>An action as one call: polls until the control is ready to be acted on, then acts once.</summary>
    protected TScope RunDoWithElement(Action<IWinFormsElement> coreOperation,
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
    /// Inside an action: runs <paramref name="act"/> on the element once, then confirms its effect
    /// with a budget of its own (R3). An effect that never shows fails the call, naming what was
    /// read; the action is never repeated (R0).
    /// </summary>
    /// <param name="action">The action's name, for the message ("SetText", "Check").</param>
    /// <param name="act">The action.</param>
    /// <param name="read">Reads the effect, typically from the element just acted on.</param>
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
                $"The control '{Locator}' accepted {action} and did not take it: expected {expected}, "
                + $"it reads '{confirmation.LastValue}'. The action ran once and is not repeated.",
                lastError));
        }
    }

    /// <summary>
    /// Checks any readiness this control needs before it is acted on. Throw
    /// <see cref="ElementNotReadyException"/> when it is not ready; the call asks again.
    /// </summary>
    protected virtual void EnsureReadyForActionCore(IWinFormsElement element)
    {
    }

    protected TScope RunSetWithElement<T>(T? value, Action<IWinFormsElement> coreOperation,
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
    protected T? RunGetWithOptionalElement<T>(Func<IWinFormsElement?, T> coreOperation,
        int? timeoutMs = null, [CallerMemberName] string? caller = null)
        => Calls.GetWithOptionalElement(coreOperation, timeoutMs, caller ?? nameof(RunGetWithOptionalElement));

    protected T? RunGetWithElement<T>(Func<IWinFormsElement, T> coreOperation,
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
    protected TScope RunAssertWithElement<T>(T? expected, Func<IWinFormsElement, T?> getActual,
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

    /// <summary>A plain string is an AutomationId: a WinForms control's <c>Name</c>.</summary>
    protected static Locator ResolveLocator(string automationId)
    {
        if (string.IsNullOrWhiteSpace(automationId))
        {
            throw new ArgumentNullException(nameof(automationId));
        }

        return Locator.ByAutomationId(automationId);
    }

    #region Keys

    /// <summary>Types keys into the control with the keyboard, through <c>PhysicalInput</c> (AD-005).</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="keys">The keys to send.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SendKeysCore(IWinFormsElement element, string keys, int? timeoutMs = null)
    {
        element.SendKeys(keys, TextInputMethod.Keys);
    }

    #endregion

    #region Visible

    /// <summary>Whether the control is shown; null when it is not there.</summary>
    /// <param name="element">The pre-found element, or null.</param>
    [AbsenceTolerant]
    protected virtual bool? IsVisibleCore(IWinFormsElement? element)
    {
        return element?.Visible;
    }

    #endregion

    #region Enabled

    /// <summary>Throws <see cref="ElementNotReadyException"/> (Disabled) when the element is not enabled.</summary>
    protected virtual void EnsureEnabledCore(IWinFormsElement element)
    {
        if (IsEnabledCore(element) != true)
        {
            throw new ElementNotReadyException(Locator, NotReadyReason.Disabled);
        }
    }

    /// <summary>Whether the control is enabled.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsEnabledCore(IWinFormsElement? element)
    {
        return element?.Enabled;
    }

    #endregion

    #region Exists

    // Hand-written, as on MAUI and Html: Exists answers bool, not bool?, and never fails on absence.
    private static bool? IsExistsBase(IWinFormsElement? element) => element != null;

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
    /// <param name="element">The pre-found element.</param>
    [GenerateComparisons(Comparison.EqualTo | Comparison.Contains | Comparison.StartsWith
        | Comparison.EndsWith | Comparison.Empty)]
    protected virtual string? GetTextCore(IWinFormsElement element)
    {
        return element.Text;
    }

    /// <summary>The text now, or <c>null</c> when the control is not there. Does not wait (R7).</summary>
    public string? TryGetText() => TryFindElement() is { } element ? GetTextCore(element) : null;

    /// <summary>Waits until the text satisfies <paramref name="until"/> (a count above zero, a value that changed).</summary>
    public bool WaitText(Func<string?, bool> until, int? timeoutMs = null)
        => RunWaitForValue(element => GetTextCore(element), until, timeoutMs);

    #endregion

    #region Help text

    /// <summary>The control's help text: its tooltip or placeholder, as UI Automation reads it.</summary>
    /// <param name="element">The pre-found element.</param>
    [GenerateComparisons(Comparison.EqualTo | Comparison.Contains)]
    protected virtual string? GetHelpTextCore(IWinFormsElement element)
    {
        return element.HelpText;
    }

    #endregion

    #region Attributes

    /// <summary>A named UI Automation property of the control (<see cref="IWinFormsElement.GetAttribute"/>).</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="name">The attribute's name.</param>
    protected virtual string? GetAttributeCore(IWinFormsElement element, string? name)
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
    /// Scrolls the element into view, as one call. Scrolling is how the element becomes visible,
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

    /// <summary>Scrolls the element into view through its ScrollItem pattern.</summary>
    /// <param name="element">The pre-found element.</param>
    [SkipGeneration("Hand-written above: scrolling is how the element becomes visible, so the call does not wait for visibility first.")]
    protected virtual void ScrollIntoViewCore(IWinFormsElement element)
    {
        element.ScrollIntoView(CallRemainingMs);
    }

    #endregion
}
