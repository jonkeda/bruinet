namespace Bruinet.Core.Interfaces;

/// <summary>
/// Interface for controls that can receive keyboard focus.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public interface IFocusableControlObject<TScope> : IControlObject<TScope>
{
    /// <summary>
    /// Checks if the control currently has focus.
    /// </summary>
    /// <returns>True if focused, false if not, null if element not found.</returns>
    bool? IsFocused();

    /// <summary>
    /// Waits for the control to have or lose focus.
    /// Uses the nullable skip pattern - null skips the check.
    /// </summary>
    /// <param name="expected">Expected focus state. Null skips the check.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>True if condition met within timeout, false if timeout reached.</returns>
    bool WaitFocused(bool? expected, int? timeoutMs = null);

    /// <summary>
    /// Asserts the control's focus state.
    /// Uses the nullable skip pattern - null skips the check.
    /// </summary>
    /// <param name="expected">Expected focus state. Null skips the check.</param>
    /// <param name="message">Optional custom assertion message.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertFocused(bool? expected, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Sets focus to the control.
    /// </summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope Focus(int? timeoutMs = null);

    /// <summary>
    /// Removes focus from the control.
    /// </summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope Blur(int? timeoutMs = null);
}
