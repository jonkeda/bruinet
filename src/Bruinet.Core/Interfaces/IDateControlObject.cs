namespace Bruinet.Core.Interfaces;

/// <summary>
/// Interface for date picker controls.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public interface IDateControlObject<TScope> : IControlObject<TScope>
{
    /// <summary>
    /// Gets the currently selected date.
    /// </summary>
    /// <returns>The selected date, or null if no date selected or element not found.</returns>
    DateTime? GetDate();

    /// <summary>
    /// Sets the date value.
    /// Uses the nullable skip pattern - null skips the operation.
    /// </summary>
    /// <param name="date">The date to set. Null skips the operation.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope SetDate(DateTime? date, int? timeoutMs = null);

    /// <summary>
    /// Waits for the date to match expected value.
    /// Uses the nullable skip pattern - null skips the check.
    /// </summary>
    /// <param name="expected">Expected date value. Null skips the check.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>True if condition met within timeout, false if timeout reached.</returns>
    bool WaitDate(DateTime? expected, int? timeoutMs = null);

    /// <summary>
    /// Asserts the current date value.
    /// Uses the nullable skip pattern - null skips the check.
    /// </summary>
    /// <param name="expected">Expected date value. Null skips the check.</param>
    /// <param name="message">Optional custom assertion message.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertDate(DateTime? expected, string? message = null, int? timeoutMs = null);
}
