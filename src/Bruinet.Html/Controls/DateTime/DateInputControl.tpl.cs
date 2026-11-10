using Bruinet.Core.Interfaces;
using System.Globalization;
using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Controls.DateTime;

/// <summary>
/// HTML date input control. Wraps &lt;input type="date"&gt;.
/// </summary>
public partial class DateInputControl<TScope> : FocusableControlBase<TScope>, IDateControlObject<TScope>
    where TScope : IHtmlScope<TScope>
{
    public DateInputControl(IHtmlScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public DateInputControl(IHtmlScope<TScope> scope, string selectorOrId)
        : base(scope, selectorOrId)
    {
    }

    private const string Format = "yyyy-MM-dd";

    /// <summary>The input's date; null when it is empty.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual DateOnly? GetDateCore(IHtmlElement element)
    {
        return Parse(element.InputValue);
    }

    /// <summary>Fills the input with a date, and confirms it took.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="date">The date; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetDateCore(IHtmlElement element, DateOnly? date, int? timeoutMs = null)
    {
        if (date is not { } target)
        {
            return;
        }

        var text = target.ToString(Format, CultureInfo.InvariantCulture);
        ActAndConfirm(
            "Fill", () => element.Fill(text),
            () => Parse(element.InputValue), actual => actual == target,
            $"'{text}'", timeoutMs);
    }

    /// <summary>The input's <c>min</c>; null when it has none.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual DateOnly? GetMinimumCore(IHtmlElement element)
    {
        return Parse(element.GetAttribute("min"));
    }

    /// <summary>The input's <c>max</c>; null when it has none.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual DateOnly? GetMaximumCore(IHtmlElement element)
    {
        return Parse(element.GetAttribute("max"));
    }

    private static DateOnly? Parse(string? value)
        => DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    // Core's date capability speaks DateTime (proposal 8, rule 1); an <input type="date"> holds a date
    // only, so the time of day is dropped going in and midnight coming out.
    System.DateTime? IDateControlObject<TScope>.GetDate(int? timeoutMs) => GetDate(timeoutMs)?.ToDateTime(TimeOnly.MinValue);

    TScope IDateControlObject<TScope>.SetDate(System.DateTime? date, int? timeoutMs)
        => SetDate(date is { } value ? DateOnly.FromDateTime(value) : null, timeoutMs);

    bool IDateControlObject<TScope>.WaitDate(System.DateTime? expected, int? timeoutMs)
        => WaitDate(expected is { } value ? DateOnly.FromDateTime(value) : null, timeoutMs);

    TScope IDateControlObject<TScope>.AssertDate(System.DateTime? expected, string? message, int? timeoutMs)
        => AssertDate(expected is { } value ? DateOnly.FromDateTime(value) : null, message, timeoutMs);
}
