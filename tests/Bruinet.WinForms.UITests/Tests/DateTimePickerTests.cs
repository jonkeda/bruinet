using Bruinet.WinForms.UITests.Fixtures;
using Bruinet.WinForms.UITests.Pages;

namespace Bruinet.WinForms.UITests.Tests;

/// <summary>
/// Tests for DateTimePicker, TrackBar, and ProgressBar controls.
/// </summary>
[Collection("WinForms UITests")]
public class DateTimePickerTests
{
    private readonly WinFormsSampleFixture _fixture;

    public DateTimePickerTests(WinFormsSampleFixture fixture) => _fixture = fixture;

    private LoginPage GetPage()
    {
        var page = new LoginPage(_fixture.Context);
        page.ClickClear();
        page.WaitForFormCleared(timeoutMs: 2000);
        return page;
    }

    [Fact]
    public void DateTimePicker_SetDate_SetsDateCorrectly()
    {
        var page = GetPage();
        page.SetStartDate(new DateTime(2025, 06, 15));
        Assert.Equal(new DateTime(2025, 06, 15), page.StartDatePicker.GetDate());
    }

    [Fact]
    public void DateTimePicker_SetDate_Today()
    {
        var page = GetPage();
        page.SetStartDate(DateTime.Today);
        Assert.Equal(DateTime.Today, page.StartDatePicker.GetDate());
    }
}

/// <summary>
/// Tests for TrackBar control.
/// </summary>
[Collection("WinForms UITests")]
public class TrackBarTests
{
    private readonly WinFormsSampleFixture _fixture;

    public TrackBarTests(WinFormsSampleFixture fixture) => _fixture = fixture;

    private LoginPage GetPage()
    {
        var page = new LoginPage(_fixture.Context);
        page.ClickClear();
        page.WaitForFormCleared(timeoutMs: 2000);
        return page;
    }

    [Fact]
    public void TrackBar_SetValue_SetsValueCorrectly()
    {
        var page = GetPage();
        page.SetVolume(75);
        Assert.Equal(75, page.GetVolume());
    }

    [Fact]
    public void TrackBar_SetMinimum_Works()
    {
        var page = GetPage();
        page.SetVolume(0);
        Assert.Equal(0, page.GetVolume());
    }

    [Fact]
    public void TrackBar_SetMaximum_Works()
    {
        var page = GetPage();
        page.SetVolume(100);
        Assert.Equal(100, page.GetVolume());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(75)]
    [InlineData(100)]
    public void TrackBar_VariousValues(int value)
    {
        var page = GetPage();
        page.SetVolume(value);
        Assert.Equal(value, page.GetVolume());
    }
}

/// <summary>
/// Tests for ProgressBar control.
/// </summary>
[Collection("WinForms UITests")]
public class ProgressBarTests
{
    private readonly WinFormsSampleFixture _fixture;

    public ProgressBarTests(WinFormsSampleFixture fixture) => _fixture = fixture;

    [Fact]
    public void ProgressBar_GetValue_ReturnsCurrentProgress()
    {
        var page = new LoginPage(_fixture.Context);
        var progress = page.GetProgress();
        Assert.NotNull(progress);
        Assert.InRange(progress.Value, 0, 100);
    }
}
