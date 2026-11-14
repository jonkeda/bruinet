using Frainet.Specs;

namespace Frainet.Specs.Tests;

/// <summary>
/// The spec check on the real specs, and on broken copies of TOD.02: each copy breaks one thing the
/// format asks, and the check names it.
/// </summary>
public sealed class SpecCheckTests
{
    private static readonly string Root = FindRoot();

    private static readonly string Tod02 = File.ReadAllText(Path.Combine(Root, "specs", "todo", "TOD.02.spec.md"))
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

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frainet.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Frainet.slnx not found above the test's folder.");
    }
}
