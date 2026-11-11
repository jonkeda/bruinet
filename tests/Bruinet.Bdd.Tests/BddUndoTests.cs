using Bruinet.Core.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bruinet.Bdd.Tests;

/// <summary>Undos a step registers: they run after the scenario, last first, and a failing one fails the scenario.</summary>
public sealed class BddUndoTests
{
    [Fact]
    public async Task Undos_RunAfterTheScenario_LastFirst()
    {
        var (log, run) = await RunAsync("""
            Given an override "first"
            And an override "second"
            When I work
            """);

        Assert.True(run.Passed);
        Assert.Equal(["set first", "set second", "work", "after", "undo second", "undo first"], log);
    }

    [Fact]
    public async Task Undos_RunWhenAStepFails()
    {
        var (log, run) = await RunAsync("""
            Given an override "first"
            When I fail
            """);

        Assert.False(run.Passed);
        Assert.Equal(["set first", "fail", "after", "undo first"], log);
    }

    [Fact]
    public async Task AFailingUndo_FailsAPassedScenario_AsASetupFailure_AndTheOthersStillRun()
    {
        var (log, run) = await RunAsync("""
            Given an override "first"
            And a broken override
            When I work
            """);

        Assert.False(run.Passed);
        Assert.Contains("undo first", log);
        var last = run.Steps[^1];
        Assert.Equal(BddStepResultStatus.Failed, last.Status);
        Assert.Contains("the undo broken failed after the scenario", last.Message, StringComparison.Ordinal);
        Assert.IsType<UiSetupException>(last.Exception);
    }

    [Fact]
    public async Task AFailingUndo_AfterAFailedStep_KeepsTheStepsFailure()
    {
        var (_, run) = await RunAsync("""
            Given a broken override
            When I fail
            """);

        var failed = Assert.Single(run.Steps, step => step.Status == BddStepResultStatus.Failed);
        Assert.Contains("broken", failed.Message, StringComparison.Ordinal);
        Assert.Contains("Also, the undo broken failed", failed.Message, StringComparison.Ordinal);
    }

    private static async Task<(List<string> Log, BddScenarioRunResult Run)> RunAsync(string steps)
    {
        var markdown = $"""
            # Feature: Undo {Guid.NewGuid():N}

            ## Scenario: Undo

            ```gherkin
            {steps}
            ```
            """;
        var parse = BddMarkdownParser.Parse(markdown, Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.feature.md"));
        Assert.True(parse.Success, string.Join(Environment.NewLine, parse.Diagnostics.Select(x => x.Message)));
        var catalog = BddStepDefinitionRuntime.FromTypes(new object(), typeof(UndoSteps)).CreateCommandCatalog();
        var bind = BddBinder.Bind(parse.Document!, catalog);
        Assert.True(bind.Success, string.Join(Environment.NewLine, bind.Diagnostics.Select(x => x.Message)));

        List<string> log = [];
        using var services = new ServiceCollection().AddSingleton(log).AddTransient<UndoSteps>().BuildServiceProvider();
        var runner = new BddScenarioRunner();
        BddStepDefinitionRuntime.SetServiceProvider(runner.Context, services);
        var run = await runner.RunAsync(Assert.Single(bind.Document!.Scenarios));
        Assert.NotNull(runner.Context.ScenarioStartedUtc);
        return (log, run);
    }

    [BddStepDefinitions]
    public sealed class UndoSteps(List<string> log)
    {
        [BddGiven("an override {name}")]
        public void Override(BddExecutionContext context, string name)
        {
            log.Add($"set {name}");
            context.RegisterUndo(() => log.Add($"undo {name}"), name);
        }

        [BddGiven("a broken override")]
        public void BrokenOverride(BddExecutionContext context)
            => context.RegisterUndo(() => throw new InvalidOperationException("cannot restore"), "broken");

        [BddAfterScenario]
        public void After() => log.Add("after");

        [BddWhen("I work")]
        public void Work() => log.Add("work");

        [BddWhen("I fail")]
        public void Fail()
        {
            log.Add("fail");
            throw new InvalidOperationException("broken step");
        }
    }
}
