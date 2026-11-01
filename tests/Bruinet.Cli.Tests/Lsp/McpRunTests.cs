using System.Text.Json.Nodes;
using Xunit;

namespace Bruinet.Bdd.LanguageServer.Tests;

/// <summary>
/// The MCP server's run_scenario and last_runs (AI1): an agent runs a scenario on the suite's runner
/// and reads how it went, as the preview's Run does. The counter suite of <see cref="StepTests"/>.
/// </summary>
public sealed class McpRunTests : IDisposable
{
    private readonly string _solution = Path.Combine(Path.GetTempPath(), "Bruinet", "McpRunTests", Guid.NewGuid().ToString("N"));
    private readonly BddMcpServer _server;

    public McpRunTests()
    {
        var features = Path.Combine(Suite, "Features");
        Directory.CreateDirectory(features);
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
            Then the counter has counted 2 times
            ```

            ## Scenario: Fails

            ```gherkin
            Given the counter counts
            Then the counter has counted 5 times
            Then the counter has counted 1 times
            ```
            """);
        _server = new BddMcpServer(_solution);
    }

    // Named as its assembly, as a built suite is: results carry the assembly's name as their suite.
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
    public void RunScenario_OfOneHeading_RunsIt_AndSaysItPassed()
    {
        var result = Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 3 });

        Assert.False((bool)result["isError"]!);
        var run = Answer(result);
        Assert.Equal("passed", (string?)run["status"]);
        var scenario = Assert.Single(run["scenarios"]!.AsArray())!;
        Assert.Equal(("Counts", "passed"), ((string?)scenario["scenario"], (string?)scenario["status"]));
        Assert.Null(scenario["failedStep"]);
    }

    [Fact]
    public void RunScenario_OfTheFile_SaysWhichFailed_AtWhichStep_AndWhy()
    {
        var run = Answer(Call("run_scenario", new JsonObject { ["path"] = Feature }));

        Assert.Equal("failed", (string?)run["status"]);
        var failed = run["scenarios"]!.AsArray()[1]!;
        Assert.Equal(("Fails", "failed"), ((string?)failed["scenario"], (string?)failed["status"]));
        Assert.Equal(15, (int?)failed["failedStep"]!["line"]);
        Assert.Equal("the counter has counted 5 times", (string?)failed["failedStep"]!["text"]);
        Assert.Contains("counted 1 times, not 5", (string?)failed["message"]);
    }

    [Fact]
    public void LastRuns_AfterARun_GiveEachScenariosNewestResult()
    {
        Call("run_scenario", new JsonObject { ["path"] = Feature });

        var runs = Answer(Call("last_runs", new JsonObject { ["path"] = Feature })).AsArray();

        Assert.Equal(["Counts", "Fails"], runs.Select(run => (string?)run!["scenario"]));
        Assert.Equal("Failed", (string?)runs[1]!["status"], ignoreCase: true);
    }

    [Fact]
    public void RunScenario_AtALineWithNoHeading_IsAToolError_SayingSo()
    {
        var result = Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 6 });

        Assert.True((bool)result["isError"]!);
        Assert.Contains("no scenario heading at line 6", Text(result));
    }

    private JsonObject Call(string tool, JsonObject arguments)
    {
        var response = _server.Handle(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = arguments },
        }.ToJsonString())!;
        return response["result"]!.AsObject();
    }

    private static string Text(JsonObject result) => (string)result["content"]![0]!["text"]!;

    private static JsonNode Answer(JsonObject result) => JsonNode.Parse(Text(result))!;
}
