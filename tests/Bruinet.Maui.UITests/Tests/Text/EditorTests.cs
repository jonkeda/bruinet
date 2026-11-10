using Bruinet.Maui.UITests.Pages;
using System.Globalization;

namespace Bruinet.Maui.UITests.Tests.Text;

/// <summary>
/// UI tests for the Editor control in the TextTestView.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "Editor")]
public class EditorTests
{
    private readonly MauiFixture _fixture;

    public EditorTests(MauiFixture fixture)
    {
        _fixture = fixture;

        fixture.Open(SamplePage.Text);
    }

    private TextTestPage GetPage()
    {
        return new(_fixture.Context);
    }

    /// <summary>
    /// Verifies that the Editor control exists on the page.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Exists")]
    public Task Editor_IsExists_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestEditor.AssertExists();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Editor control is visible.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsVisible")]
    public Task Editor_IsVisible_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestEditor.AssertVisible();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Editor control is enabled.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsEnabled")]
    public Task Editor_IsEnabled_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestEditor.AssertEnabled();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that single-line text can be entered into the Editor control.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SetText")]
    public Task Editor_SetText_SingleLine_TextIsSet()
    {
        var page = GetPage();
        const string testText = "Hello Editor";

        // Act
        page.TestEditor.SetText(testText);

        // Assert - verify the text is set by checking the status label
        page.EditorStatusLabel.AssertTextContains(testText.Length.ToString(CultureInfo.InvariantCulture));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that multi-line text can be entered into the Editor control.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "MultiLineText")]
    public Task Editor_SetText_MultiLine_TextIsSet()
    {
        var page = GetPage();
        const string testText = "Line 1\rLine 2\rLine 3";

        // Act
        page.TestEditor.SetText(testText);

        // Assert - verify multi-line text and line count
        page.EditorStatusLabel.AssertTextContains("3 lines");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Editor shows correct character count.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "CharacterCount")]
    public Task Editor_CharacterCount_IsAccurate()
    {
        var page = GetPage();
        const string testText = "Test";

        // Act
        page.TestEditor.SetText(testText);

        // Assert - verify character count in status
        page.EditorStatusLabel.AssertTextContains($"{testText.Length} chars");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Editor can be cleared with the Clear button.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Clear")]
    public Task Editor_Clear_TextIsRemoved()
    {
        var page = GetPage();
        const string testText = "Test content";

        // Arrange
        page.TestEditor.SetText(testText);
        page.EditorStatusLabel.AssertTextContains(testText.Length.ToString(CultureInfo.InvariantCulture));

        // Act - clear using the button
        page.ClearEditorButton.Click();

        // Assert
        page.EditorStatusLabel.AssertTextContains("cleared");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Editor placeholder is shown when empty.
    /// </summary>
    /*[Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Placeholder")]
    public Task Editor_Placeholder_IsShownWhenEmpty()
    {
        var page = GetPage();

        // Assert - Editor should show empty status when no text
        page.TestEditor.Clear();
        page.TestEditor.AssertExists();
        page.EditorStatusLabel.AssertTextContains("empty");
        return Task.CompletedTask;
    }*/

    /// <summary>
    /// Verifies that line breaks are preserved in the Editor.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "LineBreaks")]
    public Task Editor_LineBreaks_ArePreserved()
    {
        var page = GetPage();
        const string testText = "Line 1\nLine 2";

        // Act
        page.TestEditor.SetText(testText);

        // Assert - verify line count is 2
        page.EditorStatusLabel.AssertTextContains("2 lines");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that multiple text inputs work correctly in the Editor.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "MultipleInputs")]
    public Task Editor_MultipleInputs_ResultsInLatestText()
    {
        var page = GetPage();
        const string firstText = "First content";
        const string secondText = "Second content";

        // Act
        page.TestEditor.SetText(firstText);
        page.EditorStatusLabel.AssertTextContains(firstText.Length.ToString(CultureInfo.InvariantCulture));

        page.TestEditor.SetText(secondText);

        // Assert - latest text length should be displayed
        page.EditorStatusLabel.AssertTextContains(secondText.Length.ToString(CultureInfo.InvariantCulture));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Reset All button clears the Editor.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "ResetAll")]
    public Task Editor_ResetAll_TextIsCleared()
    {
        var page = GetPage();
        const string testText = "Test";

        // Arrange
        page.TestEditor.SetText(testText);
        page.EditorStatusLabel.AssertTextContains(testText.Length.ToString(CultureInfo.InvariantCulture));

        // Act
        page.ResetAllButton.Click();

        // Assert
        page.EditorStatusLabel.AssertTextContains("Ready");
        return Task.CompletedTask;
    }
}
