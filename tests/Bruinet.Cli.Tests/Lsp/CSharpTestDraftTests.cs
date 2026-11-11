using Xunit;

namespace Bruinet.Bdd.LanguageServer.Tests;

/// <summary>
/// A feature drafted from a C# UI test (T18): each statement becomes the suite phrase whose words match it,
/// or a commented gap, and phrases that differ only in a value are grouped.
/// </summary>
public sealed class CSharpTestDraftTests
{
    private const string Test = """
        using Xunit;

        [Trait("Module", "Shelf")]
        public sealed class ShelfTests
        {
            [Fact]
            [Trait("Journey", "SHF.01")]
            public void Refresh_ReloadsTheBooks()
            {
                try
                {
                    OpenShelf();
                    ShelfPage.Refresh();
                    Assert.Equal(3, ShelfPage.BookCount);
                    Assert.True(ShelfPage.Spinner.IsHidden);
                }
                finally
                {
                    Backend.Reset();
                }
            }
        }
        """;

    private static readonly DraftPhrase[] Phrases =
    [
        new("Given", "the app is ready for Shelf", "ShelfSteps"),
        new("When", "I refresh the shelf", "ShelfSteps"),
        new("Then", "the shelf shows 3 books", "ShelfSteps"),
        new("Then", "the shelf shows 4 books", "ShelfSteps"),
        new("When", "I open the {tab} tab", "Shared"),
    ];

    [Fact]
    public void ATest_IsDrafted_AsTheReadyStep_ItsMatchedPhrases_AndItsGaps()
    {
        var draft = CSharpTestDraft.Draft(Test, null, Phrases);

        Assert.Equal(("Shelf", "refresh-reloads-the-books"), (draft.Module, draft.Name));
        Assert.Equal(["ShelfSteps"], draft.Using);
        Assert.Contains("| Journey | SHF.01 |", draft.Text, StringComparison.Ordinal);
        Assert.Contains("Given the app is ready for Shelf\nWhen I refresh the shelf\nThen the shelf shows 3 books\n", draft.Text, StringComparison.Ordinal);
        var gap = Assert.Single(draft.Gaps);
        Assert.Equal(("Then", "Assert.True(ShelfPage.Spinner.IsHidden);"), (gap.Keyword, gap.Statement));
        Assert.Equal($"# Gap (Then): {gap.Statement}", draft.Text.Split('\n')[gap.Line - 1]);
        Assert.Equal(["Backend.Reset();"], draft.Skipped);
    }

    [Fact]
    public void AMethodTheFileLacks_IsAnError_ThatNamesItsTests()
    {
        var exception = Assert.Throws<ArgumentException>(() => CSharpTestDraft.Draft(Test, "Missing", Phrases));

        Assert.Contains("Refresh_ReloadsTheBooks", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PhrasesThatDifferOnlyInAValue_AreGrouped_WithAPlaceholder()
    {
        var similar = Assert.Single(CSharpTestDraft.Similar(Phrases));

        Assert.Equal("the shelf shows {value} books", similar.Placeholder);
        Assert.Equal(["the shelf shows 3 books", "the shelf shows 4 books"], similar.Phrases);
    }
}
