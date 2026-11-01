using System.Globalization;
using Bruinet.Uia;
using Microsoft.Maui.Controls;

namespace Bruinet.Maui.AppSupport.Uia;

/// <summary>
/// Runs a verb against a MAUI element: on the UI thread, within a budget, by lookup.
/// </summary>
/// <remarks>
/// <para>
/// <b>The threading model is here from the start deliberately.</b> UI Automation calls a
/// provider on whatever thread it likes, and touching a MAUI element off the UI thread is
/// undefined at best. Retrofitting the marshalling later would mean revisiting every verb, so
/// no verb is ever written without it.
/// </para>
/// <para>
/// <b>And so is the timeout.</b> A provider that waits forever does not fail the client - it
/// hangs it, and UI Automation's own transaction timeout then stalls every accessibility client
/// on the desktop rather than only the test. A bounded wait that returns
/// <see cref="HResults.UIA_E_TIMEOUT"/> is the difference between a failing test and a wedged
/// machine.
/// </para>
/// <para>
/// <b>No ladder.</b> There used to be one - sink, then the public MAUI API for the control's
/// type, then a matching recognizer's command, then refusal - and two of its rungs performed a
/// gesture inside the <c>when</c> clause that decided whether they matched, so a fall-through
/// left the app changed with nothing recording it. What each verb does on each element is now
/// settled when the element is published (<see cref="VerbBindings"/>), and this looks the answer
/// up. Refusal is still a real answer: it tells a test that the element does not do this, rather
/// than to retry.
/// </para>
/// <para>
/// Verbs outside the gesture range are still dispatched by name below, and move into the table
/// as their own steps land. A switch on the verb is dispatch; it was the switch on <i>mechanism</i>
/// that was the ladder.
/// </para>
/// </remarks>
internal static class MauiVerbDispatcher
{
    /// <summary>
    /// How long a verb may take before the bridge gives up on it.
    /// </summary>
    /// <remarks>
    /// Generous for a UI action and far below UI Automation's own transaction timeout, so a
    /// stuck verb reports itself rather than being reported by the client as a dead connection.
    /// </remarks>
    private const int TimeoutMs = 5_000;

    internal static int Invoke(
        VisualElement element, VerbPlan plan, BruinetVerb verb, int arg1, int arg2)
    {
        var hr = OnUiThread(element, () => PerformInvoke(element, plan, verb, arg1, arg2));

        // Reported from inside the app, because from the test side a verb that ran and had no
        // effect is indistinguishable from one that never arrived.
        BridgeDiagnostics.Report(
            $"Invoke {verb}({arg1},{arg2}) on '{element.AutomationId}' "
            + $"[{element.GetType().Name}] -> 0x{hr:X8}");

        return hr;
    }

    internal static int Exchange(
        VisualElement element,
        VerbPlan plan,
        BruinetVerb verb,
        string argument,
        out string result)
    {
        // Answered before marshalling, and it has to be.
        //
        // IsIdle works by posting to the dispatcher and waiting for the callback to run: that is
        // what "everything queued before now has finished" means. Run on the UI thread - which is
        // where every other verb runs - it would be waiting for a post it is itself blocking, and
        // it deadlocked until its budget expired. The dispatch has to be started from the calling
        // thread, so this branch sits above the marshalling rather than inside it.
        if (verb == BruinetVerb.IsIdle)
        {
            var budget = int.TryParse(
                argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out var asked)
                ? asked
                : DefaultIdleBudgetMs;

            result = MauiCapabilities.IsIdle(element, budget).ToString();
            return HResults.S_OK;
        }

        string? captured = null;

        var hr = OnUiThread(
            element,
            () => PerformExchange(element, plan, verb, argument, out captured));

        result = captured ?? string.Empty;

        // Logged like Invoke, and for the same reason: from the test side a verb that ran and
        // decided something is indistinguishable from one that never arrived. Reads outnumber
        // actions on this path, so leaving it out left the busier half of the bridge silent.
        BridgeDiagnostics.Report(
            $"Exchange {verb}('{argument}') on '{element.AutomationId}' "
            + $"[{element.GetType().Name}] -> 0x{hr:X8} '{result}'");

        return hr;
    }

