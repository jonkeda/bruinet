using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using Bruinet.Core.Recording;
using Bruinet.Core.Scopes;
using Bruinet.Core.Utilities;

namespace Bruinet.Core.Calls;

/// <summary>
/// What a stack's control base gives <see cref="ControlCalls{TElement}"/>: its element, scope and
/// settings. Each member is one attempt's worth; nothing here waits (R2).
/// </summary>
/// <typeparam name="TElement">The stack's element interface.</typeparam>
public interface IControlHost<TElement>
    where TElement : class
{
    /// <summary>The control's locator, for messages and the scroll throttle.</summary>
    Locator Locator { get; }

    /// <summary>A new call for the log, named for the control and its page.</summary>
    ControlCall NewCall();

    /// <summary>The budget of a call given no timeout.</summary>
    int DefaultTimeoutMs { get; }

    /// <summary>The pause between attempts.</summary>
    int PollingIntervalMs { get; }

    /// <summary>The scroll throttle's interval.</summary>
    int AnimationMs { get; }

    /// <summary>The scope chain's readiness, one probe (R4).</summary>
    ScopeReadiness ProbeScope();

    /// <summary>The element for an attempt: one lookup, and the stack's way to bring it into the tree when it has one.</summary>
    TElement? Locate(AttemptContext attempt);

    /// <summary>One plain lookup, for a call where the element may be absent.</summary>
    TElement? TryFind();

    /// <summary>Whether the element is on screen; null when unknown.</summary>
    bool? IsVisible(TElement element);

    /// <summary>Scrolls the element into view, within <paramref name="timeoutMs"/>.</summary>
    void ScrollIntoView(TElement element, int timeoutMs);

    /// <summary>What the scroll throttle counts scrolls by: the element or the locator.</summary>
    string ScrollKey(TElement element);

    /// <summary>The element's identity, or null when it is gone.</summary>
    string? InstanceKey(TElement element);

    /// <summary>Whether an action checks visibility first, or the element's own action waits for it (Playwright's actionability).</summary>
    bool ChecksVisibilityBeforeAction(TElement element);

    /// <summary>A control's own readiness for an action; throws <see cref="ElementNotReadyException"/> when it is short.</summary>
    void EnsureReadyForAction(TElement element);

    /// <summary>The error for a control that could not be found.</summary>
    ElementNotFoundException NotFound();

    /// <summary>What the host is called in a message: "control", or "container" for a scope's root.</summary>
    string Noun => "control";

    /// <summary>Whether a read or an assertion checks that the element is visible (a container's root is not checked).</summary>
    bool ChecksVisibility => true;

    /// <summary>The element went stale in an attempt: a host that keeps it forgets it.</summary>
    void OnStale()
    {
    }
}

