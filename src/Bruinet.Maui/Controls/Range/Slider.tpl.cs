namespace Bruinet.Maui.Controls.Range;

using Bruinet.Core.Utilities;
using System.Globalization;

/// <summary>
/// MAUI Slider control for continuous value selection.
/// Inherits GetValue, SetValue, GetMinimum, GetMaximum, Increment, Decrement from RangeControlBase.
/// Provides additional slider-specific methods like SlideToPercentage.
/// Overrides SetValueCore to clamp the value to the slider's range before the element sets it -
/// through RangeValue on Windows, through the accessibility set-progress action on Android.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Slider<TScope> : Base.RangeControlBase<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates a new slider control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the slider element.</param>
    public Slider(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates a new slider control within the specified scope using a string locator value.
    /// Uses the scope's DefaultLocatorStrategy to create the locator.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID, name).</param>
    public Slider(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Core Methods (Element-Aware, No Logging)

    /// <summary>
    /// Sets slider value, clamped to the slider's range where the range is published.
    /// The element picks the route: the RangeValue pattern on Windows, the accessibility
    /// set-progress action on Android.
    /// </summary>
    /// <param name="element">The slider element.</param>
    /// <param name="value">The target value. Null skips the operation.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected override void SetValueCore(IMauiElement element, double? value, int? timeoutMs = null)
    {
        if (value == null) return;

        EnsureSettableCore(element);

        var min = GetMinimumCore(element) ?? double.NegativeInfinity;
        var max = GetMaximumCore(element) ?? double.PositiveInfinity;

        if (max < min)
        {
            throw new InvalidOperationException($"Invalid slider range: min={min}, max={max}");
        }

        var target = Math.Clamp(value.Value, min, max);
        element.SetRangeValue(target);

        // A slider may snap to its own step; a thousandth of the range covers that.
        var tolerance = double.IsFinite(max - min) ? Math.Max(0.01, (max - min) / 1000) : 0.01;
        var confirmation = Confirm(() => GetValueCore(element),
            actual => actual.HasValue && Math.Abs(actual.Value - target) <= tolerance,
            timeoutMs);
        if (!confirmation.IsConfirmed)
        {
            throw confirmation.Failure(Locator, "SetValue", lastError => new TimeoutException(
                $"Slider '{Locator.Value}' was set to {target} and reads {Describe(confirmation.LastValue)}.",
                lastError));
        }
    }

    private static string Describe(double? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "no value";

    /// <summary>
    /// Gets the current value as a percentage of the range from a pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <returns>The percentage (0-100), or null if the value or the range is not published.</returns>
    protected virtual double? GetPercentageCore(IMauiElement? element)
    {
        if (element == null) return null;

        var current = GetValueCore(element);
        var min = GetMinimumCore(element);
        var max = GetMaximumCore(element);
        if (current == null || min == null || max == null) return null;

        if (Math.Abs(max.Value - min.Value) < 0.0001) return 0;

        return ((current.Value - min.Value) / (max.Value - min.Value)) * 100.0;
    }

    /// <summary>The slider's published bound, or a failure saying it has none.</summary>
    private double RequireBound(double? bound, string which)
        => bound ?? throw new NotSupportedException(
            $"Slider '{Locator.Value}' does not publish its {which}. On Android the app includes "
            + "Bruinet.Maui.AppSupport (AddBruinetAutomationHandlers), which publishes it.");

    #endregion

    #region Slider-Specific Core Methods

    /// <summary>
    /// Slides to the specified percentage of the slider range.
    /// </summary>
    /// <param name="element">The pre-found slider element.</param>
    /// <param name="percentage">Percentage (0-100) to slide to. Null skips the operation.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SlideToPercentageCore(IMauiElement element, double? percentage,
        int? timeoutMs = null)
    {
        if (percentage == null) return;

        var min = RequireBound(GetMinimumCore(element), "minimum");
        var max = RequireBound(GetMaximumCore(element), "maximum");
        var value = min + ((max - min) * (percentage.Value / 100.0));
        SetValueCore(element, value, timeoutMs);
    }

    /// <summary>
    /// Slides to the minimum value.
    /// </summary>
    /// <param name="element">The pre-found slider element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SlideToMinimumCore(IMauiElement element, int? timeoutMs = null)
    {
        SetValueCore(element, RequireBound(GetMinimumCore(element), "minimum"), timeoutMs);
    }

    /// <summary>
    /// Slides to the maximum value.
    /// </summary>
    /// <param name="element">The pre-found slider element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SlideToMaximumCore(IMauiElement element, int? timeoutMs = null)
    {
        SetValueCore(element, RequireBound(GetMaximumCore(element), "maximum"), timeoutMs);
    }

    #endregion
}
