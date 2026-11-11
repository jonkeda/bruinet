using System.Drawing;
using Bruinet.Uia;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace Bruinet.Maui.FlaUI.Bridge;

/// <summary>
/// Sends one verb to the app under test, and says what came back.
/// </summary>
internal static class BridgeVerbRunner
{
    /// <summary>What the app under test declared an element can do.</summary>
    /// <param name="root">The app's top-level window.</param>
    /// <param name="automation">The session.</param>
    /// <param name="automationId">The MAUI <c>AutomationId</c> of the target.</param>
    /// <param name="near">The real element's bounds, to pick among targets sharing the id.</param>
    /// <returns>The verbs, or empty if there is no bridge target for it.</returns>
    internal static IReadOnlyList<BruinetVerb> Verbs(
        AutomationElement root,
        UIA3Automation automation,
        string? automationId,
        Func<Rectangle>? near = null)
    {
        if (string.IsNullOrWhiteSpace(automationId))
        {
            return [];
        }

        return BruinetBridgeLookup.Find(root, automation, automationId, near)?.SupportedVerbs() ?? [];
    }

    /// <summary>Whether an element declares a verb.</summary>
    /// <param name="root">The app's top-level window.</param>
    /// <param name="automation">The session.</param>
    /// <param name="automationId">The MAUI <c>AutomationId</c> of the target.</param>
    /// <param name="verb">The verb.</param>
    /// <param name="near">The real element's bounds, to pick among targets sharing the id.</param>
    /// <returns>Whether the app declared it for this element.</returns>
    internal static bool Supports(
        AutomationElement root,
        UIA3Automation automation,
        string? automationId,
        BruinetVerb verb,
        Func<Rectangle>? near = null)
        => Verbs(root, automation, automationId, near).Contains(verb);

    /// <summary>
    /// Sends a verb on whichever of the two contract methods it travels on.
    /// </summary>
    /// <param name="root">The app's top-level window.</param>
    /// <param name="automation">The session.</param>
    /// <param name="automationId">The MAUI <c>AutomationId</c> of the target.</param>
    /// <param name="verb">The verb.</param>
    /// <param name="argument">Its argument, for a verb that takes one.</param>
    /// <param name="near">The real element's bounds, to pick among targets sharing the id.</param>
    /// <returns>What happened, and what the app returned.</returns>
    private static BridgeVerbResult SendOnce(
        AutomationElement root,
        UIA3Automation automation,
        string? automationId,
        BruinetVerb verb,
        string argument = "",
        Func<Rectangle>? near = null)
        => BruinetVerbs.TransportFor(verb) == BruinetVerbTransport.Exchange
            ? ExchangeOnce(root, automation, automationId, verb, argument, near)
            : InvokeOnce(root, automation, automationId, verb, near: near);

    /// <summary>
    /// Sends a verb that carries no strings.
    /// </summary>
    /// <param name="root">The app's top-level window.</param>
    /// <param name="automation">The session.</param>
    /// <param name="automationId">The MAUI <c>AutomationId</c> of the target.</param>
    /// <param name="verb">The verb.</param>
    /// <param name="arg1">The verb's first argument, where it takes one.</param>
    /// <param name="arg2">The verb's second argument, where it takes one.</param>
    /// <param name="near">The real element's bounds, to pick among targets sharing the id.</param>
    /// <returns>What happened.</returns>
    private static BridgeVerbResult InvokeOnce(
        AutomationElement root,
        UIA3Automation automation,
        string? automationId,
        BruinetVerb verb,
        int arg1 = 0,
        int arg2 = 0,
        Func<Rectangle>? near = null)
    {
        if (!TryResolve(root, automation, automationId, near, out var pattern, out var failure, out _))
        {
            return failure;
        }

        var hr = pattern!.Invoke((int)verb, arg1, arg2);

        return new BridgeVerbResult(
            BruinetVerbFailure.Succeeded(hr),
            hr,
            string.Empty,
            Describe(pattern!, verb, hr));
    }

