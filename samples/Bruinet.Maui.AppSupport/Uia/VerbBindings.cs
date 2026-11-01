using System.Windows.Input;
using Bruinet.Uia;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace Bruinet.Maui.AppSupport.Uia;

/// <summary>
/// Works out, once, what each declared verb will actually do on one element.
/// </summary>
/// <remarks>
/// <para>
/// <b>The dispatcher used to do this per call, by trying things.</b> Sink first, then a public
/// MAUI API for the control's type, then a gesture recognizer, then refusal - each rung reached
/// only if the one above declined. That is the activation ladder in another process, and it had
/// the ladder's two faults: a rung that runs and reports nothing cannot be told from one that
/// never ran, and a `when` clause that performs a gesture in order to decide whether it matches
/// leaves the app changed on the way past.
/// </para>
/// <para>
/// <b>Nothing here is tried.</b> Resolution looks at what the element has and picks one handler,
/// or picks none and says why. Every fact it consults - the element's type, its recognizers, the
/// verbs the sink names - was settled when the page was written, so consulting them once per
/// publish rather than once per call is not an optimisation but a correction: the call path can
/// no longer reach a decision the markup had already made.
/// </para>
/// <para>
/// <b>What is not here.</b> Only the gesture range binds. Focus, text, navigation and state
/// reads are still dispatched by name in <see cref="MauiVerbDispatcher"/>, and join this table
/// as their own steps land - see stage D. A verb outside the gesture range is therefore accepted
/// as declared, which is the one place this file still takes the markup's word for something.
/// </para>
/// </remarks>
internal static class VerbBindings
{
    /// <summary>
    /// Binds a declaration to this element.
    /// </summary>
    /// <param name="element">The element being published.</param>
    /// <param name="declared">The verbs the markup named.</param>
    /// <param name="sink">The app's own handler, if it attached one.</param>
    /// <returns>What the element will answer, and what it will not.</returns>
    internal static VerbPlan Resolve(
        VisualElement element,
        IReadOnlyCollection<BruinetVerb> declared,
        IBruinetGestureSink? sink)
    {
        var invocations = new Dictionary<BruinetVerb, VerbBinding>();
        var sinkOwned = new HashSet<BruinetVerb>();
        var capabilities = new List<BruinetVerb>();
        var refusals = new List<VerbRefusal>();

        foreach (var verb in declared)
        {
            // 1. A sink owns the verbs it names. Not first refusal - ownership. There is no
            //    second opinion to fall back on, which is what makes this a rule rather than
            //    a rung.
            if (sink is not null && sink.Verbs.Contains(verb))
            {
                sinkOwned.Add(verb);
                capabilities.Add(verb);

                if (BruinetVerbs.TransportFor(verb) == BruinetVerbTransport.Invoke)
                {
                    invocations[verb] = new VerbBinding(
                        verb, (target, arg1, arg2) => sink.Invoke(target, verb, arg1, arg2));
                }

                continue;
            }

            // 2. Outside the gesture range, dispatch is still by name. Said here rather than
            //    left implicit, because it is the remaining gap in "capabilities cannot lie".
            if (BruinetVerbs.RangeOf((int)verb) != BruinetVerbRange.Gestures)
            {
                capabilities.Add(verb);
                continue;
            }

            // 3. The table.
            var binding = GestureBinding(element, verb, out var reason);
            if (binding is null)
            {
                refusals.Add(new VerbRefusal(verb, reason));
                continue;
            }

            invocations[verb] = binding;
            capabilities.Add(verb);
        }

        return new VerbPlan(sink, invocations, sinkOwned, capabilities, refusals);
    }

