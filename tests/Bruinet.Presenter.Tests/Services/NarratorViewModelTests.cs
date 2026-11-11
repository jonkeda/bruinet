using Bruinet.Presenter.Models;
using Bruinet.Presenter.ViewModels;

namespace Bruinet.Presenter.Tests.Services;

/// <summary>
/// Narrator mode's script: opened from its file, its suite found by walking up to the config, its
/// scenes and lines as rows, and what the script has wrong shown where it is wrong.
/// </summary>
public sealed class NarratorViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BruinetNarrator", Guid.NewGuid().ToString("N"));

    public NarratorViewModelTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Features", "Videos"));
        File.WriteAllText(Path.Combine(_root, "uat.config.md"), "# UAT Config\n");
        File.WriteAllText(
            Path.Combine(_root, "Features", "greeting.feature.md"),
            """
            # Feature: Greeting

            ## Scenario: A greeting appears

            ```gherkin
            Given I am on the Main page
            When I tap Greet
            Then Greeting should contain "Hello"
            ```

            ## Scenario: A name is needed

            ```gherkin
            Given I am on the Main page
            When I tap Greet
            ```
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Open_FindsTheSuiteAboveTheScript_AndShowsScenesLinesAndSteps()
    {
        // Two folders under the config: the suite is found by walking up, not by where the script is.
        var script = Write(Path.Combine("Features", "Videos", "tour.video.md"),
            """
            # Video: A tour

            ## Scene: Saying hello

            Scenario: Features/greeting.feature.md#A-greeting-appears

            The app greets you.

            | Step | Say |
            | --- | --- |
            | When I tap Greet | Tap Greet, |

            That is all.
            """);
        var narrator = new NarratorViewModel();

        narrator.Open(script);

        Assert.Equal(_root, narrator.ConfigDirectory);
        Assert.Equal("tour.video.md", narrator.ScriptName);
        Assert.False(narrator.HasProblems, narrator.ProblemsText);
        var scene = Assert.Single(narrator.Scenes);
        Assert.Equal("1 Saying hello", scene.Node.Name);
        Assert.Equal("A greeting appears", scene.Node.Detail);
        Assert.Equal(
            ["The app greets you.|intro", "Given I am on the Main page|silent", "When I tap Greet|Tap Greet,", "Then Greeting should contain \"Hello\"|silent", "That is all.|outro"],
            scene.Node.Children.Select(row => $"{row.Name}|{row.Note}"));
        Assert.Contains("A tour  (1 scenes)", narrator.TreeText, StringComparison.Ordinal);
    }

    [Fact]
    public void Open_ShowsWhatTheScriptHasWrong_OnItsSceneAndItsRow()
    {
        var script = Write(Path.Combine("Features", "bad.video.md"),
            """
            # Video: Wrong

            ## Scene: No such scenario

            Scenario: Features/greeting.feature.md#Nothing-like-this

            ## Scene: No such step

            Scenario: Features/greeting.feature.md#A-name-is-needed

            | Step | Say |
            | --- | --- |
            | When I tap Nothing | This step is not in the scenario. |
            """);
        var narrator = new NarratorViewModel();

        narrator.Open(script);
        narrator.Link([Scenario("A name is needed")]);

        Assert.True(narrator.HasProblems);
        Assert.Contains("VID012", narrator.Scenes[0].Node.Problem, StringComparison.Ordinal);
        Assert.False(narrator.Scenes[0].CanRun);
        Assert.False(narrator.Scenes[0].Node.IsCheckEnabled);
        Assert.False(narrator.Scenes[0].Node.IsChecked);

        var wrong = Assert.Single(narrator.Scenes[1].Node.Children, row => row.HasProblem);
        Assert.Equal("When I tap Nothing", wrong.Name);
        Assert.Contains("VID013", wrong.Problem, StringComparison.Ordinal);
        Assert.Contains("2 problems", narrator.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Open_WithNoConfigAbove_SaysSo()
    {
        var folder = Path.Combine(Path.GetTempPath(), "BruinetNarrator", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var script = Path.Combine(folder, "lost.video.md");
            File.WriteAllText(script, "# Video: Lost\n\n## Scene: One\n\nScenario: Features/a.feature.md#A\n");
            var narrator = new NarratorViewModel();

            narrator.Open(script);

            Assert.Null(narrator.ConfigDirectory);
            Assert.Contains("VID010", narrator.ProblemsText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Link_ChecksTheScenesThatCanRun_AndTheirRowsFollowTheRun()
    {
        var script = Write(Path.Combine("Features", "two.video.md"),
            """
            # Video: Two

            ## Scene: First

            Scenario: Features/greeting.feature.md#A-greeting-appears

            ## Scene: Second

            Scenario: Features/greeting.feature.md#A-name-is-needed
            """);
        var narrator = new NarratorViewModel();
        narrator.Open(script);
        Assert.Empty(narrator.CheckedScenes);

        var first = Scenario("A greeting appears", 6, 7, 8);
        narrator.Link([first, Scenario("A name is needed", 14, 15)]);

        Assert.Equal(2, narrator.CheckedScenes.Count);
        Assert.True(narrator.Roots[0].IsChecked);

        // The script's checkbox is every scene's.
        narrator.Scenes[1].Node.IsChecked = false;
        Assert.False(narrator.Roots[0].IsChecked);
        Assert.Equal([1], narrator.CheckedScenes.Select(scene => scene.Number));
        narrator.Roots[0].IsChecked = true;
        Assert.Equal(2, narrator.CheckedScenes.Count);

        first.Steps[1].Status = "fail";
        first.Status = "fail";
        Assert.Equal("fail", narrator.Scenes[0].Node.Status);
        Assert.Equal("fail", narrator.Scenes[0].Node.Children[1].Status);
        Assert.Equal("wait", narrator.Scenes[0].Node.Children[2].Status);
    }

    [Fact]
    public void RefreshStates_ShowsWhatEachRecordingIsWorth_AndChecksTheScenesToRecord()
    {
        var script = Write(Path.Combine("Features", "two.video.md"),
            """
            # Video: Two

            ## Scene: First

            Scenario: Features/greeting.feature.md#A-greeting-appears

            ## Scene: Second

            Scenario: Features/greeting.feature.md#A-name-is-needed
            """);
        var scenes = Path.Combine(_root, "recordings", "two.scenes");
        var beat = TimeSpan.FromMilliseconds(600);
        var narrator = new NarratorViewModel();
        narrator.Open(script);
        narrator.Link([Scenario("A greeting appears", 6, 7, 8), Scenario("A name is needed", 14, 15)]);

        narrator.RefreshStates(scenes, null, beat, resetChecks: true);

        Assert.All(narrator.Scenes, scene => Assert.Equal(Bruinet.Narrator.BddSceneState.NotRecorded, scene.State));
        Assert.Equal(2, narrator.CheckedScenes.Count);
        Assert.False(narrator.CanStitch);
        Assert.Equal("not recorded, A greeting appears", narrator.Scenes[0].Node.Detail);

        // The first scene is recorded from what it is now; the second from another text.
        Record(scenes, 1, narrator.Scenes[0].Hash);
        Record(scenes, 2, "another text");
        narrator.RefreshStates(scenes, null, beat, resetChecks: true);

        Assert.Equal(Bruinet.Narrator.BddSceneState.Recorded, narrator.Scenes[0].State);
        Assert.Equal(Bruinet.Narrator.BddSceneState.OutOfDate, narrator.Scenes[1].State);
        Assert.Equal([2], narrator.CheckedScenes.Select(scene => scene.Number));
        Assert.True(narrator.CanStitch);
        Assert.Equal("recorded 0:05, A greeting appears", narrator.Scenes[0].Node.Detail);
        Assert.Equal("pass", narrator.Scenes[0].Node.Status);
        Assert.True(narrator.Scenes[0].Node.CanWatch);
        Assert.Equal("2 scenes, 1 checked, 1 recorded", narrator.Summary);

        // Another voice is another recording: every scene is out of date.
        narrator.RefreshStates(scenes, "Microsoft Jenny", beat, resetChecks: true);
        Assert.All(narrator.Scenes, scene => Assert.Equal(Bruinet.Narrator.BddSceneState.OutOfDate, scene.State));
        Assert.Equal(2, narrator.CheckedScenes.Count);
    }

    private static void Record(string scenes, int number, string hash)
    {
        var folder = Bruinet.Narrator.BddKeptScenes.SceneFolder(scenes, number);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "video.mp4"), "not a video");
        new Bruinet.Narrator.BddSceneTimeline(number, "Scene", null, "video.mp4", 5, BddStepResultStatus.Passed, [], [], [], Hash: hash)
            .Save(Path.Combine(folder, Bruinet.Narrator.BddSceneTimeline.FileName));
    }

    private string Write(string relative, string text)
    {
        var path = Path.Combine(_root, relative);
        File.WriteAllText(path, text);
        return path;
    }

    private BddScenarioViewModel Scenario(string name, params int[] lines)
        => new(new BddScenarioLoadResult(
            name,
            "Greeting",
            Path.Combine(_root, "Features", "greeting.feature.md"),
            [],
            [.. lines.Select(line => new BddStepLoadResult("wait", $"step at {line}", "Any", line))]));
}
