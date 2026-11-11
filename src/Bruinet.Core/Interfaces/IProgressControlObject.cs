namespace Bruinet.Core.Interfaces;

/// <summary>
/// Interface for progress indicator controls.
/// Progress values are normalized to 0.0-1.0 range.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public interface IProgressControlObject<TScope> : IControlObject<TScope>
{
    /// <summary>
    /// Checks if the progress is indeterminate (unknown duration).
    /// </summary>
    /// <returns>True if indeterminate, false if determinate, false when the element is not found.</returns>
    bool IsIndeterminate();

    /// <summary>
    /// Gets the current progress value (0.0 to 1.0).
    /// </summary>
    /// <returns>Progress value between 0.0 and 1.0, or false when the element is not found.</returns>
    double? GetProgress(int? timeoutMs = null);

    /// <summary>
    /// Waits for progress to reach a specific value.
    /// Uses the nullable skip pattern - null skips the check.
    /// </summary>
    /// <param name="expected">Expected progress value (0.0 to 1.0). Null skips the check.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>True if condition met within timeout, false if timeout reached.</returns>
    bool WaitProgress(double? expected, int? timeoutMs = null);

    /// <summary>
    /// Asserts the current progress value.
    /// Uses the nullable skip pattern - null skips the check.
    /// </summary>
    /// <param name="expected">Expected progress value (0.0 to 1.0). Null skips the check.</param>
    /// <param name="message">Optional custom assertion message.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertProgress(double? expected, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Waits for progress to complete (reach 1.0 or disappear).
    /// </summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>True if completed within timeout, false if timeout reached.</returns>
    bool WaitComplete(int? timeoutMs = null);

    /// <summary>
    /// Asserts progress is complete.
    /// </summary>
    /// <param name="message">Optional custom assertion message.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertComplete(string? message = null, int? timeoutMs = null);
}
