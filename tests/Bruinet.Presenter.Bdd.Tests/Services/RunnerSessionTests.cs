using Bruinet.Bdd;
using System.Diagnostics;
using Bruinet.Bdd.Runner;
using Bruinet.Presenter.Services;

namespace Bruinet.Presenter.Bdd.Tests.Services;

/// <summary>
/// A run must not make its own workspace unbuildable, nor grow Presenter: a session's scenario
/// runs in a runner process (AD-025) started from a copy of the suite's output, which waits for the
/// next session (AD-025) and ends with Presenter. This test project's own output is the suite.
/// </summary>
public sealed class RunnerSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BruinetRunnerSession", Guid.NewGuid().ToString("N"));
    private readonly string _feature;

    public RunnerSessionTests()
    {
        Directory.CreateDirectory(_root);
        _feature = Path.Combine(_root, "probe.feature.md");
        File.WriteAllText(
            _feature,
            """
            # Feature: Probe

            ## Scenario: The probe is ready

            ```gherkin
            Given the probe is ready
            ```

            ## Scenario: The probe knows its folder

            ```gherkin
            Given the probe runs in its own output
            ```

            ## Scenario: The probe runs from a copy

            ```gherkin
            Given the probe runs from a copy of its output
            ```

            ## Scenario: The probe is checked

            ```gherkin
            Given the probe is checked
            ```
            """);
        File.WriteAllText(
            Path.Combine(_root, "probe.steps.md"),
            """
            # Steps: Probe

            ## Step: the probe is checked

            ```gherkin
            Given the probe is ready
            ```
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact(Timeout = 120000)]
    public async Task ASession_RunsInAProcessOfItsOwn_FromACopyOfTheOutput_ThatWaitsForTheNext_AndEndsWithPresenter()
    {
        Config(nameof(ProbeFixture));
        var service = new BddExecutionService();

        var first = await service.CreateSessionAsync(_root, _feature, "The probe is ready", CancellationToken.None);
        var runner = Process.GetProcessById(first.RunnerProcessId);
        var step = await first.RunNextAsync(CancellationToken.None);
        first.Dispose();
        await service.EndRunnersAsync();
        var second = await service.CreateSessionAsync(_root, _feature, "The probe runs from a copy", CancellationToken.None);
        var inCopy = await second.RunNextAsync(CancellationToken.None);
        second.Dispose();

        Assert.NotEqual(Environment.ProcessId, runner.Id);
        Assert.Equal(BddStepResultStatus.Passed, step.Status);
        Assert.True(first.Passed);
        Assert.Equal(first.RunnerProcessId, second.RunnerProcessId);
        Assert.False(runner.HasExited, "The runner waits for the next session.");

        Assert.True(inCopy.Status == BddStepResultStatus.Passed, inCopy.Message);
        await service.EndRunnersAsync();
        service.Dispose();
        await runner.WaitForExitAsync();
    }

    [Fact(Timeout = 120000)]
    public async Task Warm_StartsTheRunnerAhead_SoASessionTakesIt()
    {
        Config(nameof(ProbeFixture));
        using var service = new BddExecutionService();

        await service.WarmAsync(_root);
        var clock = Stopwatch.StartNew();
        using var session = await service.CreateSessionAsync(_root, _feature, "The probe is ready", CancellationToken.None);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), $"The session took {clock.Elapsed} to open on a warm runner.");
    }

    [Fact(Timeout = 120000)]
    public async Task ASession_HasItsRunnerPaceAndRecordTheScenario()
    {
        Config(nameof(ProbeFixture));
        using var service = new BddExecutionService();
        using var session = await service.CreateSessionAsync(_root, _feature, "The probe is ready", CancellationToken.None);
        List<string> said = [];
        session.Said = say => said.Add(say.Wav);
        var folder = Path.Combine(_root, "scene-01");
        var clips = new BddRunnerClips(new BddRunnerClip("Hello.", "intro.wav", 0.01), new Dictionary<int, BddRunnerClip>(), null);

        await session.RecordAsync(new BddRunnerRecord(1, "Probe", "probe.feature.md#The-probe-is-ready", folder, "hash-1", clips, 0, SpeakAloud: true));
        await session.RunNextAsync(CancellationToken.None);
        var recorded = await session.FinishRecordingAsync(CancellationToken.None);

        // The probe shows nothing to record: the scene still gets its timeline, and says why it has no video.
        var timeline = Bruinet.Narrator.BddSceneTimeline.Load(recorded.Timeline!);
        Assert.Equal(("Probe", "hash-1", BddStepResultStatus.Passed), (timeline.Name, timeline.Hash, timeline.Status));
        Assert.Contains(recorded.Warnings, warning => warning.Contains("IRecordable"));
        Assert.Equal(["intro.wav"], said);
    }

    [Fact(Timeout = 120000)]
    public async Task AFinishedScenario_IsWrittenWhereTheIdesPreviewReadsIt_NotInTheOutputsRepository()
    {
        Config(nameof(ProbeFixture));
        using var service = new BddExecutionService();
        using var session = await service.CreateSessionAsync(_root, _feature, "The probe is ready", CancellationToken.None);

        await session.RunNextAsync(CancellationToken.None);

        // The suite's folder is in no repository here, so its results folder is its own (TestArtifactOptions.RootFor).
        var results = Path.Combine(_root, "TestResults");
        Assert.Single(Directory.EnumerateFiles(results, "*__result.json", SearchOption.AllDirectories));
        var manifest = File.ReadAllText(Assert.Single(Directory.EnumerateFiles(results, "manifest.json", SearchOption.AllDirectories)));
        Assert.Matches("\"mode\":\\s*\"runner\"", manifest);
    }

    [Fact(Timeout = 120000)]
    public async Task ASession_SaysWhereTheNextStepIs_InsideACall()
    {
        Config(nameof(ProbeFixture));
        using var service = new BddExecutionService();
        using var session = await service.CreateSessionAsync(_root, _feature, "The probe is checked", CancellationToken.None);

        var next = session.Next!;
        Assert.Equal((Path.Combine(_root, "probe.steps.md"), 6, 1), (next.File, next.Line, next.Depth));
        Assert.Equal((_feature, 24), (next.Calls[0].File, next.Calls[0].Line));

        await session.RunNextAsync(CancellationToken.None);
        Assert.Null(session.Next);
    }

    [Fact(Timeout = 120000)]
    public async Task ASuiteThatCannotStart_SaysWhy()
    {
        Config(nameof(FailingProbeFixture));
        using var service = new BddExecutionService();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateSessionAsync(_root, _feature, "The probe is ready", CancellationToken.None));

        Assert.Contains(FailingProbeFixture.Reason, failure.Message, StringComparison.Ordinal);
    }

    private void Config(string fixture)
        => File.WriteAllText(
            Path.Combine(_root, "uat.config.md"),
            $$"""
            # UAT Config

            ## Runtime

            | Field | Value |
            | --- | --- |
            | Target | WPF |
            | Fixture | {{fixture}} |

            ## Assemblies

            | Kind | Assembly |
            | --- | --- |
            | Pages | {{Path.GetFileName(typeof(RunnerSessionTests).Assembly.Location)}} |
            """);
}

/// <summary>A fixture with nothing to launch, so the test measures the runner and nothing else.</summary>
public sealed class ProbeFixture
{
    /// <summary>The one phrase the probe scenario uses.</summary>
    [BddGiven("the probe is ready")]
    public bool Ready() => true;

    /// <summary>Passes when the process runs in the suite's output, as under <c>dotnet test</c>.</summary>
    /// <summary>Passes when the process runs from a copy of the suite's output (AD-025), so the output is free.</summary>
    [BddGiven("the probe runs from a copy of its output")]
    public void RunsFromACopy()
        => Assert.StartsWith(Bruinet.Bdd.Runner.BddShadowCopy.DefaultRoot, AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase);

    [BddGiven("the probe runs in its own output")]
    public void RunsInItsOutput()
        => Assert.Equal(
            Path.GetDirectoryName(typeof(ProbeFixture).Assembly.Location),
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
}

/// <summary>A fixture whose type cannot be initialized, as one that reads a missing file when it loads.</summary>
public sealed class FailingProbeFixture
{
    /// <summary>What the failure says.</summary>
    public const string Reason = "The probe fixture cannot start.";

    private static readonly string Setting;

    static FailingProbeFixture() => Setting = Fail();

    /// <summary>What the fixture read when it loaded.</summary>
    public string Read => Setting;

    private static string Fail() => throw new InvalidOperationException(Reason);
}
