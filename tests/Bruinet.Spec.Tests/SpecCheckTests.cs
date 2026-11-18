using Lirenet.Specs;

namespace Lirenet.Specs.Tests;

/// <summary>
/// The spec check on the real specs, and on broken copies of TOD.02: each copy breaks one thing the
/// format asks, and the check names it. The specs are the Todo sample's, kept with the sample they describe
/// (<c>samples/Todo/specs</c>).
/// </summary>
public sealed class SpecCheckTests
{
    private static readonly string Root = FindRoot();

    private static readonly string Tod02 = File.ReadAllText(Path.Combine(Root, "specs", "TOD.02.spec.md"))
        .ReplaceLineEndings("\n");

    public static TheoryData<string> Specs() =>
        new(Directory.EnumerateFiles(Path.Combine(Root, "specs"), "*.spec.md", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Root, path)));

    [Theory]
    [MemberData(nameof(Specs))]
    public void EverySpec_FollowsTheFormat(string spec)
        => Assert.Empty(SpecCheck.CheckFile(Path.Combine(Root, spec)));

    [Theory]
    [InlineData("# Spec: TOD.02 Create a todo", "# Create a todo", "title")]
    [InlineData("| Where | The app on Windows and Android, and the same app on the web |\n", "", "header-field")]
    [InlineData("| Journey | TOD.02 Create a todo |", "| Journey | TOD.03 Create a todo |", "header-journey")]
    [InlineData("| Status | accepted 2026-10-26 |", "| Status | done |", "header-status")]
    [InlineData("## Choices", "## Notes", "section-unknown")]
    [InlineData("## Intent", "## Background", "section-missing")]
    [InlineData("| TOD.02.R4 |", "| R4 |", "rule-id")]
    [InlineData("| TOD.02.R4 |", "| TOD.02.R3 |", "rule-id-twice")]
    [InlineData("shows no error. |", "shows no error. It is ready. |", "rule-sentence")]
    [InlineData("### R5 An error shows only after Save", "### R12 An error shows only after Save", "example-unknown")]
    [InlineData("### R5 An error shows only after Save", "### An error shows only after Save", "example-heading")]
    [InlineData("The form does not complain while the person is still typing. Once an error is shown, the next\nkey in the title takes it away.\n", "", "example-why")]
    [InlineData("When I type \"B\" in the title", "Next I type \"B\" in the title", "gherkin-step")]
    [InlineData("Then no error is shown\n```\n\n### R6", "Then no error is shown\nGiven a new todo's form\n```\n\n### R6", "gherkin-order")]
    [InlineData("When I type \"B\" in the title\n", "", "gherkin-incomplete")]
    [InlineData("A todo's notes have no length limit.", "A todo's notes have no length limit, etc.", "vague-word")]
    [InlineData("Then I see \"Title is required\" under the title", "Then I should see \"Title is required\" under the title", "vague-word")]
    [InlineData("Each rule holds however a todo is created.", "Each rule holds wherever `TodoService` creates a todo.", "code-word")]
    [InlineData("Each rule holds however a todo is created.", "Each rule holds however TodoService creates a todo.", "code-word")]
    public void ABrokenCopy_IsFound(string original, string broken, string code)
    {
        Assert.Contains(original, Tod02);

        var findings = SpecCheck.Check(Tod02.Replace(original, broken));

        Assert.Contains(findings, finding => finding.Code == code);
    }

    [Fact]
    public void ARuleWithoutAnExample_IsFound()
    {
        var start = Tod02.IndexOf("### R5 ", StringComparison.Ordinal);
        var end = Tod02.IndexOf("### R6 ", StringComparison.Ordinal);

        var findings = SpecCheck.Check(Tod02.Remove(start, end - start));

        var finding = Assert.Single(findings);
        Assert.Equal("example-missing", finding.Code);
        Assert.Contains("R5", finding.Message);
    }

    [Fact]
    public void AnOpenQuestion_HasAProposal_AndKeepsTheSpecADraft()
    {
        var withQuestion = Tod02 + "\n## Open questions\n\n- May a title hold a line break?\n";

        var codes = SpecCheck.Check(withQuestion).Select(finding => finding.Code).ToList();

        Assert.Contains("question-proposal", codes);
        Assert.Contains("accepted-open", codes);
    }

    [Fact]
    public void AJourneyId_MayCarryAProduct()
    {
        var withProduct = Tod02.Replace("TOD.02", "B7.TOD.02");

        Assert.Empty(SpecCheck.Check(withProduct));
    }

    [Fact]
    public void ANumberedValue_InARule_IsNotASentenceBreak()
    {
        var numbered = Tod02.Replace("shows no error. |", "shows no error, as \"00. Intern\" does. |");

        Assert.Empty(SpecCheck.Check(numbered));
    }

    [Fact]
    public void AnAbbreviation_InBracketsOrQuotes_IsNotASentenceBreak()
    {
        var abbreviated = Tod02.Replace("shows no error. |", "shows no error in Amount (excl. VAT) or \"Note. Here\". |");

        Assert.Empty(SpecCheck.Check(abbreviated));
    }

    [Fact]
    public void AnOpenQuestionWithAProposal_InADraft_IsFine()
    {
        var draft = Tod02.Replace("| Status | accepted 2026-10-26 |", "| Status | draft |")
            + "\n## Open questions\n\n- May a title hold a line break? Proposal: no, a title is one line of the list.\n";

        Assert.Empty(SpecCheck.Check(draft));
    }

    [Fact]
    public void AFinding_SaysItsLine()
    {
        var findings = SpecCheck.Check(Tod02.Replace("| Status | accepted 2026-10-26 |", "| Status | done |"));

        Assert.Equal(8, Assert.Single(findings).Line);
    }

    [Fact]
    public void A_step_may_have_a_table_under_it()
    {
        var withTable = Tod02.Replace("When I type \"B\" in the title", "When I type \"B\" in the title\n  | Field | Value |\n  | ----- | ----- |\n  | Title | B     |");

        Assert.NotEqual(Tod02, withTable);
        Assert.Empty(SpecCheck.Check(withTable));
    }

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var sample = Path.Combine(dir.FullName, "samples", "Todo");
            if (Directory.Exists(Path.Combine(sample, "specs")))
            {
                return sample;
            }
        }

        throw new InvalidOperationException("samples/Todo/specs not found above the test's folder.");
    }

    private const string Chapters = """
        # Spec: ABC.01 Look at the list

        | Field | Value |
        | --- | --- |
        | Product | ABC |
        | Journey | ABC.01 Look at the list |
        | Where | The web app |
        | Status | draft |

        ## Intent

        Someone checks the list.

        ## Rules

        | Id | Rule |
        | --- | --- |
        | ABC.01.R1 | The page shows "List". |
        | ABC.01.R2 | A text in the filter narrows the list. |

        ## Background

        ```gherkin
        Given Creator is signed in
        ```

        ## Examples

        ### Chapter: At a glance

        ```gherkin
        When I open the List page
        ```

        #### R1 The title

        The person sees where they are.

        ```gherkin
        Then the title says "List"
        ```

        ### R2 Filtering

        People remember part of a name.

        ```gherkin
        When I type "a" in the filter above Name
        Then the list shows only "Ann"
        ```
        """;

    private static string Lf => Chapters.ReplaceLineEndings("\n");

    [Fact]
    public void A_background_and_a_chapter_go_before_their_examples_so_a_then_only_example_is_whole()
        => Assert.Empty(SpecCheck.Check(Lf));

    [Fact]
    public void A_chapter_with_a_then_is_found()
    {
        var findings = SpecCheck.Check(Lf.Replace("When I open the List page", "When I open the List page\nThen the title says 'List'"));

        Assert.Contains(findings, finding => finding.Code == "chapter-steps");
    }

    [Fact]
    public void A_then_only_example_outside_a_chapter_is_incomplete()
    {
        var start = Lf.IndexOf("### Chapter:", StringComparison.Ordinal);
        var end = Lf.IndexOf("#### R1", StringComparison.Ordinal);
        var findings = SpecCheck.Check(Lf.Remove(start, end - start).Replace("#### R1", "### R1"));

        Assert.Contains(findings, finding => finding.Code == "gherkin-incomplete");
    }
}