    /// <summary>
    /// The gesture table: what performs this verb on this element, if anything does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The type rows win over the recognizer rows</b>, and the ordering is a statement rather
    /// than an accident of where the code sits: a <c>SwipeView</c> asked to swipe is being asked
    /// to reveal its items, whatever recognizers it also carries. Because only one candidate is
    /// ever chosen and neither is run to find out, this is still a resolution.
    /// </para>
    /// <para>
    /// <b>Three verbs bind to nothing at all.</b> <c>Pan</c> and <c>Pinch</c> need
    /// <c>SendPan</c> and <c>SendPinch</c>, which are internal in MAUI 10 - and reflecting into
    /// them would turn every MAUI upgrade into a runtime failure found by a test rather than a
    /// build failure found by a compiler. <c>LongPress</c> is worse off still: MAUI has no
    /// long-press recognizer, and the nearest thing,
    /// <c>PointerGestureRecognizer.PointerPressedCommand</c>, means something else. Pressing is
    /// not holding; an app that starts a timer on press and cancels it on release would never
    /// see a long press, and a test asking for one would be told it got one. All three are
    /// reachable through a sink, by name, and through nothing else.
    /// </para>
    /// </remarks>
    /// <param name="element">The element being published.</param>
    /// <param name="verb">The gesture verb declared on it.</param>
    /// <param name="reason">Why nothing bound, when nothing does.</param>
    /// <returns>The binding, or null.</returns>
    private static VerbBinding? GestureBinding(VisualElement element, BruinetVerb verb, out string reason)
    {
        reason = string.Empty;

        switch (verb)
        {
            case BruinetVerb.Tap:
            case BruinetVerb.DoubleTap:
                {
                    var taps = verb == BruinetVerb.Tap ? 1 : 2;
                    var command = TapCommand(element, taps);
                    if (command is null)
                    {
                        reason = $"no TapGestureRecognizer with NumberOfTapsRequired={taps} and a Command";
                        return null;
                    }

                    return Commanded(verb, command);
                }

            case BruinetVerb.SwipeLeft:
            case BruinetVerb.SwipeRight:
            case BruinetVerb.SwipeUp:
            case BruinetVerb.SwipeDown:
                {
                    // A SwipeView's own items come first: swiping one is what the control is for.
                    if (element is SwipeView && MauiCapabilities.SwipeItemFor(verb) is { } item)
                    {
                        return new VerbBinding(verb, (target, _, _) =>
                        {
                            MauiCapabilities.OpenSwipeView((SwipeView)target, item);
                            return HResults.S_OK;
                        });
                    }

                    // A CarouselView moves one card along its own axis. Its list does publish a
                    // Scroll pattern, but scrolling that shows the next card without MAUI's Position
                    // following, so the pattern cannot stand in for the swipe (measured 2026-09-18).
                    if (element is CarouselView carousel && MauiCapabilities.CarouselStepFor(carousel, verb) is { } step)
                    {
                        return new VerbBinding(verb, (target, _, _) =>
                            MauiCapabilities.StepCarousel((CarouselView)target, step)
                                ? HResults.S_OK
                                : HResults.BRUINET_E_DECLINED);
                    }

                    // Then pull-to-refresh, which is a downward swipe and nothing else. A ContentView
                    // that wraps a RefreshView declares it for its content, one level deep.
                    if (verb == BruinetVerb.SwipeDown && RefreshTarget(element) is not null)
                    {
                        return new VerbBinding(verb, (target, _, _) =>
                        {
                            if (RefreshTarget(target) is not { } view)
                            {
                                return HResults.BRUINET_E_DECLINED;
                            }

                            // A refresh already running is a real state of the app, and reporting it
                            // as success would let a test assert a refresh it did not cause. That was
                            // the intent behind S_FALSE here, and S_FALSE never delivered it: the
                            // client read S_OK and asserted the refresh anyway. A disabled view is
                            // one a user cannot pull.
                            if (view.IsRefreshing || !view.IsEnabled)
                            {
                                return HResults.BRUINET_E_DECLINED;
                            }

                            MauiCapabilities.StartRefresh(view);
                            return HResults.S_OK;
                        });
                    }

                    var direction = MauiCapabilities.SwipeDirectionFor(verb);
                    var swipeCommand = direction is null ? null : SwipeCommand(element, direction.Value);
                    if (swipeCommand is null)
                    {
                        reason = $"not a SwipeView, and no SwipeGestureRecognizer with Direction {direction} and a Command";
                        return null;
                    }

                    return Commanded(verb, swipeCommand);
                }

            case BruinetVerb.LongPress:
                reason = "MAUI has no long-press gesture recognizer; attach a sink that names LongPress";
                return null;

            case BruinetVerb.Pan:
            case BruinetVerb.Pinch:
                reason = $"MAUI's Send{verb} is internal and is not reflected into; attach a sink that names {verb}";
                return null;

            default:
                reason = "not a gesture this build knows how to bind";
                return null;
        }
    }

