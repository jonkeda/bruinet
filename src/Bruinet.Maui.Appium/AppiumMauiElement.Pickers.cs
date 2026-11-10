using System.Globalization;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Recording;
using Bruinet.Maui.Enums;
using OpenQA.Selenium;

namespace Bruinet.Maui.Appium;

/// <summary>
/// Setting a date or a time on Android, through the dialog a MAUI picker opens.
/// </summary>
/// <remarks>
/// <para>
/// A MAUI <c>DatePicker</c> or <c>TimePicker</c> on Android is an <c>EditText</c> that opens the
/// framework's dialog when tapped, and the dialog is the only way in: the field takes no typing.
/// So these do what a person does - open it, choose, press OK - with every part found by its id or
/// its description, never by a coordinate (measured on the sample, API 36):
/// </para>
/// <list type="bullet">
/// <item>The date dialog is a calendar: days described as <c>"01 October 2026"</c>, a day outside
/// <c>MinimumDate</c>/<c>MaximumDate</c> disabled, <c>android:id/prev</c> and <c>android:id/next</c>
/// to page by month, absent at the range's ends.</item>
/// <item>The time dialog is a clock with <c>am_label</c>/<c>pm_label</c> and a <c>toggle_mode</c>
/// button that turns it into two fields, <c>input_hour</c> and <c>input_minute</c>.</item>
/// </list>
/// <para>
/// The day's description is the platform's own text and follows the device's language; the
/// emulator runs in English. iOS shows wheels instead (<c>AppiumMauiElement.Wheels.cs</c>).
/// </para>
/// </remarks>
internal sealed partial class AppiumMauiElement
{
    private const int PickerDialogWaitMs = 3_000;

    private const int MaxMonthPages = 36;

    private static readonly CultureInfo DayDescriptionCulture = CultureInfo.GetCultureInfo("en-US");

    /// <inheritdoc />
    /// <exception cref="BruinetException">
    /// The date is outside the picker's <c>MinimumDate</c> and <c>MaximumDate</c>: the dialog does
    /// not offer it. Nothing is changed.
    /// </exception>
    public void SetDate(DateTime date)
    {
        using var mark = Report(InteractionKind.Type);
        _driver.Appium.SetDate(this, date);
    }

    /// <summary>Android: chooses the day in the date dialog.</summary>
    internal void SetDateInDialog(DateTime date)
    {
        // Read before the dialog opens: the page under it, this field included, goes stale.
        var name = AutomationId;
        Live(() => _element.Click());
        WaitForDialogPart("month_view", name);
        ClosingTheDialogOnFailure(() => ChooseDay(date, name));
    }

    private void ChooseDay(DateTime date, string? name)
    {
        var wanted = date.ToString("dd MMMM yyyy", DayDescriptionCulture);

        for (var page = 0; page <= MaxMonthPages; page++)
        {
            // Asked with its enabled state in one query: the calendar re-renders as it settles,
            // and a day found first and asked "enabled?" after went stale in between.
            if (Shown($"new UiSelector().description(\"{wanted}\")"))
            {
                if (!Shown($"new UiSelector().description(\"{wanted}\").enabled(true)"))
                {
                    RefuseAndClose(date, name);
                }

                Retrying(() => DialogPart(MobileBy.AccessibilityId(wanted))!.Click());
                Retrying(() => DialogPart(AndroidId("button1"))!.Click());
                return;
            }

            var shown = ShownMonth();
            var forward = shown is null || new DateTime(date.Year, date.Month, 1) > shown.Value;

            var turn = DialogPart(AndroidId(forward ? "next" : "prev"));
            if (turn is null)
            {
                // No page further that way: the range ends before the date.
                RefuseAndClose(date, name);
            }

            Retrying(() => turn!.Click());
            WaitForDay(forward ? shown?.AddMonths(1) : shown?.AddMonths(-1));
        }

        RefuseAndClose(date, name);
    }

    /// <summary>
    /// Runs a choice in an open picker dialog, and cancels the dialog if the choice fails.
    /// </summary>
    /// <remarks>
    /// A dialog left open covers the page, and every later test in the class then failed for want
    /// of its page root. A refusal closes the dialog itself; this covers everything else.
    /// </remarks>
    private void ClosingTheDialogOnFailure(Action choose)
    {
        try
        {
            choose();
        }
        catch (Exception) when (CancelIfOpen())
        {
            throw;
        }
    }

