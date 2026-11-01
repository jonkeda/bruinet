namespace Bruinet.Core.Interfaces;

/// <summary>
/// Text input capability for entries, editors, and other editable text controls.
/// Action methods return TScope for fluent method chaining.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public interface IEditableTextControlObject<TScope> : ITextControlObject<TScope>
{
    /// <summary>
    /// Enter text into the control (appends to existing).
    /// If text is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    [BddWhen("I enter {value} into {control}", CommandId = "Control.Enter")]
    [BddWhen("{user} enters {value} into {control}", CommandId = "Control.Enter")]
    TScope Enter(string? text, int? timeoutMs = null);

    /// <summary>
    /// Clear the control's text content.
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    [BddWhen("I clear {control}", CommandId = "Control.Clear")]
    [BddWhen("{user} clears {control}", CommandId = "Control.Clear")]
    TScope Clear(int? timeoutMs = null);

    /// <summary>
    /// Set the control's text (clears first, then enters).
    /// If text is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    [BddWhen("I set {control} to {value}", CommandId = "Control.SetText")]
    [BddWhen("{user} sets {control} to {value}", CommandId = "Control.SetText")]
    TScope SetText(string? text, int? timeoutMs = null);

    /// <summary>
    /// Append text to existing content without clearing.
    /// If text is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope Append(string? text, int? timeoutMs = null);

    /// <summary>
    /// Get the placeholder/hint text.
    /// Returns null if not available.
    /// </summary>
    string? GetPlaceholder(int? timeoutMs = null);

    /// <summary>
    /// Wait until placeholder text matches expected value.
    /// If expected is null, the wait is skipped and null is returned.
    /// </summary>
    bool WaitPlaceholder(string? expected, int? timeoutMs = null);

    /// <summary>
    /// Assert placeholder text matches expected value.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertPlaceholder(string? expected, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Check if the control is read-only.
    /// Returns null if element not found.
    /// </summary>
    bool? IsReadOnly();

    /// <summary>
    /// Wait until read-only state matches expected value.
    /// If expected is null, returns true immediately (skip).
    /// </summary>
    bool WaitReadOnly(bool? expected, int? timeoutMs = null);

    /// <summary>
    /// Assert read-only state matches expected value.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertReadOnly(bool? expected, string? message = null, int? timeoutMs = null);
}
