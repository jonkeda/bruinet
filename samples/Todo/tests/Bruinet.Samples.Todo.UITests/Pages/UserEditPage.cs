using Bruinet.Bdd;
using System.Globalization;
using Bruinet.Core.Testing;
using Bruinet.Maui.Controls.Range;
using Bruinet.Samples.Todo.UITests.Containers;
using Bruinet.Samples.Todo.UITests.Controls;
using Bruinet.Uat;

namespace Bruinet.Samples.Todo.UITests.Pages;

/// <summary>
/// Page object for the user form, new or existing: one field of each kind a test drives.
/// </summary>
/// <remarks>
/// <para>
/// Save and Cancel are toolbar items. Save stays enabled while the form is invalid (it shows the
/// errors) and is disabled offline. Each error label is hidden until its field fails, so it is a
/// <see cref="ShownLabel{TScope}"/>.
/// </para>
/// <para>
/// The status, hours, start of day, workload and end date have page phrases: Bruinet's step verbs
/// cover text, toggles and selectors, and these fields are a radio group, a stepper, a time picker,
/// a slider and a date behind a check box.
/// </para>
/// </remarks>
[BddNamespace("Users")]
public class UserEditPage(IMauiTestContext context) : PageObjectBase<UserEditPage>(context)
{
    private readonly AppRoot _appRoot = new(context);

    /// <inheritdoc />
    public override string Name => "UserEditPage";

    /// <summary>Validates, saves on the server and returns; or shows the errors and stays.</summary>
    public ToolbarButton<AppRoot> SaveButton => new(_appRoot, Locator.ByAccessibilityId("UserEdit_Save"));

    /// <summary>Returns without saving.</summary>
    [UatNavigatesTo(typeof(UserListPage))]
    public ToolbarButton<AppRoot> CancelButton => new(_appRoot, Locator.ByAccessibilityId("UserEdit_Cancel"));

    /// <summary>"You are offline. ..."; not shown online.</summary>
    [UatName("Offline Notice")]
    public ShownLabel<UserEditPage> OfflineNotice => new(this, "UserEdit_Offline");

    /// <summary>Why the server could not be asked; not shown otherwise.</summary>
    public ShownLabel<UserEditPage> SaveError => new(this, "UserEdit_SaveError");

    /// <summary>The name.</summary>
    [UatName("Name")]
    public Entry<UserEditPage> NameField => new(this, "UserEdit_Name");

    /// <summary>The name's error.</summary>
    public ShownLabel<UserEditPage> NameError => new(this, "UserEdit_NameError");

    /// <summary>The email address.</summary>
    public Entry<UserEditPage> Email => new(this, "UserEdit_Email");

    /// <summary>The email's error.</summary>
    public ShownLabel<UserEditPage> EmailError => new(this, "UserEdit_EmailError");

    /// <summary>The notes.</summary>
    [UatName("Notes")]
    public Editor<UserEditPage> Notes => new(this, "UserEdit_Notes");

    /// <summary>The notes' error.</summary>
    public ShownLabel<UserEditPage> NotesError => new(this, "UserEdit_NotesError");

    /// <summary>The Active status.</summary>
    [UatName("Active")]
    public RadioButton<UserEditPage> ActiveStatus => new(this, "UserEdit_Status_Active");

    /// <summary>The On leave status.</summary>
    [UatName("On Leave")]
    public RadioButton<UserEditPage> OnLeaveStatus => new(this, "UserEdit_Status_OnLeave");

    /// <summary>The Inactive status.</summary>
    [UatName("Inactive")]
    public RadioButton<UserEditPage> InactiveStatus => new(this, "UserEdit_Status_Inactive");

    /// <summary>Whether the user has an end date.</summary>
    public CheckBox<UserEditPage> HasEndDate => new(this, "UserEdit_HasEndDate");

    /// <summary>The end date, enabled while <see cref="HasEndDate"/> is checked.</summary>
    public DatePicker<UserEditPage> AllowedUntil => new(this, "UserEdit_AllowedUntil");

    /// <summary>Member, Lead or Admin.</summary>
    public Picker<UserEditPage> Role => new(this, "UserEdit_Role");

    /// <summary>The hours per week. On Windows it is two buttons, <c>UserEdit_HoursMinus</c> and <c>UserEdit_HoursPlus</c>.</summary>
    public Stepper<UserEditPage> Hours => new(this, "UserEdit_Hours");

    /// <summary>"38 hours": the stepper's value, as the page shows it.</summary>
    public Label<UserEditPage> HoursValue => new(this, "UserEdit_HoursValue");

    /// <summary>The hours' error.</summary>
    public ShownLabel<UserEditPage> HoursError => new(this, "UserEdit_HoursError");

