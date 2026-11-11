
namespace Bruinet.Core.Interfaces;

/// <summary>
/// Click capability for buttons, links, images.
/// Action methods return TScope for fluent method chaining.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public interface IClickableControlObject<TScope> : IControlObject<TScope>
{
    /// <summary>
    /// Check if the control is clickable (visible and enabled).
    /// False when the element is not found.
    /// </summary>
    bool IsClickable();

    /// <summary>
    /// Perform a single click on the control.
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    [BddWhen("I tap {control}", CommandId = "Control.Tap", Navigates = true)]
    [BddWhen("{user} taps {control}", CommandId = "Control.Tap", Navigates = true)]
    TScope Click(int? timeoutMs = null);

    /// <summary>
    /// Perform a double-click on the control.
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope DoubleClick(int? timeoutMs = null);

    /// <summary>
    /// Perform a right-click (context click) on the control.
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope RightClick(int? timeoutMs = null);

    /// <summary>
    /// Wait until control is clickable (visible and enabled).
    /// If expected is null, returns true immediately (skip).
    /// </summary>
    bool WaitClickable(bool? expected = true, int? timeoutMs = null);

    /// <summary>
    /// Assert control clickable state matches expected value.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertClickable(bool? expected = true, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Hover the mouse over the control.
    /// </summary>
    /// <param name="timeoutMs">Optional timeout for element to be visible.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope Hover(int? timeoutMs = null);

    /// <summary>
    /// Perform a long press (touch and hold) on the control.
    /// Primarily used for mobile platforms.
    /// </summary>
    /// <param name="durationMs">Duration of the press in milliseconds. Default is platform-specific.</param>
    /// <param name="timeoutMs">Optional timeout for element to be ready.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope LongPress(int? durationMs = null, int? timeoutMs = null);
}
