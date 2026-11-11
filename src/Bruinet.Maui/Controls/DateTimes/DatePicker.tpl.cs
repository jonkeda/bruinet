using Bruinet.Core;
using Bruinet.Core.Utilities;
using System.Linq;

namespace Bruinet.Maui.Controls.DateTimes;

/// <summary>
/// MAUI DatePicker control for date selection.
/// Provides GetDate, SetDate, and date assertion methods.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class DatePicker<TScope> : Base.FocusableControlBase<TScope>, Bruinet.Core.Interfaces.IDateControlObject<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates a new date picker control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the date picker element.</param>
    public DatePicker(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates a new date picker control within the specified scope using a string locator value.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID, name).</param>
    public DatePicker(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Format

    private string? _format;
    private System.Globalization.CultureInfo? _culture;

    /// <summary>
    /// Declares the format this control's date is rendered and entered in.
    /// </summary>
    /// <remarks>
    /// Overrides <see cref="DateTimeFormats.Date"/> for this control only. When a format is
    /// declared, reads parse with it exactly and a string that does not match is an error rather
    /// than a null - see <see cref="ParseDate"/>.
    /// </remarks>
    /// <param name="format">A .NET date format string, e.g. <c>dd-MMM-yy</c>.</param>
    /// <param name="culture">Culture for the format. Defaults to <see cref="DateTimeFormats.Culture"/>.</param>
    /// <returns>This control, for chaining onto a locator expression.</returns>
    public DatePicker<TScope> WithFormat(string format, System.Globalization.CultureInfo? culture = null)
    {
        _format = format ?? throw new ArgumentNullException(nameof(format));
        _culture = culture;
        return this;
    }

    /// <summary>Gets the format in force: the control's own, else the suite default.</summary>
    protected string Format => _format ?? DateTimeFormats.Date;

    /// <summary>Gets the culture in force: the control's own, else the suite default.</summary>
    protected System.Globalization.CultureInfo Culture => _culture ?? DateTimeFormats.Culture;

    /// <summary>
    /// Parses a rendered date using the declared format.
    /// </summary>
    protected System.DateTime? ParseDate(string? text)
    {
        var cleaned = DateTimeFormats.Clean(text);
        if (cleaned.Length == 0)
            return null;

        if (System.DateTime.TryParseExact(cleaned, Format, Culture,
                System.Globalization.DateTimeStyles.None, out var exact))
            return exact;

        if (_format != null)
        {
            throw new BruinetException(
                $"Could not read a date from '{cleaned}' using format '{Format}' " +
                $"({Culture.Name}). Locator: {Locator}");
        }

        return System.DateTime.TryParse(cleaned, Culture,
            System.Globalization.DateTimeStyles.None, out var loose)
            ? loose
            : null;
    }

    #endregion

    #region Date - Core Methods

    // Named ReadDate rather than GetDateCore so the generated trio lands on DateValue;
    // AssertDate/WaitDate compare whole days and stay hand-written below.

    /// <summary>
    /// Reads the date the control is showing.
    /// </summary>
    protected virtual System.DateTime? ReadDate(IMauiElement? element)
    {
        if (element == null)
            return null;

        var valueDate = ParseDate(element.Value);
        if (valueDate != null)
            return valueDate;

        foreach (var child in element.FindElements(Locator.ByAutomationId("DateText")))
        {
            var childDate = ParseDate(child.Name) ?? ParseDate(child.Text);
            if (childDate != null)
                return childDate;
        }

        return ParseDate(element.Name) ?? ParseDate(element.Text);
    }

    /// <summary>
    /// Sets the date.
    /// </summary>
    protected virtual void SetDateCore(IMauiElement element, System.DateTime? date, int? timeoutMs = null)
    {
        if (date == null)
            return;

        element.SetDate(date.Value);
    }

    #endregion

    #region Hand-written Convenience Members

    // Whole-day comparison (.Date): the control holds a day, and a caller passing
    // DateTime.Now should not fail against a picker showing today.

    /// <summary>
    /// Gets the currently selected date.
    /// </summary>
    /// <param name="timeoutMs">Optional timeout for finding the element.</param>
    /// <returns>The selected date, or null if element not found.</returns>
    public System.DateTime? GetDate(int? timeoutMs = null)
        => RunGetWithElement(element => ReadDate(element), timeoutMs);

    /// <summary>
    /// Waits for the date to match the expected value, comparing whole days.
    /// </summary>
    /// <param name="expected">Expected date. Null skips the wait.</param>
    /// <param name="timeoutMs">Optional timeout.</param>
    /// <returns>True if condition met, false if timeout.</returns>
    public bool WaitDate(System.DateTime? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;

        return RunWaitWithElement(expected,
            e =>
            {
                var actual = ReadDate(e);
                return actual.HasValue && actual.Value.Date == expected.Value.Date;
            },
            timeoutMs);
    }

    /// <summary>
    /// Asserts the date matches the expected value, comparing whole days.
    /// </summary>
    /// <param name="expected">Expected date. Null skips the assertion.</param>
    /// <param name="message">Optional assertion message.</param>
    /// <param name="timeoutMs">Optional timeout.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    public TScope AssertDate(System.DateTime? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;

        return RunAssertWithElement(expected,
            ReadDate,
            (actual, exp) => actual.HasValue && exp.HasValue && actual.Value.Date == exp.Value.Date,
            message ?? $"Expected date {expected:yyyy-MM-dd}. Locator: {Locator}", timeoutMs);
    }

    #endregion
}
