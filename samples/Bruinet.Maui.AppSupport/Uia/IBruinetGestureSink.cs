using Bruinet.Uia;
using Microsoft.Maui.Controls;

namespace Bruinet.Maui.AppSupport.Uia;

/// <summary>
/// Lets an app answer verbs the bridge has no MAUI API for.
/// </summary>
/// <remarks>
/// <para>
/// <b>It owns the verbs it names.</b> Not "first refusal" - ownership. A sink that lists
/// <see cref="BruinetVerb.Pan"/> is what performs Pan on the elements it is attached to, and
/// there is no second opinion behind it. This is the change that stopped the dispatcher being a
/// ladder: the old interface returned <c>bool</c> from each method, meaning "I did not handle
/// this, carry on", which is a sentence only a ladder needs and an invitation to write its
/// <c>else</c>.
/// </para>
/// <para>
/// <b>Three verbs cannot be delivered any other way.</b> <c>Pan</c> and <c>Pinch</c> need MAUI
/// internals; <c>LongPress</c> has no MAUI recognizer at all. Without a sink the only remaining
/// route to any of them is a real pointer, which is the thing the background-mode work exists to
/// stop needing.
/// </para>
/// <para>
/// <b>Implement it on the control, on its view model, or on any object at all</b> - it is
/// attached with <see cref="GestureAutomation.SinkProperty"/> and does not have to be the
/// element itself.
/// </para>
/// <para>
/// Called on the UI thread, inside the bridge's timeout budget. Throwing is a failure and is
/// reported as one; it is not a way of declining.
/// </para>
/// </remarks>
public interface IBruinetGestureSink
{
    /// <summary>
    /// The verbs this sink performs.
    /// </summary>
    /// <remarks>
    /// Read once, when an element carrying this sink is published. A verb named here is bound to
    /// this sink for that element and nothing else is consulted for it; a verb not named here is
    /// resolved as though the sink were absent.
    /// </remarks>
    IReadOnlyCollection<BruinetVerb> Verbs { get; }

    /// <summary>Performs a numeric verb.</summary>
    /// <param name="element">The element the verb was aimed at.</param>
    /// <param name="verb">The verb, which this sink named in <see cref="Verbs"/>.</param>
    /// <param name="arg1">First argument, meaning defined per verb.</param>
    /// <param name="arg2">Second argument, meaning defined per verb.</param>
    /// <returns>
    /// An HRESULT. <c>S_OK</c> when it happened; <c>S_FALSE</c> when the app's own state meant
    /// it could not, which is a fact about the app rather than a failure of the bridge.
    /// </returns>
    int Invoke(VisualElement element, BruinetVerb verb, int arg1, int arg2);

    /// <summary>Performs a verb that takes or returns text.</summary>
    /// <param name="element">The element the verb was aimed at.</param>
    /// <param name="verb">The verb, which this sink named in <see cref="Verbs"/>.</param>
    /// <param name="argument">The argument, possibly empty.</param>
    /// <returns>The result, possibly empty.</returns>
    string Exchange(VisualElement element, BruinetVerb verb, string argument)
        => throw new NotSupportedException(
            $"{GetType().Name} named {verb} but does not implement Exchange. A verb that carries "
            + "a string needs it; one that does not is answered by Invoke.");

    /// <summary>
    /// The <see cref="BruinetVerb.GetState"/> names this sink answers through <see cref="Exchange"/>.
    /// </summary>
    /// <remarks>
    /// Android has no bridge to ask a name: there each name listed here that the sink answers for the element is
    /// published on its accessibility node, which the Android head reads (R2-T112). Windows asks by name and does
    /// not need the list. A name the sink refuses for an element (<see cref="NotSupportedException"/>) is left out.
    /// </remarks>
    IReadOnlyCollection<string> StateNames => [];
}
