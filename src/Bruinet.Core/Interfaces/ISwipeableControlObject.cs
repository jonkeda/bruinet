namespace Bruinet.Core.Interfaces;

/// <summary>
/// Interface for controls that support swipe gestures.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not mobile-only</b>, though it said so until the gesture bridge existed. A swipe on Windows
/// is a verb the app under test answers on an element it declared, which needs no pointer and no
/// touch screen; on Android and iOS the same call is real touch input. The control names the
/// gesture either way.
/// </para>
/// <para>
/// What is still platform-shaped is <i>addressability</i>, and that is a different limitation:
/// MAUI's <c>SwipeView</c> publishes no <c>AutomationId</c> on Windows, so a member that finds
/// its element before acting cannot run there whatever the gesture costs.
/// </para>
/// </remarks>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public interface ISwipeableControlObject<TScope> : IControlObject<TScope>
{
    /// <summary>
    /// Performs a swipe left gesture.
    /// </summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope SwipeLeft(int? timeoutMs = null);

    /// <summary>
    /// Performs a swipe right gesture.
    /// </summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope SwipeRight(int? timeoutMs = null);

    /// <summary>
    /// Performs a swipe up gesture.
    /// </summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope SwipeUp(int? timeoutMs = null);

    /// <summary>
    /// Performs a swipe down gesture.
    /// </summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope SwipeDown(int? timeoutMs = null);

    /// <summary>
    /// Performs a swipe from one point to another within the control.
    /// </summary>
    /// <param name="startX">Starting X coordinate (relative to control).</param>
    /// <param name="startY">Starting Y coordinate (relative to control).</param>
    /// <param name="endX">Ending X coordinate (relative to control).</param>
    /// <param name="endY">Ending Y coordinate (relative to control).</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope Swipe(int startX, int startY, int endX, int endY, int? timeoutMs = null);
}