    /// <summary>Presses the dialog's Cancel if one is open. Always false, so a filter can call it.</summary>
    private bool CancelIfOpen()
    {
        try
        {
            DialogPart(AndroidId("button2"))?.Click();
        }
        catch (WebDriverException)
        {
            // Closing is a courtesy to the next test; the failure being reported is the real one.
        }

        return false;
    }

    private bool Shown(string uiSelector)
        => _driver.Driver.FindElements(MobileBy.AndroidUIAutomator(uiSelector)).Count > 0;

    /// <summary>Runs an action on a dialog part again, once, if the part went stale under it.</summary>
    private static void Retrying(Action act)
    {
        try
        {
            act();
        }
        catch (StaleElementReferenceException)
        {
            act();
        }
    }

    /// <inheritdoc />
    public void SetTime(TimeSpan time)
    {
        using var mark = Report(InteractionKind.Type);
        _driver.Appium.SetTime(this, time);
    }

    /// <summary>Android: types the time into the time dialog.</summary>
    internal void SetTimeInDialog(TimeSpan time)
    {
        var name = AutomationId;
        Live(() => _element.Click());
        WaitForDialogPart("timePicker", name);
        ClosingTheDialogOnFailure(() => ChooseTime(time));
    }

    private void ChooseTime(TimeSpan time)
    {
        // AM or PM on the clock first: the text fields that follow keep the clock's half of the day.
        var am = DialogPart(AndroidId("am_label"));
        var twelveHour = am is not null;
        if (twelveHour)
        {
            (time.Hours < 12 ? am : DialogPart(AndroidId("pm_label")))!.Click();
        }

        DialogPart(AndroidId("toggle_mode"))?.Click();
        WaitForDialogPart("input_hour", "the time dialog");

        var hour = twelveHour ? (time.Hours % 12 == 0 ? 12 : time.Hours % 12) : time.Hours;
        TypeInto("input_hour", hour.ToString(twelveHour ? "0" : "00", CultureInfo.InvariantCulture));
        TypeInto("input_minute", time.Minutes.ToString("00", CultureInfo.InvariantCulture));

        DialogPart(AndroidId("button1"))!.Click();
    }

    /// <summary>Cancels the dialog and refuses, as Windows does for a clamped date.</summary>
    private void RefuseAndClose(DateTime date, string? name)
    {
        DialogPart(AndroidId("button2"))?.Click();
        throw new BruinetException(
            $"'{name}' was asked for {date:yyyy-MM-dd}, which its date dialog does not offer: "
            + "it is outside the picker's MinimumDate and MaximumDate. Nothing was changed.");
    }

    /// <summary>The first day the calendar shows, as the first of its month.</summary>
    private DateTime? ShownMonth()
    {
        var first = DialogPart(MobileBy.AndroidUIAutomator(
            "new UiSelector().descriptionMatches(\"^01 .*\")"));

        return first?.GetAttribute("content-desc") is { } text
               && DateTime.TryParseExact(text, "dd MMMM yyyy", DayDescriptionCulture,
                   DateTimeStyles.None, out var day)
            ? day
            : null;
    }

    private void WaitForDay(DateTime? firstOfMonth)
    {
        if (firstOfMonth is null)
        {
            return;
        }

        var wanted = firstOfMonth.Value.ToString("dd MMMM yyyy", DayDescriptionCulture);
        Poll(() => DialogPart(MobileBy.AccessibilityId(wanted)) is not null);
    }

    private void WaitForDialogPart(string id, string? name)
    {
        if (!Poll(() => DialogPart(AndroidId(id)) is not null))
        {
            throw new BruinetException(
                $"'{name}' was tapped and its dialog (android:id/{id}) did not open within "
                + $"{PickerDialogWaitMs} ms.");
        }
    }

    // UiAutomator2 sets an EditText's text, replacing what it held: no clear first, one trip fewer.
    private void TypeInto(string id, string text) => DialogPart(AndroidId(id))!.SendKeys(text);

    private IWebElement? DialogPart(By by) => _driver.Driver.FindElements(by).FirstOrDefault();

    private static By AndroidId(string id)
        => MobileBy.AndroidUIAutomator($"new UiSelector().resourceId(\"android:id/{id}\")");

    // The dialog's own wait, within what the running call has left (R2).
    private static bool Poll(Func<bool> done) => Bruinet.Core.Calls.Settle.Until(done, PickerDialogWaitMs, pollingIntervalMs: 50);
}
