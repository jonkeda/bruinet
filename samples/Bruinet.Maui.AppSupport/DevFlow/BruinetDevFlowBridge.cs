#if BRUINET_DEVFLOW && BRUINET_UIA_BRIDGE
using System.Globalization;
using System.Reflection;
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

    /// <summary>Adds the bridge's routes to the agent: <c>invoke</c>, <c>exchange</c>, <c>declares</c>, <c>media</c> and <c>rows</c>.</summary>
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
        extension.MapPost("/media", request => Media(request.BodyAs<MediaRequest>()));
        extension.MapGet("/rows", request => Rows(request.QueryParams.GetValueOrDefault("automationId")));
        return options;
    }

    /// <summary>
    /// Plays, pauses, stops or seeks a media element by its AutomationId: what its transport controls
    /// do, which the platform draws outside any tree the agent reads. Its state, position and duration
    /// the agent reads itself. Called by name (<c>Play</c>, <c>Pause</c>, <c>Stop</c>, <c>SeekTo</c>), so
    /// this library needs no reference to the toolkit that has the element.
    /// </summary>
    private static async Task<HttpResponse> Media(MediaRequest? request)
    {
        if (!BruinetBridgeGate.IsOpen)
            return Answer(declared: false, delivered: false, HResults.UIA_E_NOTSUPPORTED, string.Empty, 0, "the bridge is off");
        if (request is not { AutomationId: { Length: > 0 } automationId, Action: { Length: > 0 } action })
            return HttpResponse.Error("automationId and action are required.");

        return await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var element = FindElement(automationId);
            if (element is null)
                return Answer(declared: false, delivered: false, HResults.UIA_E_ELEMENTNOTAVAILABLE, string.Empty, 0, $"no element '{automationId}' is on screen");

            var type = element.GetType();
            MethodInfo? method;
            object?[] arguments;
            if (string.Equals(action, "SeekTo", StringComparison.Ordinal))
            {
                method = type.GetMethod("SeekTo", [typeof(TimeSpan), typeof(CancellationToken)]);
                arguments = [TimeSpan.FromSeconds(Math.Max(0, request.Seconds)), CancellationToken.None];
            }
            else
            {
                method = action is "Play" or "Pause" or "Stop" ? type.GetMethod(action, Type.EmptyTypes) : null;
                arguments = [];
            }

            if (method is null)
                return Answer(declared: false, delivered: false, HResults.UIA_E_NOTSUPPORTED, string.Empty, 0, $"'{automationId}' ({type.Name}) has no {action}");

            if (method.Invoke(element, arguments) is Task running)
                await running.WaitAsync(TimeSpan.FromSeconds(5));
            return Answer(declared: true, delivered: true, HResults.S_OK, string.Empty, 0, string.Empty);
        });
    }

    /// <summary>
    /// The item index of each row a list (CollectionView, ListView, CarouselView) has realized, by the
    /// row's <c>Element.Id</c>: what UI Automation's <c>PositionInSet</c> gives on Windows and the agent
    /// gives nowhere. The runner matches the ids to the agent's, which are made from them. A header
    /// answers -1, and a container still showing an item that was removed -2.
    /// </summary>
    private static async Task<HttpResponse> Rows(string? automationId)
    {
        if (!BruinetBridgeGate.IsOpen)
            return Answer(declared: false, delivered: false, HResults.UIA_E_NOTSUPPORTED, string.Empty, 0, "the bridge is off");
        if (string.IsNullOrEmpty(automationId))
            return HttpResponse.Error("automationId is required.");

        return await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (FindElement(automationId) is not Microsoft.Maui.Controls.ItemsView list)
                return Answer(declared: false, delivered: false, HResults.UIA_E_ELEMENTNOTAVAILABLE, string.Empty, 0, $"no list '{automationId}' is on screen");

            var items = list.ItemsSource as System.Collections.IList ?? list.ItemsSource?.Cast<object>().ToList();
            var rows = ((Microsoft.Maui.IVisualTreeElement)list).GetVisualChildren()
                .OfType<Microsoft.Maui.Controls.Element>()
                .Select(row => new
                {
                    id = row.Id.ToString("N"),
                    automationId = (row as Microsoft.Maui.Controls.VisualElement)?.AutomationId,
                    // -1: not an item (a header or footer, which carries the list's own context);
                    // -2: an item no longer in the source, which a recycled container still shows.
                    index = row.BindingContext is not { } item || ReferenceEquals(item, list.BindingContext) || items is null ? -1
                        : items.IndexOf(item) is var at and >= 0 ? at : -2,
                })
                .ToList();
            return HttpResponse.Json(new { declared = true, delivered = true, rows });
        });
    }

    // The first element with the AutomationId in the app's windows, by their visual trees.
    private static Microsoft.Maui.Controls.VisualElement? FindElement(string automationId)
    {
        var pending = new Stack<Microsoft.Maui.IVisualTreeElement>();
        foreach (var window in Microsoft.Maui.Controls.Application.Current?.Windows ?? [])
            pending.Push(window);
        while (pending.Count > 0)
        {
            var next = pending.Pop();
            if (next is Microsoft.Maui.Controls.VisualElement { AutomationId: var id } found && id == automationId)
                return found;
            foreach (var child in next.GetVisualChildren())
                pending.Push(child);
        }

        return null;
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

    private sealed class MediaRequest
    {
        public string? AutomationId { get; set; }

        public string? Action { get; set; }

        public double Seconds { get; set; }
    }

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
