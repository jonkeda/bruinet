using Bruinet.Bdd;
using Bruinet.Cli;
using System.Text.Json;
using Bruinet.Core.Composition;
using Bruinet.Core.Testing;
using Bruinet.Uat;
using Xunit;

namespace Bruinet.DataValidator.Tests;

/// <summary>The scenario side of <c>bruinet data</c>: the gate's checks, from a fixture type (AD-016).</summary>
public sealed class ScenarioValidationTests : IDisposable
{
    private static readonly string Assembly = typeof(ScenarioValidationTests).Assembly.Location;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Bruinet", "ScenarioValidationTests", Guid.NewGuid().ToString("N"));

    public ScenarioValidationTests()
    {
        Directory.CreateDirectory(_folder);
        Write("uat.config.md", $"""
            # UAT Config

            ## Runtime

            | Field | Value |
            | --- | --- |
            | Fixture | {nameof(LibraryFixture)} |

            ## Apps

            | Name | User |
            | --- | --- |
            | Eva's PC | Eva |

            ## Assemblies

            | Kind | Assembly |
            | --- | --- |
            | Pages | {Assembly} |
            """);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Validate_CleanScenarios_ExitZero()
    {
        Scenario("""
            When I lend "Dune" to Eva until 12-May-2026
            And I shelve the books
              | Title | Pages |
              | ----- | ----- |
              | Dune  | 1,250 |
            """);

        var (exit, output, _) = Run("validate");

        Assert.Equal(0, exit);
        Assert.Contains("No problems in 1 scenario file(s).", output);
    }

    [Fact]
    public void Validate_ReportsEveryArgumentProblem_AtItsLine()
    {
        Scenario("""
            When I lend "Dune" to Eva until 2026-05-12
            And I lend "Dune" to Carol until 12-May-2026
            And I shelve the books
              | Title | Pages |
              | ----- | ----- |
              | Dune  | 1,5   |
            And I shelve the books
            And I forget the day
            """);

        var (exit, output, _) = Run("validate");

        Assert.Equal(1, exit);
        Assert.Contains("Features/library.feature.md:5: UATA002", output);
        Assert.Contains("Write 12-May-2026.", output);
        Assert.Contains("Features/library.feature.md:6: UATA005", output);
        Assert.Contains("Features/library.feature.md:10: UATA003", output);
        Assert.Contains("Features/library.feature.md:11: UATA007", output);
        Assert.Contains("Features/library.feature.md:12: UATA010", output);
        Assert.Contains("5 error(s).", output);
    }

    [Fact]
    public void Validate_OneFile_AsJson()
    {
        var file = Scenario("""
            When I lend "Dune" to Carol until 12-May-2026
            """);

        var (exit, output, _) = Run("validate", file, "--format", "json");

        Assert.Equal(1, exit);
        var problem = Assert.Single(JsonDocument.Parse(output).RootElement.EnumerateArray());
        Assert.Equal("UATA005", problem.GetProperty("code").GetString());
    }

    [Fact]
    public void ValidateFix_RewritesWhatHasOneRightAnswer()
    {
        var file = Scenario("""
            When I lend "Dune" to Eva until 2026-05-12
            And I lend "Dune" to Eva until 3-jan-2027
            """);

        var (exit, output, _) = Run("validate", "--fix");

        Assert.Equal(0, exit);
        Assert.Contains("Fixed 1 value(s) in Features/library.feature.md.", output);
        var text = File.ReadAllText(file);
        Assert.Contains("until 12-May-2026", text);
        Assert.Contains("until 3-jan-2027", text);
    }

    [Fact]
    public void Validate_ReportsAFileInTheOldForm_AndFixRenamesItAndRewritesItsHeadings()
    {
        Write(Path.Combine("Features", "library.uat.md"), """
            # UAT: Library

            ## Scenario: Lending

            ```gherkin
            When a loan of "Dune" to Eva on 12-May-2026
            ```
            """);
        Write(Path.Combine("Features", "Library.phrases.md"), """
            # Phrases: Library

            ## Phrase: a loan of {title} to {reader} on {due}

            ```gherkin
            When I lend <title> to <reader> until <due>
            ```
            """);

        var (exit, output, _) = Run("validate");

        Assert.Equal(1, exit);
        Assert.Contains("UAT029 'library.uat.md' is the old name of 'library.feature.md'", output);
        Assert.Contains("UAT029 'Library.phrases.md' is the old name of 'Library.steps.md'", output);

        Directory.Move(Path.Combine(_folder, "Features"), Path.Combine(_folder, "Scenarios"));
        (exit, output, _) = Run("validate");

        Assert.Equal(1, exit);
        Assert.Contains("UAT029 The folder 'Scenarios' is the old name of 'Features'", output);

        (exit, output, _) = Run("validate", "--fix");

        Assert.True(exit == 0, output);
        Assert.Contains("Renamed Features/library.uat.md to library.feature.md.", output);
        Assert.StartsWith("# Feature: Library", File.ReadAllText(Path.Combine(_folder, "Features", "library.feature.md")));
        var steps = File.ReadAllText(Path.Combine(_folder, "Features", "Library.steps.md"));
        Assert.StartsWith("# Steps: Library", steps);
        Assert.Contains("## Step: a loan of {title}", steps);
    }

    [Fact]
    public void Validate_BareSteps_HaveNoSteps_AndFixPutsThemInAGherkinBlock()
    {
        var file = Write(Path.Combine("Features", "library.feature.md"), """
            # Feature: Library

            ## Scenario: Lending

            When I lend "Dune" to Eva until 12-May-2026
            """);

        var (exit, output, _) = Run("validate");

        Assert.Equal(1, exit);
        Assert.Contains("UAT007 Scenario section must contain at least one step. " + BddFileFormat.FenceHint, output);

        (exit, output, _) = Run("validate", "--fix");

        Assert.True(exit == 0, output);
        Assert.Contains("Put the steps of 1 section(s) in a gherkin block in Features/library.feature.md.", output);
        Assert.Contains("```gherkin\nWhen I lend \"Dune\" to Eva until 12-May-2026\n```", File.ReadAllText(file).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Phrases_ListsTypesAndBrokenPhrases()
    {
        var (exit, output, _) = Run("phrases", "--keyword", "When");

        Assert.Equal(0, exit);
        Assert.Contains("When I lend {title: text} to {reader: user} until {due: date}", output);
        Assert.Contains("When I shelve the books  + table 'books': list of ShelvedBook", output);
        Assert.Contains("When I forget the day", output);
        Assert.Contains("warning UATA010", output);
        Assert.DoesNotContain("Then ", output);
    }

    [Fact]
    public void Validate_ChecksCallsToCallablePhrases_AndWarnsAboutBrokenOnes()
    {
        PhraseFile();
        var file = Scenario("""
            Given a loan of "Dune" to Eva on 2026-05-12
            """);

        var (exit, output, errors) = Run("validate");

        Assert.Equal(1, exit);
        Assert.Contains("Features/library.feature.md:5: UATA002", output);
        Assert.Contains("Features/Library.steps.md:9: UATC001", errors);
        Assert.Contains("does not copy to its output", errors);

        (exit, output, _) = Run("validate", "--fix");

        Assert.Equal(0, exit);
        Assert.Contains("Fixed 1 value(s) in Features/library.feature.md.", output);
        Assert.Contains("Given a loan of \"Dune\" to Eva on 12-May-2026", File.ReadAllText(file));
    }

    [Fact]
    public void Phrases_ListsCallablePhrases_WithTheirTypes()
    {
        PhraseFile();

        var (exit, output, _) = Run("phrases");

        Assert.Equal(0, exit);
        Assert.Contains("Callable a loan of {title: text} to {reader: user} on {due: date}  <- ", output);
        Assert.Contains("Library.steps.md:3", output);
        Assert.Contains("warning UATC001", output);
    }

    [Fact]
    public void Validate_ChecksAFoldersPhraseFile_AndACallToItFromAnotherFolder()
    {
        Write(Path.Combine("Features", "Lending", "Lending.steps.md"), """
            # Steps: Lending

            ## Step: a loan of {title} to {reader} on {due}

            ```gherkin
            When I lend <title> to <reader> until <due>
            ```

            ## Step: a broken one {spare}

            ```gherkin
            When I shelve the books
            ```
            """);
        Write(Path.Combine("Features", "Shelving", "shelving.feature.md"), """
            # Feature: Shelving

            ## Scenario: Borrowed first

            ```gherkin
            Given a loan of "Dune" to Eva on 12-May-2026
            ```
            """);

        var (exit, output, errors) = Run("validate");
        var (_, phrases, _) = Run("phrases");

        Assert.Equal(1, exit);
        Assert.Contains("Features/Lending/Lending.steps.md:9: UATC001", errors);
        Assert.Contains("Features/Shelving/shelving.feature.md:6: UATB010", output);
        Assert.Contains("of the folder 'Lending'", output);
        Assert.Contains("Callable a loan of {title: text} to {reader: user} on {due: date}  <- ", phrases);
        Assert.Contains("  in Lending", phrases);
    }

    [Fact]
    public void PhrasesForAFile_ListWhatItSeesByNamespace_AndReferencesFindTheFilesThatUseOne()
    {
        Write(Path.Combine("Features", "Lending", "Lending.steps.md"), """
            # Steps: Lending

            ## Step: a quick loan

            ```gherkin
            When I lend "Dune" to Eva until 12-May-2026
            ```
            """);
        Write(Path.Combine("Features", "Shelving", "shelving.feature.md"), """
            # Feature: Shelving

            ## Metadata

            | Field | Value |
            | --- | --- |
            | Using | Lending |

            ## Scenario: Borrowed first

            ```gherkin
            Given a quick loan
            ```
            """);

        var (validated, _, _) = Run("validate");
        var (_, phrases, _) = Run("phrases", "Features/Shelving/shelving.feature.md");
        var (_, references, _) = Run("references", "Lending");

        Assert.Equal(0, validated);
        Assert.Contains("## Lending (Using)", phrases);
        Assert.Contains("Callable a quick loan", phrases);
        Assert.Contains("## Every scenario", phrases);
        Assert.Contains("Features/Shelving/shelving.feature.md: Using", references);
        Assert.Contains("1 use(s) of Lending.", references);
    }

    [Fact]
    public void Validate_ReportsAConfigUsingThatNamesNothing_Once()
    {
        File.AppendAllText(Path.Combine(_folder, "uat.config.md"), "\n## Using\n\n| Namespace |\n| --- |\n| Lendng |\n");
        Scenario("""
            When I lend "Dune" to Eva until 12-May-2026
            """);

        var (exit, output, _) = Run("validate");

        Assert.Equal(1, exit);
        Assert.Contains("uat.config.md:1: UATB011", output);
        Assert.Contains("1 error(s).", output);
    }

    [Fact]
    public void Snippet_WritesAMethodForAStepThatBindsToNothing()
    {
        Scenario("""
            When I archive "Dune" on 12-May-2026
            """);

        var (exit, output, _) = Run("snippet", "Features/library.feature.md:5");

        Assert.Equal(0, exit);
        Assert.Contains("[BddWhen]", output);
        Assert.Contains("public void When_I_archive_P1_on_P2(string p1, DateOnly p2)", output);
    }

    private string Scenario(string steps) => Write(Path.Combine("Features", "library.feature.md"), $"""
        # Feature: Library

        ## Scenario: Lending
        ```gherkin
        {steps}
        ```
        """);

    private void PhraseFile() => Write(Path.Combine("Features", "Library.steps.md"), """
        # Steps: Library

        ## Step: a loan of {title} to {reader} on {due}

        ```gherkin
        When I lend <title> to <reader> until <due>
        ```

        ## Step: a broken one {spare}

        ```gherkin
        When I shelve the books
        ```
        """);

    private (int Exit, string Output, string Errors) Run(params string[] args)
    {
        var output = new StringWriter();
        var errors = new StringWriter();
        var exit = BruinetCommand.RunAsync(["data", .. args], new CliIo(TextReader.Null, output, errors, _folder, Interactive: false)).GetAwaiter().GetResult();
        return (exit, output.ToString(), errors.ToString());
    }

    private string Write(string relativePath, string content)
    {
        var path = Path.Combine(_folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}

/// <summary>Loaded by the tool from this test assembly, by type only: it is never created.</summary>
[TestModuleScan(typeof(LibraryFixture), NamespacePrefix = "Bruinet.DataValidator.Tests.Library")]
public sealed class LibraryFixture
{
    public TestComposition Composition => throw new NotSupportedException("The tool reads the fixture's type only.");
}

public sealed record ShelvedBook(string Title, int Pages);
