
namespace Bruinet.Core.Interfaces;

/// <summary>
/// Text display capability for labels, spans, and other text elements.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public interface ITextControlObject<TScope> : IControlObject<TScope>
{
    /// <summary>
    /// Wait until text equals expected value.
    /// If expected is null, returns true immediately (skip).
    /// </summary>
    bool WaitText(string? expected, int? timeoutMs = null);

    /// <summary>
    /// Wait until text contains expected substring.
    /// If expected is null, the wait is skipped and null is returned.
    /// </summary>
    bool WaitTextContains(string? expected, int? timeoutMs = null);

    /// <summary>
    /// Assert text starts with expected prefix.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertTextStartsWith(string? expected, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Assert text ends with expected suffix.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertTextEndsWith(string? expected, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Assert text is empty or not empty.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <param name="expected">True to assert empty, false to assert not empty, null to skip.</param>
    /// <param name="message">Optional custom assertion message.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertTextEmpty(bool? expected, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Assert text equals expected value.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    [BddThen("{control} should equal {value}", CommandId = "Control.AssertText")]
    TScope AssertText(string? expected, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Assert text contains expected substring.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    [BddThen("{control} should contain {value}", CommandId = "Control.AssertTextContains")]
    TScope AssertTextContains(string? expected, string? message = null, int? timeoutMs = null);
}
