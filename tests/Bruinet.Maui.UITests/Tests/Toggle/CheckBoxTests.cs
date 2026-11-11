using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Toggle;

/// <summary>
/// UI tests for the CheckBox control in the ToggleTestView.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "CheckBox")]
public class CheckBoxTests
{
    private readonly MauiFixture _fixture;

    public CheckBoxTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _fixture.Open(SamplePage.Toggle);
    }

    private ToggleTestPage GetPage()
    {
        return new(_fixture.Context);
    }

    /// <summary>
    /// Verifies that clicking the CheckBox toggles its state.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task CheckBox_Click_TogglesChecked()
    {
        // Arrange - start from a known unchecked state, whatever earlier tests left behind
        var page = GetPage();
        page.TestCheckBox.CheckOff();

        // Act & Assert - "is checked" rather than "checked", which also matches "unchecked"
        page.TestCheckBox.Click()
            .CheckBoxStatusLabel.AssertTextContains("is checked");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that unchecking the CheckBox updates the status.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task CheckBox_ClickTwice_TogglesUnchecked()
    {
        // Arrange - the two clicks only land on checked-then-unchecked if we start off
        var page = GetPage();
        page.TestCheckBox.CheckOff();

        // Act & Assert
        page.TestCheckBox.Click()
            .CheckBoxStatusLabel.AssertTextContains("is checked")
            .TestCheckBox.Click()
            .CheckBoxStatusLabel.AssertTextContains("is unchecked");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Reset button clears the CheckBox state.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Reset")]
    public Task CheckBox_Reset_ClearsState()
    {
        // Arrange - Reset only proves anything if the box is on when we press it
        var page = GetPage();
        page.TestCheckBox.CheckOn();

        // Act & Assert
        page.CheckBoxStatusLabel.AssertTextContains("is checked")
            .ResetButton.Click()
            .CheckBoxStatusLabel.AssertTextContains("is unchecked");

        return Task.CompletedTask;
    }
}
