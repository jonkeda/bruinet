
namespace Bruinet.Core.Interfaces;

/// <summary>
/// Base interface for all controls in the Bruinet framework.
/// Provides identity, state querying, waiting, and assertion capabilities.
/// Action methods return TScope for fluent method chaining.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public interface IElementObject<TScope>
{
    /// <summary>
    /// Check if the element exists in the UI tree.
    /// </summary>
    bool Exists();

    /// <summary>
    /// Check if the element is visible.
    /// Returns null if element doesn't exist.
    /// </summary>
    bool? IsVisible();

    /// <summary>
    /// Check if the element is enabled.
    /// Returns null if element doesn't exist.
    /// </summary>
    bool? IsEnabled();

    /// <summary>
    /// Wait until element existence matches expected value.
    /// If expected is null, returns true immediately (skip).
    /// </summary>
    bool WaitExists(bool? expected = true, int? timeoutMs = null);

    /// <summary>
    /// Wait until element visibility matches expected value.
    /// If expected is null, returns true immediately (skip).
    /// </summary>
    bool WaitVisible(bool? expected = true, int? timeoutMs = null);

    /// <summary>
    /// Wait until element enabled state matches expected value.
    /// If expected is null, returns true immediately (skip).
    /// </summary>
    bool WaitEnabled(bool? expected = true, int? timeoutMs = null);

    /// <summary>
    /// Assert element existence matches expected value.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    [BddThen("{control} should exist", HasLiteral = true, Literal = true, CommandId = "Control.AssertExists")]
    [BddThen("{control} should not exist", HasLiteral = true, Literal = false, CommandId = "Control.AssertExists.False")]
    TScope AssertExists(bool? expected = true, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Assert element visibility matches expected value.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    [BddThen("{control} should be visible", HasLiteral = true, Literal = true, CommandId = "Control.AssertVisible")]
    [BddThen("{control} should not be visible", HasLiteral = true, Literal = false, CommandId = "Control.AssertVisible.False")]
    TScope AssertVisible(bool? expected = true, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Assert element enabled state matches expected value.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    [BddThen("{control} should be enabled", HasLiteral = true, Literal = true, CommandId = "Control.AssertEnabled")]
    [BddThen("{control} should be disabled", HasLiteral = true, Literal = false, CommandId = "Control.AssertEnabled.False")]
    TScope AssertEnabled(bool? expected = true, string? message = null, int? timeoutMs = null);

}
