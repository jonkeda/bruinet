using Bruinet.Core.Interfaces;
using System.Globalization;
using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Controls.DateTime;

/// <summary>
/// HTML time input control. Wraps &lt;input type="time"&gt;.
/// </summary>
public partial class TimeInputControl<TScope> : FocusableControlBase<TScope>, ITimeControlObject<TScope>
    where TScope : IHtmlScope<TScope>
{
    public TimeInputControl(IHtmlScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public TimeInputControl(IHtmlScope<TScope> scope, string selectorOrId)
        : base(scope, selectorOrId)
    {
    }

    private const string Format = "HH:mm";

    /// <summary>The input's time; null when it is empty.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual TimeOnly? GetTimeCore(IHtmlElement element)
    {
        return Parse(element.InputValue);
    }

    /// <summary>Fills the input with a time, and confirms it took.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="time">The time; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetTimeCore(IHtmlElement element, TimeOnly? time, int? timeoutMs = null)
    {
        if (time is not { } target)
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
    protected virtual TimeOnly? GetMinimumCore(IHtmlElement element)
    {
        return Parse(element.GetAttribute("min"));
    }

    /// <summary>The input's <c>max</c>; null when it has none.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual TimeOnly? GetMaximumCore(IHtmlElement element)
    {
        return Parse(element.GetAttribute("max"));
    }

    private static TimeOnly? Parse(string? value)
        => TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    // Core's time capability speaks TimeSpan (proposal 8, rule 1): a time of day.
    TimeSpan? ITimeControlObject<TScope>.GetTime(int? timeoutMs) => GetTime(timeoutMs)?.ToTimeSpan();

    TScope ITimeControlObject<TScope>.SetTime(TimeSpan? time, int? timeoutMs)
        => SetTime(time is { } value ? TimeOnly.FromTimeSpan(value) : null, timeoutMs);

    bool ITimeControlObject<TScope>.WaitTime(TimeSpan? expected, int? timeoutMs)
        => WaitTime(expected is { } value ? TimeOnly.FromTimeSpan(value) : null, timeoutMs);

    TScope ITimeControlObject<TScope>.AssertTime(TimeSpan? expected, string? message, int? timeoutMs)
        => AssertTime(expected is { } value ? TimeOnly.FromTimeSpan(value) : null, message, timeoutMs);
}
