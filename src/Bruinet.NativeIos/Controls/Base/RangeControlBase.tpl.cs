using System.Globalization;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls;

/// <summary>
/// Base class for native iOS ranges (a slider, a progress view): GetValue, SetValue, Increment,
/// Decrement. XCUITest reports the position as a percentage ("50%") and sets a slider by a fraction
/// from 0 to 1; it does not report the range's bounds, so the page object gives them. Without a
/// maximum the value is the percentage itself, 0 to 100.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class RangeControlBase<TScope> : ClickableControlBase<TScope>, IRangeControlObject<TScope>
    where TScope : INativeIosScope<TScope>
{
    private const int AdjustAttempts = 3;

    // How close SetValue must land, as a share of the range: what XCUITest can reach (AdjustTo).
    private const double SetValuePrecision = 0.05;

    private readonly double _minimum;
    private readonly double? _maximum;
    private readonly double _step;

    protected RangeControlBase(INativeIosScope<TScope> scope, Locator locator, double minimum = 0, double? maximum = null, double step = 1)
        : base(scope, locator)
    {
        (_minimum, _maximum, _step) = (minimum, maximum, step);
    }

    protected RangeControlBase(INativeIosScope<TScope> scope, string identifierOrXPath, double minimum = 0, double? maximum = null, double step = 1)
        : base(scope, identifierOrXPath)
    {
        (_minimum, _maximum, _step) = (minimum, maximum, step);
    }

    #region Core Methods

    /// <summary>The progress; null when it does not read as a number.</summary>
    /// <param name="element">The pre-found view.</param>
    [GenerateComparisons(Comparison.EqualTo | Comparison.GreaterThan | Comparison.AtLeast
        | Comparison.LessThan | Comparison.AtMost)]
    protected virtual double? GetValueCore(INativeIosElement? element)
        => Parse(element?.Value) is { } percent ? _minimum + (percent / 100 * Span) : null;

    /// <summary>
    /// Sets the value once, and confirms it took to within 5% of the range: XCUITest cannot place a
    /// slider's thumb more precisely.
    /// </summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="value">The value; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetValueCore(INativeIosElement element, double? value, int? timeoutMs = null)
    {
        if (value is not { } target)
        {
            return;
        }

        var fraction = Math.Clamp((target - _minimum) / Span, 0, 1);
        var tolerance = Math.Max(_step / 2, Span * SetValuePrecision);
        ActAndConfirm(
            "SetValue", () => AdjustTo(element, target, fraction, tolerance),
            () => GetValueCore(element), actual => actual is { } value && Math.Abs(value - target) <= tolerance,
            $"'{target.ToString(CultureInfo.InvariantCulture)}'", timeoutMs);
    }

    // The range's width: from the page object's bounds, else the percentage's 100.
    private double Span => _maximum is { } maximum ? maximum - _minimum : 100;

    /// <summary>The range's lowest value, as the page object gives it.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual double? GetMinimumCore(INativeIosElement? element) => element == null ? null : _minimum;

    /// <summary>The range's highest value, as the page object gives it; null when it gives none.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual double? GetMaximumCore(INativeIosElement? element) => element == null ? null : _maximum;

    /// <summary>One step of the range, as the page object gives it.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual double? GetStepCore(INativeIosElement? element) => element == null ? null : _step;

    /// <summary>Raises the value by one step, up to the maximum.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void IncrementCore(INativeIosElement element, int? timeoutMs = null)
    {
        var next = (GetValueCore(element) ?? _minimum) + _step;
        SetValueCore(element, _maximum is { } max ? Math.Min(next, max) : next, timeoutMs);
    }

    /// <summary>Lowers the value by one step, down to the minimum.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void DecrementCore(INativeIosElement element, int? timeoutMs = null)
    {
        var next = (GetValueCore(element) ?? _minimum) - _step;
        SetValueCore(element, Math.Max(next, _minimum), timeoutMs);
    }

    // XCUITest places the thumb only roughly, and overshoots in the direction it moves: a slider of
    // 0 to 100 set to 0.65 read 68, 71 or 70 depending on where the thumb started, and a slow drag
    // fell short by up to 6 and ignored moves under 4 (iOS 27 simulator, 2026-11-16). One SetValue
    // is one act, so it corrects itself by the error it reads, a few times.
    private void AdjustTo(INativeIosElement element, double target, double fraction, double tolerance)
    {
        for (var attempt = 0; attempt < AdjustAttempts; attempt++)
        {
            element.Adjust(fraction.ToString(CultureInfo.InvariantCulture));
            if (GetValueCore(element) is not { } now || Math.Abs(now - target) <= tolerance)
            {
                return;
            }

            fraction = Math.Clamp(fraction - ((now - target) / Span), 0, 1);
        }
    }

    private static double? Parse(string? value)
        => double.TryParse(value?.Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;

    #endregion

    #region Hand-written Convenience Members

    /// <summary>Waits until the value is within <paramref name="tolerance"/> of <paramref name="expected"/>.</summary>
    public bool WaitValueWithin(double? expected, double tolerance, int? timeoutMs = null)
    {
        return RunWaitWithElement(expected,
            element => GetValueCore(element) is { } actual && Math.Abs(actual - expected!.Value) <= tolerance,
            timeoutMs);
    }

    /// <summary>Asserts the value is within <paramref name="tolerance"/> of <paramref name="expected"/>.</summary>
    public TScope AssertValueWithin(double? expected, double tolerance, string? message = null,
        int? timeoutMs = null)
    {
        return RunAssertWithElement(expected,
            GetValueCore,
            (actual, expected1) => actual != null && expected1 != null
                ? Math.Abs(actual.Value - expected1.Value) <= tolerance
                : actual == expected1,
            message ?? $"Expected Value to be '{expected}' (±{tolerance}). Locator: {Locator}",
            timeoutMs);
    }

    #endregion
}
