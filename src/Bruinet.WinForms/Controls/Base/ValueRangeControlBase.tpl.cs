using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls;

/// <summary>
/// Base class for WinForms ranges whose number is text in a Value pattern rather than a RangeValue
/// (a <c>TrackBar</c>, a <c>NumericUpDown</c>): the value is read and written as that text, and
/// confirmed. UI Automation does not report these controls' bounds or step, so the page object
/// gives them, as native Android's ranges do.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ValueRangeControlBase<TScope> : RangeControlBase<TScope>
    where TScope : IWinFormsScope<TScope>
{
    private readonly double _minimum;
    private readonly double? _maximum;
    private readonly double _step;

    protected ValueRangeControlBase(IWinFormsScope<TScope> scope, Locator locator, double minimum = 0, double? maximum = null, double step = 1)
        : base(scope, locator)
    {
        (_minimum, _maximum, _step) = (minimum, maximum, step);
    }

    protected ValueRangeControlBase(IWinFormsScope<TScope> scope, string automationId, double minimum = 0, double? maximum = null, double step = 1)
        : base(scope, automationId)
    {
        (_minimum, _maximum, _step) = (minimum, maximum, step);
    }

    /// <summary>The element that holds the number in its Value pattern: the control itself by default.</summary>
    /// <param name="element">The control's element.</param>
    protected virtual IWinFormsElement ValueElement(IWinFormsElement element) => element;

    /// <summary>The number the control shows; null when it does not read as one.</summary>
    /// <param name="element">The pre-found element.</param>
    protected override double? GetValueCore(IWinFormsElement? element)
        => element is null ? null : Parse(ValueElement(element).Value);

    /// <summary>Writes the number once as the Value pattern's text, and confirms it took.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="value">The value; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected override void SetValueCore(IWinFormsElement element, double? value, int? timeoutMs = null)
    {
        if (value is not { } target)
        {
            return;
        }

        var text = target.ToString(CultureInfo.CurrentCulture);
        ActAndConfirm(
            "SetValue",
            () =>
            {
                ValueElement(element).SetValue(text);
                Commit(element, target);
            },
            () => GetValueCore(element), actual => actual is { } read && Math.Abs(read - target) < 1e-9,
            $"'{text}'", timeoutMs);
    }

    /// <summary>
    /// Makes the control take the text just written, as a person's leaving the field would. Nothing
    /// by default: a track bar takes it at once.
    /// </summary>
    /// <param name="element">The control's element.</param>
    /// <param name="target">The value written.</param>
    protected virtual void Commit(IWinFormsElement element, double target)
    {
    }

    /// <summary>The range's lowest value, as the page object gives it.</summary>
    /// <param name="element">The pre-found element.</param>
    protected override double? GetMinimumCore(IWinFormsElement? element) => element == null ? null : _minimum;

    /// <summary>The range's highest value, as the page object gives it; null when it gives none.</summary>
    /// <param name="element">The pre-found element.</param>
    protected override double? GetMaximumCore(IWinFormsElement? element) => element == null ? null : _maximum;

    /// <summary>One step of the range, as the page object gives it.</summary>
    /// <param name="element">The pre-found element.</param>
    protected override double? GetStepCore(IWinFormsElement? element) => element == null ? null : _step;

    private static double? Parse(string? value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var number)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
}
