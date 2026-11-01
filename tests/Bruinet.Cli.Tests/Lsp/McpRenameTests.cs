using System.Text.Json.Nodes;
using Xunit;

namespace Bruinet.Bdd.LanguageServer.Tests;

/// <summary>
/// The MCP server's rename_step (AI5): the language server's rename of a callable step, as edits an
/// agent reads first and applies after. The counter suite of <see cref="StepTests"/>.
/// </summary>
public sealed class McpRenameTests : IDisposable
{
    private readonly string _solution = Path.Combine(Path.GetTempPath(), "Bruinet", "McpRenameTests", Guid.NewGuid().ToString("N"));
    private readonly BddMcpServer _server;

    public McpRenameTests()
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

            ## Scenario: Counts twice

            ```gherkin
            Given the counter counts twice
            Then the counter has counted 2 times
            ```
            """);
        File.WriteAllText(Steps, """
            # Steps: Counter

            ## Step: the counter counts twice

            ```gherkin
            Given the counter counts
            When the counter counts
            ```
            """);
        _server = new BddMcpServer(_solution);
    }

    private string Suite => Path.Combine(_solution, typeof(StepCounterFixture).Assembly.GetName().Name!);

    private string Feature => Path.Combine(Suite, "Features", "counter.feature.md");

    private string Steps => Path.Combine(Suite, "Features", "Counter.steps.md");

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
    public void RenameStep_WithoutApply_GivesTheEdits_AndWritesNothing()
    {
        var answer = Answer(Call("rename_step", new JsonObject { ["path"] = Steps, ["line"] = 3, ["newPhrase"] = "the counter counts two times" }));

        Assert.False((bool)answer["applied"]!);
        var edits = answer["files"]!.AsArray().SelectMany(file => file!["edits"]!.AsArray()).Select(edit => ((string?)edit!["before"], (string?)edit["after"])).ToList();
        Assert.Contains(("## Step: the counter counts twice", "## Step: the counter counts two times"), edits);
        Assert.Contains(("Given the counter counts twice", "Given the counter counts two times"), edits);
        Assert.Contains("## Step: the counter counts twice", File.ReadAllText(Steps));
    }

    [Fact]
    public void RenameStep_Applied_WritesTheHeadingAndEveryCall_AndTheyStillBind()
    {
        var answer = Answer(Call("rename_step", new JsonObject { ["path"] = Feature, ["line"] = 6, ["newPhrase"] = "the counter counts two times", ["apply"] = true }));

        Assert.True((bool)answer["applied"]!, answer.ToJsonString());
        Assert.Contains("## Step: the counter counts two times", File.ReadAllText(Steps));
        Assert.Contains("Given the counter counts two times", File.ReadAllText(Feature));
    }

    [Fact]
    public void RenameStep_ThatDropsAPlaceholder_OrAtALineWithNoStep_IsAToolError()
    {
        var result = Call("rename_step", new JsonObject { ["path"] = Feature, ["line"] = 1, ["newPhrase"] = "anything" });

        Assert.True((bool)result["isError"]!);
        Assert.Contains("not a '## Step:' heading or a call", Text(result));
    }

    private JsonObject Call(string tool, JsonObject arguments)
        => _server.Handle(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = arguments },
        }.ToJsonString())!["result"]!.AsObject();

    private static string Text(JsonObject result) => (string)result["content"]![0]!["text"]!;

    private static JsonNode Answer(JsonObject result) => JsonNode.Parse(Text(result))!;
}
