using System.Text.Json.Nodes;
using Xunit;

namespace Bruinet.Bdd.LanguageServer.Tests;

/// <summary>
/// The Bruinet MCP server over the Todo sample, one JSON-RPC line at a time as an agent sends them.
/// Needs the Todo sample built (<c>dotnet build samples/Todo/Bruinet.Samples.Todo.slnx</c>).
/// </summary>
// Not beside LanguageServerTests: both set BRUINET_FFMPEG, which the whole process shares.
[Collection(FfmpegVariable.Name)]
public sealed class McpServerTests : IDisposable
{
    private readonly BddMcpServer _server = new(TodoPaths.Todo);

    public void Dispose() => _server.Dispose();

    [Fact]
    public async Task ASession_Initializes_ListsItsTools_AndAnswersOneLinePerRequest()
    {
        var input = new StringReader(string.Join('\n',
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":2,"method":"tools/list"}"""));
        var output = new StringWriter();

        await _server.RunAsync(input, output);

        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonNode.Parse(line)!).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal("bruinet-bdd", (string?)lines[0]["result"]!["serverInfo"]!["name"]);
        Assert.NotNull(lines[0]["result"]!["capabilities"]!["tools"]);
        Assert.Equal(
            ["list_suites", "list_steps", "list_pages", "list_test_sets", "validate_scenario", "find_step_usages", "record_video", "review_page", "list_scraped_projects", "get_scraper_progress", "start_recording", "capture_page", "stop_recording", "list_recorded_pages", "get_page_snapshot", "find_similar_elements", "search_recordings", "list_controls", "detect_patterns", "control_brief", "match_controls", "diff_page", "suggest_controls", "scraped_header", "write_code", "list_recorded_actions", "resolve_actions", "test_brief", "draft_test", "run_test", "check_code", "explore_app", "draft_page_objects", "verify_page_objects", "update_scraper_config", "create_scraped_project", "list_states", "set_state", "run_scenario", "last_runs", "step_start", "step_move", "step_state", "step_screenshot", "coverage", "where_is", "get_failure", "http_calls", "failing", "step_log", "tests_affected", "rename_step", "rename_page_object", "delete_results"],
            lines[1]["result"]!["tools"]!.AsArray().Select(tool => (string?)tool!["name"]));
    }

    [Fact]
    public void ListSteps_GivesThePhrases_HoldingEveryWordOfTheQuery()
    {
        var text = CallText("list_steps", new JsonObject { ["suite"] = "Bruinet.Samples.Todo.Uat", ["query"] = "tap" });

        Assert.Contains("\"phrase\":\"I tap {control}\"", text);
        Assert.DoesNotContain("\"phrase\":\"I am on the {page} page\"", text);
    }

    [Fact]
    public void ListPages_GivesThePagesAndTheirControls()
    {
        var text = CallText("list_pages", new JsonObject { ["suite"] = "Bruinet.Samples.Todo.Uat" });

        Assert.Contains("\"page\":\"Todo List\"", text);
        Assert.Contains("\"name\":\"Sync\"", text);
    }

    [Fact]
    public void ValidateScenario_ChecksADraft_WithTheGatesWords()
    {
        var path = Path.Combine(TodoPaths.Project, "Features", "TOD.99-draft.feature.md");

        var text = CallText("validate_scenario", new JsonObject
        {
            ["path"] = path,
            ["text"] = "# Feature: TOD.99 Draft\n\n## Scenario: TOD.99.1 A draft\n\n```gherkin\nGiven I am on the Todo List page\nWhen I polish the list\n```\n",
        });

        Assert.Contains("\"valid\":false", text);
        Assert.Contains("\"line\":7,\"severity\":\"error\",\"code\":\"UATB001\"", text);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void RecordVideo_WithoutFfmpeg_IsAToolError_SayingHowToGetIt()
    {
        var original = Environment.GetEnvironmentVariable("BRUINET_FFMPEG");
        Environment.SetEnvironmentVariable("BRUINET_FFMPEG", Path.Combine(Path.GetTempPath(), "no-ffmpeg-here.exe"));
        try
        {
            var result = Call("record_video", new JsonObject { ["path"] = Path.Combine(TodoPaths.Project, "VideoScripts", "how-todo-works.video.md") });

            Assert.True((bool)result["isError"]!);
            Assert.Contains("winget install", (string?)result["content"]![0]!["text"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BRUINET_FFMPEG", original);
        }
    }

    [Fact]
    public void ReviewPage_ShowsEachVideo_WithCaptionsChaptersAndStepResults()
    {
        var folder = Directory.CreateTempSubdirectory("bruinet-review-").FullName;
        try
        {
            var video = Path.Combine(folder, "how-todo-works-20260929-101500.mp4");
            File.WriteAllBytes(video, []);
            File.WriteAllText(Path.ChangeExtension(video, ".srt"), "1\n00:00:01,000 --> 00:00:02,500\nYou tap Sync.\n\n");
            new Bruinet.Narrator.BddVideoTimeline("How the Todo app works",
            [
                new Bruinet.Narrator.BddSceneTimeline(1, "Opening the app", "Features/TOD.01-see-my-todos.feature.md#TOD.01.1", null, 12, BddStepResultStatus.Failed,
                    [
                        new Bruinet.Narrator.BddTimedStep(15, "Given I am on the Todo List page", 1.5, 2, BddStepResultStatus.Passed),
                        new Bruinet.Narrator.BddTimedStep(22, "When I tap Sync", 64, 66, BddStepResultStatus.Failed, "Sync is gone."),
                    ],
                    [], []),
            ]).Save(Path.ChangeExtension(video, ".timeline.json"));

            var text = CallText("review_page", new JsonObject
            {
                ["title"] = "TOD.12 Archive a todo",
                ["summary"] = "Added Archive to the detail page.",
                ["changes"] = new JsonArray("`TodoDetailPage.xaml`: an Archive button", "TOD.12-archive.feature.md: two scenarios"),
                ["videos"] = new JsonArray(video),
            });
            var page = (string)JsonNode.Parse(text)!["page"]!;
            var html = File.ReadAllText(page);

            Assert.Equal(folder, Path.GetDirectoryName(page));
            Assert.Contains("<h1>TOD.12 Archive a todo</h1>", html);
            Assert.Contains("<li><code>TodoDetailPage.xaml</code>: an Archive button</li>", html);
            Assert.Contains("src=\"how-todo-works-20260929-101500.mp4\"", html);
            Assert.Contains("<track kind=\"captions\" srclang=\"en\" label=\"Captions\" default src=\"how-todo-works-20260929-101500.vtt\">", html);
            Assert.Contains("data-at=\"64\">1:04</a> When I tap Sync<div class=\"message\">Sync is gone.</div>", html);
            Assert.Contains("data-status=\"failed\">failed</span>", html);
            Assert.StartsWith("WEBVTT\n\n1\n00:00:01.000 --> 00:00:02.500", File.ReadAllText(Path.ChangeExtension(video, ".vtt")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AnUnknownSuite_IsAToolError_SayingWhichSuitesThereAre()
    {
        var result = Call("list_steps", new JsonObject { ["suite"] = "Nope" });

        Assert.True((bool)result["isError"]!);
        Assert.Contains("The suites are", (string?)result["content"]![0]!["text"]);
    }

    private string CallText(string tool, JsonObject arguments)
    {
        var result = Call(tool, arguments);
        Assert.False((bool)result["isError"]!, (string?)result["content"]![0]!["text"]);
        return (string)result["content"]![0]!["text"]!;
    }

    private JsonNode Call(string tool, JsonObject arguments)
        => _server.Handle(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 7,
            ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = arguments },
        }.ToJsonString())!["result"]!;
}
