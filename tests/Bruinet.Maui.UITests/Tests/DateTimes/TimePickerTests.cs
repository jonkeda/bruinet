using Bruinet.Maui.UITests.Pages;
using DateTimeType = System.DateTime;
using System.Globalization;

namespace Bruinet.Maui.UITests.Tests.DateTimes;

/// <summary>
/// UI tests for the TimePicker control in the DateTimeTestView.
/// Validates time selection, value updates, and formatting.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "TimePicker")]
public class TimePickerTests
{
    private readonly MauiFixture _fixture;

    public TimePickerTests(MauiFixture fixture)
    {
        _fixture = fixture;

        // Navigate to DateTime test page if needed
        _fixture.Open(SamplePage.DateTime);
    }

    private DateTimeTestPage GetPage()
    {
        return new(_fixture.Context);
    }

    /// <summary>
    /// Verifies that the TimePicker control exists on the page.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Exists")]
    public Task TimePicker_IsExists_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestTimePicker.AssertExists();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the TimePicker is visible.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsVisible")]
    public Task TimePicker_IsVisible_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestTimePicker.AssertVisible();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the TimePicker is enabled.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsEnabled")]
    public Task TimePicker_IsEnabled_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestTimePicker.AssertEnabled();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that selecting a time updates the displayed time status.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SetTime")]
    public Task TimePicker_SetTime_UpdatesDisplay()
    {
        var page = GetPage();
        var testTime = new TimeSpan(14, 30, 0); // 2:30 PM

        // Act & Assert
        page.TestTimePicker.SetTime(testTime)
            .TimeStatusLabel.AssertTextContains("Selected Time");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that time value is correctly formatted (hh:mm:ss).
    /// </summary>
    /// <remarks>
    /// Whole minutes, because the WinUI flyout offers hour, minute and period selectors and no
    /// seconds. A time carrying seconds cannot be set through the control at all, so asserting
    /// one would test the test rather than the control.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Format")]
    public Task TimePicker_TimeFormat_DisplaysCorrectly()
    {
        var page = GetPage();
        var testTime = new TimeSpan(9, 45, 0); // 9:45 AM

        // Act & Assert
        page.TestTimePicker.SetTime(testTime)
            .TimeStatusLabel.AssertTextContains(testTime.ToString(@"hh\:mm\:ss", CultureInfo.CurrentCulture));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that changing time multiple times updates the display each time.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SetTime")]
    public Task TimePicker_MultipleTimeChanges_UpdatesEachTime()
    {
        var page = GetPage();
        var time1 = new TimeSpan(8, 0, 0);
        var time2 = new TimeSpan(12, 30, 0);
        var time3 = new TimeSpan(18, 45, 0);

        // Act & Assert
        page.TestTimePicker.SetTime(time1)
            .TimeStatusLabel.AssertTextContains("08:00:00")
            .TestTimePicker.SetTime(time2)
            .TimeStatusLabel.AssertTextContains("12:30:00")
            .TestTimePicker.SetTime(time3)
            .TimeStatusLabel.AssertTextContains("18:45:00");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that midnight (00:00:00) is handled correctly.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SetTime")]
    public Task TimePicker_Midnight_DisplaysCorrectly()
    {
        var page = GetPage();
        var midnight = new TimeSpan(0, 0, 0);

        // Act & Assert
        page.TestTimePicker.SetTime(midnight)
            .TimeStatusLabel.AssertTextContains("00:00:00");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that end-of-day is handled correctly.
    /// </summary>
    /// <remarks>
    /// 23:59, not 23:59:59 - the flyout has no seconds selector. This still covers what the case
    /// is for: the last minute of the day, and the PM end of the 12-hour conversion.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SetTime")]
    public Task TimePicker_EndOfDay_DisplaysCorrectly()
    {
        var page = GetPage();
        var endOfDay = new TimeSpan(23, 59, 0);

        // Act & Assert
        page.TestTimePicker.SetTime(endOfDay)
            .TimeStatusLabel.AssertTextContains("23:59:00");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Reset button clears the time selection and status.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Reset")]
    public Task TimePicker_Reset_ClearsSelection()
    {
        var page = GetPage();
        var testTime = new TimeSpan(14, 30, 0);

        // Act & Assert
        page.TestTimePicker.SetTime(testTime)
            .TimeStatusLabel.AssertTextContains("Selected Time")
            .ResetButton.Click()
            .StatusLabel.AssertTextContains("Ready");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that date and time can be set together and form valid combined DateTime.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Combined")]
    public Task TimePicker_CombinedWithDate_WorksTogether()
    {
        var page = GetPage();
        var testDate = DateTimeType.Now.Date.AddDays(14);
        var testTime = new TimeSpan(15, 30, 0);

        // Act & Assert
        page.TestDatePicker.SetDate(testDate)
            .TestTimePicker.SetTime(testTime)
            .StatusLabel.AssertTextContains("✓");

        return Task.CompletedTask;
    }
}
