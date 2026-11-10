using System.Globalization;
using Bruinet.Core.Exceptions;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Bruinet.Maui.Appium;

/// <summary>
/// Setting a picker, a date or a time on iOS, through the wheels a MAUI picker shows.
/// </summary>
/// <remarks>
/// <para>
/// On iOS a MAUI <c>Picker</c>, <c>DatePicker</c> or <c>TimePicker</c> is a text field whose
/// keyboard is a set of <c>XCUIElementTypePickerWheel</c>s with a Done button above them. XCUITest
/// turns a wheel by sending it the text of a row; a row it does not have is an error.
/// </para>
/// <para>
/// Not yet run on a simulator: the wheels' values (a date's month name, day and year; a time's
/// hour, minute and AM/PM, which UIKit may suffix with "o'clock" and "minutes") are UIKit's as
/// documented, not measured. A wheel is told apart by its value, never by its place.
/// </para>
/// </remarks>
internal sealed partial class AppiumMauiElement
{
    private static readonly By IosWheels = By.XPath("//XCUIElementTypePickerWheel");

    private static readonly By IosDone =
        MobileBy.IosNSPredicate("type == 'XCUIElementTypeButton' AND (name == 'Done' OR label == 'Done')");

    /// <summary>How many rows a wheel is turned back at most to find its first.</summary>
    private const int MaxWheelRows = 500;

    /// <summary>How long a turned wheel may take to report its new row.</summary>
    private const int WheelSettleMs = 1500;

    /// <summary>Taps the field open, hands its one wheel to <paramref name="choose"/>, presses Done.</summary>
    internal void OnTheWheel(string operation, Action<IWebElement> choose)
    {
        var wheels = OpenWheels(operation);
        if (wheels.Count != 1)
        {
            PressDone();
            throw new RouteUnavailableException(
                $"'{AutomationId}' shows {wheels.Count} wheel(s); {operation} expects one.");
        }

        try
        {
            choose(wheels[0]);
        }
        finally
        {
            PressDone();
        }
    }

    /// <summary>Turns a picker's wheel to the row with the text.</summary>
    internal void TurnTo(IWebElement wheel, string text)
    {
        try
        {
            wheel.SendKeys(text);
        }
        catch (WebDriverException error)
        {
            throw new InvalidOperationException($"'{AutomationId}' has no item with text '{text}'.", error);
        }
    }

    /// <summary>
    /// Turns a picker's wheel to its first row, then <paramref name="index"/> rows on. A wheel
    /// names only its current row, so there is no list to index.
    /// </summary>
    internal void TurnToIndex(IWebElement wheel, int index)
    {
        // Inside a call, what the call has left: each row is a round trip, and up to 500 of them
        // back used to run past any budget.
        var budgetMs = Bruinet.Core.Calls.CallBudget.RemainingOr(int.MaxValue);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        TurnWheel(AutomationId, order => StepWheel(wheel, order), index, budgetMs, () => clock.ElapsedMilliseconds);
    }

