namespace Bruinet.Maui.Controls.Base;

/// <summary>
/// Base class for MAUI controls with range/numeric capability.
/// Implements IRangeControlObject with GetValue, SetValue, Increment, Decrement.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class RangeControlBase<TScope> : FocusableControlBase<TScope>,
    IRangeControlObject<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates a new range control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the element.</param>
    protected RangeControlBase(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates a new range control within the specified scope using a string locator value.
    /// Uses the scope's DefaultLocatorStrategy to create the locator.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID, name).</param>
    protected RangeControlBase(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Core Methods (Element-Aware, No Logging)

    /// <summary>
    /// Gets value from pre-found element: the range value the element publishes, in the app's units.
    /// </summary>
    /// <remarks>
    /// No text fallback: an Android seek bar's text is its raw progress, 0 to <c>int.MaxValue</c>,
    /// and reading it as the value presented a fraction as a number. The Android driver publishes a
    /// range value only when the app says it is in app units (<c>Bruinet.Maui.AppSupport</c>).
    /// </remarks>
    /// <param name="element">The pre-found element.</param>
    /// <returns>The current value, or null when the element publishes none.</returns>
    [GenerateComparisons(Comparison.EqualTo | Comparison.GreaterThan | Comparison.AtLeast
        | Comparison.LessThan | Comparison.AtMost)]
    protected virtual double? GetValueCore(IMauiElement? element)
    {
        return element?.RangeValue;
    }

    /// <summary>
    /// Sets value on pre-found element.
    /// The element picks the route: the RangeValue pattern on Windows, the accessibility
    /// set-progress action on Android.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="value">The value to set. Null skips the operation.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetValueCore(IMauiElement element, double? value, int? timeoutMs = null)
    {
        if (value == null)
            return;

        EnsureSettableCore(element);

        // A refused value throws rather than typing into whatever has focus.
        element.SetRangeValue(value.Value);
    }

    /// <summary>
    /// Gets minimum value from pre-found element.
    /// Uses FlaUI RangeValue pattern when available.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <returns>The minimum value, or null if not available.</returns>
    protected virtual double? GetMinimumCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        // No attribute fallback: a range's bounds are published by the range pattern, or on
        // Android by AppSupport, or not at all.
        return element.RangeMinimum;
    }

    /// <summary>
    /// Gets maximum value from pre-found element.
    /// Uses FlaUI RangeValue pattern when available.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <returns>The maximum value, or null if not available.</returns>
    protected virtual double? GetMaximumCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        // Published by the range pattern or not at all - see GetMinimumCore.
        return element.RangeMaximum;
    }

    /// <summary>
    /// Gets step value from pre-found element.
    /// Uses RangeValue pattern when available.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <returns>The step value, or null if not available.</returns>
    protected virtual double? GetStepCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        // Published by the range pattern or not at all - see GetMinimumCore.
        return element.RangeSmallChange ?? 1.0;
    }

    /// <summary>
    /// Increments value by step amount.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void IncrementCore(IMauiElement element, int? timeoutMs = null)
    {
        var current = GetValueCore(element) ?? 0;
        var step = GetStepCore(element) ?? 1;
        var max = GetMaximumCore(element);

        var newValue = current + step;
        if (max.HasValue && newValue > max.Value)
        {
            newValue = max.Value;
        }

        SetValueCore(element, newValue, timeoutMs);
    }

    /// <summary>
    /// Decrements value by step amount.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void DecrementCore(IMauiElement element, int? timeoutMs = null)
    {
        var current = GetValueCore(element) ?? 0;
        var step = GetStepCore(element) ?? 1;
        var min = GetMinimumCore(element);

        var newValue = current - step;
        if (min.HasValue && newValue < min.Value)
        {
            newValue = min.Value;
        }

        SetValueCore(element, newValue, timeoutMs);
    }

    #endregion

    #region Guards

    /// <summary>
    /// Throws when the element cannot accept a new value.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual void EnsureSettableCore(IMauiElement element)
    {
        if (IsEnabledCore(element) != true)
        {
            throw new ElementNotReadyException(Locator, NotReadyReason.Disabled);
        }
    }

    #endregion

    #region Hand-written Convenience Members

    /// <summary>
    /// Waits until the value matches expected within the given tolerance.
    /// </summary>
    /// <param name="expected">The expected value. Null skips the wait.</param>
    /// <param name="tolerance">The allowed absolute difference.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>True when the value matched within the timeout.</returns>
    public bool WaitValueWithin(double? expected, double tolerance, int? timeoutMs = null)
    {
        return RunWaitWithElement(expected,
            element =>
            {
                var actual = GetValueCore(element);
                return actual != null && Math.Abs(actual.Value - expected!.Value) <= tolerance;
            },
            timeoutMs);
    }

    /// <summary>
    /// Asserts the value matches expected within the given tolerance.
    /// </summary>
    /// <param name="expected">The expected value. Null skips the assertion.</param>
    /// <param name="tolerance">The allowed absolute difference.</param>
    /// <param name="message">Optional custom message for the assertion failure.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    public TScope AssertValueWithin(double? expected, double tolerance, string? message = null,
        int? timeoutMs = null)
    {
        return RunAssertWithElement(expected,
            GetValueCore,
            (actual, expected1) =>
            {
                if (actual == null || expected1 == null)
                    return actual == expected1;
                return Math.Abs(actual.Value - expected1.Value) <= tolerance;
            },
            message ?? $"Expected Value to be '{expected}' (±{tolerance}). Locator: {Locator}",
            timeoutMs);
    }

    #endregion
}
