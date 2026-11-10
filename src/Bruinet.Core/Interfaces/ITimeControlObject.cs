namespace Bruinet.Core.Interfaces;

/// <summary>
/// Interface for time picker controls.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public interface ITimeControlObject<TScope> : IControlObject<TScope>
{
    /// <summary>
    /// Gets the currently selected time.
    /// </summary>
    /// <returns>The selected time, or null if no time selected or element not found.</returns>
    TimeSpan? GetTime(int? timeoutMs = null);

    /// <summary>
    /// Sets the time value.
    /// Uses the nullable skip pattern - null skips the operation.
    /// </summary>
    /// <param name="time">The time to set. Null skips the operation.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope SetTime(TimeSpan? time, int? timeoutMs = null);

    /// <summary>
    /// Waits for the time to match expected value.
    /// Uses the nullable skip pattern - null skips the check.
    /// </summary>
    /// <param name="expected">Expected time value. Null skips the check.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>True if condition met within timeout, false if timeout reached.</returns>
    bool WaitTime(TimeSpan? expected, int? timeoutMs = null);

    /// <summary>
    /// Asserts the current time value.
    /// Uses the nullable skip pattern - null skips the check.
    /// </summary>
    /// <param name="expected">Expected time value. Null skips the check.</param>
    /// <param name="message">Optional custom assertion message.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertTime(TimeSpan? expected, string? message = null, int? timeoutMs = null);
}
