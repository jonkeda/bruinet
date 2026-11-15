namespace Bruinet.Narrator.Tests;

public sealed class BddVideoScriptParserTests
{
    private const string Script = """
        # Video: How it works

        | Field | Value |
        | --- | --- |
        | Voice | Microsoft Jenny |
        | Beat | 400 |

        ## Scene: Opening

        Scenario: Features/A.feature.md#A.1-Opens

        This is the app.
        It keeps your todos.

        | Step | Say |
        | --- | --- |
        | Given I am on the list | The app opens on your list. |
        | When I tap Sync | |

        That was it.

        ## Notes

        Not narrated.
        """;

    [Fact]
    public void A_script_has_a_title_fields_and_scenes()
    {
        var parse = BddVideoScriptParser.Parse(Script, "how.video.md");

        Assert.Empty(parse.Diagnostics);
        var script = parse.Script!;
        Assert.Equal("How it works", script.Title);
        Assert.Equal("Microsoft Jenny", script.Voice);
        Assert.Equal(TimeSpan.FromMilliseconds(400), script.Beat);
        var scene = Assert.Single(script.Scenes);
        Assert.Equal("Opening", scene.Name);
        Assert.Equal("Features/A.feature.md#A.1-Opens", scene.Scenario);
        Assert.Equal("This is the app. It keeps your todos.", scene.Intro);
        Assert.Equal("That was it.", scene.Outro);
        var line = Assert.Single(scene.Lines);
        Assert.Equal(("Given I am on the list", "The app opens on your list.", 17), (line.Step, line.Say, line.Line));
    }

    [Fact]
    public void Without_a_title_it_is_not_a_script()
    {
        var parse = BddVideoScriptParser.Parse("## Scene: A\n", "x.video.md");

        Assert.Null(parse.Script);
        Assert.Contains(parse.Diagnostics, diagnostic => diagnostic.Code == BddVideoScriptParser.NoTitleCode);
    }

    [Fact]
    public void Touches_and_their_colour_are_read_and_a_value_that_is_neither_is_an_error()
    {
        var parse = BddVideoScriptParser.Parse("""
            # Video: V
            | Field | Value |
            | --- | --- |
            | Touches | outline-only |
            | TouchColor | #00a0ff |
            """, "x.video.md");
        var wrong = BddVideoScriptParser.Parse("""
            # Video: V
            | Field | Value |
            | --- | --- |
            | Touches | sometimes |
            | TouchColor | red |
            """, "x.video.md");

        Assert.Empty(parse.Diagnostics);
        Assert.Equal(BddTouchMode.OutlineOnly, parse.Script!.Touches);
        Assert.Equal("#00A0FF", parse.Script.TouchColor);
        Assert.Equal([(BddVideoScriptParser.BadValueCode, 4), (BddVideoScriptParser.BadValueCode, 5)], wrong.Diagnostics.Select(diagnostic => (diagnostic.Code, diagnostic.Location.LineNumber)));
        Assert.Null(wrong.Script!.Touches);
        Assert.Null(wrong.Script.TouchColor);
    }

    [Fact]
    public void Pause_and_Say_are_read_and_a_value_out_of_range_is_an_error()
    {
        var parse = BddVideoScriptParser.Parse("""
            # Video: V
            | Field | Value |
            | --- | --- |
            | Pause | 900 |
            | Say | Before |
            """, "x.video.md");
        var wrong = BddVideoScriptParser.Parse("""
            # Video: V
            | Field | Value |
            | --- | --- |
            | Pause | 6000 |
            | Say | after |
            | Say | 1 |
            """, "x.video.md");
        var none = BddVideoScriptParser.Parse("# Video: V", "x.video.md");

        Assert.Empty(parse.Diagnostics);
        Assert.Equal(TimeSpan.FromMilliseconds(900), parse.Script!.Pause);
        Assert.Equal(BddSayMode.Before, parse.Script.Say);
        Assert.Equal(new BddScenePacing(TimeSpan.FromMilliseconds(900), BddSayMode.Before), BddScenePacing.Of(parse.Script));
        Assert.Equal([4, 5, 6], wrong.Diagnostics.Where(diagnostic => diagnostic.Code == BddVideoScriptParser.BadValueCode).Select(diagnostic => diagnostic.Location.LineNumber));
        Assert.Null(wrong.Script!.Pause);
        Assert.Null(wrong.Script.Say);
        Assert.Same(BddScenePacing.Default, BddScenePacing.Of(none.Script));
    }

