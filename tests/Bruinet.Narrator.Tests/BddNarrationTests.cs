using Bruinet.Bdd;
using System.Diagnostics;
using Bruinet.Core.Recording;
using Bruinet.Core.Testing;
using Bruinet.Recording;

namespace Bruinet.Narrator.Tests;

public sealed class BddNarrationPacerTests
{
    [Fact]
    public async Task A_line_is_waited_for_before_its_step_and_a_beat_after_it()
    {
        var scenario = Scenarios.Bind();
        var clips = new BddSceneClips(
            new SpeechClip("Intro.", null, TimeSpan.FromMilliseconds(60)),
            new Dictionary<int, SpeechClip> { [7] = new("You tap Sync.", null, TimeSpan.FromMilliseconds(80)) },
            new SpeechClip("Outro.", null, TimeSpan.FromMilliseconds(40)));
        var pacer = new BddNarrationPacer(clips, TimeSpan.FromMilliseconds(30), say: BddSayMode.Before);
        var start = Stopwatch.GetTimestamp();

        await new BddScenarioRunner(new BddExecutionContext { Breaks = null, Pacer = pacer }).RunAsync(scenario);
        await pacer.FinishAsync(CancellationToken.None);
        var timeline = pacer.Timeline(1, "Opening", "Features/A.feature.md#A.1", "video.mp4", start, Stopwatch.GetTimestamp(), []);

        Assert.Equal(["Intro.", "You tap Sync.", "Outro."], timeline.Clips.Select(clip => clip.Text));
        Assert.Equal(["Given I am on the List page", "When I tap Sync"], timeline.Steps.Select(step => step.Text));
        var (intro, say, outro) = (timeline.Clips[0], timeline.Clips[1], timeline.Clips[2]);
        var (first, second) = (timeline.Steps[0], timeline.Steps[1]);
        Assert.True(first.Start >= intro.End - 0.01, "The intro plays before the first step.");
        Assert.True(say.Start >= first.End + 0.02, "A beat follows a step.");
        Assert.True(second.Start >= say.End - 0.01, "A step waits for its line.");
        Assert.True(outro.Start >= second.End + 0.02, "The outro follows the last step's beat.");
        Assert.Equal(BddStepResultStatus.Passed, timeline.Status);
        Assert.True(timeline.Duration >= outro.End - 0.01);
    }

    [Fact]
    public async Task A_line_plays_while_its_step_runs_and_the_outro_waits_for_it()
    {
        var scenario = Scenarios.Bind();
        var clips = new BddSceneClips(
            new SpeechClip("Intro.", null, TimeSpan.FromMilliseconds(60)),
            new Dictionary<int, SpeechClip> { [7] = new("You tap Sync.", null, TimeSpan.FromMilliseconds(150)) },
            new SpeechClip("Outro.", null, TimeSpan.FromMilliseconds(40)));
        var pacer = new BddNarrationPacer(clips, TimeSpan.FromMilliseconds(30));
        var start = Stopwatch.GetTimestamp();

        await new BddScenarioRunner(new BddExecutionContext { Breaks = null, Pacer = pacer }).RunAsync(scenario);
        await pacer.FinishAsync(CancellationToken.None);
        var timeline = pacer.Timeline(1, "Opening", "Features/A.feature.md#A.1", "video.mp4", start, Stopwatch.GetTimestamp(), []);

        var (intro, say, outro) = (timeline.Clips[0], timeline.Clips[1], timeline.Clips[2]);
        var (first, second) = (timeline.Steps[0], timeline.Steps[1]);
        Assert.True(first.Start >= intro.End - 0.01, "The intro still plays before the first step.");
        Assert.True(second.Start < say.End - 0.05, "A step runs while its line plays.");
        Assert.InRange(second.Start - say.Start, 0, 0.05);
        Assert.True(outro.Start >= say.End - 0.01, "The outro waits for the line.");
    }