/// <summary>
/// The call model's members for a control, once for every stack (.docs/contracts/call-model.md):
/// every public member of a control is one call with one log pair, one budget and one poll. Each
/// attempt checks the scope, finds the element again, makes one visibility check, then reads,
/// compares or checks readiness (R1, R2, R4). An action runs once, after the poll (R0). The stacks'
/// <c>Run*</c> members keep their names (R8) and call these.
/// </summary>
/// <typeparam name="TElement">The stack's element interface.</typeparam>
public sealed class ControlCalls<TElement>(IControlHost<TElement> host)
    where TElement : class
{
    private int Budget(int? timeoutMs) => timeoutMs ?? host.DefaultTimeoutMs;

    /// <summary>The failure of a phase that ran out of budget, from what it last saw.</summary>
    public Exception Failure(AttemptContext attempt, string caller, int budgetMs)
        => attempt.Log.ToException(
            host.Locator,
            budgetMs,
            host.NotFound,
            readiness => ScopeGate.NotReady(readiness, caller, $"{host.Noun} '{host.Locator}'", budgetMs));

    private Observation? CheckScope() => ScopeGate.Check(host.ProbeScope());

    /// <summary>The element's identity, or null when it is already gone.</summary>
    private string? KeyOf(TElement element)
    {
        try
        {
            return host.InstanceKey(element);
        }
        catch (StaleElementException)
        {
            return null;
        }
    }

    /// <summary>
    /// One attempt against the element: the scope, then a fresh lookup, then (optionally) one
    /// visibility check, then <paramref name="body"/>. An observation after the first attempt
    /// carries the element's identity, so a replaced element is counted.
    /// </summary>
    public Observation Attempt(AttemptContext attempt, bool ensureVisible, Func<TElement, Observation> body)
    {
        if (CheckScope() is { } notReady)
        {
            return notReady;
        }

        var element = host.Locate(attempt);
        if (element == null)
        {
            return Observation.Missing();
        }

        try
        {
            if (ensureVisible && host.ChecksVisibility)
            {
                EnsureVisible(element, attempt);
            }

            var observation = body(element);
            return observation.Kind == ObservationKind.Done && attempt.Log.Attempts == 0
                ? observation
                : observation with { InstanceKey = KeyOf(element) };
        }
        catch (StaleElementException error)
        {
            host.OnStale();
            return Observation.Stale(error, null);
        }
        catch (ElementNotReadyException error)
        {
            return Observation.NotReady(error, KeyOf(element));
        }
        catch (AssertionException error)
        {
            return Observation.Mismatch(error, KeyOf(element));
        }
    }

    /// <summary>
    /// One attempt at "visible": visible now, or scroll it into view (throttled) and look once
    /// more. Throws <see cref="ElementNotReadyException"/> when it is still not visible.
    /// </summary>
    private void EnsureVisible(TElement element, AttemptContext attempt)
    {
        if (host.IsVisible(element) == true)
        {
            return;
        }

        if (attempt.MayScrollIntoView(host.ScrollKey(element)))
        {
            host.ScrollIntoView(element, Math.Max(1, attempt.Deadline.RemainingMs));

            if (host.IsVisible(element) == true)
            {
                return;
            }
        }

        throw new ElementNotReadyException(host.Locator, NotReadyReason.NotVisible);
    }

    private void AwaitScope(AttemptContext attempt, string caller, int budgetMs)
    {
        if (!Poller.Until(_ => CheckScope() ?? Observation.Done(), attempt, host.PollingIntervalMs))
        {
            throw Failure(attempt, caller, budgetMs);
        }
    }

    /// <summary>One call: waits for the scope, then runs <paramref name="operation"/> once.</summary>
    public T Run<T>(string caller, string? value, Func<T> operation, int? timeoutMs)
    {
        var budget = Budget(timeoutMs);
        return host.NewCall().Run(caller, value, budget, host.AnimationMs, attempt =>
        {
            AwaitScope(attempt, caller, budget);
            return operation();
        });
    }

    /// <summary>Polls <paramref name="operation"/> with the scope ready; false when it stayed false.</summary>
    public bool Wait(Func<bool> operation, int? timeoutMs, string caller)
        => WaitPolling(_ => CheckScope() ?? (operation() ? Observation.Done() : Observation.Pending()), timeoutMs, caller);

    /// <summary>Polls a predicate on the element, found again on every attempt.</summary>
    public bool WaitWithElement(Func<TElement, bool> operation, int? timeoutMs, string caller)
        => WaitObserving(element => operation(element) ? Observation.Done() : Observation.Pending(), timeoutMs, caller);

    /// <summary>
    /// Polls <paramref name="until"/> on the value <paramref name="read"/> gives, until it holds.
    /// The last value read is in the call's log when it never did.
    /// </summary>
    public bool WaitForValue(Func<TElement, string?> read, Func<string?, bool> until, int? timeoutMs, string caller)
    {
        ArgumentNullException.ThrowIfNull(until);
        return WaitObserving(element =>
            {
                var value = read(element);
                return until(value) ? Observation.Done() : Observation.Pending($"value '{value}'");
            },
            timeoutMs, caller);
    }

    /// <summary>Polls a predicate that is meaningful when the element is absent.</summary>
    public bool WaitWithOptionalElement(Func<TElement?, bool> operation, int? timeoutMs, string caller,
        Func<TElement?>? resolve = null)
    {
        var find = resolve ?? host.TryFind;
        return WaitPolling(_ => CheckScope() ?? (operation(find()) ? Observation.Done() : Observation.Pending()), timeoutMs, caller);
    }

    private bool WaitObserving(Func<TElement, Observation> observe, int? timeoutMs, string caller)
        => WaitPolling(a => Attempt(a, ensureVisible: true, observe), timeoutMs, caller);

    private bool WaitPolling(Func<AttemptContext, Observation> attemptOnce, int? timeoutMs, string caller)
    {
        var budget = Budget(timeoutMs);
        return host.NewCall().Run(caller, null, budget, host.AnimationMs, attempt =>
        {
            if (Poller.Until(attemptOnce, attempt, host.PollingIntervalMs))
            {
                return true;
            }

            // A predicate that stayed false answers false; a call that never got to ask fails.
            return attempt.Log.Last.Kind == ObservationKind.Pending ? false : throw Failure(attempt, caller, budget);
        }, succeeded: met => met);
    }

    /// <summary>
    /// A read of the element, found again on every attempt; visible first unless
    /// <paramref name="ensureVisible"/> is false (a scroll into view, which is how it becomes visible).
    /// </summary>
    public T? GetWithElement<T>(Func<TElement, T> operation, int? timeoutMs, string caller, bool ensureVisible = true)
    {
        var budget = Budget(timeoutMs);
        return host.NewCall().Run(caller, null, budget, host.AnimationMs, attempt =>
        {
            var value = default(T);
            if (!Poller.Until(a => Attempt(a, ensureVisible, element =>
                {
                    value = operation(element);
                    return Observation.Done();
                }), attempt, host.PollingIntervalMs))
            {
                throw Failure(attempt, caller, budget);
            }

            return value;
        });
    }

    /// <summary>A read that is meaningful when the element is absent: once, after the scope is ready.</summary>
    public T? GetWithOptionalElement<T>(Func<TElement?, T> operation, int? timeoutMs, string caller)
        => Run(caller, null, () => operation(host.TryFind()), timeoutMs);

    /// <summary>An assertion on a value read without the element, with the scope ready.</summary>
    public void Assert<T>(T? expected, Func<T?> getActual, Func<T?, T?, bool> compare, string? message,
        int? timeoutMs, string caller)
        => AssertPolling(caller, timeoutMs, _ =>
        {
            if (CheckScope() is { } notReady)
            {
                return notReady;
            }

            return Compare(getActual(), expected, compare, message, null);
        });

    /// <summary>An assertion on the element, found again on every attempt.</summary>
    public void AssertWithElement<T>(T? expected, Func<TElement, T?> getActual, Func<T?, T?, bool> compare,
        string? message, int? timeoutMs, string caller)
        => AssertPolling(caller, timeoutMs, a => Attempt(a, ensureVisible: true,
            element => Compare(getActual(element), expected, compare, message, null)));

    /// <summary>An assertion on a value that is meaningful when the element is absent.</summary>
    public void AssertWithOptionalElement<T>(T? expected, Func<TElement?, T?> getActual, Func<T?, T?, bool> compare,
        string? message, int? timeoutMs, string caller, Func<TElement?>? resolve = null)
    {
        var find = resolve ?? host.TryFind;
        AssertPolling(caller, timeoutMs, _ =>
        {
            if (CheckScope() is { } notReady)
            {
                return notReady;
            }

            return Compare(getActual(find()), expected, compare, message, null);
        });
    }

    private static Observation Compare<T>(T? actual, T? expected, Func<T?, T?, bool> compare, string? message, string? key)
        => compare(actual, expected)
            ? Observation.Done()
            : Observation.Mismatch(new AssertionException(message ?? "Assert exception", expected, actual), key);

    private void AssertPolling(string caller, int? timeoutMs, Func<AttemptContext, Observation> attemptOnce)
    {
        var budget = Budget(timeoutMs);
        host.NewCall().Run(caller, null, budget, host.AnimationMs, attempt =>
        {
            if (!Poller.Until(attemptOnce, attempt, host.PollingIntervalMs))
            {
                throw Failure(attempt, caller, budget);
            }

            return true;
        });
    }

    /// <summary>
    /// An action as one call: polls until the control is ready to be acted on, then acts once,
    /// unless the action reports that it did not act (<see cref="ElementNotReadyException"/>).
    /// </summary>
    public void Act(Action<TElement> act, bool ensureVisible, int? timeoutMs, string caller, string? value = null)
    {
        var budget = Budget(timeoutMs);

        // While a video records the action takes at least its pause, outside the call's own time,
        // so the log and the near-misses stay as a test run has them.
        var paced = ActionPacing.Start();
        var succeeded = false;
        try
        {
            host.NewCall().Run(caller, value, budget, host.AnimationMs, attempt =>
            {
                ActOnce(attempt, ensureVisible, caller, budget, act);
                return true;
            });
            succeeded = true;
        }
        finally
        {
            ActionPacing.End(paced, succeeded);
        }
    }

    private void ActOnce(AttemptContext attempt, bool ensureVisible, string caller, int budgetMs, Action<TElement> act)
    {
        while (true)
        {
            var element = ResolveReady(attempt, ensureVisible, caller, budgetMs);
            try
            {
                act(element);
                return;
            }
            catch (ElementNotReadyException error) when (attempt.Deadline.RemainingMs > 0)
            {
                attempt.Log.Add(Observation.NotReady(error, null), attempt.Deadline.ElapsedMs);
                WaitHelper.Pause(Math.Max(1, Math.Min(host.PollingIntervalMs, attempt.Deadline.RemainingMs)));
            }
            catch (ElementNotReadyException error) when (error.Locator != host.Locator)
            {
                // The element reported it by its own selector (Playwright's actionability); the call names the control's.
                throw new ElementNotReadyException(host.Locator, error.Reason, error.Detail);
            }
        }
    }

    private TElement ResolveReady(AttemptContext attempt, bool ensureVisible, string caller, int budgetMs)
    {
        TElement? ready = null;
        if (!Poller.Until(a => Attempt(a, ensureVisible: false, element =>
            {
                // An element whose actions wait for actionability themselves is not checked twice.
                if (ensureVisible && host.ChecksVisibilityBeforeAction(element))
                {
                    EnsureVisible(element, a);
                }

                host.EnsureReadyForAction(element);
                ready = element;
                return Observation.Done();
            }), attempt, host.PollingIntervalMs))
        {
            throw Failure(attempt, caller, budgetMs);
        }

        return ready!;
    }

    /// <summary>
    /// Waits, inside an action, for the effect of what the action has already done, with a budget
    /// of its own (R3). Never repeats the action (R0).
    /// </summary>
    public Confirmation<T> Confirm<T>(Func<T?> read, Func<T?, bool> done, int? timeoutMs)
        => Confirmer.Run(read, done, timeoutMs ?? host.DefaultTimeoutMs, host.PollingIntervalMs);
}
