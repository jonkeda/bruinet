using Bruinet.Core;
using Bruinet.Core.Utilities;
using System.Linq;

namespace Bruinet.Maui.Controls.DateTimes;

/// <summary>
/// MAUI TimePicker control for time selection.
/// Provides GetTime, SetTime, and time assertion methods.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class TimePicker<TScope> : Base.FocusableControlBase<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates a new time picker control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the time picker element.</param>
    public TimePicker(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates a new time picker control within the specified scope using a string locator value.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID, name).</param>
    public TimePicker(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Format

    private string? _format;
    private System.Globalization.CultureInfo? _culture;

    /// <summary>
    /// Declares the format this control's time is rendered and entered in.
    /// </summary>
    /// <param name="format">A .NET time format string, e.g. <c>h:mm tt</c>.</param>
    /// <param name="culture">Culture for the format. Defaults to <see cref="DateTimeFormats.Culture"/>.</param>
    public TimePicker<TScope> WithFormat(string format, System.Globalization.CultureInfo? culture = null)
    {
        _format = format ?? throw new ArgumentNullException(nameof(format));
        _culture = culture;
        return this;
    }

    /// <summary>Gets the format in force: the control's own, else the suite default.</summary>
    protected string Format => _format ?? DateTimeFormats.Time;

    /// <summary>Gets the culture in force: the control's own, else the suite default.</summary>
    protected System.Globalization.CultureInfo Culture => _culture ?? DateTimeFormats.Culture;

    /// <summary>
    /// Parses a rendered time using the declared format.
    /// </summary>
    protected TimeSpan? ParseTime(string? text)
    {
        var cleaned = DateTimeFormats.Clean(text);
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned, @"\s*time\s*picker\s*$", string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

        if (cleaned.Length == 0)
            return null;

        if (System.DateTime.TryParseExact(cleaned, Format, Culture,
                System.Globalization.DateTimeStyles.None, out var exact))
            return exact.TimeOfDay;

        if (_format != null)
        {
            throw new BruinetException(
                $"Could not read a time from '{cleaned}' using format '{Format}' " +
                $"({Culture.Name}). Locator: {Locator}");
        }

        return System.DateTime.TryParse(cleaned, Culture,
            System.Globalization.DateTimeStyles.None, out var loose)
            ? loose.TimeOfDay
            : null;
    }

    #endregion

    #region Time - Core Methods

    // Named ReadTime rather than GetTimeCore so the generated trio lands on TimeValue;
    // AssertTime/WaitTime compare within a tolerance and stay hand-written below.

    /// <summary>
    /// Reads the time the control is showing.
    /// </summary>
    protected virtual TimeSpan? ReadTime(IMauiElement? element)
    {
        if (element == null)
            return null;

        foreach (var child in element.FindElements(Locator.ByAutomationId("FlyoutButton")))
        {
            var childTime = ParseTime(child.Name) ?? ParseTime(child.Text);
            if (childTime != null)
                return childTime;
        }

        return ParseTime(element.Name) ?? ParseTime(element.Text);
    }

    /// <summary>
    /// Sets the time through the app's <c>SetTime</c> verb, or throws naming it.
    /// </summary>
    protected virtual void SetTimeCore(IMauiElement element, TimeSpan? time, int? timeoutMs = null)
    {
        if (time == null)
            return;

        element.SetTime(time.Value);
    }

    #endregion

    #region Hand-written Convenience Members

    // Tolerance-based comparison: a time picker's value moves while a test runs.

    /// <summary>
    /// Gets the currently selected time.
    /// </summary>
    /// <param name="timeoutMs">Optional timeout for finding the element.</param>
    /// <returns>The selected time, or null if element not found.</returns>
    public TimeSpan? GetTime(int? timeoutMs = null)
        => RunGetWithElement(element => ReadTime(element), timeoutMs);

    /// <summary>
    /// Waits for the time to match the expected value.
    /// </summary>
    /// <param name="expected">Expected time. Null skips the wait.</param>
    /// <param name="toleranceSeconds">Tolerance in seconds for comparison.</param>
    /// <param name="timeoutMs">Optional timeout.</param>
    /// <returns>True if condition met, false if timeout.</returns>
    public bool WaitTime(TimeSpan? expected, int toleranceSeconds = 60, int? timeoutMs = null)
    {
        if (expected == null)
            return true;

        var tolerance = TimeSpan.FromSeconds(toleranceSeconds);

        return RunWaitWithElement(expected,
            e =>
            {
                var actual = ReadTime(e);
                if (!actual.HasValue)
                    return false;
                var diff = (actual.Value - expected.Value).Duration();
                return diff <= tolerance;
            },
            timeoutMs);
    }

    /// <summary>
    /// Asserts the time matches the expected value.
    /// </summary>
    /// <param name="expected">Expected time. Null skips the assertion.</param>
    /// <param name="toleranceSeconds">Tolerance in seconds for comparison.</param>
    /// <param name="message">Optional assertion message.</param>
    /// <param name="timeoutMs">Optional timeout.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    public TScope AssertTime(TimeSpan? expected, int toleranceSeconds = 60, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;

        var tolerance = TimeSpan.FromSeconds(toleranceSeconds);

        return RunAssertWithElement(expected,
            ReadTime, (actual, exp) =>
            {
                if (!actual.HasValue || !exp.HasValue)
                    return false;
                var diff = (actual.Value - exp.Value).Duration();
                return diff <= tolerance;
            },
            message ?? $"Expected time {expected:hh\\:mm}. Locator: {Locator}", timeoutMs);
    }

    /// <summary>
    /// Gets the hours component of the selected time.
    /// </summary>
    /// <returns>The hours (0-23), or null if not available.</returns>
    public int? GetHours()
    {
        var time = GetTime();
        return time?.Hours;
    }

    /// <summary>
    /// Gets the minutes component of the selected time.
    /// </summary>
    /// <returns>The minutes (0-59), or null if not available.</returns>
    public int? GetMinutes()
    {
        var time = GetTime();
        return time?.Minutes;
    }

    #endregion
}
