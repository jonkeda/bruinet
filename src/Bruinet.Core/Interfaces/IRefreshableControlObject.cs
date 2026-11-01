namespace Bruinet.Core.Interfaces;

/// <summary>
/// Interface for controls that support pull-to-refresh.
/// Primarily used for mobile platforms.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public interface IRefreshableControlObject<TScope> : IControlObject<TScope>
{
    /// <summary>
    /// Checks if the control is currently refreshing.
    /// </summary>
    /// <returns>True if refreshing, false if not, null if element not found.</returns>
    bool? IsRefreshing();

    /// <summary>
    /// Waits for refresh state to match expected.
    /// Uses the nullable skip pattern - null skips the check.
    /// </summary>
    /// <param name="expected">Expected refresh state. Null skips the check.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>True if condition met within timeout, false if timeout reached.</returns>
    bool WaitRefreshing(bool? expected, int? timeoutMs = null);

    /// <summary>
    /// Asserts the refresh state.
    /// Uses the nullable skip pattern - null skips the check.
    /// </summary>
    /// <param name="expected">Expected refresh state. Null skips the check.</param>
    /// <param name="message">Optional custom assertion message.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertRefreshing(bool? expected, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Performs a pull-to-refresh gesture.
    /// </summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope PullToRefresh(int? timeoutMs = null);
}
