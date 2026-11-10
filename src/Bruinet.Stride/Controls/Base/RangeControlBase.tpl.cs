using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Controls;

/// <summary>
/// Base class for Stride ranges (a slider): GetValue, SetValue, Increment, Decrement. The game reports
/// the slider's value and bounds; its step is one unless the page object says otherwise.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class RangeControlBase<TScope> : ClickableControlBase<TScope>, IRangeControlObject<TScope>
    where TScope : IStrideScope<TScope>
{
    protected RangeControlBase(IStrideScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected RangeControlBase(IStrideScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    #region Core Methods

    /// <summary>The value; null for an element that is not a slider.</summary>
    /// <param name="element">The pre-found element.</param>
    [GenerateComparisons(Comparison.Equals | Comparison.GreaterThan | Comparison.AtLeast
        | Comparison.LessThan | Comparison.AtMost)]
    protected virtual double? GetValueCore(IStrideElement? element)
        => element?.RangeValue;

    /// <summary>Sets the value once, and confirms it took.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="value">The value; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetValueCore(IStrideElement element, double? value, int? timeoutMs = null)
    {
        if (value is not { } target)
        {
            return;
        }

        ActAndConfirm(
            "SetValue", () => element.SetRangeValue(target),
            () => GetValueCore(element), actual => actual is { } read && Math.Abs(read - target) < 1e-9,
            target.ToString(CultureInfo.InvariantCulture), timeoutMs);
    }

    /// <summary>The range's lowest value.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual double? GetMinimumCore(IStrideElement? element) => element?.RangeMinimum;

    /// <summary>The range's highest value.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual double? GetMaximumCore(IStrideElement? element) => element?.RangeMaximum;

    /// <summary>One step of the range: one, unless a derived control says otherwise.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual double? GetStepCore(IStrideElement? element) => element == null ? null : 1;

    /// <summary>Raises the value by one step, up to the maximum.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void IncrementCore(IStrideElement element, int? timeoutMs = null)
    {
        var next = (GetValueCore(element) ?? 0) + (GetStepCore(element) ?? 1);
        SetValueCore(element, GetMaximumCore(element) is { } max ? Math.Min(next, max) : next, timeoutMs);
    }

    /// <summary>Lowers the value by one step, down to the minimum.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void DecrementCore(IStrideElement element, int? timeoutMs = null)
    {
        var next = (GetValueCore(element) ?? 0) - (GetStepCore(element) ?? 1);
        SetValueCore(element, GetMinimumCore(element) is { } min ? Math.Max(next, min) : next, timeoutMs);
    }

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