    /// <summary>
    /// Answers a numeric verb: the bound handler, or a name-dispatched one, or refusal.
    /// </summary>
    /// <remarks>
    /// The lookup comes first and is the whole gesture story. What follows is the ranges that
    /// have not moved into the table yet - each a plain switch on the verb, with no second
    /// mechanism behind it to fall through to.
    /// </remarks>
    private static int PerformInvoke(
        VisualElement element, VerbPlan plan, BruinetVerb verb, int arg1, int arg2)
    {
        if (plan.For(verb) is { } binding)
        {
            return binding.Perform(element, arg1, arg2);
        }

        switch (verb)
        {
            case BruinetVerb.Focus:
                return MauiCapabilities.TryFocus(element)
                    ? HResults.S_OK
                    : HResults.UIA_E_NOTSUPPORTED;

            case BruinetVerb.Unfocus:
                MauiCapabilities.Unfocus(element);
                return HResults.S_OK;

            case BruinetVerb.CloseFlyout when element is SwipeView openSwipeView:
                MauiCapabilities.CloseSwipeView(openSwipeView);
                return HResults.S_OK;

            // A Shell's flyout, which is one public property and a chrome button nothing can
            // find. Both answers matter: S_OK opened it, S_FALSE means it was already like that.
            case BruinetVerb.OpenFlyout:
                return MauiCapabilities.PresentFlyout(element, presented: true);

            case BruinetVerb.CloseFlyout:
                return MauiCapabilities.PresentFlyout(element, presented: false);

            case BruinetVerb.ScrollToIndex:
                return MauiCapabilities.ScrollToIndex(element, arg1)
                    ? HResults.S_OK
                    : HResults.BRUINET_E_DECLINED;

            case BruinetVerb.SelectIndex:
                // Its own four answers, passed through. A negative index is the caller's bug, an
                // index past the end is a list that may still be filling, a control that is not a
                // picker is a refusal, and a binding that declines the value is none of those.
                return MauiCapabilities.SelectIndex(element, arg1);

            case BruinetVerb.NavigateBack:
                // The verb's own answer, passed through rather than flattened. It distinguishes
                // popped (S_OK) from nothing-to-pop (BRUINET_E_DECLINED) from a stale target
                // (UIA_E_ELEMENTNOTAVAILABLE) from not-a-page (UIA_E_NOTSUPPORTED), and the
                // caller does something different for each. Collapsing all four into a bool is
                // what made the client guess, and the guess was a two-second timeout that fired
                // on the commonest answer of the four.
                return MauiCapabilities.NavigateBack(element);
        }

        // Not a crash and not a retry - a fact about this element.
        return HResults.UIA_E_NOTSUPPORTED;
    }

