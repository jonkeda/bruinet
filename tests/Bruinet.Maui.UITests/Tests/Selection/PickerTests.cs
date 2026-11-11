using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Selection;

/// <summary>
/// UI tests for the Picker control in the SelectionTestView.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "Picker")]
public class PickerTests
{
    private readonly MauiFixture _fixture;

    public PickerTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _fixture.Open(SamplePage.Selection);
    }

    private SelectionTestPage GetPage()
    {
        return new(_fixture.Context);
    }

    /// <summary>
    /// Verifies that selecting an item in the Picker updates the status message.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SelectByIndex")]
    public Task Picker_SelectByIndex_UpdatesStatus()
    {
        var page = GetPage();

        // Act & Assert
        page.TestPicker.SelectByIndex(0)
            .StatusLabel.AssertTextContains("Selected");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that selecting an item by text updates the status.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SelectByText")]
    public Task Picker_SelectByText_UpdatesStatus()
    {
        var page = GetPage();

        // Act & Assert
        page.TestPicker.SelectByText("Option 1")
            .StatusLabel.AssertTextContains("Option 1");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that multiple selections update the status correctly.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "MultipleSelections")]
    public Task Picker_MultipleSelections_UpdatesStatus()
    {
        var page = GetPage();

        // Act & Assert
        page.TestPicker.SelectByIndex(0)
            .StatusLabel.AssertTextContains("Option 1")
            .TestPicker.SelectByIndex(2)
            .StatusLabel.AssertTextContains("Option 3");

        return Task.CompletedTask;
    }

    /// <summary>
    /// A picker with nothing chosen reads no index and no text (the long picker starts that way). The Core
    /// interface says null only for a missing element; Maui's picker answers null for no choice too.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "GetSelected")]
    public Task Picker_WithNothingChosen_ReadsNoIndex_AndNoText()
    {
        var page = GetPage();

        Assert.Null(page.LongPicker.GetSelectedIndex());
        Assert.Null(page.LongPicker.GetSelectedText());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that tapping the Reset button clears the selection.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Reset")]
    public Task Picker_Reset_ClearsSelection()
    {
        var page = GetPage();

        // Act
        page.TestPicker.SelectByIndex(1)
            .StatusLabel.AssertTextContains("Selected")
            .ResetButton.Click()
            .StatusLabel.AssertTextContains("Ready");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Picker displays the correct selected value.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "GetSelectedText")]
    public Task Picker_GetSelectedText_ReturnsCorrectValue()
    {
        var page = GetPage();

        // Act
        page.TestPicker.SelectByIndex(0);
        var selectedText = page.TestPicker.GetSelectedText();

        // Assert
        Assert.Equal("Option 1", selectedText);
        return Task.CompletedTask;
    }
}
