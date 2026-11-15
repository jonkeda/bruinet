using Xunit;

namespace Bruinet.Bdd.Tests;

/// <summary>A runnable spec read as a feature: an example per rule, the Background and the chapters before it.</summary>
public sealed class BddSpecReaderTests
{
    private const string Flat = """
        # Spec: B7.CON.01 Find a contact

        | Field | Value |
        | --- | --- |
        | Status | accepted 2026-11-14 |

        ## Intent

        Someone looks up a contact.

        ## Rules

        | Id | Rule |
        | --- | --- |
        | B7.CON.01.R1 | The page shows "Contacts". |
        | B7.CON.01.R4 | A text in the filter above Name shows only the contacts whose name holds it. |

        ## Background

        ```gherkin
        Given Creator is signed in
        ```

        ## Examples

        ### R1 The page at a glance

        The person sees where they are.

        ```gherkin
        When I open the Contacts page
        Then I see the title "Contacts"
        ```

        ### R4 Part of a name is enough

        ```gherkin
        Given I am on the Contacts page
        When I type "arend" in the filter above Name
        Then the list shows only "Arend de Krom"
        ```

        ## Choices

        - Filters together narrow the list further.
        """;

    private const string Chapters = """
        # Spec: B7.CON.01 Find a contact

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

        #### R3 How many contacts

        ```gherkin
        Then the footer says "1-14 of 14"
        ```

        ### Chapter: Filtering

        ```gherkin
        Given Creator is signed in
        And I am on the Contacts page
        ```

        #### R4 Part of a name is enough

        ```gherkin
        When I type "arend" in the filter above Name
        Then the list shows only "Arend de Krom"
        ```

        ### R5 Outside a chapter

        ```gherkin
        Given Reader is signed in
        When I open the Contacts page
        Then the list has rows
        ```
        """;

    [Fact]
    public void Each_example_is_a_scenario_named_and_tagged_with_its_rule()
    {
        var document = Read(Flat);

        Assert.Equal("B7.CON.01 Find a contact", document.Title);
        Assert.Equal(["B7.CON.01.R1 The page at a glance", "B7.CON.01.R4 Part of a name is enough"], document.Scenarios.Select(s => s.Name));
        Assert.Contains("B7.CON.01.R4", document.Scenarios[1].Tags);
        Assert.Contains(BddSpecReader.SpecTag, document.Scenarios[1].Tags);
    }

    [Fact]
    public void The_background_goes_before_every_example_and_steps_keep_their_lines()
    {
        var document = Read(Flat);

        Assert.Equal("Creator is signed in", Assert.Single(document.Background).Text);
        var type = document.Scenarios[1].Steps.Single(step => step.Text.StartsWith("I type", StringComparison.Ordinal));
        var line = Flat.Split('\n').Select((text, index) => (text, index)).Single(entry => entry.text.Contains("I type \"arend\"", StringComparison.Ordinal)).index + 1;
        Assert.Equal(line, type.Source.LineNumber);
    }

    [Fact]
    public void A_chapters_steps_go_before_each_of_its_examples_and_the_chapter_is_no_scenario()
    {
        var document = Read(Chapters);

        Assert.Equal(["B7.CON.01.R1 The page at a glance", "B7.CON.01.R3 How many contacts", "B7.CON.01.R4 Part of a name is enough", "B7.CON.01.R5 Outside a chapter"], document.Scenarios.Select(s => s.Name));
        Assert.Equal(["Reader is signed in", "I open the Contacts page", "the list has rows"], document.Scenarios[3].Steps.Select(s => s.Text));
        Assert.Equal(["Creator is signed in", "I open the Contacts page", "I see the title \"Contacts\""], document.Scenarios[0].Steps.Select(s => s.Text));
        Assert.Equal(["Creator is signed in", "I am on the Contacts page", "I type \"arend\" in the filter above Name", "the list shows only \"Arend de Krom\""], document.Scenarios[2].Steps.Select(s => s.Text));
    }

    [Fact]
    public void A_then_only_example_is_marked_to_share_its_chapters_run_and_one_with_a_when_is_not()
    {
        var document = Read(Chapters);

        Assert.Contains(BddSpecReader.ThenOnlyTag, document.Scenarios[0].Tags);
        Assert.Contains("chapter:The-list-at-a-glance", document.Scenarios[1].Tags);
        Assert.DoesNotContain(BddSpecReader.ThenOnlyTag, document.Scenarios[2].Tags);
    }

    [Fact]
    public void A_spec_is_known_by_its_file_name_and_by_its_title()
    {
        Assert.True(BddSpecReader.IsSpecFile("Modules/Contacts/Specs/B7.CON.01.spec.md"));
        Assert.False(BddSpecReader.IsSpecFile("Features/TOD.01.feature.md"));
        Assert.Equal("B7.CON.01 Find a contact", BddMarkdownParser.Parse(Flat).Document!.Title);
    }

    private static BddDocument Read(string spec)
    {
        var result = BddMarkdownParser.Parse(spec, "B7.CON.01.spec.md");
        Assert.True(result.Document is not null, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == BddDiagnosticSeverity.Error);
        return result.Document!;
    }
}
