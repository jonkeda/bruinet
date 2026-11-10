using System.Globalization;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Controls;

/// <summary>
/// Base class for numeric Html inputs (&lt;input type="range|number"&gt;): GetValue, SetValue,
/// Increment, Decrement.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class RangeControlBase<TScope> : FocusableControlBase<TScope>, IRangeControlObject<TScope>
    where TScope : IHtmlScope<TScope>
{
    protected RangeControlBase(IHtmlScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected RangeControlBase(IHtmlScope<TScope> scope, string selectorOrId)
        : base(scope, selectorOrId)
    {
    }

    #region Core Methods

    /// <summary>The input's value; null when it is empty or not a number.</summary>
    /// <param name="element">The pre-found element.</param>
    [GenerateComparisons(Comparison.EqualTo | Comparison.GreaterThan | Comparison.AtLeast
        | Comparison.LessThan | Comparison.AtMost)]
    protected virtual double? GetValueCore(IHtmlElement? element)
        => Parse(element?.InputValue);

    /// <summary>Fills the input with a value, and confirms it took.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="value">The value; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetValueCore(IHtmlElement element, double? value, int? timeoutMs = null)
    {
        if (value is not { } target)
        {
            return;
        }

        var text = target.ToString(CultureInfo.InvariantCulture);
        ActAndConfirm(
            "Fill", () => element.Fill(text),
            () => GetValueCore(element), actual => actual == target,
            $"'{text}'", timeoutMs);
    }

    /// <summary>The input's <c>min</c>; null when it has none.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual double? GetMinimumCore(IHtmlElement? element)
        => Parse(element?.GetAttribute("min"));

    /// <summary>The input's <c>max</c>; null when it has none.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual double? GetMaximumCore(IHtmlElement? element)
        => Parse(element?.GetAttribute("max"));

    /// <summary>The input's <c>step</c>; 1 when it has none.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual double? GetStepCore(IHtmlElement? element)
        => element == null ? null : Parse(element.GetAttribute("step")) ?? 1;

    /// <summary>Raises the value by one step, up to the maximum.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void IncrementCore(IHtmlElement element, int? timeoutMs = null)
    {
        var next = (GetValueCore(element) ?? 0) + (GetStepCore(element) ?? 1);
        SetValueCore(element, GetMaximumCore(element) is { } max ? Math.Min(next, max) : next, timeoutMs);
    }

    /// <summary>Lowers the value by one step, down to the minimum.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void DecrementCore(IHtmlElement element, int? timeoutMs = null)
    {
        var next = (GetValueCore(element) ?? 0) - (GetStepCore(element) ?? 1);
        SetValueCore(element, GetMinimumCore(element) is { } min ? Math.Max(next, min) : next, timeoutMs);
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