    /// <summary>
    /// Sends a verb that carries a string in, a string out, or both.
    /// </summary>
    /// <param name="root">The app's top-level window.</param>
    /// <param name="automation">The session.</param>
    /// <param name="automationId">The MAUI <c>AutomationId</c> of the target.</param>
    /// <param name="verb">The verb.</param>
    /// <param name="argument">The verb's argument, or empty where it takes none.</param>
    /// <param name="near">The real element's bounds, to pick among targets sharing the id.</param>
    /// <returns>What happened, and what the app returned.</returns>
    private static BridgeVerbResult ExchangeOnce(
        AutomationElement root,
        UIA3Automation automation,
        string? automationId,
        BruinetVerb verb,
        string argument = "",
        Func<Rectangle>? near = null)
    {
        if (!TryResolve(root, automation, automationId, near, out var pattern, out var failure, out _))
        {
            return failure;
        }

        var hr = pattern!.Exchange((int)verb, argument, out var value);

        return new BridgeVerbResult(
            BruinetVerbFailure.Succeeded(hr),
            hr,
            value,
            Describe(pattern!, verb, hr));
    }

    /// <summary>
    /// Invokes a verb only where the target declares it, telling "not declared" apart from
    /// "declared and refused" in a single walk.
    /// </summary>
    /// <param name="root">The app's top-level window.</param>
    /// <param name="automation">The session.</param>
    /// <param name="automationId">The MAUI <c>AutomationId</c> of the target.</param>
    /// <param name="verb">The verb.</param>
    /// <param name="arg1">The verb's first argument.</param>
    /// <param name="arg2">The verb's second argument.</param>
    /// <param name="near">The real element's bounds, to pick among targets sharing the id.</param>
    /// <param name="declared">Whether the target declared the verb at all.</param>
    /// <returns>What happened.</returns>
    private static BridgeVerbResult InvokeIfDeclaredOnce(
        AutomationElement root,
        UIA3Automation automation,
        string? automationId,
        BruinetVerb verb,
        int arg1,
        int arg2,
        Func<Rectangle>? near,
        out bool declared)
    {
        declared = false;

        if (!TryResolve(root, automation, automationId, near, out var pattern, out var failure,
                out var target))
        {
            return failure;
        }

        if (!target!.SupportedVerbs().Contains(verb))
        {
            return new BridgeVerbResult(
                false,
                HResults.UIA_E_NOTSUPPORTED,
                string.Empty,
                $"the app under test does not declare {verb} on '{automationId}'.");
        }

        declared = true;

        var hr = pattern!.Invoke((int)verb, arg1, arg2);

        return new BridgeVerbResult(
            BruinetVerbFailure.Succeeded(hr),
            hr,
            string.Empty,
            Describe(pattern!, verb, hr));
    }

    /// <summary>
    /// Exchanges a verb only where the target declares it, telling "not declared" apart from
    /// "declared and refused" in a single walk.
    /// </summary>
    /// <param name="root">The app's top-level window.</param>
    /// <param name="automation">The session.</param>
    /// <param name="automationId">The MAUI <c>AutomationId</c> of the target.</param>
    /// <param name="verb">The verb.</param>
    /// <param name="argument">The verb's argument, or empty where it takes none.</param>
    /// <param name="near">The real element's bounds, to pick among targets sharing the id.</param>
    /// <param name="declared">Whether the target declared the verb at all.</param>
    /// <returns>What happened. Not delivered, with <paramref name="declared"/> false, when the
    /// target does not declare the verb or could not be resolved.</returns>
    private static BridgeVerbResult SendIfDeclaredOnce(
        AutomationElement root,
        UIA3Automation automation,
        string? automationId,
        BruinetVerb verb,
        string argument,
        Func<Rectangle>? near,
        out bool declared)
    {
        declared = false;

        if (!TryResolve(root, automation, automationId, near, out var pattern, out var failure,
                out var target))
        {
            return failure;
        }

        // The declaration is read off the target TryResolve already resolved. Calling
        // BruinetBridgeLookup.Find again here would walk the bridge a second time, which is the
        // cost this method exists to remove.
        if (!target!.SupportedVerbs().Contains(verb))
        {
            return new BridgeVerbResult(
                false,
                HResults.UIA_E_NOTSUPPORTED,
                string.Empty,
                $"the app under test does not declare {verb} on '{automationId}'.");
        }

        declared = true;

        var hr = pattern!.Exchange((int)verb, argument, out var value);

        return new BridgeVerbResult(
            BruinetVerbFailure.Succeeded(hr),
            hr,
            value,
            Describe(pattern!, verb, hr));
    }

