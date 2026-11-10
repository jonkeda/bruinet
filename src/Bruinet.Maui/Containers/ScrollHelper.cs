namespace Bruinet.Maui.Containers;

/// <summary>
/// Element-level scrolling primitives shared by containers and collections.
/// </summary>
public static class ScrollHelper
{
    /// <summary>
    /// Asks an element to bring itself into view.
    /// </summary>
    /// <param name="element">The element, or null.</param>
    /// <param name="timeoutMs">The most time the scroll may take: what is left of the caller's call.</param>
    /// <returns>True if the request was made; false for no element, or one with no route.</returns>
    /// <remarks>
    /// A true return means the request was accepted, <b>not</b> that the viewport moved.
    /// Callers that need to know must observe the resulting state themselves.
    /// </remarks>
    public static bool ScrollIntoView(IMauiElement? element, int timeoutMs)
    {
        if (element == null)
            return false;

        try
        {
            element.ScrollIntoView(timeoutMs);
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        finally
        {
            Bruinet.Core.Calls.AttemptMemo.Forget();
        }
    }

    /// <summary>
    /// Scrolls one step towards the end by the element's route.
    /// </summary>
    /// <returns>
    /// Whether anything may have moved: false only when the platform confirms nothing did. A swipe
    /// cannot confirm either way, so it reports true and the caller checks the content.
    /// </returns>
    public static bool StepForward(IMauiElement? element) => Step(element, forward: true);

    /// <summary>Scrolls one step towards the start. See <see cref="StepForward"/>.</summary>
    public static bool StepBack(IMauiElement? element) => Step(element, forward: false);

    private static bool Step(IMauiElement? element, bool forward)
    {
        if (element == null)
            return false;

        return Moving(() => element.ScrollContent(forward ? 1 : -1)) != ScrollStep.NotMoved;
    }

    /// <summary>
    /// Runs a scroll and then forgets what the running attempt read (<see cref="Bruinet.Core.Calls.AttemptMemo"/>):
    /// rows and bounds read before it are wrong after it. Not every head's scroll forgets by itself.
    /// </summary>
    internal static T Moving<T>(Func<T> scroll)
    {
        try
        {
            return scroll();
        }
        finally
        {
            Bruinet.Core.Calls.AttemptMemo.Forget();
        }
    }
}
