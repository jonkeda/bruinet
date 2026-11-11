using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Display;

/// <summary>
/// UI tests for the ProgressBar control in the DisplayTestView.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "ProgressBar")]
public class ProgressBarTests
{
    private readonly MauiFixture _fixture;

    public ProgressBarTests(MauiFixture fixture)
    {
        _fixture = fixture;
        // Navigate to Display tab
        _fixture.Open(SamplePage.Display);
    }

    private DisplayTestPage GetPage()
    {
        return new(_fixture.Context);
    }

    /// <summary>
    /// Verifies that increasing the progress bar value updates the status message.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IncreaseProgress")]
    public Task ProgressBar_IncreaseProgress_UpdatesValue()
    {
        var page = GetPage();
        // Act - Increase progress from initial 50% to 60%
        page.IncreaseProgressButton.Click()
            .StatusLabel.AssertTextContains("60%");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that decreasing the progress bar value updates the status message.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "DecreaseProgress")]
    public Task ProgressBar_DecreaseProgress_UpdatesValue()
    {
        var page = GetPage();
        // Act - Decrease progress from initial 50% to 40%
        page.DecreaseProgressButton.Click()
            .StatusLabel.AssertTextContains("40%");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that progress bar is bounded by 0-100%.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "BoundedProgress")]
    public Task ProgressBar_Progress_BoundedByMinMax()
    {
        var page = GetPage();

        // Act - Increase to max (100%)
        page.IncreaseProgressButton.Click()
            .IncreaseProgressButton.Click()
            .IncreaseProgressButton.Click()
            .IncreaseProgressButton.Click()
            .IncreaseProgressButton.Click()
            // At 100%, should not exceed
            .IncreaseProgressButton.Click()
            .StatusLabel.AssertTextContains("100%");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that resetting returns the progress to its initial value.
    /// </summary>
    /// <remarks>
    /// Asserts through the status label rather than the control's own progress: Android exposes
    /// no range information for a ProgressBar, so its value is unreadable there and only the app
    /// can report what it restored.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Reset")]
    public Task ProgressBar_Reset_ReturnsToInitialState()
    {
        var page = GetPage();

        // Act - Change progress then reset
        page.IncreaseProgressButton.Click()
            .StatusLabel.AssertTextContains("60%")
            .ResetButton.Click()
            .StatusLabel.AssertTextContains("50%");

        return Task.CompletedTask;
    }
}
