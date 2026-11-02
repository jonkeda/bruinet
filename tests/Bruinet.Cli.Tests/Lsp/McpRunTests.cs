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

            ## Scenario: Complains

            ```gherkin
            Given the counter counts
            When the counter complains
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
        Assert.Equal("Features/counter.feature.md#Counts", (string?)scenario["address"]);
        Assert.True((long?)scenario["durationMs"] >= 0);
        Assert.Null(scenario["failedStep"]);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("\"ten\"")]
    public void RunScenario_WithATimeoutThatIsNoWholeNumberOfMinutes_IsAToolError_AndTheServerGoesOn(string timeout)
    {
        var result = Call("run_scenario", JsonNode.Parse($$"""{"path":{{JsonValue.Create(Feature).ToJsonString()}},"timeoutMinutes":{{timeout}}}""")!.AsObject());

        Assert.True((bool)result["isError"]!);
        Assert.Contains("'timeoutMinutes' must be a whole number from 1 to 120", Text(result));
        Assert.False((bool)Call("last_runs", new JsonObject { ["path"] = Feature })["isError"]!);
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

        Assert.Equal(["Complains", "Counts", "Fails"], runs.Select(run => (string?)run!["scenario"]));
        Assert.Equal("Failed", (string?)runs[2]!["status"], ignoreCase: true);
    }

    [Fact]
    public void GetFailure_AfterAFailedRun_GivesTheKind_TheBinding_AndTheFrame_InFewWords()
    {
        var run = Answer(Call("run_scenario", new JsonObject { ["path"] = Feature }));
        var text = Text(Call("get_failure", new JsonObject { ["path"] = Feature, ["line"] = 11 }));
        var failure = JsonNode.Parse(text)!;

        Assert.True(run["scenarios"]![1]!["failure"] is not null, run.ToJsonString());
        Assert.Equal("assertion", (string?)run["scenarios"]![1]!["failure"]!["kind"]);
        Assert.Equal(("Fails", 15), ((string?)failure["scenario"], (int?)failure["failedStep"]!["line"]));
        Assert.Equal("assertion", (string?)failure["failure"]!["kind"]);
        Assert.Contains(failure["failure"]!["frames"]!.AsArray(), frame => ((string?)frame)!.StartsWith("StepCounterFixture.HasCounted", StringComparison.Ordinal));
        Assert.True(text.Length < 2000, $"{text.Length} characters: {text}");
    }

    [Fact]
    public void Failing_AfterARun_NamesOnlyTheFailedScenarios_InOneCall()
    {
        Call("run_scenario", new JsonObject { ["path"] = Feature });

        var answer = Answer(Call("failing", new JsonObject()));

        Assert.Equal(1, (int?)answer["count"]);
        var failed = answer["failing"]![0]!;
        Assert.Equal(("Fails", "Features/counter.feature.md#Fails", "assertion"), ((string?)failed["scenario"], (string?)failed["address"], (string?)failed["kind"]));
    }

    [Fact]
    public void StepLog_GivesWhatTheAppSaidAtItsStep_OnlyFromTheNewestRun()
    {
        Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 19 });
        Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 19 });

        var log = Answer(Call("step_log", new JsonObject { ["path"] = Feature, ["line"] = 19 }));

        var step = Assert.Single(log["steps"]!.AsArray())!;
        Assert.Equal(23, (int)step["line"]!);
        var said = Assert.Single(step["events"]!.AsArray())!;
        Assert.Equal(("warning", "counter", "Counted too often"), ((string?)said["level"], (string?)said["source"], (string?)said["message"]));
    }

    [Fact]
    public void GetFailure_OfAScenarioThatPassed_IsAToolError_SayingSo()
    {
        Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 3 });

        var result = Call("get_failure", new JsonObject { ["path"] = Feature, ["line"] = 3 });

        Assert.True((bool)result["isError"]!);
        Assert.Contains("passed", Text(result));
    }

    [Fact]
    public void RunScenario_AtALineWithNoHeading_IsAToolError_SayingSo()
    {
        var result = Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 6 });

        Assert.True((bool)result["isError"]!);
        Assert.Contains("no scenario heading at line 6", Text(result));
    }

    [Fact]
    public void RunScenario_WithReplay_GivesTheRunnerTheMode()
    {
        var replays = Path.Combine(Suite, "Features", "replay.feature.md");
        File.WriteAllText(replays, """
            # Feature: Replay

            ## Scenario: Replays

            ```gherkin
            Then the HTTP replay mode is Replay
            ```
            """);

        var replayed = Answer(Call("run_scenario", new JsonObject { ["path"] = replays, ["line"] = 3, ["replay"] = "replay" }));
        var plain = Answer(Call("run_scenario", new JsonObject { ["path"] = replays, ["line"] = 3 }));

        Assert.Equal("passed", (string)replayed["scenarios"]![0]!["status"]!);
        Assert.Equal("failed", (string)plain["scenarios"]![0]!["status"]!);
        Assert.Contains("record, replay or refresh", Text(Call("run_scenario", new JsonObject { ["path"] = replays, ["line"] = 3, ["replay"] = "often" })));
    }

    [Fact]
    public void HttpCalls_GivesTheScenariosCallsInItsNewestRun_WithTheMisses()
    {
        var log = Bruinet.Mocking.Http.HttpCallLog.PathFor(Bruinet.Mocking.Http.HttpReplaySession.PathFor(Path.Combine(Suite, "Recordings"), "Counts"));
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        File.WriteAllText(log, """
            {
              "Mode": "Replay",
              "Calls": [
                { "Method": "GET", "Url": "https://api.test/todos", "Status": 200, "Outcome": "matched", "Exchange": 0 },
                { "Method": "POST", "Url": "https://api.test/todos", "Status": 0, "Outcome": "missed", "Exchange": -1 }
              ],
              "Unused": [1],
              "Misses": [ "HTTP replay miss in Counts.json: POST /todos; nearest recorded is exchange 1 POST /todos: $.title is '\"Bread\"' where '\"Milk\"' was recorded." ]
            }
            """);

        var calls = Answer(Call("http_calls", new JsonObject { ["path"] = Feature, ["line"] = 3 }));

        Assert.Equal("Counts", (string)calls["scenario"]!);
        Assert.Equal(["matched", "missed"], calls["calls"]!.AsArray().Select(call => (string)call!["outcome"]!));
        Assert.Contains("Bread", (string)calls["misses"]![0]!, StringComparison.Ordinal);
        Assert.Equal(1, (int)calls["unusedExchanges"]![0]!);
        Assert.Contains("run it with run_scenario", Text(Call("http_calls", new JsonObject { ["path"] = Feature, ["line"] = 11 })), StringComparison.Ordinal);
    }

    [Fact]
    public void ListStates_GivesTheDeclaredStates_AndOnlyThem()
    {
        var states = Answer(Call("list_states", new JsonObject())).AsArray();

        Assert.Equal(["the counter has already counted {times} times"], states.Select(state => (string)state!["phrase"]!));
        Assert.Contains("HasAlreadyCounted", (string)states[0]!["from"]!, StringComparison.Ordinal);
    }

    [Fact]
    public void SetState_PutsTheAppInTheState_AndLeavesItPausedThere()
    {
        var paused = Answer(Call("set_state", new JsonObject { ["state"] = "the counter has already counted 3 times" }));
        var ended = Answer(Call("step_move", new JsonObject { ["move"] = "stop" }));

        Assert.True((bool)paused["paused"]!);
        Assert.Equal("the run pauses here", (string)paused["text"]!);
        Assert.Null(paused["lastStepFailed"]);
        Assert.Contains("give 'page'", (string)paused["next"]!, StringComparison.Ordinal);
        Assert.True((bool)ended["ended"]!);
    }

    [Fact]
    public void SetState_OfAGivenThatIsNoState_IsRefused()
    {
        var refused = Call("set_state", new JsonObject { ["state"] = "the counter counts" });

        Assert.True((bool)refused["isError"]!);
        Assert.Contains("is not a declared state", Text(refused), StringComparison.Ordinal);
    }

    [Fact]
    public void SetState_RefusesWhatCouldRunAnythingButTheState()
    {
        string Refused(JsonObject arguments)
        {
            var result = Call("set_state", arguments);
            Assert.True((bool)result["isError"]!);
            return Text(result);
        }

        Assert.Contains("must be one line", Refused(new JsonObject { ["state"] = "the counter has already counted 3 times", ["page"] = "Home\nWhen the counter counts" }), StringComparison.Ordinal);
        Assert.Contains("is not a test set", Refused(new JsonObject { ["state"] = "the counter has already counted 3 times", ["testData"] = "nothing-like-it" }), StringComparison.Ordinal);
        Assert.Contains("HasAlreadyCountedNine", Refused(new JsonObject { ["state"] = "the counter has already counted 9 times" }), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(Suite, ".bruinet")) && Directory.EnumerateFiles(Path.Combine(Suite, ".bruinet")).Any());
    }

    [Fact]
    public void AScenario_StartsFromAState_ByNamingItInAGiven()
    {
        var states = Path.Combine(Suite, "Features", "states.feature.md");
        File.WriteAllText(states, """
            # Feature: States

            ## Scenario: Starts from a state

            ```gherkin
            Given the counter has already counted 3 times
            When the counter counts
            Then the counter has counted 4 times
            ```
            """);

        var run = Answer(Call("run_scenario", new JsonObject { ["path"] = states, ["line"] = 3 }));

        Assert.Equal("passed", (string)run["scenarios"]![0]!["status"]!);
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
