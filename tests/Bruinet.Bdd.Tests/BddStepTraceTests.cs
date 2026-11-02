using Bruinet.Bdd.Runner;
using Bruinet.Core.Logging;
using Xunit;

namespace Bruinet.Bdd.Tests;

// A step's trace (AI8): the control calls it made, and the page after it when it failed.
public sealed class BddStepTraceTests
{
    [Fact]
    public async Task AFailedStep_HasItsCalls_AndThePageAfterIt_APassedOneNoPage()
    {
        var reads = 0;
        var runner = new BddScenarioRunner();
        runner.Context.PageReader = _ =>
        {
            reads++;
            // A read is no step's call: the reader's own calls stay out of the step's.
            Timing.Call("Todo Edit", "Title", "GetText", Timing.NowMs, 3, 1, "success");
            return new BddRunnerPageState("Todo Edit", [new BddRunnerControlState("Title", "Entry", true, "Buy milk", null)], null);
        };

        var result = await runner.RunAsync(Scenario());

        var (tap, ask) = (result.Steps[0], result.Steps[1]);
        Assert.Equal(("Todo Detail", "Delete", "Tap"), Single(tap.Calls));
        Assert.Null(tap.PageAfter);
        Assert.Equal(("Todo Edit", "Dialog", "Exists"), Single(ask.Calls));
        Assert.Equal("Buy milk", Assert.Single(ask.PageAfter!.Controls).Value);
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task APageThatCannotBeRead_LeavesTheFailureAsItWas_WithWhy()
    {
        var runner = new BddScenarioRunner();
        runner.Context.PageReader = _ => throw new InvalidOperationException("The app closed.");

        var ask = (await runner.RunAsync(Scenario())).Steps[1];

        Assert.Equal(BddStepResultStatus.Failed, ask.Status);
        Assert.Contains("The app closed.", ask.PageAfter!.Reason, StringComparison.Ordinal);
    }

    private static (string, string, string) Single(IReadOnlyList<StepTraceCall> calls)
    {
        var call = Assert.Single(calls);
        return (call.Page, call.Control, call.Action);
    }

    private static BddBoundScenario Scenario()
    {
        var catalog = new BddCommandCatalog();
        catalog.Register(BddEffectiveStepKeyword.When, "I tap Delete", "Tap", handler: (_, invocation, _) =>
        {
            Timing.Call("Todo Detail", "Delete", "Tap", Timing.NowMs, 95, 1, "success");
            return Task.FromResult(BddStepResult.Passed(invocation));
        });
        catalog.Register(BddEffectiveStepKeyword.Then, "the dialog asks", "Ask", handler: (_, invocation, _) =>
        {
            Timing.Call("Todo Edit", "Dialog", "Exists", Timing.NowMs, 5004, 24, "error");
            return Task.FromResult(BddStepResult.Failed(invocation, "No dialog."));
        });
        var parsed = BddMarkdownParser.Parse("""
            # Feature: Delete

            ## Scenario: Asks

            ```gherkin
            When I tap Delete
            Then the dialog asks
            ```
            """);
        var bind = BddBinder.Bind(parsed.Document!, catalog);
        Assert.True(bind.Success);
        return Assert.Single(bind.Document!.Scenarios);
    }
}