    private static int PerformExchange(
        VisualElement element,
        VerbPlan plan,
        BruinetVerb verb,
        string argument,
        out string? result)
    {
        result = null;

        // The sink owns what it named, on this path too. It is asked because the declaration
        // says so, not to find out whether it wants the verb.
        if (plan.ExchangesThroughSink(verb))
        {
            result = plan.SinkExchange(element, verb, argument);
            return HResults.S_OK;
        }

        switch (verb)
        {
            case BruinetVerb.IsFocused:
                result = element.IsFocused ? bool.TrueString : bool.FalseString;
                return HResults.S_OK;

            case BruinetVerb.GetState:
                result = ReadState(element, argument);
                return result is null ? HResults.UIA_E_NOTSUPPORTED : HResults.S_OK;

            case BruinetVerb.GetText:
                if (!MauiCapabilities.TryGetText(element, out var current))
                {
                    return HResults.UIA_E_NOTSUPPORTED;
                }

                result = current;
                return HResults.S_OK;

            case BruinetVerb.SetText:
                return Written(element, argument, out result);

            case BruinetVerb.ClearText:
                // The argument is ignored rather than rejected. Clearing is setting to empty,
                // and a client that sends the empty string it is about to write should not have
                // to know which of the two spellings this end prefers.
                return Written(element, string.Empty, out result);

            case BruinetVerb.AppendText:
                // Read and write on the same UI-thread pass, which is what makes this safe:
                // splitting it into two verbs would let anything the app does in between land
                // in the middle, and the client would be writing over a value it no longer has.
                if (!MauiCapabilities.TryGetText(element, out var existing))
                {
                    return HResults.UIA_E_NOTSUPPORTED;
                }

                return Written(element, existing + argument, out result);

            case BruinetVerb.Submit:
                return MauiCapabilities.TrySubmit(element)
                    ? HResults.S_OK
                    : HResults.UIA_E_NOTSUPPORTED;

            case BruinetVerb.CurrentRoute:
                result = MauiCapabilities.CurrentRoute(element);
                return result is null ? HResults.UIA_E_NOTSUPPORTED : HResults.S_OK;

            // Scrolling. The offset and the extent together, because neither means anything
            // alone: a percentage cannot tell a short page that cannot scroll from a long one
            // already at the end, and that is the assertion tests actually want to make.
            case BruinetVerb.ScrollPosition when element is ScrollView positioned:
                result = MauiCapabilities.ReadScrollPosition(positioned);
                return HResults.S_OK;

            case BruinetVerb.ScrollTo when element is ScrollView scroller:
                // UIA_E_ELEMENTNOTAVAILABLE rather than a refusal: the scroller does this verb
                // perfectly well, and what was missing is the descendant the caller named.
                return MauiCapabilities.ScrollTo(scroller, argument)
                    ? HResults.S_OK
                    : HResults.UIA_E_ELEMENTNOTAVAILABLE;

            // A menu item, raised by id without opening the menu. The only route there is: MAUI
            // publishes no AutomationId for menu chrome on Windows, so the item is not in the
            // tree the client is searching.
            case BruinetVerb.InvokeMenuItem:
                {
                    // The outcome travels as a value: a success HRESULT cannot carry it, because UI
                    // Automation reports every one of them to the client as S_OK.
                    var hr = MauiCapabilities.InvokeMenuItem(element, argument, out var outcome);
                    result = outcome;
                    return hr;
                }

            // A toolbar item, raised by id. Its automation peer accepts Invoke and raises nothing
            // on Windows, so without this the only route was the mouse.
            case BruinetVerb.InvokeToolbarItem:
                {
                    var hr = MauiCapabilities.InvokeToolbarItem(element, argument, out var outcome);
                    result = outcome;
                    return hr;
                }

            // What the alert on screen is asking. Answered by any published element, because it
            // is a question about the app rather than about a control - and answered with an
            // empty payload when nothing is open, which is a fact rather than a refusal.
            case BruinetVerb.CurrentAlert:
                result = BruinetAlerts.Current();
                return HResults.S_OK;

            // Selection. The item now shown comes back with the answer, so a caller that asked
            // for a text two items share can see which one it got without asking again.
            case BruinetVerb.SelectByText:
                {
                    var hr = MauiCapabilities.SelectByText(element, argument, out var landed);
                    result = landed;
                    return hr;
                }

            // Dates and times. Two properties, replacing the calendar and clock flyout
            // navigation the client used to have to perform.
            //
            // E_INVALIDARG separates "you sent me nonsense" from "I do not do this", which the
            // old single refusal could not. A malformed date is the caller's bug; a DatePicker
            // that cannot set a date would be ours.
            case BruinetVerb.SetDate when element is DatePicker datePicker:
                if (!DateTime.TryParseExact(
                        argument,
                        MauiCapabilities.DateFormat,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var date))
                {
                    return HResults.E_INVALIDARG;
                }

                MauiCapabilities.SetDate(datePicker, date);

                // What the control now holds, not what was sent. A DatePicker with a
                // MinimumDate or MaximumDate clamps, and the caller should see that here
                // rather than discover it in an assertion about something else.
                result = MauiCapabilities.ReadDate(datePicker);
                return result == argument ? HResults.S_OK : HResults.S_FALSE;

            case BruinetVerb.SetTime when element is TimePicker timePicker:
                if (!TimeSpan.TryParseExact(
                        argument,
                        MauiCapabilities.TimeFormat,
                        CultureInfo.InvariantCulture,
                        out var time))
                {
                    return HResults.E_INVALIDARG;
                }

                MauiCapabilities.SetTime(timePicker, time);
                result = MauiCapabilities.ReadTime(timePicker);
                return result == argument ? HResults.S_OK : HResults.S_FALSE;
        }

        // Dates, routes and selection are steps 20 through 26. Refusing them by falling through
        // to a named refusal rather than to nothing keeps the boundary explicit: this build
        // genuinely does not implement them, and a client should be told so rather than left to
        // infer it from a value that never changed.
        return HResults.UIA_E_NOTSUPPORTED;
    }

