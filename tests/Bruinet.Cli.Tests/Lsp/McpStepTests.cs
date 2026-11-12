using System.Text.Json.Nodes;
using Xunit;

namespace Bruinet.Bdd.LanguageServer.Tests;

/// <summary>
/// The MCP server's Step-through (AI4): step_start and step_move, each answering where the run paused or how
/// it ended. The counter suite of <see cref="StepTests"/>.
/// </summary>
public sealed class McpStepTests : IDisposable
{
    private readonly string _solution = Path.Combine(Path.GetTempPath(), "Bruinet", "McpStepTests", Guid.NewGuid().ToString("N"));
    private readonly BddMcpServer _server;

    public McpStepTests()
    {
        Directory.CreateDirectory(Path.Combine(Suite, "Features"));
        File.WriteAllText(Path.Combine(_solution, "Counter.sln"), string.Empty);
        File.WriteAllText(Path.Combine(Suite, "uat.config.md"), $"""
            # UAT Config

            ## Runtime

            | Field | Value |
            | --- | --- |
            | Target | MAUI |
            | Fixture | {nameof(StepCounterFixture)} |

            ## Assemblies

            | Kind | Assembly |
            | --- | --- |
            | Pages | {typeof(StepCounterFixture).Assembly.Location} |
            """);
        File.WriteAllText(Feature, """
            # Feature: Counter

            ## Scenario: Counts

            ```gherkin
            Given the counter counts
            When the counter counts
            Then the counter has counted 3 times
            ```
            """);
        _server = new BddMcpServer(_solution);
    }

    private string Suite => Path.Combine(_solution, typeof(StepCounterFixture).Assembly.GetName().Name!);

    private string Feature => Path.Combine(Suite, "Features", "counter.feature.md");

    public void Dispose()
    {
        _server.Dispose();
        try
        {
            Directory.Delete(_solution, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A load context not collected yet; the temp folder is cleaned by the OS.
        }
    }

    [Fact]
    public void AStepThrough_PausesBeforeEachStep_SaysWhenOneFailed_AndHowItEnded()
    {
        var first = Answer(Call("step_start", new JsonObject { ["path"] = Feature, ["line"] = 3 }));
        var second = Answer(Call("step_move", new JsonObject { ["move"] = "over" }));
        var third = Answer(Call("step_move", new JsonObject { ["move"] = "over" }));
        var ended = Answer(Call("step_move", new JsonObject { ["move"] = "over" }));

        Assert.Equal((true, 6, "the counter counts"), ((bool)first["paused"]!, (int)first["line"]!, (string?)first["text"]));
        Assert.Equal(7, (int)second["line"]!);
        Assert.Equal((8, null), ((int)third["line"]!, third["lastStepFailed"]));
        Assert.Equal((true, "failed"), ((bool)ended["ended"]!, (string?)ended["status"]));
        Assert.Contains("counted 2 times, not 3", (string?)ended["message"]);
    }

    [Fact]
    public void APausedStepThrough_TakesAScreenshot_AndAStopEndsIt()
    {
        Call("step_start", new JsonObject { ["path"] = Feature, ["line"] = 3 });

        var shot = Answer(Call("step_screenshot", new JsonObject()));
        var stopped = Answer(Call("step_move", new JsonObject { ["move"] = "stop" }));

        Assert.True(File.Exists(Path.Combine(_solution, (string)shot["screenshot"]!)));
        Assert.Equal((true, "stopped"), ((bool)stopped["ended"]!, (string?)stopped["status"]));
        Assert.Contains("No Step-through run is going", Text(Call("step_move", new JsonObject { ["move"] = "over" })));
    }

    [Fact]
    public void ThePageState_OfASuiteWithNoPages_SaysNoPageIsCurrent()
    {
        Call("step_start", new JsonObject { ["path"] = Feature, ["line"] = 3 });

        var state = Answer(Call("step_state", new JsonObject()));
        Call("step_move", new JsonObject { ["move"] = "stop" });

        Assert.Empty(state["controls"]!.AsArray());
        Assert.Contains("No page is current", (string?)state["reason"]);
    }

    [Fact]
    public void AStart_AtAStepsLine_OpensItsScenario()
    {
        var paused = Answer(Call("step_start", new JsonObject { ["path"] = Feature, ["line"] = 6 }));
        Call("step_move", new JsonObject { ["move"] = "stop" });

        Assert.True((bool)paused["paused"]!);
    }

    [Fact]
    public void AStart_AboveTheFirstScenario_IsAToolError()
    {
        var result = Call("step_start", new JsonObject { ["path"] = Feature, ["line"] = 1 });

        Assert.True((bool)result["isError"]!);
        Assert.Contains("no scenario at line 1", Text(result));
    }

    [Fact]
    public void AMove_WithNoStepThrough_IsAToolError()
    {
        var result = Call("step_move", new JsonObject { ["move"] = "over" });

        Assert.True((bool)result["isError"]!);
        Assert.Contains("No Step-through run is going", Text(result));
    }

    private JsonObject Call(string tool, JsonObject arguments)
        => _server.Handle(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = arguments },
        }.ToJsonString())!["result"]!.AsObject();

    // A head without pixels (bUnit) saves the page's markup: step_screenshot gives the text it shows, not 100 KB of attributes.
    [Fact]
    public void AScreenshotsMarkup_IsGivenAsTheTextItShows()
    {
        var text = BddMcpServer.VisibleText("<div class=\"x\"><style>.a{}</style><span>45</span> &amp; <b>% to hit</b><script>var a=1;</script></div>"
            + "<div id=\"blazor-error-ui\" data-nosnippet>An unhandled error has occurred. <a href=\".\">Reload</a></div>");

        Assert.Equal("45\n&\n% to hit", text);
    }

    private static string Text(JsonObject result) => (string)result["content"]![0]!["text"]!;

    private static JsonNode Answer(JsonObject result) => JsonNode.Parse(Text(result))!;
}
