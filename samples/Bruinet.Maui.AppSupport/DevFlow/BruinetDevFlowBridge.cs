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

    /// <summary>Adds the bridge's routes to the agent: <c>invoke</c>, <c>exchange</c>, <c>declares</c>, <c>media</c>, <c>rows</c>, <c>selectrow</c>, <c>tapbehavior</c>, and on iOS <c>alert</c>.</summary>
    /// <param name="options">The agent's options, in <c>AddMauiDevFlowAgent(options => ...)</c>.</param>
    /// <returns>The same options.</returns>
    public static AgentOptions AddBruinetBridge(this AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        AllowDeepTrees();
        var extension = options.RegisterExtension(Namespace, "Bruinet's verb bridge: the verbs an element declares, by AutomationId.");
        extension.MapPost("/invoke", request => Task.FromResult(Invoke(request.BodyAs<VerbRequest>())));
        extension.MapPost("/exchange", request => Task.FromResult(Exchange(request.BodyAs<VerbRequest>())));
        extension.MapGet("/declares", request => Task.FromResult(Declares(
            request.QueryParams.GetValueOrDefault("automationId"), request.QueryParams.GetValueOrDefault("verb"))));
        extension.MapPost("/media", request => Media(request.BodyAs<MediaRequest>()));
        extension.MapGet("/rows", request => Rows(request.QueryParams.GetValueOrDefault("automationId")));
        extension.MapGet("/names", _ => Names());
        extension.MapPost("/selectrow", request => SelectRow(request.BodyAs<RowRequest>()));
        extension.MapPost("/openswipe", request => OpenSwipe(request.BodyAs<SwipeOpenRequest>()));
        extension.MapPost("/tapbehavior", request => TapBehavior(request.BodyAs<BehaviorTapRequest>()));
#if IOS
        extension.MapGet("/alert", _ => Alert(null));
        extension.MapPost("/alert", request => Alert(request.BodyAs<AlertRequest>() ?? new AlertRequest()));
#endif
        return options;
    }

    // The agent writes its tree with JsonSerializerOptions that keep the default MaxDepth of 64, so a
    // page more than 31 elements deep (a form input inside a form section inside a PagePattern) fails
    // as "a possible object cycle" and the runner sees no page. The agent has no setting for it; its
    // template is raised here, before it serves a request, to fit its own layout-depth clamp of 200.
    private const int TreeJsonDepth = 256;

    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicFields, typeof(HttpResponse))]
    private static void AllowDeepTrees()
    {
        var field = typeof(HttpResponse).GetField("JsonOptions", BindingFlags.NonPublic | BindingFlags.Static);
        if (field?.GetValue(null) is System.Text.Json.JsonSerializerOptions { IsReadOnly: false } template)
        {
            template.MaxDepth = Math.Max(template.MaxDepth, TreeJsonDepth);
            return;
        }

        System.Diagnostics.Debug.WriteLine("[Bruinet] The DevFlow agent's HttpResponse.JsonOptions is gone or read-only: a page deeper than 31 elements cannot be read.");
    }

    /// <summary>
    /// Shows a popup page in the agent's tree while it is open. A popup library (Mopups) draws its page
    /// over the window without making it a child of anything, and the agent walks a window only through
    /// its visual children, so the popup could not be found, read or tapped. The app calls this when a
    /// popup opens, and <see cref="PopupClosed"/> when it closes.
    /// </summary>
    /// <param name="page">The popup page that opened.</param>
    public static void PopupOpened(Page page)
    {
        if (BruinetBridgeGate.IsOpen && VisualChildren(page) is { } children && !children.Contains(page))
            children.Add(page);
    }

    /// <summary>Takes a popup page that <see cref="PopupOpened"/> showed out of the agent's tree again.</summary>
    /// <param name="page">The popup page that closed.</param>
    public static void PopupClosed(Page page)
        => VisualChildren(page)?.Remove(page);

    // A Window's visual children are its logical children (its page and modal pages), kept in this
    // Element list. The page goes straight into the list: AddLogicalChild would re-parent it, and only
    // the list changes here, so the popup's parent, layout and rendering stay as the popup library made them.
    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicFields, typeof(Element))]
    private static IList<Element>? VisualChildren(Page page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var window = page.Window ?? Application.Current?.Windows.FirstOrDefault();
        if (window is null)
            return null;

        if (typeof(Element).GetField("_internalChildren", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(window) is IList<Element> children)
            return children;

        System.Diagnostics.Debug.WriteLine("[Bruinet] MAUI's Element._internalChildren is gone: a popup cannot be shown in the DevFlow tree.");
        return null;
    }

#if IOS
    /// <summary>
    /// The alert on an iOS screen, which the agent's tree does not show: without a request what it
    /// says (title, message, a prompt's field, the buttons accepting first and dismissing last), with
    /// one a button pressed or the field filled. A button is pressed as UIKit handles a tap: the alert
    /// closes, then the action's handler runs (VoiceOver's <c>accessibilityActivate</c> does neither on iOS 26).
    /// </summary>
    private static async Task<HttpResponse> Alert(AlertRequest? request)
    {
        if (!BruinetBridgeGate.IsOpen)
            return Answer(declared: false, delivered: false, HResults.UIA_E_NOTSUPPORTED, string.Empty, 0, "the bridge is off");

        return await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var alert = TopAlert();
            if (request is null)
            {
                if (alert?.View is not { } view)
                    return HttpResponse.Json(new { open = false });
                var frame = view.ConvertRectToView(view.Bounds, null);
                var buttons = alert.Actions.Where(action => action.Style != UIKit.UIAlertActionStyle.Cancel)
                    .Concat(alert.Actions.Where(action => action.Style == UIKit.UIAlertActionStyle.Cancel))
                    .Select(action => action.Title ?? string.Empty)
                    .ToList();
                return HttpResponse.Json(new
                {
                    open = true,
                    title = alert.Title,
                    message = alert.Message,
                    hasField = alert.TextFields is { Length: > 0 },
                    field = alert.TextFields is { Length: > 0 } fields ? fields[0].Text : null,
                    buttons,
                    x = (double)frame.X,
                    y = (double)frame.Y,
                    width = (double)frame.Width,
                    height = (double)frame.Height,
                });
            }

            if (alert?.View is null)
                return Answer(declared: true, delivered: false, HResults.UIA_E_ELEMENTNOTAVAILABLE, string.Empty, 0, "no alert is open");

            switch (request.Action)
            {
                case "press":
                    if (alert.Actions.FirstOrDefault(action => action.Title == request.Button) is not { } pressed)
                        return Answer(declared: true, delivered: false, HResults.UIA_E_ELEMENTNOTAVAILABLE, string.Empty, 0, $"the alert has no button '{request.Button}'");
                    return await Press(alert, pressed) is { } failure
                        ? Answer(declared: true, delivered: false, HResults.UIA_E_NOTSUPPORTED, string.Empty, 0, failure)
                        : Answer(declared: true, delivered: true, HResults.S_OK, string.Empty, 0, string.Empty);
                case "fill":
                    if (alert.TextFields is not { Length: > 0 } inputs)
                        return Answer(declared: true, delivered: false, HResults.UIA_E_ELEMENTNOTAVAILABLE, string.Empty, 0, "the alert has no field");
                    inputs[0].Text = (request.Replace ? string.Empty : inputs[0].Text ?? string.Empty) + (request.Text ?? string.Empty);
                    inputs[0].SendActionForControlEvents(UIKit.UIControlEvent.EditingChanged);
                    return Answer(declared: true, delivered: true, HResults.S_OK, string.Empty, 0, string.Empty);
                default:
                    return HttpResponse.Error($"Unknown alert action '{request.Action}'.");
            }
        });
    }

    // The alert presented last, over every window of the app.
    private static UIKit.UIAlertController? TopAlert()
    {
        UIKit.UIAlertController? found = null;
        foreach (var scene in UIKit.UIApplication.SharedApplication.ConnectedScenes.OfType<UIKit.UIWindowScene>())
        {
            foreach (var window in scene.Windows)
            {
                for (var controller = window.RootViewController; controller is not null; controller = controller.PresentedViewController)
                {
                    if (controller is UIKit.UIAlertController alert)
                        found = alert;
                }
            }
        }

        return found;
    }

    // A tapped alert button: UIKit closes the alert, then runs the action's handler. The handler is
    // private (the "handler" key), so this is for test-only debug builds; a block's invoke pointer
    // sits after its isa and flags.
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private delegate void BlockInvoke(IntPtr block, IntPtr action);

    private static async Task<string?> Press(UIKit.UIAlertController alert, UIKit.UIAlertAction action)
    {
        Foundation.NSObject? handler;
        try
        {
            handler = action.ValueForKey(new Foundation.NSString("handler"));
        }
        catch (Exception e)
        {
            return $"the alert's button has no handler to run ({e.Message})";
        }

        // UIKit ignores a dismissal while the alert is still being shown.
        for (var wait = 0; wait < 40 && alert.IsBeingPresented; wait++)
            await Task.Delay(50);
        await alert.DismissViewControllerAsync(false);
        if (alert.PresentingViewController is not null)
            return "the alert did not close";

        if (handler is { Handle: var block } && block != IntPtr.Zero)
        {
            var invoke = System.Runtime.InteropServices.Marshal.ReadIntPtr(block, 2 * IntPtr.Size);
            System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<BlockInvoke>(invoke)(block, action.Handle);
        }

        return null;
    }

    private sealed class AlertRequest
    {
        public string? Action { get; set; }

        public string? Button { get; set; }

        public string? Text { get; set; }

        public bool Replace { get; set; }
    }
