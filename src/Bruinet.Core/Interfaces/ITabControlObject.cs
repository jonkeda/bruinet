namespace Bruinet.Core.Interfaces;

/// <summary>
/// Interface for tab controls allowing tab selection.
/// Tabs are clickable controls that can be selected/unselected.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public interface ITabControlObject<TScope> : IClickableControlObject<TScope>
{
    /// <summary>
    /// Gets the title/text of the tab.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Checks if the tab is currently selected.
    /// </summary>
    /// <returns>True if selected, false if not, null if element not found.</returns>
    bool? IsSelected();

    /// <summary>
    /// Waits for the tab to be selected or unselected.
    /// Uses the nullable skip pattern - null skips the check.
    /// </summary>
    /// <param name="expected">Expected selected state. Null skips the check.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>True if condition met within timeout, false if timeout reached.</returns>
    bool WaitSelected(bool? expected, int? timeoutMs = null);

    /// <summary>
    /// Asserts the tab is selected or unselected.
    /// Uses the nullable skip pattern - null skips the check.
    /// </summary>
    /// <param name="expected">Expected selected state. Null skips the check.</param>
    /// <param name="message">Optional custom assertion message.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    /// <exception cref="Bruinet.Core.Exceptions.AssertionException">Thrown when assertion fails.</exception>
    TScope AssertSelected(bool? expected, string? message = null, int? timeoutMs = null);
}