    [Fact]
    public async Task A_step_without_a_line_follows_its_beat_while_the_line_before_it_still_plays()
    {
        // A loading state checked right after the tap that started it must not wait for the tap's line.
        var scenario = Scenarios.Bind();
        var clips = new BddSceneClips(null, new Dictionary<int, SpeechClip> { [6] = new("Here is your list.", null, TimeSpan.FromMilliseconds(300)) }, null);
        var pacer = new BddNarrationPacer(clips, TimeSpan.FromMilliseconds(20));
        var start = Stopwatch.GetTimestamp();

        await new BddScenarioRunner(new BddExecutionContext { Breaks = null, Pacer = pacer }).RunAsync(scenario);
        await pacer.FinishAsync(CancellationToken.None);
        var timeline = pacer.Timeline(1, "Opening", null, null, start, Stopwatch.GetTimestamp(), []);

        var (first, second) = (timeline.Steps[0], timeline.Steps[1]);
        var say = Assert.Single(timeline.Clips);
        Assert.True(second.Start >= first.End + 0.015, "A beat follows a step.");
        Assert.True(second.Start < say.End - 0.15, "A silent step does not wait for the line before it.");
    }

    [Fact]
    public async Task A_step_with_a_line_waits_for_the_line_before_it_so_lines_never_overlap()
    {
        var scenario = Scenarios.Bind();
        var clips = new BddSceneClips(null, new Dictionary<int, SpeechClip>
        {
            [6] = new("Here is your list.", null, TimeSpan.FromMilliseconds(200)),
            [7] = new("You tap Sync.", null, TimeSpan.FromMilliseconds(50)),
        }, null);
        var pacer = new BddNarrationPacer(clips, TimeSpan.Zero);
        var start = Stopwatch.GetTimestamp();

        await new BddScenarioRunner(new BddExecutionContext { Breaks = null, Pacer = pacer }).RunAsync(scenario);
        await pacer.FinishAsync(CancellationToken.None);
        var timeline = pacer.Timeline(1, "Opening", null, null, start, Stopwatch.GetTimestamp(), []);

        Assert.True(timeline.Clips[1].Start >= timeline.Clips[0].End - 0.01, "The second line starts when the first has ended.");
        Assert.True(timeline.Steps[1].Start >= timeline.Clips[0].End - 0.01, "Its step starts with it.");
    }

    [Fact]
    public async Task A_failed_step_still_waits_for_its_line()
    {
        var scenario = Scenarios.Bind(tapFails: true);
        var clips = new BddSceneClips(null, new Dictionary<int, SpeechClip> { [7] = new("You tap Sync.", null, TimeSpan.FromMilliseconds(120)) }, null);
        var pacer = new BddNarrationPacer(clips, TimeSpan.Zero);
        var start = Stopwatch.GetTimestamp();

        await new BddScenarioRunner(new BddExecutionContext { Breaks = null, Pacer = pacer }).RunAsync(scenario);
        await pacer.FinishAsync(CancellationToken.None);
        var end = Stopwatch.GetTimestamp();
        var timeline = pacer.Timeline(1, "Opening", null, null, start, end, []);

        Assert.Equal(BddStepResultStatus.Failed, timeline.Status);
        Assert.True(Stopwatch.GetElapsedTime(start, end).TotalSeconds >= timeline.Clips[0].End - 0.01, "The scene ends after the line.");
    }

    [Fact]
    public async Task A_failed_step_is_in_the_timeline_with_its_message()
    {
        var scenario = Scenarios.Bind(tapFails: true);
        var pacer = new BddNarrationPacer(BddSceneClips.Silent, TimeSpan.Zero);
        var start = Stopwatch.GetTimestamp();

        await new BddScenarioRunner(new BddExecutionContext { Breaks = null, Pacer = pacer }).RunAsync(scenario);
        await pacer.FinishAsync(CancellationToken.None);
        var timeline = pacer.Timeline(1, "Opening", null, null, start, Stopwatch.GetTimestamp(), []);

        Assert.Equal(BddStepResultStatus.Failed, timeline.Status);
        Assert.Equal("Sync is broken.", timeline.Steps[^1].Message);
    }
}

