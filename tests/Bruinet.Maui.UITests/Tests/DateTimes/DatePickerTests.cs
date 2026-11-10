using Bruinet.Core.Exceptions;
using System;
using Bruinet.Maui.UITests.Pages;
using DateTimeType = System.DateTime;
using System.Globalization;

namespace Bruinet.Maui.UITests.Tests.DateTimes;

/// <summary>
/// UI tests for the DatePicker control in the DateTimeTestView.
/// Validates date selection, min/max constraint enforcement, and formatting.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "DatePicker")]
public class DatePickerTests
{
    private readonly MauiFixture _fixture;

    public DatePickerTests(MauiFixture fixture)
    {
        _fixture = fixture;
        // Navigate to DateTime test page if needed
        // For now, assumes the view is accessible via direct route or default navigation

        _fixture.Open(SamplePage.DateTime);
    }

    private DateTimeTestPage GetPage()
    {
        return new(_fixture.Context);
    }

    /// <summary>
    /// Verifies that the DatePicker control exists on the page.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Exists")]
    public Task DatePicker_IsExists_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestDatePicker.AssertExists();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the DatePicker is visible.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsVisible")]
    public Task DatePicker_IsVisible_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestDatePicker.AssertVisible();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the DatePicker is enabled.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsEnabled")]
    public Task DatePicker_IsEnabled_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestDatePicker.AssertEnabled();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that selecting a date updates the displayed date status.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SetDate")]
    public Task DatePicker_SetDate_UpdatesDisplay()
    {
        var page = GetPage();

        // The label carries "Selected Date: {0}" from its StringFormat, so asserting that literal
        // passes whether or not the date changed. Assert the date the control reports back.
        var target = DateTimeType.Now.Date.AddDays(5);
        page.TestDatePicker.SetDate(target)
            .TestDatePicker.AssertDate(target);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that a date before MinimumDate is refused rather than silently ignored.
    /// </summary>
    /// <remarks>
    /// The app's "before minimum" validation branch is unreachable on Windows: MinimumDate keeps
    /// the day out of the calendar entirely, so there is no way to select it and nothing for the
    /// view model to reject. What the control can be held to is that it says so, instead of
    /// reporting a set that did not happen - which is what it used to do.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Constraints")]
    public Task DatePicker_DateBeforeMinimum_IsRefused()
    {
        var page = GetPage();
        var pastDate = DateTimeType.Now.Date.AddDays(-1); // Yesterday (before minimum of today)

        var error = Assert.Throws<BruinetException>(() => page.TestDatePicker.SetDate(pastDate));

        // Asserted on the two facts rather than on the wording. This used to look for the phrase
        // "Could not set date", which broke in step 20 when the route changed and the behaviour
        // did not - the refusal is still a refusal, and it now also says what the control holds.
        Assert.Contains(pastDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), error.Message);
        Assert.Contains("MinimumDate", error.Message);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that a date after MaximumDate is refused rather than silently ignored.
    /// </summary>
    /// <remarks>See <see cref="DatePicker_DateBeforeMinimum_IsRefused"/>.</remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Constraints")]
    public Task DatePicker_DateAfterMaximum_IsRefused()
    {
        var page = GetPage();
        var futureDate = DateTimeType.Now.Date.AddDays(31); // 31 days from now (max is 30)

        var error = Assert.Throws<BruinetException>(() => page.TestDatePicker.SetDate(futureDate));

        // Asserted on the two facts rather than on the wording. This used to look for the phrase
        // "Could not set date", which broke in step 20 when the route changed and the behaviour
        // did not - the refusal is still a refusal, and it now also says what the control holds.
        Assert.Contains(futureDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), error.Message);
        Assert.Contains("MinimumDate", error.Message);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that a valid date within constraints shows success message.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Constraints")]
    public Task DatePicker_DateWithinRange_ShowsSuccess()
    {
        var page = GetPage();
        var validDate = DateTimeType.Now.Date.AddDays(15); // 15 days from now (within 0-30 range)

        // Act & Assert
        page.TestDatePicker.SetDate(validDate)
            .StatusLabel.AssertTextContains("✓");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the date format displays correctly (e.g., "Monday, January 1, 2025").
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Format")]
    public Task DatePicker_DateFormat_DisplaysCorrectly()
    {
        var page = GetPage();
        var testDate = DateTimeType.Now.Date.AddDays(10);

        // Act & Assert
        page.TestDatePicker.SetDate(testDate)
            .DateStatusLabel.AssertTextContains(testDate.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies the picker can be focused, and reports it, without the pointer.
    /// </summary>
    /// <remarks>
    /// DatePicker derived from ViewBase until this was added, so it had no Focus at all - the
    /// call did not compile. Focusing goes through the platform's own focus rather than a click,
    /// which matters here more than elsewhere: clicking this control opens its calendar.
    /// </remarks>
    [WindowsOnlyFact("on Android focusing a DatePicker is a tap, which opens its dialog over the page", Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Focus")]
    public Task DatePicker_Focus_IsReported()
    {
        var page = GetPage();

        page.TestDatePicker.Focus();

        page.TestDatePicker.AssertFocused();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Reset button clears the date selection and status.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Reset")]
    public Task DatePicker_Reset_ClearsSelection()
    {
        var page = GetPage();

        // Act & Assert
        page.TestDatePicker.SetDate(DateTimeType.Now.Date.AddDays(10))
            .DateStatusLabel.AssertTextContains("Selected Date")
            .ResetButton.Click()
            .StatusLabel.AssertTextContains("Ready");

        return Task.CompletedTask;
    }
}