    /// <summary>Sends a verb on whichever of the two contract methods it travels on.</summary>
    /// <remarks>
    /// <c>onlyRoute</c>: whether the bridge is the caller's only way to do this, so a target not
    /// published yet is waited for (<see cref="UntilPublished"/>). A caller that falls back to UI
    /// Automation passes false, or an element the app never instrumented would cost the window on
    /// every call.
    /// </remarks>
    internal static BridgeVerbResult Send(
        AutomationElement root, UIA3Automation automation, string? automationId, BruinetVerb verb,
        string argument = "", Func<Rectangle>? near = null, bool onlyRoute = false)
        => onlyRoute
            ? UntilPublished(() => SendOnce(root, automation, automationId, verb, argument, near))
            : SendOnce(root, automation, automationId, verb, argument, near);

    /// <summary>Sends a verb that carries no strings.</summary>
    /// <remarks><c>onlyRoute</c> as for <see cref="Send"/>.</remarks>
    internal static BridgeVerbResult Invoke(
        AutomationElement root, UIA3Automation automation, string? automationId, BruinetVerb verb,
        int arg1 = 0, int arg2 = 0, Func<Rectangle>? near = null, bool onlyRoute = false)
        => onlyRoute
            ? UntilPublished(() => InvokeOnce(root, automation, automationId, verb, arg1, arg2, near))
            : InvokeOnce(root, automation, automationId, verb, arg1, arg2, near);

    /// <summary>Sends a verb that carries a string in, a string out, or both.</summary>
    /// <remarks><c>onlyRoute</c> as for <see cref="Send"/>.</remarks>
    internal static BridgeVerbResult Exchange(
        AutomationElement root, UIA3Automation automation, string? automationId, BruinetVerb verb,
        string argument = "", Func<Rectangle>? near = null, bool onlyRoute = false)
        => onlyRoute
            ? UntilPublished(() => ExchangeOnce(root, automation, automationId, verb, argument, near))
            : ExchangeOnce(root, automation, automationId, verb, argument, near);

    /// <summary>Invokes a verb only where the target declares it.</summary>
    /// <remarks>See <see cref="UntilPublished"/>.</remarks>
    internal static BridgeVerbResult InvokeIfDeclared(
        AutomationElement root, UIA3Automation automation, string? automationId, BruinetVerb verb,
        int arg1, int arg2, Func<Rectangle>? near, out bool declared)
    {
        var said = false;
        var result = UntilPublished(
            () => InvokeIfDeclaredOnce(root, automation, automationId, verb, arg1, arg2, near, out said),
            () => said || BruinetBridgeLookup.HasPublished(root, automationId));
        declared = said;
        return result;
    }

    /// <summary>Exchanges a verb only where the target declares it.</summary>
    /// <remarks>See <see cref="UntilPublished"/>.</remarks>
    internal static BridgeVerbResult SendIfDeclared(
        AutomationElement root, UIA3Automation automation, string? automationId, BruinetVerb verb,
        string argument, Func<Rectangle>? near, out bool declared)
    {
        var said = false;
        var result = UntilPublished(
            () => SendIfDeclaredOnce(root, automation, automationId, verb, argument, near, out said),
            () => said || BruinetBridgeLookup.HasPublished(root, automationId));
        declared = said;
        return result;
    }

