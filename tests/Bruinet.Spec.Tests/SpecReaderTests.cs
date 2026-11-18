using Bruinet.Spec.Format;

namespace Bruinet.Spec.Tests;

/// <summary>
/// The format's one reader, which the spec check and the spec runner share. Where the runner's own reader once read
/// more loosely, the format decides: the title is the first line, a chapter is "Chapter:" as written, a "####" example
/// is one only in a chapter, and a heading inside a fenced block is text.
/// </summary>
public sealed class SpecReaderTests
{
    private const string Spec = """
        # Spec: B7.CON.01 Find a contact

        | Field | Value |
        | --- | --- |
        | Status | accepted 2026-11-14 |

        ## Rules

        | Id | Rule |
        | --- | --- |
        | B7.CON.01.R1 | The page shows "Contacts". |
        | B7.CON.01.R3 | The footer counts the contacts. |
        | B7.CON.01.R4 | A text in the filter above Name narrows the list. |

        ## Examples

        ### Chapter: The list at a glance

        ```gherkin
        Given Creator is signed in
        When I open the Contacts page
        ```

        #### R1 The page at a glance

        ```gherkin
        Then I see the title "Contacts"
        ```

        #### R3 The footer counts

        ```markdown
        ## Not a section
        ### R9 Not an example
        ```

        ### R4 Part of a name is enough

        ```gherkin
        When I type "arend" in the filter above Name
        Then the list shows only "Arend de Krom"
        ```

        #### R5 Not an example outside a chapter

        ### An example without a rule

        ## Choices

        - Filters together narrow the list further.
        """;

    private static readonly SpecDocument Read = SpecReader.Read(Spec);

    [Fact]
    public void TheTitle_GivesTheJourneyAndItsName()
    {
        Assert.Equal("B7.CON.01", Read.JourneyId);
        Assert.Equal(" Find a contact", Read.TitleRest);
    }

    [Fact]
    public void TheTitle_IsTheFirstLineOnly() =>
        Assert.Null(SpecReader.Read("\n# Spec: B7.CON.01 Find a contact\n").JourneyId);

    [Fact]
    public void Sections_AreTheLevelTwoHeadings_NotThoseInAFence()
    {
        Assert.Equal(["Rules", "Examples", "Choices"], Read.Sections.Select(section => section.Name));
        Assert.Equal(3, Read.Header.Count(line => line.Text.StartsWith('|')));
    }

    [Fact]
    public void RuleIds_AreTheRulesTablesFirstColumn() =>
        Assert.Equal(["B7.CON.01.R1", "B7.CON.01.R3", "B7.CON.01.R4"], Read.RuleIds);

    [Fact]
    public void AChapter_HoldsItsStepsAndItsExamples()
    {
        var chapter = Assert.Single(Read.Chapters);

        Assert.Equal("The list at a glance", chapter.Name);
        Assert.Contains(chapter.Preamble, line => line.Text == "When I open the Contacts page");
        Assert.Equal([1, 3], chapter.Examples.Select(example => example.RuleNumber));
        Assert.All(chapter.Examples, example => Assert.Equal("The list at a glance", example.ChapterName));
    }

    [Fact]
    public void Examples_AreInTheirOrder_WithTheirRuleAndName()
    {
        Assert.Equal(
            [(1, "The page at a glance"), (3, "The footer counts"), (4, "Part of a name is enough"), (null, "An example without a rule")],
            Read.Examples.Select(example => (example.RuleNumber, example.Name)));
        Assert.Equal(Spec.Split('\n').ToList().FindIndex(line => line.StartsWith("### R4", StringComparison.Ordinal)) + 1, Read.Examples[2].Line);
    }

    [Fact]
    public void ALevelFourHeading_OutsideAChapter_IsPartOfTheExampleAboveIt() =>
        Assert.Contains(Read.Examples[2].Body, line => line.Text.StartsWith("#### R5", StringComparison.Ordinal));

    [Fact]
    public void AChapterHeading_IsCaseSensitive() =>
        Assert.Empty(SpecReader.Read("# Spec: A.1 X\n\n## Examples\n\n### chapter: lower\n").Chapters);

    [Theory]
    [InlineData("Modules/Contacts/Specs/B7.CON.01.spec.md", true)]
    [InlineData("B7.CON.01.SPEC.MD", true)]
    [InlineData("Features/TOD.01.feature.md", false)]
    [InlineData(null, false)]
    public void IsSpecFile_GoesByTheExtension(string? path, bool spec) =>
        Assert.Equal(spec, SpecReader.IsSpecFile(path));
}