    /// <summary>
    /// Writes text to the element and reports what the element made of it.
    /// </summary>
    /// <remarks>
    /// The write is not assumed to have taken. A field with a <c>MaxLength</c>, a converter or
    /// a two-way binding that rejects the value leaves something other than what was sent, and
    /// returning the value now in the control lets the client see that without a second round
    /// trip. <see cref="HResults.S_FALSE"/> says so explicitly.
    /// </remarks>
    private static int Written(VisualElement element, string text, out string? actual)
    {
        actual = null;

        if (!MauiCapabilities.TrySetText(element, text))
        {
            return HResults.UIA_E_NOTSUPPORTED;
        }

        MauiCapabilities.TryGetText(element, out var landed);
        actual = landed;

        return landed == text ? HResults.S_OK : HResults.S_FALSE;
    }

    /// <summary>
    /// Reads a property of the element that a user could perceive.
    /// </summary>
    /// <remarks>
    /// <c>BindingContext</c> and arbitrary reflection stay out, deliberately. If an assertion
    /// needs the view model then it is a view-model test and belongs in
    /// <c>Bruinet.Maui.Tests</c>, where it runs in milliseconds without a window.
    /// </remarks>
    /// <summary>How long <c>IsIdle</c> waits for the dispatcher when the caller says nothing.</summary>
    /// <remarks>
    /// Long enough for a page's own layout and bindings to run, short enough that asking is not
    /// itself a sleep. A caller that needs longer says so.
    /// </remarks>
    private const int DefaultIdleBudgetMs = 2000;