#endif

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

    /// <summary>
    /// The AutomationId and visibility of each element in the windows that is no <c>VisualElement</c>
    /// (a swipe item, a menu item), by its <c>Element.Id</c>: the agent sends such an element nameless
    /// and hidden, under an id made from that Guid. A swipe item counts as visible only while its
    /// SwipeView is open, as on iOS and Windows, which create it only then.
    /// </summary>
    private static async Task<HttpResponse> Names()
    {
        if (!BruinetBridgeGate.IsOpen)
            return Answer(declared: false, delivered: false, HResults.UIA_E_NOTSUPPORTED, string.Empty, 0, "the bridge is off");

        return await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
        {
            var names = new List<object>();
            var pending = new Stack<(Microsoft.Maui.IVisualTreeElement Element, bool? SwipeOpen)>();
            var seen = new HashSet<Microsoft.Maui.IVisualTreeElement>(ReferenceEqualityComparer.Instance);
            foreach (var window in Microsoft.Maui.Controls.Application.Current?.Windows ?? [])
                pending.Push((window, null));
            while (pending.Count > 0)
            {
                var (next, swipeOpen) = pending.Pop();
                if (!seen.Add(next))
                    continue;
                if (next is Microsoft.Maui.ISwipeView swipe)
                    swipeOpen = swipe.IsOpen;
                if (next is Microsoft.Maui.Controls.Element { AutomationId: { Length: > 0 } automationId } element
                    and not Microsoft.Maui.Controls.VisualElement)
                {
                    var visible = element switch
                    {
                        Microsoft.Maui.Controls.SwipeItem item => item.IsVisible && swipeOpen == true,
                        _ => true,
                    };
                    names.Add(new { id = element.Id.ToString("N"), automationId, visible, enabled = (element as Microsoft.Maui.Controls.MenuItem)?.IsEnabled ?? true });
                }
                foreach (var child in next.GetVisualChildren())
                    pending.Push((child, swipeOpen));
            }
            return HttpResponse.Json(new { declared = true, delivered = true, names });
        });
    }

    /// <summary>
    /// Selects a list's item as a tap on its row does: in a single selection it becomes the
    /// <c>SelectedItem</c>, in a multiple one it is added or taken out. For iOS, where the agent's tap
    /// reaches no row's selection (the collection view's delegate selects, not the cell).
    /// </summary>
    private static async Task<HttpResponse> SelectRow(RowRequest? request)
    {
        if (!BruinetBridgeGate.IsOpen)
            return Answer(declared: false, delivered: false, HResults.UIA_E_NOTSUPPORTED, string.Empty, 0, "the bridge is off");
        if (string.IsNullOrEmpty(request?.AutomationId))
            return HttpResponse.Error("automationId is required.");

        return await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (FindElement(request.AutomationId) is not Microsoft.Maui.Controls.SelectableItemsView list)
                return Answer(declared: false, delivered: false, HResults.UIA_E_ELEMENTNOTAVAILABLE, string.Empty, 0, $"no selectable list '{request.AutomationId}' is on screen");

            var items = list.ItemsSource as System.Collections.IList ?? list.ItemsSource?.Cast<object>().ToList();
            if (items is null || request.Index < 0 || request.Index >= items.Count)
                return Answer(declared: true, delivered: false, HResults.UIA_E_ELEMENTNOTAVAILABLE, string.Empty, 0, $"the list has no item {request.Index}");

            var item = items[request.Index]!;
            switch (list.SelectionMode)
            {
                case Microsoft.Maui.Controls.SelectionMode.Single:
                    list.SelectedItem = item;
                    break;
                case Microsoft.Maui.Controls.SelectionMode.Multiple:
                    if (!list.SelectedItems.Remove(item))
                        list.SelectedItems.Add(item);
                    break;
                default:
                    return Answer(declared: true, delivered: false, HResults.UIA_E_NOTSUPPORTED, string.Empty, 0, "the list selects nothing");
            }

            return Answer(declared: true, delivered: true, HResults.S_OK, string.Empty, 0, string.Empty);
        });
    }

    /// <summary>
    /// Opens the SwipeView whose <c>Element.Id</c> starts with <c>id</c> on <c>side</c>, as a finger
    /// dragging it does, the rule MauiHeadless applies (D19): the agent's swipe reaches
    /// SwipeGestureRecognizers only. Not declared when it has no items on that side.
    /// </summary>
    private static async Task<HttpResponse> OpenSwipe(SwipeOpenRequest? request)
    {
        if (!BruinetBridgeGate.IsOpen)
            return Answer(declared: false, delivered: false, HResults.UIA_E_NOTSUPPORTED, string.Empty, 0, "the bridge is off");
        if (request is not { Id.Length: > 0 } || !Enum.TryParse<Microsoft.Maui.OpenSwipeItem>(request.Side + "Items", out var side))
            return HttpResponse.Error("id and side (Left, Right, Top, Bottom) are required.");

        return await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
        {
            var targets = AllElements().OfType<Microsoft.Maui.Controls.SwipeView>()
                .Where(view => view.Id.ToString("N").StartsWith(request.Id, StringComparison.Ordinal))
                .ToList();
            if (targets.Count != 1)
                return Answer(declared: false, delivered: false, HResults.UIA_E_ELEMENTNOTAVAILABLE, string.Empty, 0, $"{targets.Count} SwipeViews match {request.Id}");
            var view = targets[0];
            var items = side switch
            {
                Microsoft.Maui.OpenSwipeItem.LeftItems => view.LeftItems,
                Microsoft.Maui.OpenSwipeItem.RightItems => view.RightItems,
                Microsoft.Maui.OpenSwipeItem.TopItems => view.TopItems,
                _ => view.BottomItems,
            };
            if (items is not { Count: > 0 })
                return Answer(declared: false, delivered: false, HResults.UIA_E_NOTSUPPORTED, string.Empty, 0, $"the SwipeView has no {side}");
            if (!view.IsEnabled)
                return Answer(declared: true, delivered: false, HResults.UIA_E_ELEMENTNOTAVAILABLE, string.Empty, 0, "the SwipeView is disabled");

            view.Open(side, animated: false);
            return Answer(declared: true, delivered: true, HResults.S_OK, string.Empty, 0, string.Empty);
        });
    }

    /// <summary>
    /// Taps a view whose tap is a platform behavior's (the toolkit's TouchBehavior): the behavior
    /// listens to the platform's touches, which the agent's tap does not make. Runs the behavior's
    /// <c>Command</c> as a finger's tap would, the rule MauiHeadless applies to such behaviors. The view
    /// is the agent's element: its AutomationId, and the start of its <c>Element.Id</c> (32 hex digits)
    /// where the agent's id carries one, or else none of the starts in <c>exclude</c> (the agent's
    /// other elements with that AutomationId). Not declared when the view has no such command: the
    /// agent's tap is the route.
    /// </summary>
    private static async Task<HttpResponse> TapBehavior(BehaviorTapRequest? request)
    {
        if (!BruinetBridgeGate.IsOpen)
            return Answer(declared: false, delivered: false, HResults.UIA_E_NOTSUPPORTED, string.Empty, 0, "the bridge is off");
        if (request is null || (string.IsNullOrEmpty(request.AutomationId) && string.IsNullOrEmpty(request.Id)))
            return HttpResponse.Error("automationId or id is required.");

        return await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
        {
            var exclude = request.Exclude ?? [];
            var targets = AllElements()
                .Where(element => string.IsNullOrEmpty(request.AutomationId) || element.AutomationId == request.AutomationId)
                .Where(element =>
                {
                    var id = element.Id.ToString("N");
                    return string.IsNullOrEmpty(request.Id)
                        ? !exclude.Any(other => id.StartsWith(other, StringComparison.Ordinal))
                        : id.StartsWith(request.Id, StringComparison.Ordinal);
                })
                .ToList();
            var name = request.AutomationId is { Length: > 0 } automationId ? $"'{automationId}'" : $"element {request.Id}";
            if (targets.Count != 1 || BehaviorCommand(targets[0]) is not { } command)
                return Answer(declared: false, delivered: false, HResults.UIA_E_NOTSUPPORTED, string.Empty, 0,
                    targets.Count == 1 ? $"{name} has no platform behavior with a Command" : $"{targets.Count} views match {name}");
            if (!targets[0].IsEnabled || !command.Command.CanExecute(command.Parameter))
                return Answer(declared: true, delivered: false, HResults.UIA_E_ELEMENTNOTAVAILABLE, string.Empty, 0, "the behavior's command cannot execute");

            command.Command.Execute(command.Parameter);
            return Answer(declared: true, delivered: true, HResults.S_OK, string.Empty, 0, string.Empty);
        });
    }

    private static (System.Windows.Input.ICommand Command, object? Parameter)? BehaviorCommand(Microsoft.Maui.Controls.VisualElement element)
    {
        foreach (var behavior in element.Behaviors.Where(IsPlatformBehavior))
        {
            var type = behavior.GetType();
            if (type.GetProperty("Command")?.GetValue(behavior) is System.Windows.Input.ICommand command)
                return (command, type.GetProperty("CommandParameter")?.GetValue(behavior));
        }

        return null;
    }

    private static bool IsPlatformBehavior(Microsoft.Maui.Controls.Behavior behavior)
    {
        for (var type = behavior.GetType(); type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Microsoft.Maui.Controls.PlatformBehavior<,>))
                return true;
        }

        return false;
    }

    // Every element in the app's windows, by their visual trees, each once: a legacy ListView lists a
    // realized cell among its visual children twice once its page has been covered and shown again.
    private static IEnumerable<Microsoft.Maui.Controls.VisualElement> AllElements()
    {
        var pending = new Stack<Microsoft.Maui.IVisualTreeElement>();
        var seen = new HashSet<Microsoft.Maui.IVisualTreeElement>(ReferenceEqualityComparer.Instance);
        foreach (var window in Microsoft.Maui.Controls.Application.Current?.Windows ?? [])
            pending.Push(window);
        while (pending.Count > 0)
        {
            var next = pending.Pop();
            if (!seen.Add(next))
                continue;
            if (next is Microsoft.Maui.Controls.VisualElement element)
                yield return element;
            foreach (var child in next.GetVisualChildren())
                pending.Push(child);
        }
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

    private sealed class RowRequest
    {
        public string? AutomationId { get; set; }

        public int Index { get; set; }
    }

    private sealed class SwipeOpenRequest
    {
        public string? Id { get; set; }

        public string? Side { get; set; }
    }

    private sealed class BehaviorTapRequest
    {
        public string? AutomationId { get; set; }

        public string? Id { get; set; }

        public string[]? Exclude { get; set; }
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
