using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Text;

/// <summary>
/// UI tests for the Entry control in the TextTestView.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "Entry")]
public class EntryTests
{
    private readonly MauiFixture _fixture;

    public EntryTests(MauiFixture fixture)
    {
        _fixture = fixture;

        fixture.Open(SamplePage.Text);
    }

    private TextTestPage GetPage()
    {
        return new(_fixture.Context);
    }

    /// <summary>
    /// Verifies that the Entry control exists on the page.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Exists")]
    public Task Entry_IsExists_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestEntry.AssertExists();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Entry control is visible.
    /// </summary>
    /*[Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsVisible")]
    public Task Entry_IsVisible_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestEntry.AssertVisible();
        return Task.CompletedTask;
    }*/

    /// <summary>
    /// Verifies that the Entry control is enabled.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsEnabled")]
    public Task Entry_IsEnabled_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestEntry.AssertEnabled();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that text can be entered into the Entry control.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SetText")]
    public Task Entry_SetText_TextIsSet()
    {
        var page = GetPage();
        const string testText = "Hello Entry";

        // Act
        page.TestEntry.SetText(testText);

        // Assert - verify the text is set by checking the status label
        page.EntryStatusLabel.AssertTextContains(testText);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that text input is captured in the Entry control.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "TypeText")]
    public Task Entry_TypeText_TextIsDisplayed()
    {
        var page = GetPage();
        const string testText = "Test input";

        // Act
        page.TestEntry.SetText(testText);

        // Assert
        page.EntryStatusLabel.AssertTextContains($"'{testText}'");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Entry shows correct character count.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "CharacterCount")]
    public Task Entry_CharacterCount_IsAccurate()
    {
        var page = GetPage();
        const string testText = "Test";

        // Act
        page.TestEntry.SetText(testText);

        // Assert - verify character count
        page.EntryStatusLabel.AssertTextContains($"({testText.Length} chars)");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Entry can be cleared with the Clear button.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Clear")]
    public Task Entry_Clear_TextIsRemoved()
    {
        var page = GetPage();
        const string testText = "Test";

        // Arrange
        page.TestEntry.SetText(testText);
        page.EntryStatusLabel.AssertTextContains(testText);

        // Act - clear using the button
        page.ClearEntryButton.Click();

        // Assert
        page.EntryStatusLabel.AssertTextContains("cleared");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Entry placeholder is shown when empty.
    /// </summary>
    /*[Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Placeholder")]
    public Task Entry_Placeholder_IsShownWhenEmpty()
    {
        var page = GetPage();

        // Assert - Entry should have placeholder text visible when empty
        page.TestEntry.AssertExists();
        page.EntryStatusLabel.AssertTextContains("empty");
        return Task.CompletedTask;
    }*/

    /// <summary>
    /// Verifies that multiple text inputs work correctly.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "MultipleInputs")]
    public Task Entry_MultipleInputs_ResultsInLatestText()
    {
        var page = GetPage();
        const string firstText = "First";
        const string secondText = "Second";

        // Act
        page.TestEntry.SetText(firstText);
        page.EntryStatusLabel.AssertTextContains(firstText);

        page.TestEntry.SetText(secondText);

        // Assert - latest text should be displayed
        page.EntryStatusLabel.AssertTextContains(secondText);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Reset All button clears the Entry.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "ResetAll")]
    public Task Entry_ResetAll_TextIsCleared()
    {
        var page = GetPage();
        const string testText = "Test";

        // Arrange
        page.TestEntry.SetText(testText);
        page.EntryStatusLabel.AssertTextContains(testText);

        // Act
        page.ResetAllButton.Click();

        // Assert
        page.EntryStatusLabel.AssertTextContains("Ready");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Focusing the entry is observable through IsFocused.
    /// </summary>
    /// <remarks>
    /// The test that was missing. <c>IsFocused</c> read three attribute names that Windows does
    /// not publish and returned false when none answered, so it reported "not focused" for a
    /// focused control on every Windows run - and nothing failed, because nothing asked.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsFocused")]
    public Task Entry_Focus_IsReported()
    {
        var page = GetPage();

        page.TestEntry.Focus();

        page.TestEntry.AssertFocused();

        return Task.CompletedTask;
    }

    /// <summary>
    /// A read-only entry reports itself read-only, and an ordinary one does not.
    /// </summary>
    /// <remarks>
    /// Windows answers through the Value pattern. Android publishes no editability, so the test
    /// asks the element whether the pattern is there at all and asserts nothing when it is not -
    /// what each platform can actually know, rather than a platform branch.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsReadOnly")]
    public Task Entry_ReadOnly_IsReported()
    {
        var page = GetPage();

        // null means the platform publishes no editability, so there is nothing to assert. The
        // tri-state answers the capability question directly - no need to reach for the element
        // and ask it about its patterns.
        var readOnly = page.ReadOnlyEntry.IsReadOnly();
        if (readOnly == null)
        {
            return Task.CompletedTask;
        }

        page.ReadOnlyEntry.AssertReadOnly(true)
            .TestEntry.AssertReadOnly(false);

        return Task.CompletedTask;
    }
}