public sealed class BddSceneRecordingTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("bruinet-scene-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task The_recorder_starts_at_the_first_step_and_the_timeline_is_on_its_clock()
    {
        var recorder = new FakeRecorder();
        var request = new BddSceneRequest(2, "Opening", "Features/A.feature.md#A.1", Path.Combine(_folder, "scene-02"), BddSceneClips.Silent, TimeSpan.Zero);
        await using var recording = new BddSceneRecording(request, () => new RecordingTarget.Window(42), new ScreenRecorders([recorder]));

        Assert.Equal(0, recorder.Started);
        await new BddScenarioRunner(new BddExecutionContext { Breaks = null, Pacer = recording }).RunAsync(Scenarios.Bind());
        var timeline = await recording.FinishAsync();

        Assert.Equal(1, recorder.Started);
        Assert.Equal("video.mp4", timeline.Video);
        Assert.Equal(2, timeline.Number);
        Assert.Equal(timeline, BddSceneTimeline.Load(request.TimelinePath) with { Steps = timeline.Steps, Clips = timeline.Clips, Warnings = timeline.Warnings, Interactions = timeline.Interactions });
        Assert.Equal(2, BddSceneTimeline.Load(request.TimelinePath).Steps.Count);
    }

    [Fact]
    public async Task The_tests_taps_while_the_scene_records_are_in_its_timeline_with_the_area_the_video_shows()
    {
        var request = new BddSceneRequest(1, "Opening", "Features/A.feature.md#A.1", Path.Combine(_folder, "scene-01"), BddSceneClips.Silent, TimeSpan.Zero);
        await using var recording = new BddSceneRecording(request, () => new RecordingTarget.Window(42), new ScreenRecorders([new FakeRecorder()]));
        using (Interactions.Report(InteractionKind.Tap, () => new System.Drawing.Rectangle(1, 1, 1, 1)))
        {
            // Before the scene: the fixture's, not the scene's.
        }

        await new BddScenarioRunner(new BddExecutionContext { Breaks = null, Pacer = recording }).RunAsync(Scenarios.Bind());
        using (Interactions.Report(InteractionKind.Tap, () => new System.Drawing.Rectangle(110, 220, 40, 30), () => "SyncButton"))
        {
        }

        var timeline = await recording.FinishAsync();
        using (Interactions.Report(InteractionKind.Tap, () => new System.Drawing.Rectangle(1, 1, 1, 1)))
        {
        }

        var saved = BddSceneTimeline.Load(request.TimelinePath);
        var tap = Assert.Single(saved.Interactions!);
        Assert.Equal(InteractionKind.Tap, tap.Kind);
        Assert.Equal(new BddBox(110, 220, 40, 30), tap.Bounds);
        Assert.Equal("SyncButton", tap.Label);
        Assert.InRange(tap.At, timeline.Steps[^1].Start, timeline.Duration);
        Assert.Equal(new BddBox(100, 200, 800, 600), saved.Source);
        Assert.Null(Interactions.Log);
    }

    [Fact]
    public async Task While_the_scene_records_its_actions_are_paced_and_after_it_they_are_not()
    {
        var request = new BddSceneRequest(1, "Opening", "Features/A.feature.md#A.1", Path.Combine(_folder, "scene-01"), BddSceneClips.Silent, TimeSpan.Zero)
        {
            Pacing = new BddScenePacing(TimeSpan.FromMilliseconds(450), BddSayMode.While),
        };
        await using var recording = new BddSceneRecording(request, () => new RecordingTarget.Window(42), new ScreenRecorders([new FakeRecorder()]));
        var before = ActionPacing.Pause;

        await new BddScenarioRunner(new BddExecutionContext { Breaks = null, Pacer = recording }).RunAsync(Scenarios.Bind());
        var during = ActionPacing.Pause;
        await recording.FinishAsync();

        Assert.Null(before);
        Assert.Equal(TimeSpan.FromMilliseconds(450), during);
        Assert.Null(ActionPacing.Pause);
    }

    [Fact]
    public async Task A_context_that_cannot_say_what_it_shows_still_gets_a_timeline_with_a_warning()
    {
        var request = new BddSceneRequest(1, "Opening", "Features/A.feature.md#A.1", Path.Combine(_folder, "scene-01"), BddSceneClips.Silent, TimeSpan.Zero);
        await using var recording = new BddSceneRecording(request, () => null, new ScreenRecorders([new FakeRecorder()]));

        await new BddScenarioRunner(new BddExecutionContext { Breaks = null, Pacer = recording }).RunAsync(Scenarios.Bind());
        var timeline = await recording.FinishAsync();

        Assert.Null(timeline.Video);
        Assert.Contains("IRecordable", Assert.Single(timeline.Warnings), StringComparison.Ordinal);
        Assert.True(File.Exists(request.TimelinePath));
    }

    private sealed class FakeRecorder : IScreenRecorder
    {
        public int Started { get; private set; }

        public bool CanRecord(RecordingTarget target) => target is RecordingTarget.Window;

        public Task<IRecording> StartAsync(RecordingTarget target, RecordingOptions options, CancellationToken cancellationToken = default)
        {
            Started++;
            return Task.FromResult<IRecording>(new FakeRecording(options.OutputPath));
        }
    }

    private sealed class FakeRecording(string path) : IRecording
    {
        public long StartedAt { get; } = Stopwatch.GetTimestamp();

        public Task<RecordingResult> StopAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new RecordingResult(path, Stopwatch.GetElapsedTime(StartedAt), new System.Drawing.Size(2, 2), StartedAt, []) { Source = new System.Drawing.Rectangle(100, 200, 800, 600) });

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public sealed class BddRecordingSessionTests : IDisposable
{
    private readonly string _suite = Directory.CreateTempSubdirectory("bruinet-session-").FullName;

    public BddRecordingSessionTests()
    {
        File.WriteAllText(Path.Combine(_suite, BddFileFormat.UiConfigFileName), "# UAT Config\n");
        Directory.CreateDirectory(Path.Combine(_suite, "Features"));
        File.WriteAllText(Path.Combine(_suite, "Features", "A.feature.md"), Scenarios.Feature);
        File.WriteAllText(Path.Combine(_suite, "Features", "a.video.md"), """
            # Video: A

            ## Scene: Opening

            Scenario: Features/A.feature.md#A.1-Opens

            Here it is.

            | Step | Say |
            | --- | --- |
            | When I tap Sync | You tap Sync. |
            """);
    }

    public void Dispose() => Directory.Delete(_suite, recursive: true);

    [Fact]
    public void Without_BRUINET_RECORD_nothing_is_recorded()
    {
        Assert.Null(BddRecordingSession.FromEnvironment(_suite, _ => null));
        Assert.Null(BddRecordingSession.FromEnvironment(_suite, name => name == BddRecordingSession.Variable ? "0" : null));
    }

    [Fact]
    public void Recording_without_ffmpeg_says_how_to_get_it()
    {
        var variables = new Dictionary<string, string?> { [BddRecordingSession.Variable] = "1", [Ffmpeg.Variable] = @"C:\nowhere\ffmpeg.exe" };

        Assert.Throws<FfmpegNotFoundException>(() => BddRecordingSession.FromEnvironment(_suite, variables.GetValueOrDefault));
    }

    [Fact]
    public void A_script_names_the_scenes_folder_and_its_voice_and_beat_win()
    {
        var variables = new Dictionary<string, string?>
        {
            [BddRecordingSession.Variable] = "Features/a.video.md",
            [BddRecordingSession.FolderVariable] = Path.Combine(_suite, "Recordings"),
            [BddRecordingSession.BeatVariable] = "250",
            [Ffmpeg.Variable] = Environment.ProcessPath,
        };

        var session = BddRecordingSession.FromEnvironment(_suite, variables.GetValueOrDefault)!;

        Assert.Equal(Path.Combine(_suite, "Recordings", "a.run"), session.ScenesFolder, ignoreCase: true);
        Assert.Equal(TimeSpan.FromMilliseconds(250), session.Beat);
    }

    [Fact]
    public async Task The_pause_comes_from_the_script_else_BRUINET_PAUSE_else_the_default_and_reaches_each_scene()
    {
        var script = Path.Combine(_suite, "Features", "a.video.md");
        File.WriteAllText(Path.Combine(_suite, "Features", "b.video.md"), File.ReadAllText(script).Replace("# Video: A", "# Video: B\n\n| Field | Value |\n| --- | --- |\n| Pause | 900 |\n| Say | before |", StringComparison.Ordinal));
        BddRecordingSession Session(string video, string? pause) => BddRecordingSession.FromEnvironment(_suite, new Dictionary<string, string?>
        {
            [BddRecordingSession.Variable] = video,
            [BddRecordingSession.FolderVariable] = Path.Combine(_suite, "Recordings"),
            [BddRecordingSession.PauseVariable] = pause,
            [Ffmpeg.Variable] = Environment.ProcessPath,
        }.GetValueOrDefault)!;

        var fromScript = Session("Features/b.video.md", "300");
        var fromVariable = Session("Features/a.video.md", "300");
        var byDefault = Session("Features/a.video.md", null);
        var scene = await fromScript.SceneForAsync(Path.Combine(_suite, "Features", "A.feature.md"), "A.1 Opens");

        Assert.Equal(new BddScenePacing(TimeSpan.FromMilliseconds(900), BddSayMode.Before), fromScript.Pacing);
        Assert.Equal(new BddScenePacing(TimeSpan.FromMilliseconds(300), BddSayMode.While), fromVariable.Pacing);
        Assert.Equal(BddScenePacing.Default, byDefault.Pacing);
        Assert.Equal(fromScript.Pacing, scene!.Pacing);
    }

    [Fact]
    public async Task A_scenario_in_the_script_gets_its_scene_and_its_lines()
    {
        var script = Path.Combine(_suite, "Features", "a.video.md");
        var session = new BddRecordingSession(_suite, BddVideoScriptCheck.Check(File.ReadAllText(script), script), Path.Combine(_suite, "out"), null, null, TimeSpan.Zero);

        var scene = await session.SceneForAsync(Path.Combine(_suite, "Features", "A.feature.md"), "A.1 Opens");
        var other = await session.SceneForAsync(Path.Combine(_suite, "Features", "A.feature.md"), "A.2 Closes");

        Assert.NotNull(scene);
        Assert.Equal((1, "Opening", Path.Combine(_suite, "out", "scene-01")), (scene.Number, scene.Name, scene.Folder));
        Assert.Equal("Here it is.", scene.Clips.Intro!.Text);
        Assert.Equal("You tap Sync.", scene.Clips.Says[7].Text);
        Assert.Null(scene.Clips.Says[7].WavPath);
        Assert.Null(other);
    }

    [Fact]
    public async Task Without_a_script_every_scenario_is_a_scene_of_its_own()
    {
        var session = new BddRecordingSession(_suite, null, Path.Combine(_suite, "out"), null, null, TimeSpan.Zero);

        var first = await session.SceneForAsync(Path.Combine(_suite, "Features", "A.feature.md"), "A.1 Opens");
        var second = await session.SceneForAsync(Path.Combine(_suite, "Features", "A.feature.md"), "A.2 Closes");

        Assert.Equal((1, "Features/A.feature.md#A.1-Opens"), (first!.Number, first.Scenario));
        Assert.Equal((1, "scene-01"), (second!.Number, Path.GetFileName(second.Folder)));
        Assert.Equal("A.1-Opens.run", Path.GetFileName(Path.GetDirectoryName(first.Folder)));
        Assert.Equal("A.2-Closes.run", Path.GetFileName(Path.GetDirectoryName(second.Folder)));
        Assert.Same(BddSceneClips.Silent, first.Clips);
    }
}

internal static class Scenarios
{
    public const string Feature = """
        # Feature: A

        ## Scenario: A.1 Opens

        ```gherkin
        Given I am on the List page
        When I tap Sync
        ```

        ## Scenario: A.2 Closes

        ```gherkin
        Given I am on the List page
        ```
        """;

    public static BddBoundScenario Bind(bool tapFails = false)
    {
        var catalog = new BddCommandCatalog();
        catalog.Register(BddEffectiveStepKeyword.Given, "I am on the {page} page", "Pages.Open",
            handler: (_, invocation, _) => Task.FromResult(BddStepResult.Passed(invocation)));
        catalog.Register(BddEffectiveStepKeyword.When, "I tap {control}", "Controls.Tap", allowsTable: false,
            handler: (_, invocation, _) => tapFails ? throw new InvalidOperationException("Sync is broken.") : Task.FromResult(BddStepResult.Passed(invocation)));
        var bind = BddBinder.Bind(BddMarkdownParser.Parse(Feature, "A.feature.md").Document!, catalog);
        return bind.Document!.Scenarios[0];
    }
}

public sealed class RecordingPackagesTests
{
    [Fact]
    public void A_run_that_cannot_reference_the_Windows_packages_loads_them_from_BRUINET_RECORDERS()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Bruinet.Narrator.Windows/bin",
            new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name, "net10.0-windows10.0.19041.0"));

        var loaded = RecordingPackages.Load(["Bruinet.Narrator.Windows"], name => name == RecordingPackages.Variable ? folder : null);

        Assert.Equal(["Bruinet.Narrator.Windows"], loaded.Select(assembly => assembly.GetName().Name));
        Assert.Equal("WindowsSpeech", Speech.Discover()?.GetType().Name);
        Assert.Equal("WindowsCaptureRecorder", ScreenRecorders.Discover().Recorders[0].GetType().Name);
    }
}
