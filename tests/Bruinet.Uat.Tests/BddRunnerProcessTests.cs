using Bruinet.Bdd;
using System.Diagnostics;
using Bruinet.Bdd.Runner;
using Xunit;

namespace Bruinet.Uat.Tests;

/// <summary>
/// The runner process: this test project's own output is the suite, started as a host starts it,
/// and driven over its pipe.
/// </summary>
public sealed class BddRunnerProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BruinetRunnerTests", Guid.NewGuid().ToString("N"));
    private readonly string _config;
    private readonly string _feature;
    private readonly string _pages = typeof(RunnerProcessFixture).Assembly.Location;

    public BddRunnerProcessTests()
    {
        Directory.CreateDirectory(_root);
        _config = Path.Combine(_root, "uat.config.md");
        _feature = Path.Combine(_root, "runner.feature.md");
        File.WriteAllText(_config, $"""
            # UAT Config

            ## Runtime

            | Field | Value |
            | --- | --- |
            | Target | MAUI |
            | Fixture | {nameof(RunnerProcessFixture)} |

            ## Assemblies

            | Kind | Assembly |
            | --- | --- |
            | Pages | {Path.GetFileName(_pages)} |
            """);
        File.WriteAllText(_feature, """
            # Feature: The runner

            ## Scenario: Counts to two

            ```gherkin
            Given the runner counts
            When the runner counts
            Then the runner has counted 2 times
            ```

            ## Scenario: Counts wrong

            ```gherkin
            Given the runner counts
            Then the runner has counted 5 times
            Then the runner has counted 1 times
            ```

            ## Scenario: Waits

            ```gherkin
            Given the runner waits to be stopped
            ```

            ## Scenario: Knows where it runs

            ```gherkin
            Then the runner's base directory is the suite's output
            Then the runner sees RUNNER_TEST_VALUE as from-the-host
            ```

            ## Scenario: Calls a step

            ```gherkin
            Given the runner counts twice
            Then the runner has counted 2 times
            ```

            ## Step: the runner counts twice

            ```gherkin
            Given the runner counts
            When the runner counts
            ```

            ## Scenario: Made once

            ```gherkin
            Then the runner made its fixture 1 times
            ```

            ## Scenario: Made twice

            ```gherkin
            Then the runner made its fixture 2 times
            ```
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task AKeptFixture_RunsTheNextScenario_WithNoNewFixture_AndAPlainClose_EndsIt()
    {
        await using var runner = await StartAsync();
        await RunAsync(runner, "Counts to two", keep: true);

        // Kept: the same fixture, its count started clean by BeforeScenario; then closed for good.
        await RunAsync(runner, "Counts to two", keep: true);
        await RunAsync(runner, "Made once", keep: false);
        await RunAsync(runner, "Made twice", keep: false);
    }

    [Fact]
    public async Task APreparedFixture_IsTheOneTheFirstOpenUses()
    {
        await using var runner = await StartAsync();

        Assert.IsType<BddRunnerDone>(await runner.RequestAsync(new BddRunnerPrepare(_config, _pages)));

        await RunAsync(runner, "Made once", keep: true);
    }

    private async Task RunAsync(BddRunnerClient runner, string scenario, bool keep)
    {
        Assert.IsType<BddRunnerOpened>(await runner.RequestAsync(Open(scenario)));
        BddRunnerStep step;
        do
        {
            step = Assert.IsType<BddRunnerStep>(await runner.RequestAsync(new BddRunnerNext()));
            Assert.True(step.Status == BddStepResultStatus.Passed, $"{scenario}: {step.Message}");
        }
        while (step.HasNext);

        Assert.IsType<BddRunnerDone>(await runner.RequestAsync(new BddRunnerClose(keep)));
    }

    [Fact]
    public async Task AScenario_RunsStepByStep_AndEveryStepAnswers()
    {
        await using var runner = await StartAsync();

        var opened = Assert.IsType<BddRunnerOpened>(await runner.RequestAsync(Open("Counts to two")));
        Assert.Equal(3, opened.Steps);
        Assert.Contains("Command catalog:", opened.Catalog);

        for (var index = 0; index < 3; index++)
        {
            var step = Assert.IsType<BddRunnerStep>(await runner.RequestAsync(new BddRunnerNext()));
            Assert.Equal((index, BddStepResultStatus.Passed, index < 2), (step.Index, step.Status, step.HasNext));
            Assert.Equal(index + 1, step.Report.Count(line => line.Contains("Passed", StringComparison.OrdinalIgnoreCase)));
        }

        Assert.IsType<BddRunnerDone>(await runner.RequestAsync(new BddRunnerClose()));
    }

    [Fact]
    public async Task AFailingStep_AnswersWithItsMessage_AndTheRunGoesOn()
    {
        await using var runner = await StartAsync();
        await runner.RequestAsync(Open("Counts wrong"));
        await runner.RequestAsync(new BddRunnerNext());

        var failed = Assert.IsType<BddRunnerStep>(await runner.RequestAsync(new BddRunnerNext()));
        var skipped = Assert.IsType<BddRunnerStep>(await runner.RequestAsync(new BddRunnerSkip("Not now.")));

        Assert.Equal(BddStepResultStatus.Failed, failed.Status);
        Assert.Contains("counted 1", failed.Message);
        Assert.Equal((2, BddStepResultStatus.Skipped, false), (skipped.Index, skipped.Status, skipped.HasNext));
    }

    [Fact]
    public async Task TheRunner_RunsInTheSuitesOutput_WithTheHostsEnvironment()
    {
        await using var runner = await StartAsync(new Dictionary<string, string> { ["RUNNER_TEST_VALUE"] = "from-the-host" });
        await runner.RequestAsync(Open("Knows where it runs"));

        var baseDirectory = Assert.IsType<BddRunnerStep>(await runner.RequestAsync(new BddRunnerNext()));
        var environment = Assert.IsType<BddRunnerStep>(await runner.RequestAsync(new BddRunnerNext()));

        Assert.True(baseDirectory.Status == BddStepResultStatus.Passed, baseDirectory.Message);
        Assert.True(environment.Status == BddStepResultStatus.Passed, environment.Message);
        Assert.Null(Environment.GetEnvironmentVariable("RUNNER_TEST_VALUE"));
    }

    [Fact]
    public async Task OneRunner_OpensScenarioAfterScenario_EachWithANewFixture()
    {
        await using var runner = await StartAsync();

        foreach (var _ in Enumerable.Range(0, 2))
        {
            await runner.RequestAsync(Open("Counts to two"));
            BddRunnerStep? last = null;
            for (var index = 0; index < 3; index++)
            {
                last = Assert.IsType<BddRunnerStep>(await runner.RequestAsync(new BddRunnerNext()));
            }

            Assert.True(last!.Status == BddStepResultStatus.Passed, last.Message);
        }
    }

    [Fact]
    public async Task WhatCannotBeOpened_SaysWhy()
    {
        await using var runner = await StartAsync();

        var failed = Assert.IsType<BddRunnerFailed>(await runner.RequestAsync(Open("No such scenario")));
        var next = Assert.IsType<BddRunnerFailed>(await runner.RequestAsync(new BddRunnerNext()));

        Assert.Contains("'No such scenario' was not found", failed.Message);
        Assert.Equal("No scenario is open.", next.Message);
    }

    [Fact]
    public async Task ACanceledStep_AnswersCanceled()
    {
        await using var runner = await StartAsync();
        await runner.RequestAsync(Open("Counts to two"));
        var longIntro = new BddRunnerClips(new BddRunnerClip("A long story.", null, 600), new Dictionary<int, BddRunnerClip>(), null);
        await runner.RequestAsync(new BddRunnerRehearse(longIntro, 0, SpeakAloud: false));
        using var cancel = new CancellationTokenSource();

        var running = runner.RequestAsync(new BddRunnerNext(), cancel.Token);
        cancel.Cancel();
        var step = Assert.IsType<BddRunnerStep>(await running);

        Assert.Equal(BddStepResultStatus.Canceled, step.Status);
    }

    [Fact]
    public async Task ARehearsal_PacesTheSteps_AndAnnouncesEachLine()
    {
        await using var runner = await StartAsync();
        List<string> said = [];
        runner.Said = say => said.Add(say.Wav);
        await runner.RequestAsync(Open("Counts to two"));
        var clips = new BddRunnerClips(new BddRunnerClip("Hello.", "intro.wav", 0.3), new Dictionary<int, BddRunnerClip>(), new BddRunnerClip("Bye.", "outro.wav", 0.01));

        Assert.IsType<BddRunnerDone>(await runner.RequestAsync(new BddRunnerRehearse(clips, 0, SpeakAloud: true)));
        var watch = Stopwatch.StartNew();
        await runner.RequestAsync(new BddRunnerNext());
        var first = watch.Elapsed;
        await runner.RequestAsync(new BddRunnerNext());
        await runner.RequestAsync(new BddRunnerNext());
        var finished = Assert.IsType<BddRunnerRecorded>(await runner.RequestAsync(new BddRunnerFinishRecording()));

        Assert.True(first >= TimeSpan.FromSeconds(0.3), $"The first step waited {first} for its intro.");
        Assert.Equal(["intro.wav", "outro.wav"], said);
        Assert.Null(finished.Timeline);
    }

    [Fact]
    public async Task ARecording_WithNothingToRecord_StillWritesItsTimeline()
    {
        await using var runner = await StartAsync();
        await runner.RequestAsync(Open("Counts to two"));
        var folder = Path.Combine(_root, "scene-01");
        var none = new BddRunnerClips(null, new Dictionary<int, BddRunnerClip>(), null);

        Assert.IsType<BddRunnerDone>(await runner.RequestAsync(new BddRunnerRecord(1, "Counting", "runner.feature.md#Counts-to-two", folder, "hash-1", none, 0, false)));
        for (var index = 0; index < 3; index++)
        {
            await runner.RequestAsync(new BddRunnerNext());
        }

        var recorded = Assert.IsType<BddRunnerRecorded>(await runner.RequestAsync(new BddRunnerFinishRecording()));

        Assert.Equal(Path.Combine(folder, "timeline.json"), recorded.Timeline);
        Assert.Contains("\"hash-1\"", File.ReadAllText(recorded.Timeline!));
        Assert.Contains(recorded.Warnings, warning => warning.Contains("IRecordable"));
    }

    [Fact]
    public async Task ARunnerThatDies_EndsTheRequest_WithWhatItWrote()
    {
        await using var runner = await StartAsync();
        await runner.RequestAsync(Open("Waits"));

        var running = runner.RequestAsync(new BddRunnerNext());
        runner.Process.Kill(entireProcessTree: true);
        var exception = await Assert.ThrowsAsync<BddRunnerException>(() => running);

        Assert.Contains("ended in the middle of a run", exception.Message);
    }

    [Fact]
    public void ASuiteOutputWithoutTheRunner_SaysWhichFileIsMissing()
    {
        var pages = Path.Combine(_root, "Suite.dll");
        File.WriteAllText(pages, string.Empty);
        File.WriteAllText(Path.ChangeExtension(pages, ".runtimeconfig.json"), "{}");

        var exception = Assert.Throws<FileNotFoundException>(() => new BddRunnerStart(pages).ToStartInfo("pipe"));

        Assert.Contains("Bruinet.Uat.dll", exception.Message);
        Assert.Contains("reference Bruinet.Uat", exception.Message);
    }

    [Fact]
    public async Task AScenarioRunToItsEnd_WritesItsResult_AsTheGateDoes_MarkedByItsMode()
    {
        var results = Path.Combine(_root, "TestResults");
        await using var runner = await StartAsync(new Dictionary<string, string> { [Bruinet.Core.Artifacts.TestArtifactOptions.RootDirectoryEnvironmentVariable] = results });
        await runner.RequestAsync(Open("Counts to two") with { Mode = "step" });
        for (var index = 0; index < 3; index++)
        {
            await runner.RequestAsync(new BddRunnerNext());
        }

        await runner.RequestAsync(Open("Counts wrong"));
        for (var index = 0; index < 3; index++)
        {
            await runner.RequestAsync(new BddRunnerNext());
        }

        var records = Directory.EnumerateDirectories(results)
            .Select(Bruinet.Core.Artifacts.TestArtifactManifestWriter.Read)
            .OfType<Bruinet.Core.Artifacts.TestArtifactManifest>()
            .SelectMany(manifest => manifest.Suites.SelectMany(suite => suite.Artifacts))
            .Where(record => record.Kind == "uat-scenario")
            .ToDictionary(record => record.Name);

        Assert.Equal(("passed", "step", "runner.feature.md", "3"), (records["Counts to two"].Status, records["Counts to two"].Metadata["mode"], records["Counts to two"].Metadata["file"], records["Counts to two"].Metadata["line"]));
        Assert.Equal(("failed", UatScenarioResults.RunnerMode), (records["Counts wrong"].Status, records["Counts wrong"].Metadata["mode"]));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(Directory.EnumerateFiles(results, "manifest.json", SearchOption.AllDirectories).First())!, records["Counts to two"].Path)));
    }

    [Fact]
    public async Task EachStep_SaysWhereItIs_AndWhereTheNextIs_WithTheCallsAroundIt()
    {
        await using var runner = await StartAsync();

        var opened = Assert.IsType<BddRunnerOpened>(await runner.RequestAsync(Open("Calls a step")));
        var first = Assert.IsType<BddRunnerStep>(await runner.RequestAsync(new BddRunnerNext()));
        var second = Assert.IsType<BddRunnerStep>(await runner.RequestAsync(new BddRunnerNext()));
        var last = Assert.IsType<BddRunnerStep>(await runner.RequestAsync(new BddRunnerNext()));

        // The call's body (lines 42 and 43 of the file), then the scenario's own step after the call (36).
        Assert.Equal((42, 1, "the runner counts"), (opened.Next!.Line, opened.Next.Depth, opened.Next.Text));
        var call = Assert.Single(opened.Next.Calls);
        Assert.Equal((35, 1), (call.Line, call.Depth));
        Assert.Equal(Path.GetFullPath(_feature), Path.GetFullPath(call.File!), ignoreCase: true);
        Assert.Equal((42, 43), (first.Ran!.Line, first.Next!.Line));
        Assert.Equal((43, 1, 36, 0), (second.Ran!.Line, second.Ran.Depth, second.Next!.Line, second.Next.Depth));
        Assert.Empty(second.Next.Calls);
        Assert.Equal((36, false), (last.Ran!.Line, last.HasNext));
        Assert.Null(last.Next);
    }

    [Fact]
    public async Task AScenarioLeftAtAFailedStep_WritesItsResult_AsTheGateDoes_AndGoingOnWritesNoOther()
    {
        var results = Path.Combine(_root, "TestResults");
        await using var runner = await StartAsync(new Dictionary<string, string> { [Bruinet.Core.Artifacts.TestArtifactOptions.RootDirectoryEnvironmentVariable] = results });
        await runner.RequestAsync(Open("Counts wrong"));
        await runner.RequestAsync(new BddRunnerNext());
        await runner.RequestAsync(new BddRunnerNext());
        var written = Directory.EnumerateFiles(results, "*__result.json", SearchOption.AllDirectories).ToList();

        await runner.RequestAsync(new BddRunnerNext());
        await runner.RequestAsync(new BddRunnerClose());

        var result = File.ReadAllText(Assert.Single(written));
        Assert.Matches("\"status\":\\s*\"failed\"", result);
        Assert.Equal(written, Directory.EnumerateFiles(results, "*__result.json", SearchOption.AllDirectories));
        Assert.DoesNotContain("\"line\": 55", result);

        // The failed step says what kind of failure, its binding and the suite's frame it came through (AI2).
        var failed = System.Text.Json.Nodes.JsonNode.Parse(result)!["steps"]![1]!;
        Assert.Equal("assertion", (string?)failed["failure"]!["kind"]);
        Assert.Contains("HasCounted", (string?)failed["failure"]!["binding"]);
        Assert.Contains(failed["failure"]!["frames"]!.AsArray(), frame => ((string?)frame)!.StartsWith("RunnerProcessFixture.HasCounted (BddRunnerProcessTests.cs:", StringComparison.Ordinal));
        Assert.True((long?)failed["durationMs"] >= 0);
    }

    [Fact]
    public async Task AScenarioLeftBeforeItsEnd_WritesNoResult()
    {
        var results = Path.Combine(_root, "TestResults");
        await using var runner = await StartAsync(new Dictionary<string, string> { [Bruinet.Core.Artifacts.TestArtifactOptions.RootDirectoryEnvironmentVariable] = results });
        await runner.RequestAsync(Open("Counts to two"));
        await runner.RequestAsync(new BddRunnerNext());

        await runner.RequestAsync(new BddRunnerClose());

        Assert.Empty(Directory.Exists(results) ? Directory.EnumerateFiles(results, "*__result.json", SearchOption.AllDirectories) : []);
    }

    private Task<BddRunnerClient> StartAsync(IReadOnlyDictionary<string, string>? environment = null)
        => BddRunnerClient.StartAsync(new BddRunnerStart(_pages, Environment: environment));

    private BddRunnerOpen Open(string scenario) => new(_config, _pages, _feature, scenario);
}

/// <summary>The fixture the runner tests' scenarios run on: no app, a counter.</summary>
public sealed class RunnerProcessFixture : IUatScenarioLifecycle
{
    // How many fixtures the runner made: one kept between scenarios is made once (AD-025).
    private static int _made;
    private int _count;

    public RunnerProcessFixture() => Interlocked.Increment(ref _made);

    public bool StartsCleanWithoutLaunch => true;

    public void BeforeFile(BddDocument document)
    {
    }

    // The clean start: the count, as a new fixture's.
    public void BeforeScenario(BddBoundScenario scenario) => _count = 0;

    [BddThen("the runner made its fixture {times} times")]
    public void Made(int times) => Assert.True(times == _made, $"The runner made its fixture {_made} times, not {times}.");

    [BddGiven("the runner counts")]
    [BddWhen("the runner counts")]
    public void Count() => _count++;

    [BddThen("the runner has counted {times} times")]
    public void HasCounted(int times) => Assert.True(times == _count, $"The runner counted {_count} times, not {times}.");

    [BddGiven("the runner waits to be stopped")]
    public Task WaitAsync() => Task.Delay(Timeout.Infinite);

    [BddThen("the runner's base directory is the suite's output")]
    public void BaseDirectoryIsTheOutput()
        => Assert.Equal(Path.GetDirectoryName(typeof(RunnerProcessFixture).Assembly.Location), AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));

    [BddThen("the runner sees {name} as {value}")]
    public void Sees(string name, string value) => Assert.Equal(value, Environment.GetEnvironmentVariable(name));
}
