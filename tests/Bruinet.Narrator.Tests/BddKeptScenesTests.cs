namespace Bruinet.Narrator.Tests;

/// <summary>
/// Kept scenes: a scene's recording is worth what it was made from. A change to one scene's lines
/// makes that scene out of date, and only that one; the voice and the beat are every scene's.
/// </summary>
public sealed class BddKeptScenesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("bruinet-kept-").FullName;

    public BddKeptScenesTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Features"));
        File.WriteAllText(Path.Combine(_root, "bdd.config.md"), "# UAT Config\n");
        File.WriteAllText(
            Path.Combine(_root, "Features", "todo.feature.md"),
            """
            # Feature: Todo

            ## Scenario: Opening

            ```gherkin
            Given I am on the list
            When I tap Sync
            ```

            ## Scenario: Adding

            ```gherkin
            Given I am on the list
            When I tap Add
            ```
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void A_changed_line_makes_its_scene_out_of_date_and_only_that_one()
    {
        var before = Plans("You tap Sync.");
        var after = Plans("You tap Sync to fetch it.");

        Assert.NotEqual(Hash(before[0]), Hash(after[0]));
        Assert.Equal(Hash(before[1]), Hash(after[1]));
    }

    [Fact]
    public void The_voice_and_the_beat_are_part_of_every_scene()
    {
        var plan = Plans("You tap Sync.")[0];
        var hash = BddKeptScenes.Hash(plan, null, TimeSpan.FromMilliseconds(600));

        Assert.Equal(hash, BddKeptScenes.Hash(plan, null, TimeSpan.FromMilliseconds(600)));
        Assert.NotEqual(hash, BddKeptScenes.Hash(plan, "Microsoft Jenny", TimeSpan.FromMilliseconds(600)));
        Assert.NotEqual(hash, BddKeptScenes.Hash(plan, null, TimeSpan.FromMilliseconds(300)));
    }

    [Fact]
    public void A_changed_step_of_the_scenario_makes_its_scene_out_of_date()
    {
        var before = Hash(Plans("You tap Sync.")[1]);
        var feature = Path.Combine(_root, "Features", "todo.feature.md");
        File.WriteAllText(feature, File.ReadAllText(feature).Replace("When I tap Add", "When I tap Add\nThen the form is empty", StringComparison.Ordinal));

        Assert.NotEqual(before, Hash(Plans("You tap Sync.")[1]));
    }

    [Fact]
    public void A_scene_is_not_recorded_then_recorded_failed_or_out_of_date()
    {
        var scenes = BddKeptScenes.Folder(Path.Combine(_root, "recordings"), Path.Combine(_root, "Features", "how.video.md"));
        Assert.EndsWith(Path.Combine("recordings", "how.scenes"), scenes, StringComparison.Ordinal);
        Assert.Equal(BddSceneState.NotRecorded, BddKeptScenes.State(scenes, 1, "A"));

        // A timeline without its video is no recording: the scene ran but nothing recorded it.
        Save(scenes, 1, "A", BddStepResultStatus.Passed, video: false);
        Assert.Equal(BddSceneState.NotRecorded, BddKeptScenes.State(scenes, 1, "A"));

        Save(scenes, 1, "A", BddStepResultStatus.Passed);
        Assert.Equal(BddSceneState.Recorded, BddKeptScenes.State(scenes, 1, "A"));
        Assert.Equal(BddSceneState.OutOfDate, BddKeptScenes.State(scenes, 1, "B"));
        Assert.Equal(BddSceneState.NotRecorded, BddKeptScenes.State(scenes, 2, "A"));

        Save(scenes, 1, "A", BddStepResultStatus.Failed);
        Assert.Equal(BddSceneState.Failed, BddKeptScenes.State(scenes, 1, "A"));

        // Made before scenes were kept: no hash, so it is not known to be current.
        Save(scenes, 1, null, BddStepResultStatus.Passed);
        Assert.Equal(BddSceneState.OutOfDate, BddKeptScenes.State(scenes, 1, "A"));
    }

    private static string Hash(BddVideoScenePlan plan) => BddKeptScenes.Hash(plan, null, TimeSpan.FromMilliseconds(600));

    private IReadOnlyList<BddVideoScenePlan> Plans(string firstLine)
    {
        var script = Path.Combine(_root, "Features", "how.video.md");
        var text =
            $"""
            # Video: How it works

            ## Scene: Opening

            Scenario: Features/todo.feature.md#Opening

            | Step | Say |
            | --- | --- |
            | When I tap Sync | {firstLine} |

            ## Scene: Adding

            Scenario: Features/todo.feature.md#Adding

            | Step | Say |
            | --- | --- |
            | When I tap Add | Tap Add. |
            """;
        var check = BddVideoScriptCheck.Check(text, script);
        Assert.True(check.Success, string.Join(Environment.NewLine, check.Diagnostics));
        return check.Scenes;
    }

    private static void Save(string scenes, int number, string? hash, BddStepResultStatus status, bool video = true)
    {
        var folder = BddKeptScenes.SceneFolder(scenes, number);
        Directory.CreateDirectory(folder);
        if (video)
        {
            File.WriteAllText(Path.Combine(folder, "video.mp4"), "not a video");
        }
        else
        {
            File.Delete(Path.Combine(folder, "video.mp4"));
        }

        new BddSceneTimeline(number, "Scene", null, video ? "video.mp4" : null, 5, status, [], [], [], Hash: hash)
            .Save(Path.Combine(folder, BddSceneTimeline.FileName));
    }
}
