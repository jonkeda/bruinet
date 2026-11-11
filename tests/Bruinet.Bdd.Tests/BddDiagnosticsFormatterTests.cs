using Xunit;

namespace Bruinet.Bdd.Tests;

/// <summary>What a run and a bind failure print (review 08, finding 12: no test would notice a change here).</summary>
public sealed class BddDiagnosticsFormatterTests
{
    [Fact]
    public void FormatDiagnostics_GivesEachAtItsLine_AndTheStepsItPassedThrough()
    {
        var diagnostics = new[]
        {
            new BddDiagnostic(BddDiagnosticSeverity.Error, "UATB001", "No phrase matches 'I fly'.", new BddSourceLocation("a.feature.md", 7))
            {
                Within = [new BddSourceLocation("lib.feature.md", 3)],
            },
            new BddDiagnostic(BddDiagnosticSeverity.Error, "UATB002", "Unknown page 'Moon'.", new BddSourceLocation("a.feature.md", 9)),
        };

        var text = BddDiagnosticsFormatter.FormatDiagnostics(diagnostics).Split(Environment.NewLine);

        Assert.Equal(3, text.Length);
        Assert.Contains("UATB001 No phrase matches 'I fly'.", text[0], StringComparison.Ordinal);
        Assert.StartsWith("    via ", text[1], StringComparison.Ordinal);
        Assert.Contains("lib.feature.md", text[1], StringComparison.Ordinal);
        Assert.Contains("UATB002 Unknown page 'Moon'.", text[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task FormatSteps_GivesOneLinePerStep_AndAFailuresMessageOnLinesUnderIt()
    {
        var result = await Run(fail: true);

        var lines = BddDiagnosticsFormatter.FormatSteps(result.Steps).ToList();

        Assert.Equal(4, lines.Count);
        Assert.StartsWith("Passed: ", lines[0], StringComparison.Ordinal);
        Assert.Contains("the application is running", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("Failed: ", lines[1], StringComparison.Ordinal);
        Assert.Contains("I tap Save", lines[1], StringComparison.Ordinal);
        Assert.Equal("    The button is gone.", lines[2]);
        Assert.Equal("    Looked for 'Save'.", lines[3]);
    }

    [Fact]
    public async Task FormatResults_GivesTheStepsAndTheTrace_NotThePhraseCatalog()
    {
        var result = await Run(fail: true);

        var text = BddDiagnosticsFormatter.FormatResults(result, new BddExecutionContext(), "discovery", Catalog(fail: true), evidencePath: "evidence/run");

        Assert.Contains("Runtime trace:", text, StringComparison.Ordinal);
        Assert.Contains("Evidence: evidence/run", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Command catalog:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("discovery", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatBindFailure_GivesTheDiagnostics_TheDiscovery_AndTheCatalog()
    {
        var diagnostic = new BddDiagnostic(BddDiagnosticSeverity.Error, "UATB001", "No phrase matches.", new BddSourceLocation("a.feature.md", 2));

        var text = BddDiagnosticsFormatter.FormatBindFailure([diagnostic], "Found 2 step classes.", Catalog(fail: false));

        Assert.Contains("UATB001 No phrase matches.", text, StringComparison.Ordinal);
        Assert.Contains("Found 2 step classes.", text, StringComparison.Ordinal);
        Assert.Contains("Command catalog:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatCatalog_ListsThePhrases_ByKeywordThenPhrase()
    {
        var lines = BddDiagnosticsFormatter.FormatCatalog(Catalog(fail: false)).Split(Environment.NewLine);

        Assert.Equal("Command catalog:", lines[0]);
        Assert.Equal("- Given: the application is running -> AppCommands.AssertRunning", lines[1]);
        Assert.Equal("- When: I tap {control} -> InteractionCommands.Tap", lines[2]);
    }

    private static async Task<BddScenarioRunResult> Run(bool fail)
    {
        var parsed = BddMarkdownParser.Parse("""
            # Feature: Settings

            ## Scenario: Save

            ```gherkin
            Given the application is running
            When I tap Save
            ```
            """);
        Assert.True(parsed.Success);
        var bind = BddBinder.Bind(parsed.Document!, Catalog(fail));
        Assert.True(bind.Success);
        return await new BddScenarioRunner().RunAsync(Assert.Single(bind.Document!.Scenarios));
    }

    private static BddCommandCatalog Catalog(bool fail)
    {
        var catalog = new BddCommandCatalog();
        catalog.Register(
            BddEffectiveStepKeyword.When,
            "I tap {control}",
            "InteractionCommands.Tap",
            allowsTable: false,
            handler: (_, invocation, _) => Task.FromResult(fail
                ? BddStepResult.Failed(invocation, "The button is gone.\r\nLooked for 'Save'.")
                : BddStepResult.Passed(invocation)));
        catalog.Register(
            BddEffectiveStepKeyword.Given,
            "the application is running",
            "AppCommands.AssertRunning",
            handler: (_, invocation, _) => Task.FromResult(BddStepResult.Passed(invocation)));
        return catalog;
    }
}
