using System.Globalization;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Controls.Display;

/// <summary>
/// HTML progress control. Wraps &lt;progress&gt;.
/// </summary>
public partial class ProgressControl<TScope> : ViewBase<TScope>, IProgressControlObject<TScope>
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
    [GenerateComparisons(Comparison.EqualTo | Comparison.GreaterThan | Comparison.AtLeast
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
    [GenerateComparisons(Comparison.EqualTo | Comparison.GreaterThan | Comparison.AtLeast
        | Comparison.LessThan | Comparison.AtMost)]
    protected virtual double? GetPercentageCore(IHtmlElement element)
    {
        var max = GetMaxCore(element) ?? 100;
        return max > 0 ? (GetValueCore(element) ?? 0) / max * 100 : 0;
    }

    /// <summary>The progress as a fraction from 0 to 1 of the maximum; 0 when indeterminate.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual double? GetProgressCore(IHtmlElement element)
    {
        return Math.Round((GetPercentageCore(element) ?? 0) / 100, 6);
    }

    /// <summary>Whether the progress is indeterminate: a &lt;progress&gt; with no value.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsIndeterminateCore(IHtmlElement? element)
    {
        return element == null ? null : element.GetAttribute("value") == null;
    }

    #region Hand-written Convenience Members

    /// <summary>Waits for the progress to reach 1.</summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    public bool WaitComplete(int? timeoutMs = null) => WaitProgress(1, timeoutMs);

    /// <summary>Asserts the progress is at 1.</summary>
    /// <param name="message">Optional custom assertion message.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    public TScope AssertComplete(string? message = null, int? timeoutMs = null) => AssertProgress(1, message, timeoutMs);

    #endregion

    private static double? Parse(string? value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
}
