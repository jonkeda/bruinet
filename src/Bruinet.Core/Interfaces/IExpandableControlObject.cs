namespace Bruinet.Core.Interfaces;

/// <summary>
/// Interface for controls that can be expanded and collapsed.
/// Used for expanders, accordions, tree nodes, and collapsible sections.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public interface IExpandableControlObject<TScope> : IClickableControlObject<TScope>
{
    /// <summary>
    /// Checks if the control is currently expanded.
    /// </summary>
    /// <returns>True if expanded, false if collapsed, null if element not found.</returns>
    bool? IsExpanded();

    /// <summary>
    /// Waits for the control to be expanded or collapsed.
    /// Uses the nullable skip pattern - null skips the check.
    /// </summary>
    /// <param name="expected">Expected expanded state. Null skips the check.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>True if condition met within timeout, false if timeout reached.</returns>
    bool WaitExpanded(bool? expected, int? timeoutMs = null);

    /// <summary>
    /// Asserts the control's expanded state.
    /// Uses the nullable skip pattern - null skips the check.
    /// </summary>
    /// <param name="expected">Expected expanded state. Null skips the check.</param>
    /// <param name="message">Optional custom assertion message.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertExpanded(bool? expected, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Expands the control. No-op if already expanded.
    /// </summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope Expand(int? timeoutMs = null);

    /// <summary>
    /// Collapses the control. No-op if already collapsed.
    /// </summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope Collapse(int? timeoutMs = null);

    /// <summary>
    /// Toggles the expanded state.
    /// </summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope ToggleExpanded(int? timeoutMs = null);
}
