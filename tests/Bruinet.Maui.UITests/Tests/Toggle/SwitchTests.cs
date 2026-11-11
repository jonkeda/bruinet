using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Toggle;

/// <summary>
/// UI tests for the Switch control in the ToggleTestView.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "Switch")]
public class SwitchTests
{
    private readonly MauiFixture _fixture;

    public SwitchTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _fixture.Open(SamplePage.Toggle);
    }

    private ToggleTestPage GetPage()
    {
        return new(_fixture.Context);
    }

    /// <summary>
    /// Verifies that toggling the Switch to ON updates the status.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task Switch_Click_TogglesOn()
    {
        // Arrange
        var page = GetPage();

        // Act & Assert
        page.TestSwitch.Click()
            .SwitchStatusLabel.AssertTextContains("on");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that toggling the Switch OFF updates the status.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task Switch_ClickTwice_TogglesOff()
    {
        // Arrange
        var page = GetPage();

        // Act & Assert
        page.TestSwitch.Click()
            .SwitchStatusLabel.AssertTextContains("on")
            .TestSwitch.Click()
            .SwitchStatusLabel.AssertTextContains("off");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that multiple toggles maintain proper state.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task Switch_MultipleToogles_MaintainsState()
    {
        // Arrange
        var page = GetPage();

        // Act & Assert
        page.TestSwitch.Click()
            .SwitchStatusLabel.AssertTextContains("on")
            .TestSwitch.Click()
            .SwitchStatusLabel.AssertTextContains("off")
            .TestSwitch.Click()
            .SwitchStatusLabel.AssertTextContains("on");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Reset button clears the Switch state.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Reset")]
    public Task Switch_Reset_ClearsState()
    {
        // Arrange
        var page = GetPage();

        // Act & Assert
        page.TestSwitch.Click()
            .SwitchStatusLabel.AssertTextContains("on")
            .ResetButton.Click()
            .SwitchStatusLabel.AssertTextContains("off");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the overall status message includes the switch state in combined output.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Status")]
    public Task Switch_CombinedStatus_ReflectsToggleState()
    {
        // Arrange
        var page = GetPage();

        // Act & Assert
        page.TestSwitch.Click()
            .StatusLabel.AssertTextContains("notifications enabled");

        return Task.CompletedTask;
    }
}
