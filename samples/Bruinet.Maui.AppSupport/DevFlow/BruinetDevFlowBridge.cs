#if BRUINET_DEVFLOW && BRUINET_UIA_BRIDGE
using System.Globalization;
using Bruinet.Maui.AppSupport.Uia;
using Bruinet.Uia;
using Microsoft.Maui.DevFlow.Agent.Core;

namespace Bruinet.Maui.AppSupport;

/// <summary>
/// Bruinet's bridge over the MauiDevFlow agent: the verbs an element declares
/// (<c>uia:GestureAutomation.Verbs</c>) and its sink answers, reached through the agent's HTTP
/// server instead of UI Automation, so the DevFlow runner (<c>Bruinet.Maui.DevFlow</c>) can do what
/// the agent has no route for: raise a menu item by its AutomationId, read a picker's items, take
/// the focus away, run an app's gesture sink.
/// </summary>
/// <remarks>
/// Behind the bridge's own gates: compiled only where the bridge is (<c>BRUINET_UIA_BRIDGE</c>, Debug)
/// and the app carries the agent (<c>BruinetDevFlow=true</c>), and answering only while the bridge is
/// on (<c>UseBruinetGestureBridge()</c> and <c>BRUINET_UIA_BRIDGE=1</c>, which the runner sets).
/// </remarks>
public static class BruinetDevFlowBridge
{
    /// <summary>The extension's namespace, in the reverse-domain form the agent requires: its routes are under <c>/api/v1/ext/dev.bruinet.bridge/</c>.</summary>
    public const string Namespace = "dev.bruinet.bridge";

    /// <summary>Adds the bridge's routes to the agent: <c>invoke</c>, <c>exchange</c> and <c>declares</c>.</summary>
    /// <param name="options">The agent's options, in <c>AddMauiDevFlowAgent(options => ...)</c>.</param>
    /// <returns>The same options.</returns>
    public static AgentOptions AddBruinetBridge(this AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var extension = options.RegisterExtension(Namespace, "Bruinet's verb bridge: the verbs an element declares, by AutomationId.");
        extension.MapPost("/invoke", request => Task.FromResult(Invoke(request.BodyAs<VerbRequest>())));
        extension.MapPost("/exchange", request => Task.FromResult(Exchange(request.BodyAs<VerbRequest>())));
        extension.MapGet("/declares", request => Task.FromResult(Declares(
            request.QueryParams.GetValueOrDefault("automationId"), request.QueryParams.GetValueOrDefault("verb"))));
        return options;
    }

    // Both routes carry a verb by the transport the verb itself names (BruinetVerbs.TransportFor), as
    // the Windows bridge's Send does: a caller cannot send an Invoke verb as an Exchange by mistake.
    private static HttpResponse Invoke(VerbRequest? request) => Run(request, (element, plan, verb) => Perform(element, plan, verb, request!));

    private static HttpResponse Exchange(VerbRequest? request) => Run(request, (element, plan, verb) => Perform(element, plan, verb, request!));

    private static (int Hr, string Value) Perform(Microsoft.Maui.Controls.VisualElement element, VerbPlan plan, BruinetVerb verb, VerbRequest request)
    {
        if (BruinetVerbs.TransportFor(verb) == BruinetVerbTransport.Invoke)
            return (MauiVerbDispatcher.Invoke(element, plan, verb, request.Arg1, request.Arg2), string.Empty);

        var hr = MauiVerbDispatcher.Exchange(element, plan, verb, request.Argument ?? string.Empty, out var value);
        return (hr, value);
    }

    // Every element that declares the verb (with the id, or anywhere), newest first, until one
    // performs it; as the Windows bridge's ExchangeAnywhere does.
    private static HttpResponse Run(VerbRequest? request, Func<Microsoft.Maui.Controls.VisualElement, VerbPlan, BruinetVerb, (int Hr, string Value)> perform)
    {
        if (request is null || !Enum.TryParse<BruinetVerb>(request.Verb, ignoreCase: false, out var verb))
            return HttpResponse.Error($"Unknown verb '{request?.Verb}'.");

        var automationId = string.IsNullOrEmpty(request.AutomationId) ? null : request.AutomationId;
        var targets = BruinetVerbRegistry.Targets(automationId, verb);
        if (targets.Count == 0)
            return Answer(declared: false, delivered: false, HResults.UIA_E_NOTSUPPORTED, string.Empty, declined: 0,
                $"no element {(automationId is null ? string.Empty : $"'{automationId}' ")}declares {verb}");

        var declined = 0;
        (int Hr, string Value) last = (HResults.UIA_E_NOTSUPPORTED, string.Empty);
        foreach (var (element, plan) in targets)
        {
            last = perform(element, plan, verb);
            if (last.Hr >= 0)
                return Answer(declared: true, delivered: true, last.Hr, last.Value, declined, string.Empty);
            declined++;
        }

        return Answer(declared: true, delivered: false, last.Hr, last.Value, declined,
            $"{verb} returned 0x{last.Hr.ToString("X8", CultureInfo.InvariantCulture)}");
    }

    private static HttpResponse Declares(string? automationId, string? verbName)
        => Enum.TryParse<BruinetVerb>(verbName, ignoreCase: false, out var verb)
            ? HttpResponse.Json(new { declared = BruinetVerbRegistry.Targets(string.IsNullOrEmpty(automationId) ? null : automationId, verb).Count > 0 })
            : HttpResponse.Error($"Unknown verb '{verbName}'.");

    private static HttpResponse Answer(bool declared, bool delivered, int hresult, string value, int declined, string reason)
        => HttpResponse.Json(new { declared, delivered, hresult, value, declined, reason });

    private sealed class VerbRequest
    {
        public string? AutomationId { get; set; }

        public string? Verb { get; set; }

        public string? Argument { get; set; }

        public int Arg1 { get; set; }

        public int Arg2 { get; set; }
    }
}
#endif
