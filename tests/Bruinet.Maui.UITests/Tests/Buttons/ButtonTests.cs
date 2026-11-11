using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Buttons;

/// <summary>
/// UI tests for the Button control in the ButtonsTestView.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "Button")]
public class ButtonTests
{
    private readonly MauiFixture _fixture;

    public ButtonTests(MauiFixture fixture)
    {
        _fixture = fixture;

        _fixture.Open(SamplePage.Buttons);
    }

    private ButtonsTestPage GetPage()
    {
        return new(_fixture.Context);
    }

    /// <summary>
    /// Verifies that tapping the Button executes its command.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Tap")]
    public Task Button_Tap_ExecutesCommand()
    {
        var page = GetPage();
        // Act
        page.TestButton.Click()
            .StatusLabel.AssertTextContains("Button tapped");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that tapping the Button multiple times increments the tap count.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Tap")]
    public Task Button_MultipleTaps_IncrementsCount()
    {
        var page = GetPage();

        // Act
        page.TestButton.Click()
            .StatusLabel.AssertTextContains("1 time")
            .TestButton.Click()
            .StatusLabel.AssertTextContains("2 times");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that tapping the Reset button clears the status.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Reset")]
    public Task Button_Reset_ClearsStatus()
    {
        var page = GetPage();
        // Arrange

        // Act
        page.TestButton.Click()
            .StatusLabel.AssertTextContains("Button tapped")
            .ResetButton.Click()
            .StatusLabel.AssertTextContains("Ready");

        return Task.CompletedTask;
    }
}
