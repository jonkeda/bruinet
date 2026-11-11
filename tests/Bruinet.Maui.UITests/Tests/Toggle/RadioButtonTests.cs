using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Toggle;

/// <summary>
/// UI tests for RadioButton controls in the ToggleTestView.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "RadioButton")]
public class RadioButtonTests
{
    private readonly MauiFixture _fixture;

    public RadioButtonTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _fixture.Open(SamplePage.Toggle);
    }

    private ToggleTestPage GetPage()
    {
        return new(_fixture.Context);
    }

    /// <summary>
    /// Verifies that selecting Option 1 updates the status.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task RadioButton_SelectOption1_UpdatesStatus()
    {
        // Arrange
        var page = GetPage();

        // Act & Assert
        page.TestRadioButton1.Click()
            .RadioButtonStatusLabel.AssertTextContains("Option 1 selected");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that selecting Option 2 updates the status.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task RadioButton_SelectOption2_UpdatesStatus()
    {
        // Arrange
        var page = GetPage();

        // Act & Assert
        page.TestRadioButton2.Click()
            .RadioButtonStatusLabel.AssertTextContains("Option 2 selected");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that selecting Option 3 updates the status.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task RadioButton_SelectOption3_UpdatesStatus()
    {
        // Arrange
        var page = GetPage();

        // Act & Assert
        page.TestRadioButton3.Click()
            .RadioButtonStatusLabel.AssertTextContains("Option 3 selected");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that RadioButtons are mutually exclusive (selection changes when switching options).
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task RadioButton_MutuallyExclusive_SelectsOneOnly()
    {
        // Arrange
        var page = GetPage();

        // Act & Assert
        page.TestRadioButton1.Click()
            .RadioButtonStatusLabel.AssertTextContains("Option 1 selected")
            .TestRadioButton2.Click()
            .RadioButtonStatusLabel.AssertTextContains("Option 2 selected")
            .TestRadioButton3.Click()
            .RadioButtonStatusLabel.AssertTextContains("Option 3 selected");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Reset button clears the RadioButton selection.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Reset")]
    public Task RadioButton_Reset_ClearsSelection()
    {
        // Arrange
        var page = GetPage();

        // Act & Assert
        page.TestRadioButton1.Click()
            .RadioButtonStatusLabel.AssertTextContains("Option 1 selected")
            .ResetButton.Click()
            .RadioButtonStatusLabel.AssertTextContains("No option selected");

        return Task.CompletedTask;
    }
}
