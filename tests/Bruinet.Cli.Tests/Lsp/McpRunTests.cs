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

            ## Skip Rules

            | Tag | EnvironmentVariable |
            | --- | --- |
            | needs-the-lab | BRUINET_TEST_LAB_NOT_SET |
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

            ## Scenario: Starts

            ```gherkin
            Given the counter counts
            Then the counter was started
            ```

            ## Scenario: Traces

            ```gherkin
            Given the counter counts
            When the counter taps Plus
            Then the counter has counted 5 times
            ```

            ## Scenario: Parts

            ```gherkin
            Given the counter counts
            When the counter taps the key the flag says
            Then the flag is down
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

        Assert.Equal(["Complains", "Counts", "Fails", "Parts", "Starts", "Traces"], runs.Select(run => (string?)run!["scenario"]));
        Assert.Equal("Failed", (string?)runs[2]!["status"], ignoreCase: true);
    }

    [Fact]
    public void RunScenario_OfAFailure_GivesTheKind_TheBinding_AndTheFrame()
    {
        var failed = Answer(Call("run_scenario", new JsonObject { ["path"] = Feature }))["scenarios"]![1]!;

        Assert.Equal(("Fails", 15), ((string?)failed["scenario"], (int?)failed["failedStep"]!["line"]));
        Assert.Equal("assertion", (string?)failed["failure"]!["kind"]);
        Assert.Contains(failed["failure"]!["frames"]!.AsArray(), frame => ((string?)frame)!.StartsWith("StepCounterFixture.HasCounted", StringComparison.Ordinal));
    }

    [Fact]
    public void Failing_AfterARun_NamesOnlyTheFailedScenarios_InOneCall()
    {
        Call("run_scenario", new JsonObject { ["path"] = Feature });

        var answer = Answer(Call("failing", new JsonObject()));

        Assert.Equal(2, (int?)answer["count"]);
        var failed = answer["failing"]!.AsArray().Single(item => (string?)item!["scenario"] == "Fails")!;
        Assert.Equal(("Fails", "Features/counter.feature.md#Fails", "assertion"), ((string?)failed["scenario"], (string?)failed["address"], (string?)failed["kind"]));
    }

    [Fact]
    public void GetTrace_GivesWhatTheAppSaidAtItsStep_OnlyFromTheNewestRun()
    {
        Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 19 });
        Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 19 });

        var step = Answer(Call("get_trace", new JsonObject { ["path"] = Feature, ["line"] = 19, ["step"] = 23 }));

        var said = Assert.Single(step["appEvents"]!.AsArray())!;
        Assert.Equal(("warning", "counter", "Counted too often"), ((string?)said["level"], (string?)said["source"], (string?)said["message"]));
    }

    [Fact]
    public void RunScenario_OfAScenarioTheSuiteSkipsHere_SaysSkipped_WithWhy_AsTheGateDoes()
    {
        var feature = Path.Combine(Suite, "Features", "lab.feature.md");
        File.WriteAllText(feature, """
            # Feature: Lab

            @needs-the-lab
            ## Scenario: In the lab

            ```gherkin
            Given the counter counts
            Then the counter has counted 9 times
            ```
            """);

        var run = Answer(Call("run_scenario", new JsonObject { ["path"] = feature }));

        Assert.Equal("skipped", (string?)run["status"]);
        var skipped = Assert.Single(run["scenarios"]!.AsArray())!;
        Assert.Equal("skipped", (string?)skipped["status"]);
        Assert.Contains("BRUINET_TEST_LAB_NOT_SET", (string?)skipped["message"]);
    }

    [Fact]
    public void RunScenario_AtAStepsLine_RunsItsScenario()
    {
        var run = Answer(Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 6 }));

        Assert.Equal("Counts", (string?)Assert.Single(run["scenarios"]!.AsArray())!["scenario"]);
    }

    [Fact]
    public void RunScenario_AboveTheFirstScenario_IsAToolError_SayingSo()
    {
        var result = Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 1 });

        Assert.True((bool)result["isError"]!);
        Assert.Contains("no scenario at line 1", Text(result));
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(3, 4)]
    [InlineData(4, 4)]
    [InlineData(5, 4)]
    [InlineData(6, 4)]
    [InlineData(7, 9)]
    [InlineData(8, 9)]
    [InlineData(9, 9)]
    [InlineData(10, 9)]
    public void AScenariosLines_AreItsHeading_ItsTags_AndWhatFollows(int line, int? heading)
    {
        string[] lines = ["# Feature", "", "@one", "## Scenario: One", "Given a step", "", "@two", "@three", "## Scenario: Two", "Given a step"];

        Assert.Equal(heading, Bruinet.Bdd.LanguageServer.BddLanguageServer.HeadingOf(lines, new HashSet<int> { 4, 9 }, line));
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
        // The state as list_states writes it, its placeholder not filled in (the benchmark's T4 runs passed it so).
        Assert.Contains("still has {times}", Refused(new JsonObject { ["state"] = "the counter has already counted {times} times" }), StringComparison.Ordinal);
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

    [Fact]
    public void RunScenario_AFixtureWithAnAsyncStart_IsStartedFirst()
    {
        var run = Answer(Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 26 }));

        Assert.Equal("passed", (string)run["scenarios"]![0]!["status"]!);
    }

    [Fact]
    public void GetTrace_AfterAFailedRun_GivesEachStepsCalls_AndThePageAfterTheFailure()
    {
        Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 33 });

        var trace = Answer(Call("get_trace", new JsonObject { ["path"] = Feature, ["line"] = 33 }));

        var steps = trace["steps"]!.AsArray();
        Assert.Equal([36, 37, 38], steps.Select(step => (int)step!["line"]!));
        Assert.Equal("Counter.Plus.Tap 1200ms x3", (string)steps[1]!["slowest"]!);
        var failed = trace["failed"]!;
        Assert.Equal(38, (int)failed["line"]!);
        Assert.NotNull(failed["pageAfter"]!["reason"]);
    }

    [Fact]
    public void GetTrace_AStep_GivesItInFull()
    {
        Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 33 });

        var step = Answer(Call("get_trace", new JsonObject { ["path"] = Feature, ["line"] = 33, ["step"] = 37 }));

        var call = Assert.Single(step["calls"]!.AsArray())!;
        Assert.Equal(("Plus", 3), ((string)call["control"]!, (int)call["attempts"]!));
    }

    [Fact]
    public void GetTrace_APassRunWithTraceAll_IsTraced()
    {
        Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 3, ["trace"] = "all" });

        var trace = Answer(Call("get_trace", new JsonObject { ["path"] = Feature, ["line"] = 3 }));

        Assert.Equal("passed", (string)trace["status"]!);
        Assert.Equal(3, trace["steps"]!.AsArray().Count);
    }

    [Fact]
    public void GetTrace_ARunWithTraceOff_HasNoTrace_AndSaysWhy()
    {
        Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 3, ["trace"] = "off" });

        var result = Call("get_trace", new JsonObject { ["path"] = Feature, ["line"] = 3 });

        Assert.True((bool)result["isError"]!);
        Assert.Contains("trace: off", Text(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Why_AfterAPassThenAFailure_NamesTheFirstStepWhereTheyPart_AndItsKind()
    {
        File.Delete(StepCounterFixture.WhyFlag);
        Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 41 });
        Directory.CreateDirectory(Path.GetDirectoryName(StepCounterFixture.WhyFlag)!);
        File.WriteAllText(StepCounterFixture.WhyFlag, "up");
        try
        {
            var run = Answer(Call("run_scenario", new JsonObject { ["path"] = Feature, ["line"] = 41 }));

            // The failure carries its short why already: no call more.
            Assert.Equal("page-changed", (string)run["scenarios"]![0]!["sinceGreen"]!["kind"]!);
            var why = Answer(Call("why", new JsonObject { ["path"] = Feature, ["line"] = 41 }));

            Assert.Equal(("page-changed", 45), ((string)why["kind"]!, (int)why["step"]!["line"]!));
            Assert.Contains("drove: Minus.Tap, which the green run did not", why["differences"]!.AsArray().Select(item => (string)item!));

            // The command line gives the same, as text.
            var output = new StringWriter();
            var exit = await Bruinet.Cli.BruinetCommand.RunAsync(["why", $"{Feature}:41", "--solution", _solution], new Bruinet.Cli.CliIo(TextReader.Null, output, TextWriter.Null, _solution, Interactive: false));
            Assert.Equal(0, exit);
            Assert.StartsWith("Parts: page-changed at line 45", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(StepCounterFixture.WhyFlag);
        }
    }

    [Fact]
    public void Why_WithNoFailureYet_RunsItFirst_AndWithNoGreenRunBefore_SaysSo()
    {
        var result = Call("why", new JsonObject { ["path"] = Feature, ["line"] = 11 });

        Assert.True((bool)result["isError"]!);
        Assert.Contains("no green run", Text(result), StringComparison.Ordinal);
    }

    private static string Text(JsonObject result) => (string)result["content"]![0]!["text"]!;

    private static JsonNode Answer(JsonObject result) => JsonNode.Parse(Text(result))!;
}
