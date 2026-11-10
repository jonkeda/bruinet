using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Text;

/// <summary>
/// UI tests for the SearchBar control in the TextTestView.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "SearchBar")]
public class SearchBarTests
{
    private readonly MauiFixture _fixture;

    public SearchBarTests(MauiFixture fixture)
    {
        _fixture = fixture;

        fixture.Open(SamplePage.Text);
    }

    private TextTestPage GetPage()
    {
        return new(_fixture.Context);
    }

    /// <summary>
    /// Verifies that the SearchBar control exists on the page.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Exists")]
    public Task SearchBar_IsExists_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestSearchBar.AssertExists();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the SearchBar control is visible once scrolled to.
    /// </summary>
    /// <remarks>
    /// The scroll is the point of the test, not boilerplate around it. The text page is a
    /// <c>ScrollView</c> and the SearchBar sits below the fold, so it genuinely reports
    /// <c>IsOffscreen</c> with a zero bounding rectangle until something scrolls to it - which
    /// is what a user does before looking at it.
    /// <para>
    /// Actions do this on their own: every path through <c>RunDoWithElement</c> calls
    /// <c>EnsureVisible</c> first, which is why <c>SetText</c> on this same control passes
    /// without a scroll. <c>IsVisible</c> is a query, and a query reports what is on screen
    /// rather than quietly changing it.
    /// </para>
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsVisible")]
    public Task SearchBar_IsVisible_ReturnsTrue()
    {
        var page = GetPage();

        // Assert
        page.TestSearchBar.AssertVisibleAfterScroll();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the SearchBar control is enabled.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsEnabled")]
    public Task SearchBar_IsEnabled_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestSearchBar.AssertEnabled();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that search text can be entered into the SearchBar control.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SetText")]
    public Task SearchBar_SetText_TextIsSet()
    {
        var page = GetPage();
        const string searchQuery = "test search";

        // Act
        page.TestSearchBar.SetText(searchQuery);

        // Assert - verify the text is set by checking the status label
        page.SearchStatusLabel.AssertTextContains(searchQuery);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that search query is displayed with correct character count.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "CharacterCount")]
    public Task SearchBar_CharacterCount_IsAccurate()
    {
        var page = GetPage();
        const string searchQuery = "Search";

        // Act
        page.TestSearchBar.SetText(searchQuery);

        // Assert - verify character count in status
        page.SearchStatusLabel.AssertTextContains($"({searchQuery.Length} chars)");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the SearchBar shows as empty when cleared.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Clear")]
    public Task SearchBar_Clear_TextIsRemoved()
    {
        var page = GetPage();
        const string searchQuery = "Query";

        // Arrange
        page.TestSearchBar.SetText(searchQuery);
        page.SearchStatusLabel.AssertTextContains(searchQuery);

        // Act - clear using the button
        page.ClearSearchButton.Click();

        // Assert
        page.SearchStatusLabel.AssertTextContains("cleared");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the SearchBar placeholder is shown when empty.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Placeholder")]
    public Task SearchBar_Placeholder_IsShownWhenEmpty()
    {
        var page = GetPage();

        page.TestSearchBar.Clear();
        // Assert - SearchBar should show empty status when no text
        page.TestSearchBar.AssertExists();
        page.SearchStatusLabel.AssertTextContains("empty");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that multiple search queries work correctly.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "MultipleSearches")]
    public Task SearchBar_MultipleSearches_ResultsInLatestQuery()
    {
        var page = GetPage();
        const string firstQuery = "First search";
        const string secondQuery = "Second search";

        // Act
        page.TestSearchBar.SetText(firstQuery);
        page.SearchStatusLabel.AssertTextContains(firstQuery);

        page.TestSearchBar.SetText(secondQuery);

        // Assert - latest query should be displayed
        page.SearchStatusLabel.AssertTextContains(secondQuery);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that search query status is updated when text is modified.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "StatusUpdate")]
    public Task SearchBar_StatusUpdate_ReflectsCurrentQuery()
    {
        var page = GetPage();
        const string searchQuery = "Status test";

        // Act
        page.TestSearchBar.SetText(searchQuery);

        // Assert - status should indicate query is ready to search
        page.SearchStatusLabel.AssertTextContains("Press search");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Reset All button clears the SearchBar.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "ResetAll")]
    public Task SearchBar_ResetAll_TextIsCleared()
    {
        var page = GetPage();
        const string searchQuery = "Test";

        // Arrange
        page.TestSearchBar.SetText(searchQuery);
        page.SearchStatusLabel.AssertTextContains(searchQuery);

        // Act
        page.ResetAllButton.Click();

        // Assert
        page.SearchStatusLabel.AssertTextContains("Ready");
        return Task.CompletedTask;
    }
}