    private static string? ReadState(VisualElement element, string property) => property switch
    {
        "IsVisible" => element.IsVisible.ToString(),
        "NavigationDepth" => MauiCapabilities.NavigationDepth(element)?
            .ToString(CultureInfo.InvariantCulture),
        // Each of these replaces a client-side inference that could be true for the wrong
        // reason. See MauiCapabilities for what each one was inferring from.
        "Source" when element is Image image => MauiCapabilities.ReadImageSource(image),
        "IsLoading" when element is Image loading
            => MauiCapabilities.IsImageLoading(loading).ToString(),
        "Progress" when element is ProgressBar bar
            => MauiCapabilities.ReadProgress(bar).ToString(CultureInfo.InvariantCulture),
        "Date" when element is DatePicker datePicker => MauiCapabilities.ReadDate(datePicker),
        "Value" when element is Stepper stepper
            => stepper.Value.ToString(CultureInfo.InvariantCulture),
        "Minimum" when element is Stepper stepper
            => stepper.Minimum.ToString(CultureInfo.InvariantCulture),
        "Maximum" when element is Stepper stepper
            => stepper.Maximum.ToString(CultureInfo.InvariantCulture),
        "Increment" when element is Stepper stepper
            => stepper.Increment.ToString(CultureInfo.InvariantCulture),

        // A picker's own list and its own position in it. Read from outside, both meant opening
        // the dropdown and walking the popup - and the index was then derived by matching the
        // selected text against the item texts, which is wrong for any picker holding two items
        // that read alike. The app has the number.
        "SelectedIndex" when element is Picker selected
            => selected.SelectedIndex.ToString(CultureInfo.InvariantCulture),
        "SelectedItem" when element is Picker showing
            => MauiCapabilities.ReadSelectedItem(showing),
        "ItemCount" when element is Picker counted
            => counted.Items.Count.ToString(CultureInfo.InvariantCulture),
        "ItemCount" when element is ItemsView itemsView
            => MauiCapabilities.Count(itemsView.ItemsSource).ToString(CultureInfo.InvariantCulture),
        "Items" when element is Picker listed => MauiCapabilities.ReadItems(listed),

        // A carousel's list publishes its cards but not which one is current. The Scroll pattern
        // percentage is no substitute: it moves under a UI Automation scroll that Position never
        // follows.
        "Position" when element is CarouselView carousel
            => carousel.Position.ToString(CultureInfo.InvariantCulture),

        // An IndicatorView draws its dots with no automation element at all on Windows, so the
        // selected dot and the number of dots are only answerable here.
        "Position" when element is IndicatorView indicator
            => indicator.Position.ToString(CultureInfo.InvariantCulture),
        "Count" when element is IndicatorView indicated
            => indicated.Count.ToString(CultureInfo.InvariantCulture),
        "Time" when element is TimePicker timePicker => MauiCapabilities.ReadTime(timePicker),
        "FlyoutIsPresented" => MauiCapabilities.IsFlyoutPresented(element)?.ToString(),
        "IsEnabled" => element.IsEnabled.ToString(),
        "IsFocused" => element.IsFocused.ToString(),
        "AutomationId" => element.AutomationId ?? string.Empty,

        // Where the element is, as "left,top,width,height" in physical pixels.
        //
        // A verb rather than a published property, and the difference is not cosmetic. UI
        // Automation reads an element's bounds every time anything walks past it, and reading
        // MAUI layout means a hop onto the app's UI thread - so publishing this made every tree
        // walk in the client cost a round trip per bridge element, and took the suite from four
        // minutes to over twenty. Asked as a verb, it is paid for once by whoever wants it.
        "Bounds" => ElementBounds.TryGetScreenBounds(element, out var bounds)
            ? string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0},{1},{2},{3}",
                bounds.Left, bounds.Top, bounds.Width, bounds.Height)
            : null,

        _ => null,
    };

    /// <summary>
    /// Runs the work on the element's dispatcher and waits, bounded.
    /// </summary>
    /// <remarks>
    /// The <c>IsDispatchRequired</c> check is not an optimisation. UI Automation can call a
    /// provider on the UI thread; queueing the work there and then blocking that same thread
    /// waiting for it is a deadlock that resolves only when the timeout expires.
    /// </remarks>
    private static int OnUiThread(VisualElement element, Func<int> work)
        => Marshal(element, work, HResults.UIA_E_TIMEOUT, HResults.UIA_E_ELEMENTNOTAVAILABLE);

    /// <summary>
    /// Runs anything that touches the UI framework on the UI thread, bounded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not only verbs.</b> UI Automation calls a provider's property getters from its own
    /// threads, far more often than it calls its methods, and a WinUI object read from the wrong
    /// thread does not throw something catchable - it raises a stowed exception that fails the
    /// process fast, inside <c>Microsoft.UI.Xaml.dll</c>, with no managed stack. That looks
    /// exactly like the automation tree collapsing, which is a different and far more alarming
    /// diagnosis. Everything that reaches a platform view goes through here.
    /// </para>
    /// <para>
    /// The <c>IsDispatchRequired</c> check is not an optimisation. UI Automation can call a
    /// provider on the UI thread; queueing work there and then blocking that same thread waiting
    /// for it is a deadlock that resolves only when the timeout expires.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">What the work returns.</typeparam>
    /// <param name="element">The element whose dispatcher to use.</param>
    /// <param name="work">The work. Must not throw; it is guarded anyway.</param>
    /// <param name="onTimeout">What to return if the UI thread does not get to it in time.</param>
    /// <param name="onFailure">What to return if the work throws or cannot be queued.</param>
    /// <returns>The result, or one of the two fallbacks.</returns>
    internal static T Marshal<T>(VisualElement element, Func<T> work, T onTimeout, T onFailure)
    {
        var dispatcher = element.Dispatcher;

        if (dispatcher is null || !dispatcher.IsDispatchRequired)
        {
            return Guarded(work, onFailure);
        }

        var result = onTimeout;
        using var completed = new ManualResetEventSlim(false);

        if (!dispatcher.Dispatch(() =>
            {
                try
                {
                    result = Guarded(work, onFailure);
                }
                finally
                {
                    completed.Set();
                }
            }))
        {
            return onFailure;
        }

        // On timeout the queued work may still run later. It is idempotent per verb and acts on
        // the app's own UI, so letting it complete is safer than trying to cancel it; the client
        // is told the verb did not finish in time, which is the truth.
        return completed.Wait(TimeoutMs) ? result : onTimeout;
    }

    private static T Guarded<T>(Func<T> work, T onFailure)
    {
        try
        {
            return work();
        }
        catch (Exception)
        {
            // The app's own code runs inside a verb - a command, a sink, a property setter.
            // When it throws, the bridge reports a failure and the app carries on; it does not
            // take the process down and it does not let the exception reach COM.
            return onFailure;
        }
    }
}