    [Fact]
    public void Form_problems_are_at_their_lines()
    {
        var parse = BddVideoScriptParser.Parse("""
            # Video: V
            | Field | Value |
            | --- | --- |
            | Beat | slow |
            | Speed | 2 |
            ## Scene: S
            | Step | Line |
            """, "x.video.md");

        Assert.Equal(
            [(BddVideoScriptParser.BadValueCode, 4), (BddVideoScriptParser.UnknownFieldCode, 5), (BddVideoScriptParser.BadTableCode, 7), (BddVideoScriptParser.NoScenarioCode, 6)],
            parse.Diagnostics.Select(diagnostic => (diagnostic.Code, diagnostic.Location.LineNumber)));
    }
}

public sealed class BddVideoScriptCheckTests : IDisposable
{
    private readonly string _suite = Directory.CreateTempSubdirectory("bruinet-video-").FullName;

    public BddVideoScriptCheckTests()
    {
        File.WriteAllText(Path.Combine(_suite, BddFileFormat.ConfigFileName), "# UAT Config\n");
        Directory.CreateDirectory(Path.Combine(_suite, "Features"));
        File.WriteAllText(Path.Combine(_suite, "Features", "A.feature.md"), """
            # Feature: A

            ## Background

            ```gherkin
            Given I am on the list
            ```

            ## Scenario: A.1 Opens

            ```gherkin
            When I tap Sync
            Then the list shows "Buy bread"
            When I tap Sync
            ```
            """);
    }

    public void Dispose() => Directory.Delete(_suite, recursive: true);

    [Fact]
    public void A_scene_finds_its_scenario_and_the_steps_it_narrates()
    {
        var check = Check("""
            | Given I am on the list | The list. |
            | When I tap Sync | First sync. |
            | when i tap  sync | Second sync. |
            """);

        Assert.True(check.Success, string.Join("\n", check.Diagnostics));
        var scene = Assert.Single(check.Scenes);
        Assert.Equal("A.1 Opens", scene.Scenario.Name);
        Assert.Equal(Path.Combine(_suite, "Features", "A.feature.md"), scene.FeatureFile);
        Assert.Equal([(6, "The list."), (12, "First sync."), (14, "Second sync.")], scene.Says.OrderBy(say => say.Key).Select(say => (say.Key, say.Value.Say)));
    }

    [Fact]
    public void A_step_the_scenario_does_not_have_is_an_error_at_its_row()
    {
        var check = Check("| When I tap Save | Save. |");

        var diagnostic = Assert.Single(check.Diagnostics);
        Assert.Equal((BddVideoScriptCheck.NoStepCode, 8), (diagnostic.Code, diagnostic.Location.LineNumber));
    }

    [Fact]
    public void A_row_out_of_step_order_is_a_warning()
    {
        var check = Check("""
            | Then the list shows "Buy bread" | Bread. |
            | Given I am on the list | The list. |
            """);

        Assert.True(check.Success);
        Assert.Equal(BddVideoScriptCheck.OrderCode, Assert.Single(check.Diagnostics).Code);
    }

    [Theory]
    [InlineData("Features/B.feature.md#A.1-Opens", BddVideoScriptCheck.NoFileCode)]
    [InlineData("Features/A.feature.md#A.2-Closes", BddVideoScriptCheck.NoScenarioCode)]
    public void An_address_that_finds_nothing_is_an_error_at_its_line(string address, string code)
    {
        var check = Check(string.Empty, address);

        var diagnostic = Assert.Single(check.Diagnostics);
        Assert.Equal((code, 4), (diagnostic.Code, diagnostic.Location.LineNumber));
        Assert.Empty(check.Scenes);
    }

    [Fact]
    public void Without_a_config_above_it_nothing_can_be_found()
    {
        var check = BddVideoScriptCheck.Check("# Video: V\n## Scene: S\nScenario: Features/A.feature.md#A.1-Opens\n", Path.Combine(Path.GetTempPath(), "x.video.md"), configDirectory: null);

        Assert.Contains(check.Diagnostics, diagnostic => diagnostic.Code == BddVideoScriptCheck.NoConfigCode);
    }

    [Fact]
    public void The_Todo_video_checks_clean_against_the_Todo_features()
    {
        var uat = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples/Todo/tests/Bruinet.Samples.Todo.Uat"));
        var path = Path.Combine(uat, "VideoScripts", "how-todo-works.video.md");

        var check = BddVideoScriptCheck.Check(File.ReadAllText(path), path);

        Assert.True(check.Diagnostics.Count == 0, string.Join("\n", check.Diagnostics));
        Assert.Equal(11, check.Scenes.Count);
        Assert.Equal(uat, check.ConfigDirectory);
    }

    private BddVideoScriptCheck Check(string rows, string address = "Features/A.feature.md#A.1-Opens")
    {
        var text = $"# Video: V\n\n## Scene: S\nScenario: {address}\n\n| Step | Say |\n| --- | --- |\n{rows}\n";
        return BddVideoScriptCheck.Check(text, Path.Combine(_suite, "Features", "v.video.md"));
    }
}