    /// <summary>
    /// Sends again, for up to a second, while the answer is that the target is not available.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A page pushed in place of another is not on the bridge at once.</b> It publishes its
    /// targets as it loads and the outgoing page withdraws its own as it unloads, so for a few
    /// milliseconds after a navigation an id can be on neither, or only on a target whose element
    /// has gone. Both answer <c>UIA_E_ELEMENTNOTAVAILABLE</c>, and a verb sent then failed once a
    /// full run: a DatePicker's <c>SetDate</c> "did not answer", a Stepper's state read was "not on
    /// the bridge".
    /// </para>
    /// <para>
    /// Asking again is safe because that answer means nothing was done. Every other answer -
    /// performed, refused, not declared, bad argument - is returned at once. The window is the one
    /// <c>NavigateBack</c> gives a page to publish, and an id that is never published pays it once
    /// before the message that names the fix.
    /// </para>
    /// </remarks>
    /// <param name="send">One attempt.</param>
    /// <param name="declared">
    /// For the <c>IfDeclared</c> forms: retry only a target that declares the verb, or an id the
    /// bridge has carried before and is between publications. Their callers fall back to UI
    /// Automation for an element the app never instrumented, and that answer must not cost the
    /// window. An Expander's state read found its own target gone for a moment between Expand and
    /// Collapse on one page, and failed as "does not report IsExpanded".
    /// </param>
    private static BridgeVerbResult UntilPublished(Func<BridgeVerbResult> send, Func<bool>? declared = null)
    {
        var publishWindowMs = Bruinet.Core.Calls.CallBudget.Within(1_000);
        const int pollMs = 25;

        var clock = System.Diagnostics.Stopwatch.StartNew();

        while (true)
        {
            var result = send();

            if (result.Delivered
                || result.HResult != HResults.UIA_E_ELEMENTNOTAVAILABLE
                || declared?.Invoke() == false
                || clock.ElapsedMilliseconds >= publishWindowMs)
            {
                return result;
            }

            Thread.Sleep(pollMs);
        }
    }

    /// <summary>The bridge's targets, the most recently published first.</summary>
    /// <remarks>
    /// A page pushed in place of another publishes its targets after the outgoing page's, which
    /// stay until it unloads. Asked oldest first, an app-wide verb such as <c>InvokeMenuItem</c>
    /// reached the old page: its command ran there and the page on screen never changed (the
    /// context-menu test, two Windows runs in three). The app appends each registration, so the
    /// newest is the page on screen. A question about the app itself - the route, idleness - has
    /// the same answer from any page.
    /// </remarks>
    private static IEnumerable<AutomationElement> NewestFirst(IReadOnlyList<AutomationElement> targets)
    {
        for (var i = targets.Count - 1; i >= 0; i--)
        {
            yield return targets[i];
        }
    }

    /// <summary>
    /// Sends a verb to whichever published element answers it, without naming one.
    /// </summary>
    /// <param name="root">The app's top-level window.</param>
    /// <param name="automation">The session.</param>
    /// <param name="verb">The verb.</param>
    /// <returns>What happened, including that nothing answered.</returns>
    internal static BridgeVerbResult InvokeAnywhere(
        AutomationElement root, UIA3Automation automation, BruinetVerb verb)
    {
        var declined = 0;
        var lastReason = string.Empty;

        foreach (var target in NewestFirst(BruinetBridgeLookup.Targets(root, automation)))
        {
            if (!target.SupportedVerbs().Contains(verb)
                || !target.TryGetBruinetPattern(out var pattern))
            {
                continue;
            }

            var hr = pattern!.Invoke((int)verb, 0, 0);

            if (hr == HResults.S_OK)
            {
                return new BridgeVerbResult(true, hr, string.Empty, Describe(pattern!, verb, hr));
            }

            declined++;
            lastReason = Describe(pattern!, verb, hr);
        }

        return declined == 0
            ? new BridgeVerbResult(
                false,
                HResults.UIA_E_NOTSUPPORTED,
                string.Empty,
                $"no element published on the app's bridge answers {verb}.")
            : new BridgeVerbResult(
                false,
                HResults.BRUINET_E_DECLINED,
                string.Empty,
                $"{declined} element(s) on the app's bridge were asked to {verb} and none did. "
                + $"The last said: {lastReason}",
                declined);
    }

    /// <summary>
    /// Asks any element on the app's bridge a question about the app itself.
    /// </summary>
    /// <param name="root">The app's top-level window.</param>
    /// <param name="automation">The session.</param>
    /// <param name="verb">The verb.</param>
    /// <param name="argument">The argument, if the verb takes one.</param>
    /// <returns>What was answered, including that nothing answered.</returns>
    internal static BridgeVerbResult ExchangeAnywhere(
        AutomationElement root,
        UIA3Automation automation,
        BruinetVerb verb,
        string argument = "")
    {
        var declinedHr = HResults.UIA_E_NOTSUPPORTED;
        var declined = 0;

        foreach (var target in NewestFirst(BruinetBridgeLookup.Targets(root, automation)))
        {
            if (!target.SupportedVerbs().Contains(verb)
                || !target.TryGetBruinetPattern(out var pattern))
            {
                continue;
            }

            var hr = pattern!.Exchange((int)verb, argument, out var answer);

            if (hr == HResults.S_OK)
            {
                return new BridgeVerbResult(
                    true, hr, answer, Describe(pattern!, verb, hr));
            }

            // Remembered, not discarded. A target that answered something other than S_OK said
            // something about the app - the item is disabled, the argument was nonsense - and
            // reporting all of those as "nothing answers" sends the caller looking for a missing
            // declaration, which is the one explanation that has already been ruled out.
            declined++;
            declinedHr = hr;
        }

        return new BridgeVerbResult(
            false,
            declinedHr,
            string.Empty,
            declined == 0
                ? $"no element published on the app's bridge answers {verb}."
                : $"{declined} element(s) were asked and none answered. The last said: "
                  + BruinetVerbFailure.Describe(verb, declinedHr),
            declined);
    }