    /// <summary>
    /// Steps a wheel back to its first row, then <paramref name="index"/> rows on, while
    /// <paramref name="elapsedMs"/> is under <paramref name="budgetMs"/>.
    /// </summary>
    /// <param name="name">The picker, for the messages.</param>
    /// <param name="step">Turns one row, <c>"previous"</c> or <c>"next"</c>; false at the wheel's end.</param>
    /// <param name="index">The row wanted, from the first.</param>
    /// <param name="budgetMs">How long the steps may take in all.</param>
    /// <param name="elapsedMs">The time spent so far.</param>
    /// <exception cref="ArgumentOutOfRangeException">The wheel has no row <paramref name="index"/>.</exception>
    /// <exception cref="TimeoutException">The budget was spent first; the message says how far it got.</exception>
    internal static void TurnWheel(string? name, Func<string, bool> step, int index, int budgetMs, Func<long> elapsedMs)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"'{name}' has no index {index}.");
        }

        var back = 0;
        for (; back < MaxWheelRows; back++)
        {
            if (elapsedMs() >= budgetMs)
            {
                throw new TimeoutException(
                    $"'{name}': the call's {budgetMs} ms ran out turning the wheel back to its first row "
                    + $"({back} row(s) back so far).");
            }

            if (!step("previous"))
            {
                break;
            }
        }

        for (var row = 0; row < index; row++)
        {
            if (elapsedMs() >= budgetMs)
            {
                throw new TimeoutException(
                    $"'{name}': the call's {budgetMs} ms ran out at row {row} of the wheel, turning to index {index}.");
            }

            if (!step("next"))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index), index, $"'{name}' shows {row + 1} item(s); there is no index {index}.");
            }
        }
    }

    /// <summary>Turns a wheel one row; false when it was already at its end.</summary>
    private bool StepWheel(IWebElement wheel, string order)
    {
        var before = wheel.GetAttribute("value");
        _driver.Driver.ExecuteScript("mobile: selectPickerWheelValue", new Dictionary<string, object>
        {
            ["elementId"] = ((AppiumElement)wheel).Id,
            ["order"] = order,
            ["offset"] = 0.15,
        });

        return wheel.GetAttribute("value") != before;
    }

    internal void SetDateOnWheels(DateTime date)
    {
        var name = AutomationId;
        var wheels = OpenWheels(nameof(SetDate));
        var culture = DayDescriptionCulture;

        IWebElement? month = null, day = null, year = null;
        foreach (var wheel in wheels)
        {
            var digits = Digits(wheel.GetAttribute("value"));
            if (digits.Length == 4)
                year = wheel;
            else if (digits.Length > 0)
                day = wheel;
            else
                month = wheel;
        }

        if (month is null || day is null || year is null)
        {
            PressDone();
            throw new RouteUnavailableException(
                $"'{name}' shows {wheels.Count} wheel(s), not a month, a day and a year.");
        }

        var before = wheels.Select(w => w.GetAttribute("value")).ToList();

        // The year and month first: a day the month lacks would be turned back by the wheel.
        year.SendKeys(date.Year.ToString(CultureInfo.InvariantCulture));
        month.SendKeys(date.ToString("MMMM", culture));
        day.SendKeys(date.Day.ToString(CultureInfo.InvariantCulture));

        bool Landed() => Digits(year.GetAttribute("value")) == date.Year.ToString(CultureInfo.InvariantCulture)
            && month.GetAttribute("value") == date.ToString("MMMM", culture)
            && Digits(day.GetAttribute("value")) == date.Day.ToString(CultureInfo.InvariantCulture);

        // A wheel reports its new row only once it has stopped turning.
        if (!Bruinet.Core.Calls.Settle.Until(Landed, WheelSettleMs, pollingIntervalMs: 100))
        {
            var shown = string.Join(", ", wheels.Select(w => w.GetAttribute("value")));
            // UIKit turns a wheel past MinimumDate or MaximumDate back by itself; put the old date
            // back so nothing changes, then refuse as Windows and Android do.
            for (var i = 0; i < wheels.Count; i++)
            {
                TrySend(wheels[i], before[i]);
            }

            PressDone();
            throw new BruinetException(
                $"'{name}' was asked for {date:yyyy-MM-dd}, which its date wheels did not keep: "
                + $"it is outside the picker's MinimumDate and MaximumDate, or the app changes a picked date "
                + $"while the wheels turn (the wheels showed {shown}). Nothing was changed.");
        }

        PressDone();
    }

    internal void SetTimeOnWheels(TimeSpan time)
    {
        var wheels = OpenWheels(nameof(SetTime));

        IWebElement? period = null;
        var numbers = new List<IWebElement>();
        foreach (var wheel in wheels)
        {
            var value = wheel.GetAttribute("value") ?? "";
            if (value.Contains("AM", StringComparison.OrdinalIgnoreCase)
                || value.Contains("PM", StringComparison.OrdinalIgnoreCase))
            {
                period = wheel;
            }
            else
            {
                numbers.Add(wheel);
            }
        }

        if (numbers.Count != 2)
        {
            PressDone();
            throw new RouteUnavailableException(
                $"'{AutomationId}' shows {wheels.Count} wheel(s), not an hour and a minute.");
        }

        // The hour wheel is left of the minute wheel in every locale UIKit draws.
        numbers.Sort((a, b) => a.Location.X.CompareTo(b.Location.X));
        var hour = period is null ? time.Hours : (time.Hours % 12 == 0 ? 12 : time.Hours % 12);

        try
        {
            numbers[0].SendKeys(hour.ToString(period is null ? "00" : "0", CultureInfo.InvariantCulture));
            numbers[1].SendKeys(time.Minutes.ToString("00", CultureInfo.InvariantCulture));
            period?.SendKeys(time.Hours < 12 ? "AM" : "PM");
        }
        finally
        {
            PressDone();
        }
    }

    /// <summary>Taps the field and waits for its wheels.</summary>
    private IReadOnlyList<IWebElement> OpenWheels(string operation)
    {
        Live(() => _element.Click());

        IReadOnlyList<IWebElement> wheels = [];
        if (Bruinet.Core.Calls.Settle.Until(() => (wheels = _driver.Driver.FindElements(IosWheels)).Count > 0,
                PickerDialogWaitMs, pollingIntervalMs: 100))
        {
            return wheels;
        }

        throw new TimeoutException(
            $"{nameof(AppiumMauiElement)}.{operation}: '{AutomationId}' was tapped, but no picker "
            + $"wheel showed within {PickerDialogWaitMs} ms.");
    }

    /// <summary>Presses the wheels' Done, which closes them and keeps what they show.</summary>
    private void PressDone()
    {
        try
        {
            _driver.Driver.FindElements(IosDone).FirstOrDefault()?.Click();
        }
        catch (WebDriverException)
        {
            // Closing is a courtesy to the next test; a failure being reported is the real one.
        }
    }

    private static void TrySend(IWebElement wheel, string? value)
    {
        try
        {
            if (value is not null)
            {
                wheel.SendKeys(value);
            }
        }
        catch (WebDriverException)
        {
        }
    }

    private static string Digits(string? value)
        => new((value ?? "").Where(char.IsDigit).ToArray());
}
