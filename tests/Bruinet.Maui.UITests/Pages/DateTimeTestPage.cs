using Bruinet.Maui.Controls.DateTimes;

namespace Bruinet.Maui.UITests.Pages;

/// <summary>
/// Page object for the DateTimeTestView. Exposes all DateTime controls and their interactions.
/// Tests DatePicker and TimePicker controls with constraint validation.
/// </summary>
public class DateTimeTestPage : PageObjectBase<DateTimeTestPage>
{
    public DateTimeTestPage(IMauiTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public override string Name => "DateTimeTestPage";


    #region DatePicker

    /// <summary>
    /// The DatePicker test control for selecting dates with min/max constraints.
    /// </summary>
    public DatePicker<DateTimeTestPage> TestDatePicker => new(this, "TestDatePicker");

    #endregion

    #region TimePicker

    /// <summary>
    /// The TimePicker test control for selecting times.
    /// </summary>
    public TimePicker<DateTimeTestPage> TestTimePicker => new(this, "TestTimePicker");

    #endregion

    #region Labels

    /// <summary>
    /// The date status label showing selected date and formatting.
    /// </summary>
    public Label<DateTimeTestPage> DateStatusLabel => new(this, "DateStatusLabel");

    /// <summary>
    /// The time status label showing selected time.
    /// </summary>
    public Label<DateTimeTestPage> TimeStatusLabel => new(this, "TimeStatusLabel");

    /// <summary>
    /// The overall status message label showing test results and validation messages.
    /// </summary>
    public Label<DateTimeTestPage> StatusLabel => new(this, "StatusLabel");

    #endregion

    #region Buttons

    /// <summary>
    /// The Reset button to clear all selections.
    /// </summary>
    public Button<DateTimeTestPage> ResetButton => new(this, "ResetButton");

    #endregion
}