    // What a failed verb means, and for E_FAIL what the app threw, when the app's bridge can say
    // (GetLastFailure; an app built before it refuses the verb and the plain reason stands).
    private static string Describe(IBruinetAutomationPattern pattern, BruinetVerb verb, int hr)
    {
        var reason = BruinetVerbFailure.Describe(verb, hr);
        if (hr != HResults.E_FAIL)
        {
            return reason;
        }

        return pattern.Exchange((int)BruinetVerb.GetLastFailure, string.Empty, out var thrown) == HResults.S_OK
               && !string.IsNullOrEmpty(thrown)
            ? $"{reason} The app said: {thrown}"
            : reason;
    }

    /// <summary>
    /// Finds the pattern for an element, or explains which of the ways it was missing.
    /// </summary>
    private static bool TryResolve(
        AutomationElement root,
        UIA3Automation automation,
        string? automationId,
        Func<Rectangle>? near,
        out IBruinetAutomationPattern? pattern,
        out BridgeVerbResult failure,
        out AutomationElement? target)
    {
        pattern = null;
        target = null;

        if (string.IsNullOrWhiteSpace(automationId))
        {
            failure = new BridgeVerbResult(
                false,
                HResults.E_INVALIDARG,
                string.Empty,
                "the target has no AutomationId, which is the only join between an element and its "
                + "bridge element. Give it one in the app's markup.");
            return false;
        }

        var element = target = BruinetBridgeLookup.Find(root, automation, automationId, near);

        if (element is null)
        {
            failure = new BridgeVerbResult(
                false,
                HResults.UIA_E_ELEMENTNOTAVAILABLE,
                string.Empty,
                BruinetBridgeLookup.HasBridge(root, automation)
                    ? "the app under test publishes a Bruinet bridge but this element is not on it. "
                      + "Add GestureAutomation.Verbs to the element in the app's markup."
                    : "the app under test publishes no Bruinet bridge. It needs the "
                      + "Bruinet.Maui.AppSupport UI Automation sources, and at least one element "
                      + "declaring GestureAutomation.Verbs.");
            return false;
        }

        if (!element.TryGetBruinetPattern(out pattern))
        {
            failure = new BridgeVerbResult(
                false,
                HResults.E_FAIL,
                string.Empty,
                "its bridge element carries no Bruinet pattern. The app under test and this test "
                + "assembly were built against different contract GUIDs.");
            return false;
        }

        failure = default;
        return true;
    }
}

/// <summary>What a verb did, and how many targets were asked before this was the answer.</summary>
/// <param name="Delivered">Whether the app performed the verb.</param>
/// <param name="HResult">What the app answered, or what the client concluded.</param>
/// <param name="Value">The payload, for the verbs that return one.</param>
/// <param name="Reason">A sentence naming the cause and the remedy.</param>
/// <param name="Declined">
/// How many published targets were asked and refused.
/// <para>
/// <b>Zero and non-zero mean opposite things and used to be told apart only by reading the
/// sentence.</b> Zero says nothing on the app's bridge implements this verb - a configuration
/// error that waiting cannot fix. Non-zero says targets that could have answered did not, which
/// during a page transition is a state that lasts milliseconds. A caller that cannot tell them
/// apart either throws on a transient or hangs on a permanent one.
/// </para>
/// </param>
internal readonly record struct BridgeVerbResult(
    bool Delivered, int HResult, string Value, string Reason, int Declined = 0);