    /// <summary>The hourly rate, as typed: <c>1,250.75</c>.</summary>
    [UatName("Rate")]
    public Entry<UserEditPage> Rate => new(this, "UserEdit_Rate");

    /// <summary>The rate's error.</summary>
    public ShownLabel<UserEditPage> RateError => new(this, "UserEdit_RateError");

    /// <summary>When the working day starts.</summary>
    public TimePicker<UserEditPage> StartOfDay => new(this, "UserEdit_StartOfDay");

    /// <summary>Whether the user receives reminders.</summary>
    [UatName("Reminders")]
    public Switch<UserEditPage> Reminders => new(this, "UserEdit_Reminders");

    /// <summary>The workload, 0-100 in steps of 5.</summary>
    public Slider<UserEditPage> Workload => new(this, "UserEdit_Workload");

    /// <summary>"35 %": the slider's value, as the page shows it.</summary>
    public Label<UserEditPage> WorkloadValue => new(this, "UserEdit_WorkloadValue");

    /// <summary>Replaces the name.</summary>
    public UserEditPage EnterName(string name)
    {
        NameField.SetText(name);
        return this;
    }

    /// <summary>Selects one of the three statuses: Active, On leave or Inactive.</summary>
    [BddWhen("I set the status to {status}")]
    public UserEditPage SetStatus(string status)
    {
        StatusButton(status).Select();
        return this;
    }

    /// <summary>Asserts which status is selected.</summary>
    [BddThen("the status should be {status}")]
    public UserEditPage AssertStatus(string status)
    {
        StatusButton(status).AssertSelected();
        return this;
    }

    /// <summary>Gives the user an end date: checks "Has an end date" and picks the date.</summary>
    [BddWhen("I set the end date to {date}")]
    public UserEditPage SetEndDate(DateOnly date)
    {
        HasEndDate.SetChecked(true);
        AllowedUntil.AssertEnabled(true);
        AllowedUntil.SetDate(date.ToDateTime(TimeOnly.MinValue));
        return this;
    }

    /// <summary>Sets the hours per week with the stepper's buttons, reading the shown value to know how far.</summary>
    [BddWhen("I set the hours per week to {hours}")]
    public UserEditPage SetHours(int hours)
    {
        var steps = hours - ShownHours();
        if (steps > 0)
        {
            Hours.IncrementBy(steps);
        }
        else if (steps < 0)
        {
            Hours.DecrementBy(-steps);
        }

        HoursValue.AssertText($"{hours} hours");
        return this;
    }

    /// <summary>Sets when the working day starts.</summary>
    [BddWhen("I set the start of day to {time}")]
    public UserEditPage SetStartOfDay(TimeOnly time)
    {
        StartOfDay.SetTime(time.ToTimeSpan());
        return this;
    }

    /// <summary>Asserts when the working day starts.</summary>
    [BddThen("the start of day should be {time}")]
    public UserEditPage AssertStartOfDay(TimeOnly time)
    {
        StartOfDay.AssertTime(time.ToTimeSpan(), toleranceSeconds: 0);
        return this;
    }

    /// <summary>Sets the workload; the app snaps it to a step of 5.</summary>
    [BddWhen("I set the workload to {percent} %")]
    public UserEditPage SetWorkload(int percent)
    {
        Workload.SetValue(percent);
        WorkloadValue.AssertText($"{percent} %");
        return this;
    }

    /// <summary>Saves a new or edited user and returns to the list.</summary>
    public UserListPage SaveNew()
    {
        SaveButton.Click();
        return UserListPage.Arrived(Context);
    }

    /// <summary>Presses Save on a form that should be refused, and stays.</summary>
    public UserEditPage SaveExpectingErrors()
    {
        SaveButton.Click();
        AssertLoaded(true);
        return this;
    }

    /// <summary>Returns to the list without saving.</summary>
    public UserListPage Cancel()
    {
        CancelButton.Click();
        return UserListPage.Arrived(Context);
    }

    /// <summary>The user form, once it is on screen.</summary>
    public static UserEditPage Arrived(IMauiTestContext context)
    {
        var page = new UserEditPage(context);
        page.AssertLoaded(true, timeoutMs: TestConstants.PageTimeoutMs);
        return page;
    }

    private RadioButton<UserEditPage> StatusButton(string status) => status.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant() switch
    {
        "active" => ActiveStatus,
        "onleave" => OnLeaveStatus,
        "inactive" => InactiveStatus,
        _ => throw new ArgumentException($"'{status}' is not a status: Active, On leave or Inactive.", nameof(status)),
    };

    // "38 hours" -> 38.
    private int ShownHours()
        => int.Parse(HoursValue.GetText()!.Split(' ')[0], CultureInfo.InvariantCulture);
}
