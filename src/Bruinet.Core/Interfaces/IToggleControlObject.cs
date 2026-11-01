namespace Bruinet.Core.Interfaces;

/// <summary>
/// Toggle state capability for checkboxes, switches, radio buttons.
/// Action methods return TScope for fluent method chaining.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public interface IToggleControlObject<TScope> : IControlObject<TScope>
{
    /// <summary>
    /// Check if the control is in checked/on state.
    /// Returns null if element not found.
    /// </summary>
    bool? IsChecked();

    /// <summary>
    /// Toggle the control state.
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope Toggle(int? timeoutMs = null);

    /// <summary>
    /// Set the control to checked/unchecked state.
    /// If checked is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope SetChecked(bool? @checked, int? timeoutMs = null);

    /// <summary>
    /// Set to checked state (convenience method).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    [BddWhen("I check {control}", CommandId = "Control.Check")]
    [BddWhen("{user} checks {control}", CommandId = "Control.Check")]
    TScope Check(int? timeoutMs = null);

    /// <summary>
    /// Set to unchecked state (convenience method).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    [BddWhen("I uncheck {control}", CommandId = "Control.Uncheck")]
    [BddWhen("{user} unchecks {control}", CommandId = "Control.Uncheck")]
    TScope Uncheck(int? timeoutMs = null);

    /// <summary>
    /// Assert checked state matches expected value.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    [BddThen("{control} should be checked", HasLiteral = true, Literal = true, CommandId = "Control.AssertChecked.True")]
    [BddThen("{control} should be unchecked", HasLiteral = true, Literal = false, CommandId = "Control.AssertChecked.False")]
    TScope AssertChecked(bool? expected, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Wait until checked state matches expected value.
    /// If expected is null, returns true immediately (skip).
    /// </summary>
    bool WaitChecked(bool? expected, int? timeoutMs = null);
}
