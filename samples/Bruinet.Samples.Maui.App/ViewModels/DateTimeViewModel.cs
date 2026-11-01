using System.Globalization;

namespace Bruinet.Samples.Maui.App.ViewModels;

/// <summary>
/// ViewModel for DateTime controls testing (DatePicker and TimePicker).
/// Tests date selection, time selection, and constraint validation.
/// </summary>
public class DateTimeViewModel : ParentViewModel
{
    private DateTime selectedDate;
    private TimeSpan selectedTime;
    private string statusMessage = "Ready. Select a date and time to test.";

    public DateTimeViewModel()
    {
        // Initialize with current date/time
        selectedDate = DateTime.Now.Date;
        selectedTime = DateTime.Now.TimeOfDay;
    }

    /// <summary>
    /// Gets the minimum date allowed in the DatePicker (today).
    /// </summary>
    public DateTime MinimumDate => DateTime.Now.Date;

    /// <summary>
    /// Gets the maximum date allowed in the DatePicker (30 days from now).
    /// </summary>
    public DateTime MaximumDate => DateTime.Now.Date.AddDays(30);

    /// <summary>
    /// Gets or sets the selected date.
    /// </summary>
    public DateTime SelectedDate
    {
        get => selectedDate;
        set
        {
            if (SetProperty(ref selectedDate, value))
            {
                // FormattedDate is computed from this field, so its binding only refreshes when
                // it is named explicitly - SetProperty raises the change for SelectedDate alone.
                OnPropertyChanged(nameof(FormattedDate));
                UpdateStatus();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected time.
    /// </summary>
    public TimeSpan SelectedTime
    {
        get => selectedTime;
        set
        {
            if (SetProperty(ref selectedTime, value))
            {
                OnPropertyChanged(nameof(FormattedTime));
                UpdateStatus();
            }
        }
    }

    /// <summary>
    /// Gets the formatted date string for display.
    /// </summary>
    public string FormattedDate => selectedDate.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture);

    /// <summary>
    /// Gets the formatted time string for display.
    /// </summary>
    public string FormattedTime => selectedTime.ToString(@"hh\:mm\:ss", CultureInfo.CurrentCulture);

    /// <summary>
    /// Gets or sets the status message displayed to the user.
    /// </summary>
    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    /// <summary>
    /// Reset command - clears all selections and returns to initial state.
    /// </summary>
    public ICommand ResetCommand => new RelayCommand(Reset);

    /// <summary>
    /// Updates the status message based on current selections.
    /// </summary>
    private void UpdateStatus()
    {
        // Validate date is within range
        if (selectedDate < MinimumDate)
        {
            StatusMessage = $"✗ Date {FormattedDate} is before minimum ({MinimumDate:M/d/yyyy}).";
            return;
        }

        if (selectedDate > MaximumDate)
        {
            StatusMessage = $"✗ Date {FormattedDate} is after maximum ({MaximumDate:M/d/yyyy}).";
            return;
        }

        StatusMessage = $"✓ Date: {FormattedDate} | Time: {FormattedTime}";
    }

    /// <summary>
    /// Resets all selections to their initial values.
    /// </summary>
    private void Reset()
    {
        SelectedDate = DateTime.Now.Date;
        SelectedTime = DateTime.Now.TimeOfDay;
        StatusMessage = "Ready. Select a date and time to test.";
    }
}
