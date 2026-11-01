namespace Bruinet.Core.Interfaces;

/// <summary>
/// Numeric range capability for sliders, steppers, progress bars.
/// Action methods return TScope for fluent method chaining.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public interface IRangeControlObject<TScope> : IControlObject<TScope>
{
    /// <summary>
    /// Get the current value.
    /// Returns null if element not found.
    /// </summary>
    double? GetValue(int? timeoutMs = null);

    /// <summary>
    /// Set the value.
    /// If value is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope SetValue(double? value, int? timeoutMs = null);

    /// <summary>
    /// Get the minimum allowed value.
    /// Returns null if element not found.
    /// </summary>
    double? GetMinimum(int? timeoutMs = null);

    /// <summary>
    /// Get the maximum allowed value.
    /// Returns null if element not found.
    /// </summary>
    double? GetMaximum(int? timeoutMs = null);

    /// <summary>
    /// Get the step/increment value.
    /// Returns null if element not found.
    /// </summary>
    double? GetStep(int? timeoutMs = null);

    /// <summary>
    /// Assert value equals expected.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertValue(double? expected, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Wait until value equals expected.
    /// If expected is null, the wait is skipped and null is returned.
    /// </summary>
    bool WaitValue(double? expected, int? timeoutMs = null);

    /// <summary>
    /// Assert value equals expected within the given tolerance.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope AssertValueWithin(double? expected, double tolerance, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Wait until value equals expected within the given tolerance.
    /// If expected is null, returns true immediately (skip).
    /// </summary>
    bool WaitValueWithin(double? expected, double tolerance, int? timeoutMs = null);

    /// <summary>
    /// Increment value by step amount.
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope Increment(int? timeoutMs = null);

    /// <summary>
    /// Decrement value by step amount.
    /// </summary>
    /// <returns>The containing scope for fluent chaining.</returns>
    TScope Decrement(int? timeoutMs = null);
}
