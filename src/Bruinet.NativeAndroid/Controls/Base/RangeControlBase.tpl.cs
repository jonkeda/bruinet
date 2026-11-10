using System.Globalization;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.NativeAndroid.Interfaces;

namespace Bruinet.NativeAndroid.Controls;

/// <summary>
/// Base class for native Android ranges (a seek bar, a rating bar, a progress bar): GetValue,
/// SetValue, Increment, Decrement. UiAutomator2 reads the progress as the view's text and sets it
/// with Android's set-progress accessibility action. Android does not report the range's bounds,
/// so the page object gives them.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class RangeControlBase<TScope> : ClickableControlBase<TScope>, IRangeControlObject<TScope>
    where TScope : INativeAndroidScope<TScope>
{
    private readonly double _minimum;
    private readonly double? _maximum;
    private readonly double _step;

    protected RangeControlBase(INativeAndroidScope<TScope> scope, Locator locator, double minimum = 0, double? maximum = null, double step = 1)
        : base(scope, locator)
    {
        (_minimum, _maximum, _step) = (minimum, maximum, step);
    }

    protected RangeControlBase(INativeAndroidScope<TScope> scope, string resourceIdOrXPath, double minimum = 0, double? maximum = null, double step = 1)
        : base(scope, resourceIdOrXPath)
    {
        (_minimum, _maximum, _step) = (minimum, maximum, step);
    }

    #region Core Methods

    /// <summary>The progress; null when it does not read as a number.</summary>
    /// <param name="element">The pre-found view.</param>
    [GenerateComparisons(Comparison.EqualTo | Comparison.GreaterThan | Comparison.AtLeast
        | Comparison.LessThan | Comparison.AtMost)]
    protected virtual double? GetValueCore(INativeAndroidElement? element)
        => Parse(element?.Text);

    /// <summary>Sets the progress once, and confirms it took.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="value">The value; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetValueCore(INativeAndroidElement element, double? value, int? timeoutMs = null)
    {
        if (value is not { } target)
        {
            return;
        }

        var text = target.ToString(CultureInfo.InvariantCulture);
        ActAndConfirm(
            "SetValue", () => element.SendKeys(text),
            () => GetValueCore(element), actual => actual == target,
            $"'{text}'", timeoutMs);
    }

    /// <summary>The range's lowest value, as the page object gives it.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual double? GetMinimumCore(INativeAndroidElement? element) => element == null ? null : _minimum;

    /// <summary>The range's highest value, as the page object gives it; null when it gives none.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual double? GetMaximumCore(INativeAndroidElement? element) => element == null ? null : _maximum;

    /// <summary>One step of the range, as the page object gives it.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual double? GetStepCore(INativeAndroidElement? element) => element == null ? null : _step;

    /// <summary>Raises the value by one step, up to the maximum.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void IncrementCore(INativeAndroidElement element, int? timeoutMs = null)
    {
        var next = (GetValueCore(element) ?? _minimum) + _step;
        SetValueCore(element, _maximum is { } max ? Math.Min(next, max) : next, timeoutMs);
    }

    /// <summary>Lowers the value by one step, down to the minimum.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void DecrementCore(INativeAndroidElement element, int? timeoutMs = null)
    {
        var next = (GetValueCore(element) ?? _minimum) - _step;
        SetValueCore(element, Math.Max(next, _minimum), timeoutMs);
    }

    private static double? Parse(string? value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;

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
