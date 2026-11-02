using System.Text.Json.Nodes;
using Bruinet.Bdd;
using Bruinet.Bdd.Runner;
using Bruinet.Core.Logging;
using Xunit;

namespace Bruinet.Uat.Tests;

// A scenario's step trace file (AI8): each step's calls, capped at the slowest 50, and the page after the failed step.
public sealed class UatScenarioTraceTests
{
    [Fact]
    public async Task TheTrace_HasEachStepsCalls_TheSlowestWhenMany_AndThePageAfterTheFailure()
    {
        var runner = new BddScenarioRunner();
        runner.Context.PageReader = _ => new BddRunnerPageState("Todo Edit", [new BddRunnerControlState("Title", "Entry", true, "Buy milk", null)], null);
        var result = await runner.RunAsync(Scenario());

        var trace = JsonNode.Parse(UatScenarioTrace.Build(result.Scenario, new BddScenarioExecutionResult(result, runner.Context, null), "failed", Path.GetTempPath()))!;

        var (scroll, ask) = (trace["steps"]![0]!, trace["steps"]![1]!);
        Assert.Equal(50, scroll["calls"]!.AsArray().Count);
        Assert.Equal(10, (int)scroll["callsLeftOut"]!);
        Assert.Equal(1059, scroll["calls"]!.AsArray().Max(call => (int)call!["ms"]!));
        Assert.Null(scroll["pageAfter"]);
        Assert.Null(scroll["message"]);
        Assert.Equal("Todo Edit", (string)ask["page"]!);
        Assert.Equal("Buy milk", (string)ask["pageAfter"]!["controls"]![0]!["value"]!);
        Assert.Equal("No dialog.", (string)ask["message"]!);
    }

    [Fact]
    public void ItsFile_IsTheReportsNameEndingInTrace()
        => Assert.Equal("delete__Asks__trace.json", UatScenarioTrace.FileName("Features/delete.feature.md", "Asks"));

    private static BddBoundScenario Scenario()
    {
        var catalog = new BddCommandCatalog();
        catalog.Register(BddEffectiveStepKeyword.When, "I scroll the list", "Scroll", handler: (_, invocation, _) =>
        {
            for (var call = 0; call < 60; call++)
            {
                Timing.Call("Todo List", "Todos", "ScrollTo", Timing.NowMs, 1000 + call, 1, "success");
            }

            return Task.FromResult(BddStepResult.Passed(invocation));
        });
        catalog.Register(BddEffectiveStepKeyword.Then, "the dialog asks", "Ask", handler: (_, invocation, _) =>
            Task.FromResult(BddStepResult.Failed(invocation, "No dialog.")));
        var parsed = BddMarkdownParser.Parse("""
            # Feature: Delete

            ## Scenario: Asks

            ```gherkin
            When I scroll the list
            Then the dialog asks
            ```
            """);
        var bind = BddBinder.Bind(parsed.Document!, catalog);
        Assert.True(bind.Success);
        return Assert.Single(bind.Document!.Scenarios);
    }
}
