using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Buttons;

/// <summary>
/// UI tests for the ImageButton control in the ButtonsTestView.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "ImageButton")]
public class ImageButtonTests
{
    private readonly MauiFixture _fixture;

    public ImageButtonTests(MauiFixture fixture)
    {
        _fixture = fixture;

        _fixture.Open(SamplePage.Buttons);
    }

    private ButtonsTestPage GetPage()
    {
        return new(_fixture.Context);
    }

    /// <summary>
    /// Verifies that the ImageButton exists on the page.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Exists")]
    public Task ImageButton_IsExists_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        Assert.True(page.TestImageButton.Exists());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the ImageButton is visible.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsVisible")]
    public Task ImageButton_IsVisible_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestImageButton.AssertVisible();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the ImageButton is enabled.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsEnabled")]
    public Task ImageButton_IsEnabled_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestImageButton.AssertVisible();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that tapping the ImageButton executes its command.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Tap")]
    public Task ImageButton_Tap_ExecutesCommand()
    {
        var page = GetPage();

        // Act
        page.TestImageButton.Click()
            .StatusLabel.AssertTextContains("ImageButton");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the status message is updated when ImageButton is tapped.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Tap")]
    public Task ImageButton_Tap_UpdatesStatus()
    {
        var page = GetPage();

        // Act
        page.TestImageButton.Click();

        // Assert
        page.StatusLabel.AssertTextContains("ImageButton");
        return Task.CompletedTask;
    }
}
