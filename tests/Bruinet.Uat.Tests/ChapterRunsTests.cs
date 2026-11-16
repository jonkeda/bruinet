using Bruinet.Bdd;
using Bruinet.Uat;
using Xunit;

namespace Bruinet.Uat.Tests;

/// <summary>A chapter's steps run once for the examples that only check, and again after anything else.</summary>
public sealed class ChapterRunsTests
{
    private const string Spec = """
        # Spec: SHOP.CAT.01 The catalogue

        ## Examples

        ### Chapter: The list at a glance

        ```gherkin
        Given the application is running
        When I open the Catalogue page
        ```

        #### R1 The title

        ```gherkin
        Then the title says "Catalogue"
        ```

        #### R2 The columns

        ```gherkin
        Then the list has the columns "Name" and "Price"
        And the footer says "1-3 of 3"
        ```

        #### R3 A search narrows the list

        ```gherkin
        When I type "tea" in the search
        Then the list is empty
        ```

        #### R4 The footer

        ```gherkin
        Then the footer says "1-3 of 3"
        ```

        ### R5 Outside the chapter

        ```gherkin
        Given the application is running
        Then the title says "Catalogue"
        ```
        """;

    private static readonly IReadOnlyList<BddScenario> Examples = BddSpecReader.Parse(Spec, "shop.spec.md").Document!.Scenarios;

    private static IReadOnlyList<string> Tags(string rule) => Examples.Single(example => example.Name.StartsWith("SHOP.CAT.01." + rule, StringComparison.Ordinal)).Tags;

    [Fact]
    public void An_example_that_only_checks_shares_the_page_the_one_before_left()
    {
        var runs = new ChapterRuns();
        Assert.False(runs.Shares("shop.spec.md", Tags("R1")));

        runs.Ran("shop.spec.md", Tags("R1"), passed: true);

        Assert.True(runs.Shares("shop.spec.md", Tags("R2")));
    }

    [Fact]
    public void An_example_with_a_When_of_its_own_starts_over_and_so_does_the_one_after_it()
    {
        var runs = new ChapterRuns();
        runs.Ran("shop.spec.md", Tags("R2"), passed: true);

        Assert.False(runs.Shares("shop.spec.md", Tags("R3")));
        runs.Ran("shop.spec.md", Tags("R3"), passed: true);

        Assert.False(runs.Shares("shop.spec.md", Tags("R4")));
    }

    [Fact]
    public void A_failed_example_leaves_nothing_to_share()
    {
        var runs = new ChapterRuns();
        runs.Ran("shop.spec.md", Tags("R1"), passed: false);

        Assert.False(runs.Shares("shop.spec.md", Tags("R2")));
    }

    [Fact]
    public void Another_file_or_an_example_outside_the_chapter_does_not_share()
    {
        var runs = new ChapterRuns();
        runs.Ran("shop.spec.md", Tags("R1"), passed: true);

        Assert.False(runs.Shares("other.spec.md", Tags("R2")));
        Assert.False(runs.Shares("shop.spec.md", Tags("R5")));
        Assert.False(runs.Shares(null, Tags("R2")));
    }

    [Theory]
    [InlineData("Given,When,Then,Then", 2)]
    [InlineData("Given,When,Then", 1)]
    [InlineData("Given,When,Then,When,Then", 1)]
    [InlineData("Given,When", 0)]
    [InlineData("Then,Then", 2)]
    public void Own_checks_are_the_Then_steps_after_the_last_Given_or_When(string keywords, int expected)
    {
        var steps = keywords.Split(',').Select(Enum.Parse<BddEffectiveStepKeyword>).ToArray();

        Assert.Equal(expected, ChapterRuns.OwnChecks(steps));
    }
}
