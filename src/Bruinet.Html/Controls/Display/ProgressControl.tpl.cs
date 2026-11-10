using System.Globalization;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Controls.Display;

/// <summary>
/// HTML progress control. Wraps &lt;progress&gt;.
/// </summary>
public partial class ProgressControl<TScope> : ViewBase<TScope>
    where TScope : IHtmlScope<TScope>
{
    public ProgressControl(IHtmlScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public ProgressControl(IHtmlScope<TScope> scope, string selectorOrId)
        : base(scope, selectorOrId)
    {
    }

    /// <summary>The progress value; 0 when it has none.</summary>
    /// <param name="element">The pre-found element.</param>
    [GenerateComparisons(Comparison.Equals | Comparison.GreaterThan | Comparison.AtLeast
        | Comparison.LessThan | Comparison.AtMost)]
    protected virtual double? GetValueCore(IHtmlElement element)
    {
        return Parse(element.GetAttribute("value")) ?? 0;
    }

    /// <summary>The progress maximum; 100 when it has none.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual double? GetMaxCore(IHtmlElement element)
    {
        return Parse(element.GetAttribute("max")) ?? 100;
    }

    /// <summary>The value as a percentage of the maximum.</summary>
    /// <param name="element">The pre-found element.</param>
    [GenerateComparisons(Comparison.Equals | Comparison.GreaterThan | Comparison.AtLeast
        | Comparison.LessThan | Comparison.AtMost)]
    protected virtual double? GetPercentageCore(IHtmlElement element)
    {
        var max = GetMaxCore(element) ?? 100;
        return max > 0 ? (GetValueCore(element) ?? 0) / max * 100 : 0;
    }

    private static double? Parse(string? value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
}
