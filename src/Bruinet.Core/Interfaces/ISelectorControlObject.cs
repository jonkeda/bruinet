
namespace Bruinet.Core.Interfaces;

/// <summary>
/// Single selection capability for pickers, comboboxes, dropdowns.
/// Action methods return TScope for fluent method chaining.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public interface ISelectorControlObject<TScope> : IControlObject<TScope>
{
    /// <summary>
    /// Select item by visible text.
    /// If text is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    [BddWhen("I select {value} from {control}", CommandId = "Control.SelectByText")]
    [BddWhen("{user} selects {value} from {control}", CommandId = "Control.SelectByText")]
    TScope SelectByText(string? text, int? timeoutMs = null);

    /// <summary>
    /// Select item by index (0-based).
    /// If index is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope SelectByIndex(int? index, int? timeoutMs = null);

    /// <summary>
    /// Select item by value attribute.
    /// If value is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope SelectByValue(string? value, int? timeoutMs = null);

    /// <summary>
    /// Get the currently selected item's text.
    /// Returns null if element not found.
    /// </summary>
    string? GetSelectedText(int? timeoutMs = null);

    /// <summary>
    /// Wait until selected text matches expected value.
    /// If expected is null, the wait is skipped and null is returned.
    /// </summary>
    bool WaitSelectedText(string? expected, int? timeoutMs = null);

    /// <summary>
    /// Assert selected text matches expected value.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    [BddThen("{control} should have selected {value}", CommandId = "Control.AssertSelectedText")]
    TScope AssertSelectedText(string? expected, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Get the currently selected item's index.
    /// Returns null if element not found.
    /// </summary>
    int? GetSelectedIndex(int? timeoutMs = null);

    /// <summary>
    /// Wait until selected index matches expected value.
    /// If expected is null, the wait is skipped and null is returned.
    /// </summary>
    bool WaitSelectedIndex(int? expected, int? timeoutMs = null);

    /// <summary>
    /// Assert selected index matches expected value.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertSelectedIndex(int? expected, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Get all available item texts.
    /// Returns null if element not found.
    /// </summary>
    IReadOnlyList<string>? GetItemTexts(int? timeoutMs = null);

    /// <summary>
    /// Get the count of available items.
    /// Returns null if element not found.
    /// </summary>
    int? GetItemCount(int? timeoutMs = null);

    /// <summary>
    /// Wait until item count matches expected value.
    /// If expected is null, the wait is skipped and null is returned.
    /// </summary>
    bool WaitItemCount(int? expected, int? timeoutMs = null);

    /// <summary>
    /// Assert item count matches expected value.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertItemCount(int? expected, string? message = null, int? timeoutMs = null);
}