    /// <summary>
    /// Turns a found command into a binding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The command is captured weakly, and that is load-bearing.</b>
    /// <see cref="MauiVerbTarget"/> holds its element weakly so that a registration which is
    /// never withdrawn cannot pin a page, its view model and everything they reference for the
    /// life of the window. A binding that captured a recognizer strongly would undo that through
    /// the recognizer's <c>Parent</c>, and the weak reference next to it would become
    /// decoration. A command is reached only while its recognizer is, which is only while the
    /// element is - so if this cannot be resolved, the element is gone.
    /// </para>
    /// <para>
    /// <c>CanExecute</c> is asked here, at call time, and not at bind time. They are different
    /// questions: bind time asks whether a command exists, call time asks whether it will run
    /// now. Collapsing them is what made today's dispatcher answer both with
    /// <c>UIA_E_NOTSUPPORTED</c>.
    /// </para>
    /// </remarks>
    /// <param name="verb">The verb being bound.</param>
    /// <param name="found">The command and the parameter it was declared with.</param>
    /// <returns>The binding.</returns>
    private static VerbBinding Commanded(BruinetVerb verb, CommandBinding found)
    {
        var command = new WeakReference<ICommand>(found.Command);
        var parameter = found.Parameter;

        return new VerbBinding(verb, (_, _, _) =>
        {
            if (!command.TryGetTarget(out var live))
            {
                return HResults.UIA_E_ELEMENTNOTAVAILABLE;
            }

            // A command whose CanExecute is false is the same case as a disabled menu item: the
            // app is refusing to offer the action, and nothing should let a test prove it
            // happened. S_FALSE said so to nobody.
            if (!live.CanExecute(parameter))
            {
                return HResults.BRUINET_E_DECLINED;
            }

            live.Execute(parameter);
            return HResults.S_OK;
        });
    }

    /// <summary>A recognizer's command and the parameter declared beside it.</summary>
    /// <param name="Command">The command.</param>
    /// <param name="Parameter">Its declared parameter, which may be null.</param>
    private sealed record CommandBinding(ICommand Command, object? Parameter);

    /// <summary>
    /// The tap recognizer expecting this many taps, if the element has one with a command.
    /// </summary>
    /// <remarks>
    /// Gesture recognizers live on <c>View</c>, not on <c>VisualElement</c>: a Page or a Window
    /// can declare verbs but cannot carry a recognizer, so there is nothing here for them.
    /// </remarks>
    private static CommandBinding? TapCommand(VisualElement element, int taps)
        => element is not View view
            ? null
            : view.GestureRecognizers
                .OfType<TapGestureRecognizer>()
                .Where(recognizer => recognizer.NumberOfTapsRequired == taps)
                .Select(recognizer => recognizer.Command is null
                    ? null
                    : new CommandBinding(recognizer.Command, recognizer.CommandParameter))
                .FirstOrDefault(found => found is not null);

    /// <summary>
    /// The swipe recognizer covering this direction, if the element has one with a command.
    /// </summary>
    /// <remarks>
    /// <c>Direction</c> is a flags enum and one recognizer commonly declares several, so this
    /// matches on the flag rather than on equality. The gestures page declares
    /// <c>Left,Right</c> on one recognizer precisely to keep that honest.
    /// </remarks>
    private static CommandBinding? SwipeCommand(VisualElement element, SwipeDirection direction)
        => element is not View view
            ? null
            : view.GestureRecognizers
                .OfType<SwipeGestureRecognizer>()
                .Where(recognizer => recognizer.Direction.HasFlag(direction))
                .Select(recognizer => recognizer.Command is null
                    ? null
                    : new CommandBinding(recognizer.Command, recognizer.CommandParameter))
                .FirstOrDefault(found => found is not null);

    /// <summary>
    /// The RefreshView a SwipeDown on this element pulls: the element itself, or the direct
    /// content of a ContentView.
    /// </summary>
    internal static RefreshView? RefreshTarget(VisualElement element) => element switch
    {
        RefreshView view => view,
        ContentView { Content: RefreshView view } => view,
        _ => null,
    };
}
