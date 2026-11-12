using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bruinet.Bdd.Workspace;
using Bruinet.Narrator;
using Bruinet.Scraper.Service;

namespace Bruinet.Bdd.LanguageServer;

/// <summary>
/// The Bruinet MCP server (phase 4, Copilot): the same engine as the language server, as tools an
/// agent calls. It gives the suites, their steps, pages and test sets, and checks a scenario draft
/// with the gate's own words, so an agent writes steps that exist rather than inventing them. It
/// holds data and checks; it calls no model. MCP over stdio: one JSON-RPC message per line.
/// </summary>
public sealed partial class BddMcpServer : IDisposable
{
    private const string ProtocolVersion = "2025-06-18";

    // Readable text for an agent: quotes and non-ASCII kept as they are, not \u escapes; a null field left out (M4).
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly BddLanguageServer _server = new();

    // The Step-through events this server has answered with: one raised since is a pause a wait gives.
    private long _stepEventsSeen;
    private readonly string _solutionDir;
    private BddAppSources? _appSources;
    private DateTime _appSourcesAt;

    /// <summary>A server for the suites under <paramref name="solutionDir"/>.</summary>
    public BddMcpServer(string solutionDir)
    {
        _solutionDir = Path.GetFullPath(solutionDir);
        _server.Initialize(new InitializeParams(null, null, new InitializationOptions(_solutionDir)));
    }

    /// <summary>Serves until the input ends.</summary>
    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken = default)
    {
        while (await input.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Trim().Length == 0)
            {
                continue;
            }

            if (Handle(line) is { } response)
            {
                await output.WriteLineAsync(response.ToJsonString()).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>One message in, its response out; null for a notification.</summary>
    public JsonObject? Handle(string message)
    {
        JsonObject request;
        try
        {
            request = JsonNode.Parse(message)?.AsObject() ?? throw new JsonException("Not an object.");
        }
        catch (JsonException exception)
        {
            return Error(null, -32700, $"Not JSON: {exception.Message}");
        }

        var id = request["id"]?.DeepClone();
        var method = (string?)request["method"];
        if (id is null)
        {
            return null;
        }

        try
        {
            JsonNode result = method switch
            {
                "initialize" => new JsonObject
                {
                    ["protocolVersion"] = ProtocolVersion,
                    ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                    ["serverInfo"] = new JsonObject { ["name"] = "bruinet", ["version"] = typeof(BddMcpServer).Assembly.GetName().Version?.ToString() ?? "0" },
                    ["instructions"] = Instructions,
                },
                "ping" => new JsonObject(),
                "tools/list" => new JsonObject { ["tools"] = new JsonArray([.. Tools.Select(tool => (JsonNode)Listed(tool))]) },
                "tools/call" => Call((string?)request["params"]?["name"] ?? string.Empty, request["params"]?["arguments"] as JsonObject ?? []),
                _ => throw new McpException(-32601, $"No method '{method}'."),
            };
            return new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };
        }
        catch (McpException exception)
        {
            return Error(id, exception.Code, exception.Message);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _server.Dispose();

    // A tool's answer is text: JSON an agent reads, or the reason it could not answer (isError).
    private JsonObject Call(string name, JsonObject arguments)
    {
        try
        {
            object answer = name switch
            {
                "list_suites" => ListSuites(arguments),
                "list_steps" => ListSteps(Suite(arguments), arguments),
                "list_states" => ListStates(Suite(arguments), arguments),
                "set_state" => SetState(arguments),
                "list_pages" => ListPages(Suite(arguments), arguments),
                "list_test_sets" => Capped("testSets", TestSets(Suite(arguments)), arguments),
                "check_scenario" => Validate(Required(arguments, "path"), (string?)arguments["text"]),
                "draft_feature" => DraftFeature(Required(arguments, "path"), (string?)arguments["method"], (string?)arguments["feature"]),
                "find_similar_steps" => SimilarSteps(Suite(arguments), arguments),
                "find_step_usages" => Usages(arguments),
                "run_scenario" => RunScenario(arguments),
                "get_coverage" => Coverage(arguments),
                "step_start" => StepStart(arguments),
                "step_move" => StepMove(arguments),
                "step_state" => StepState(arguments),
                "step_screenshot" => _server.StepScreenshotAsync().GetAwaiter().GetResult() is var shot && shot.Path is { } taken ? Screenshot(taken) : throw new ToolProblem($"No screenshot: {shot.Reason}"),
                "list_results" => ListResults(arguments),
                "get_trace" => GetTrace(arguments),
                "diff_since_green" => DiffSinceGreen(arguments),
                "get_http_calls" => HttpCalls(arguments),
                "list_affected_tests" => TestsAffected(arguments),
                "rename_step" => RenameStep(arguments),
                "delete_results" => DeleteResults(arguments),
                "record_video" => RecordVideo(arguments),
                "write_review" => WriteReview(arguments),
                "scrape_rename_page_object" => Scraper(() => RenamePageObject(arguments)),
                "scrape_list_projects" => Scraper(() => Capped("projects", _server.ScraperProjects(), arguments)),
                "scrape_next" => Scraper(() => _server.Scraper.Progress((string?)arguments["project"])),
                "scrape_start" => Scraper(() => _server.Scraper.StartRecordingAsync((string?)arguments["project"], Required(arguments, "session"), (string?)arguments["url"], (bool?)arguments["autoCapture"] ?? true, actions: (bool?)arguments["actions"] ?? false).GetAwaiter().GetResult()),
                "scrape_list_events" => Scraper(() => Capped("events", _server.Scraper.RecordedActions((string?)arguments["project"], Required(arguments, "session")), arguments)),
                "scrape_resolve_events" => Scraper(() => ResolveEvents(arguments)),
                "scrape_gap_brief" => Scraper(() => string.Join("\n\n---\n\n", _server.Scraper.TestBriefs((string?)arguments["project"], Required(arguments, "session"), Whole(arguments, "event", 1, int.MaxValue)))),
                "scrape_draft_test" => Scraper(() => _server.ScraperDraftTest(new ScraperTestParams((string?)arguments["project"], Required(arguments, "session"), (string?)arguments["target"], (string?)arguments["name"])) switch { { Problem: { } problem } => throw new ScraperProblem(problem), var drafted => drafted.Value! }),
                "scrape_run_drafts" => Scraper(() => RunDrafts(arguments)),
                "scrape_capture_page" => Scraper(() => (object?)_server.Scraper.CapturePageAsync((string?)arguments["name"]).GetAwaiter().GetResult() ?? new { Captured = false, Reason = "The page did not change since its last capture; give a name to capture it anyway." }),
                "scrape_stop" => Scraper(() => (object?)_server.Scraper.StopRecordingAsync().GetAwaiter().GetResult() ?? new { Stopped = false, Reason = "No recording was in progress." }),
                "scrape_list_pages" => Scraper(() => RecordedPages(arguments)),
                "scrape_get_page" => Scraper(() => SnapshotOutline.Render(_server.Scraper.Snapshot((string?)arguments["project"], Required(arguments, "page"), (string?)arguments["session"], Whole(arguments, "event", 1, int.MaxValue)).Snapshot, Whole(arguments, "node", 0, int.MaxValue) ?? 0, MaxOf(arguments, 400))),
                "scrape_find_similar" => Scraper(() => Capped("nodes", _server.Scraper.Similar((string?)arguments["project"], Required(arguments, "page"), Whole(arguments, "node", 1, int.MaxValue) ?? throw new ToolProblem("'node' is required.")), arguments)),
                "scrape_list_page_objects" => Scraper(() => Capped("classes", _server.Scraper.Controls((string?)arguments["project"]), arguments)),
                "scrape_detect_patterns" => Scraper(() => Capped("patterns", _server.Scraper.Patterns((string?)arguments["project"], int.MaxValue), arguments)),
                "scrape_get_control_brief" => Scraper(() => _server.Scraper.ControlBrief((string?)arguments["project"], Required(arguments, "signature"), (string?)arguments["name"], (string?)arguments["variant"], (bool?)arguments["lifted"] ?? false).Text),
                "scrape_match_controls" => Scraper(() => MatchControls(arguments)),
                "scrape_diff_page" => Scraper(() => DiffPage(arguments)),
                "scrape_suggest_controls" => Scraper(() => Capped("members", _server.Scraper.Suggest((string?)arguments["project"], Required(arguments, "page"), Whole(arguments, "node", 1, int.MaxValue)), arguments)),
                "scrape_get_header" => Scraper(() => _server.Scraper.Header((string?)arguments["project"], Required(arguments, "page"), Whole(arguments, "node", 1, int.MaxValue))),
                "scrape_write_file" => Scraper(() => WriteCode(arguments)),
                "scrape_build" => Scraper(() => _server.Scraper.CheckCodeAsync((string?)arguments["project"]).GetAwaiter().GetResult()),
                "scrape_explore" => Scraper(() => ExploreApp(arguments)),
                "scrape_update_config" => Scraper(() => UpdateConfig(arguments)),
                "scrape_create_project" => Scraper(() => CreateProject(arguments)),
                "scrape_draft_page_objects" => Scraper(() => _server.Scraper.DraftAsync((string?)arguments["project"], (string?)arguments["page"]).GetAwaiter().GetResult()),
                "scrape_check_page_objects" => Scraper(() => _server.Scraper.VerifyAsync((string?)arguments["project"], visible: !((bool?)arguments["headless"] ?? true)).GetAwaiter().GetResult()),
                "scrape_find_text" => Scraper(() => Capped("nodes", _server.Scraper.Search((string?)arguments["project"], Required(arguments, "query"), int.MaxValue), arguments)),
                _ => throw new McpException(-32602, $"No tool '{name}'. The tools are {string.Join(", ", Tools.Select(tool => tool.Name))}."),
            };

            // Text (an outline) as it is; anything else as JSON, without its nulls and with the solution's paths relative.
            return Text(answer as string ?? Answer(answer), error: false);
        }
        catch (ToolProblem problem)
        {
            return Text(problem.Message, error: true);
        }
        catch (Exception exception) when (NotLoaded(exception))
        {
            // A suite built against another Bruinet (review 03, finding 12): the suite's fault, said so, not the server's.
            return Text($"{name}: a suite's build does not fit this Bruinet ({exception.GetType().Name}: {exception.Message}). Build it again (run_scenario with build: true); list_suites says which.", error: true);
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or McpException))
        {
            // Never the server's end: an argument of the wrong kind, or a fault, is the tool's answer.
            return Text($"{name} failed: {exception.GetType().Name}: {exception.Message}", error: true);
        }
    }

    // An exception a suite's assemblies throw when they were built against another Bruinet.
    private static bool NotLoaded(Exception exception)
        => exception is TypeLoadException or System.Reflection.ReflectionTypeLoadException or MissingMemberException or FileLoadException or BadImageFormatException;

    // An answer as JSON: no null fields (M4), and every path in the solution relative to it (M5).
    private string Answer(object answer)
        => JsonSerializer.SerializeToNode(answer, Json) is { } node ? Tidy(node).ToJsonString(Json) : "null";

    private JsonNode Tidy(JsonNode node)
    {
        switch (node)
        {
            case JsonObject members:
                foreach (var (key, value) in members.ToList())
                {
                    if (value is null)
                    {
                        members.Remove(key);
                    }
                    else if (Tidy(value) is var tidied && !ReferenceEquals(tidied, value))
                    {
                        members[key] = tidied;
                    }
                }

                return members;
            case JsonArray items:
                for (var index = 0; index < items.Count; index++)
                {
                    if (items[index] is { } item && Tidy(item) is var tidied && !ReferenceEquals(tidied, item))
                    {
                        items[index] = tidied;
                    }
                }

                return items;
            case JsonValue value when value.GetValueKind() == JsonValueKind.String && Relative(value.GetValue<string>()) is { } relative:
                return JsonValue.Create(relative);
            default:
                return node;
        }
    }

    // A whole value that is a path in the solution, relative to it; null for anything else (a message, a URL, a path elsewhere).
    private string? Relative(string text)
    {
        if (text.Length < 3 || text.Contains('\n', StringComparison.Ordinal) || !Path.IsPathFullyQualified(text))
        {
            return null;
        }

        var root = _solutionDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return text.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? Path.GetRelativePath(_solutionDir, text) : null;
    }

    // The 'max' argument: at most this many rows, 50 when left out (M3).
    private static int MaxOf(JsonObject arguments, int otherwise = 50) => Whole(arguments, "max", 1, int.MaxValue) ?? otherwise;

    // A list cut at 'max': the rows shown, and how many were left out (null when none).
    private static (List<T> Shown, int? More) Cut<T>(IEnumerable<T> rows, JsonObject arguments, int otherwise = 50)
    {
        var all = rows.ToList();
        var max = MaxOf(arguments, otherwise);
        return (all.Take(max).ToList(), all.Count > max ? all.Count - max : null);
    }

    // A list as an answer: the rows under their name, and 'more' when some were left out.
    private static JsonObject Capped<T>(string name, IEnumerable<T> rows, JsonObject arguments)
    {
        var (shown, more) = Cut(rows, arguments);
        return new JsonObject { [name] = JsonSerializer.SerializeToNode(shown, Json), ["more"] = more };
    }

    // The Scraper says why it cannot answer as a problem; the agent reads it as the tool's result.
    private static object Scraper(Func<object> work)
    {
        try
        {
            return work();
        }
        catch (Exception exception) when (exception is ScraperProblem or FormatException or FileNotFoundException or InvalidOperationException or Microsoft.Playwright.PlaywrightException)
        {
            throw new ToolProblem(exception.Message);
        }
    }

    // Asks first: the summary until the person agreed, then the run. Links only and headless unless asked otherwise.
    private object ExploreApp(JsonObject arguments)
    {
        var options = new Bruinet.Scraper.Explore.ExploreOptions
        {
            DryRun = (bool?)arguments["linksOnly"] ?? true,
            Url = (string?)arguments["url"],
            MaxPages = (int?)arguments["maxPages"],
            MaxDepth = (int?)arguments["maxDepth"],
            MaxMinutes = (int?)arguments["maxMinutes"],
            Visible = !((bool?)arguments["headless"] ?? true),
            StopAtSignIn = true,
        };
        var project = (string?)arguments["project"];
        var summary = _server.Scraper.ExploreSummary(project, options);
        if ((bool?)arguments["confirm"] != true)
            return new { Started = false, Summary = summary, Next = "Show the summary to the person. Call again with confirm: true once they agree." };
        var result = _server.Scraper.ExploreAsync(project, options).GetAwaiter().GetResult();
        return new
        {
            result.Stop,
            result.StopDetail,
            result.WithinGuards,
            result.DryRun,
            Captures = result.Captures.Count,
            result.Clicks,
            Skipped = result.Skipped.Count,
            Blocked = result.Blocked.Count,
            result.Report,
            Session = result.Folder is null ? null : Path.GetFileName(result.Folder),
        };
    }

    private object UpdateConfig(JsonObject arguments)
    {
        var values = arguments["values"] as JsonArray ?? throw new ToolProblem("'values' is required: an array of strings.");
        var change = new Bruinet.Scraper.Projects.ConfigChange(Required(arguments, "action"), Required(arguments, "section"), [.. values.Select(v => (string?)v ?? "")]);
        var confirmed = (bool?)arguments["confirm"] == true;
        var edit = _server.Scraper.EditConfig((string?)arguments["project"], change, write: confirmed);
        return new
        {
            edit.Changed,
            edit.Description,
            Written = confirmed && edit.Changed,
            Next = confirmed || !edit.Changed ? null : "Tell the person what would change. Call again with confirm: true once they agree.",
        };
    }

    private object CreateProject(JsonObject arguments)
    {
        var name = Required(arguments, "name");
        var defaults = _server.Scraper.NewProjectDefaults(name);
        var request = new Bruinet.Scraper.Projects.NewProject(
            name,
            (string?)arguments["folder"] ?? defaults.Folder,
            (string?)arguments["platform"] ?? "html",
            Required(arguments, "startUrl"),
            (arguments["testIds"] as JsonArray)?.Select(v => (string?)v ?? "").ToList() ?? [],
            (string?)arguments["references"] is { } references
                ? (string.Equals(references, "packages", StringComparison.OrdinalIgnoreCase) ? Bruinet.Scraper.Projects.BruinetReferences.Packages : Bruinet.Scraper.Projects.BruinetReferences.Projects)
                : defaults.References);
        var problems = _server.Scraper.NewProjectProblems(request);
        if (problems.Count > 0 || (bool?)arguments["confirm"] != true)
        {
            return new { Written = false, Request = request, Problems = problems, Next = problems.Count > 0 ? "Fix what is wrong, then ask again." : "Tell the person what would be written. Call again with confirm: true once they agree." };
        }

        var project = _server.Scraper.NewProject(request);
        return new { Written = true, Project = project.Name, project.Folder, Next = "Add the project to the solution (dotnet sln add), then record or explore it." };
    }

    // A file that is not written is a tool error with the reason and each syntax error's place.
    private object WriteCode(JsonObject arguments)
    {
        var result = _server.Scraper.WriteCode((string?)arguments["project"], Required(arguments, "path"), Required(arguments, "code"));
        return result.Written
            ? result
            : throw new ToolProblem(result.Reason + string.Concat(result.Problems.Select(problem => $"{Environment.NewLine}line {problem.Line}, column {problem.Column}: {problem.Code} {problem.Message}")));
    }

    // The preview's Run, waited for: the suite built first when asked, then how each scenario went.
    private object RunScenario(JsonObject arguments)
    {
        var uri = FileUri(Required(arguments, "path"));
        var suite = (string?)arguments["suite"];
        if ((bool?)arguments["build"] == true)
        {
            var project = _server.Context(new Uri(uri).LocalPath, suite).Suite?.ProjectFile ?? throw new ToolProblem("No suite with a project file holds or links this feature file.");
            var build = Run("dotnet", ["build", project, "-v:minimal", "-nologo"], new Dictionary<string, string>());
            if (build.Code != 0)
            {
                throw new ToolProblem($"The build failed, so nothing ran:{Environment.NewLine}{build.Tail}");
            }

            _server.BuildDone();
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(Whole(arguments, "timeoutMinutes", 1, 120) ?? 10));
        var (started, ended) = _server.RunToEndAsync(new RunStartParams(uri, ScenarioLine(arguments, uri), suite, RunEnvironment(arguments)), timeout.Token).GetAwaiter().GetResult();
        if (ended is null)
        {
            throw new ToolProblem(started.Reason ?? "The run did not start.");
        }

        // The run's reports are written: a failed scenario's failure from its own.
        var runs = ended.Results.Any(result => result.Status == "failed") ? _server.NewestRuns(uri, suite) : null;
        return new
        {
            started.Suite,
            Status = ended.Status == "canceled" && timeout.IsCancellationRequested ? "timed out" : ended.Status,
            Message = Cap(ended.Message, 2000),
            Scenarios = ended.Results.Select(result => new
            {
                result.Scenario,
                result.Address,
                result.Status,
                result.DurationMs,
                FailedStep = result.FailedStep is { } step ? new { File = step.File is null ? null : Path.GetRelativePath(_solutionDir, step.File), step.Line, step.Text } : null,
                Failure = runs?.GetValueOrDefault(result.Scenario) is { } run ? Failure(run) : null,
                AppSource = runs?.GetValueOrDefault(result.Scenario) is { } failedRun ? AppSource(Failure(failedRun), SuiteFolder(uri, suite)) : null,
                SinceGreen = runs?.GetValueOrDefault(result.Scenario) is { Status: "failed" } traced ? SinceGreen(traced, SuiteFolder(uri, suite)) : null,
                Message = Cap(result.Message, 2000),
            }),
        };
    }

    // The replay mode a run asks for, as the variables both replays read; SQL replay has no refresh, so it records.
    // The run's variables: its replay mode and its trace (BRUINET_TRACE); a warm runner with other ones is not reused.
    private static Dictionary<string, string>? RunEnvironment(JsonObject arguments)
    {
        var environment = ReplayEnvironment((string?)arguments["replay"]);
        if ((string?)arguments["trace"] is { } trace)
        {
            environment ??= [];
            environment[Bruinet.Core.Logging.StepTrace.Variable] = trace.Trim().ToLowerInvariant() switch
            {
                "all" => "all",
                "off" => "off",
                "failed" => "failed",
                _ => throw new ToolProblem($"trace '{trace}': all, failed or off."),
            };
        }

        return environment;
    }

    private static Dictionary<string, string>? ReplayEnvironment(string? replay)
        => replay?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "record" => new() { [Bruinet.Mocking.Http.HttpReplaySession.ModeVariable] = "Record", [Bruinet.Mocking.Data.Replay.ReplayRegistry.ModeVariable] = "Record" },
            "replay" => new() { [Bruinet.Mocking.Http.HttpReplaySession.ModeVariable] = "Replay", [Bruinet.Mocking.Data.Replay.ReplayRegistry.ModeVariable] = "Replay" },
            "refresh" => new() { [Bruinet.Mocking.Http.HttpReplaySession.ModeVariable] = "Refresh", [Bruinet.Mocking.Data.Replay.ReplayRegistry.ModeVariable] = "Record" },
            _ => throw new ToolProblem($"replay '{replay}': record, replay or refresh."),
        };

    // A scenario's call log, beside its HTTP recording (HttpReplaySession.PathFor): found under the suite's project by name.
    private object HttpCalls(JsonObject arguments)
    {
        var uri = FileUri(Required(arguments, "path"));
        var line = ScenarioLine(arguments, uri) ?? throw new ToolProblem("'line' is required.");
        var name = _server.ScenarioNames(uri, line).FirstOrDefault() ?? throw new ToolProblem("There is no scenario heading at that line.");
        var project = _server.Context(new Uri(uri).LocalPath, (string?)arguments["suite"]).Suite?.ProjectDirectory
            ?? throw new ToolProblem("No suite holds or links this feature file.");
        var file = Path.GetFileName(Bruinet.Mocking.Http.HttpCallLog.PathFor(Bruinet.Mocking.Http.HttpReplaySession.PathFor(project, name)));
        var path = Directory.EnumerateFiles(project, file, SearchOption.AllDirectories)
            .Where(found => !found.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                            && !found.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault()
            ?? throw new ToolProblem($"No HTTP calls of '{name}' yet: run it with run_scenario and replay set, in a suite whose host gives the app an HttpReplaySession.");
        var log = Bruinet.Mocking.Http.HttpCallLog.Load(path)!;
        var calls = Cut(log.Calls, arguments);
        return new
        {
            Scenario = name,
            Log = Path.GetRelativePath(_solutionDir, path),
            log.Mode,
            Calls = calls.Shown.Select(call => new { call.Method, Url = Cap(call.Url, 200), call.Status, call.Outcome }),
            More = calls.More,
            Misses = log.Misses.Take(5).Select(miss => Cap(miss, 500)),
            UnusedExchanges = log.Unused.Take(20),
        };
    }

    // The suite's declared states: [BddState] phrases, each a Given. None is an empty list.
    private static object ListStates(BddSuite suite, JsonObject arguments)
        => Capped("states", Catalogs(suite).Default.Patterns
            .Where(pattern => pattern.IsState)
            .Select(pattern => new
            {
                pattern.Phrase,
                Parameters = pattern.Parameters.Select(parameter => parameter.ToString()).ToList(),
                From = pattern.CommandId,
            })
            .OrderBy(state => state.Phrase, StringComparer.OrdinalIgnoreCase), arguments);

    // A declared state, set by a Step-through of a scenario of its own: the state's Given (and the page check), then a step
    // that does nothing, where it stays paused. Only a declared state runs: the text binds to its state, in the catalog the
    // runner binds with, and no argument can add a line or a table cell to the scenario.
    private object SetState(JsonObject arguments)
    {
        var suite = Suite(arguments);
        var state = OneLine(Required(arguments, "state"), "state");
        if (state.StartsWith("Given ", StringComparison.OrdinalIgnoreCase))
        {
            state = state["Given ".Length..].Trim();
        }

        var page = (string?)arguments["pageObject"] is { Length: > 0 } named ? OneLine(named, "pageObject") : null;
        var testData = (string?)arguments["testData"] is { Length: > 0 } set ? OneLine(set, "testData") : null;
        if (testData is not null && !TestSets(suite).Contains(testData, StringComparer.Ordinal))
        {
            throw new ToolProblem($"'{testData}' is not a test set of {suite.Name}: list_test_sets names them.");
        }

        // A placeholder left in (the state as list_states writes it) would be the parameter's value: '{testSet}'.
        if (System.Text.RegularExpressions.Regex.Match(state, @"\{[^{}]+\}") is { Success: true } placeholder)
        {
            throw new ToolProblem($"'{state}' still has {placeholder.Value}: write the state with its parameters filled in, as a Given "
                + "says it (list_states gives each one's type). testData is the scenario's test data, not a parameter of the state.");
        }

        var catalog = Catalogs(suite).Default;
        var step = new BddStep(BddStepKeyword.Given, Bruinet.Bdd.BddEffectiveStepKeyword.Given, state, null, new BddSourceLocation(null, 1));
        if (!catalog.Patterns.Any(pattern => pattern.IsState && pattern.TryMatch(step) is not null))
        {
            throw new ToolProblem($"'{state}' is not a declared state of {suite.Name}: list_states names them.");
        }

        // The runner binds the step against every phrase: a more specific Given that is no state would win there.
        var bound = catalog.Match(step);
        if (bound is not [{ Pattern.IsState: true } match])
        {
            throw new ToolProblem($"'{state}' binds to {string.Join(" and ", bound.Select(other => other.Pattern.CommandId))}, not to a state alone: say the state as list_states gives it.");
        }

        var metadata = new List<string>();
        if (testData is not null)
        {
            metadata.Add($"| {BddMetadataFields.TestData} | {testData} |");
        }

        if (match.Pattern.Namespace.Length > 0)
        {
            metadata.Add($"| {BddMetadataFields.Using} | {match.Pattern.Namespace} |");
        }

        var lines = new List<string> { "# Feature: Set state", string.Empty };
        if (metadata.Count > 0)
        {
            lines.AddRange(["## Metadata", string.Empty, "| Field | Value |", "| --- | --- |", .. metadata, string.Empty]);
        }

        var line = lines.Count + 1;
        // With a page, the built-in check of it, which also makes it the page step_state reads.
        lines.AddRange([$"## Scenario: {state}", string.Empty, "```gherkin", $"Given {state}"]);
        if (page is not null)
        {
            lines.Add($"Then I should be on the {page} page");
        }

        lines.AddRange([$"Then {Bruinet.Uat.UatReflectionRuntime.PausePhrase}", "```", string.Empty]);

        // In the suite's project, so the suite holds it; a file of this call's own, gone once the runner has read it, so
        // no run, preview or second call finds it.
        var path = Path.Combine(suite.ProjectDirectory, ".bruinet", $"set-state-{Guid.NewGuid():N}.feature.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Join("\n", lines));
        try
        {
            StepStart(new JsonObject { ["path"] = path, ["line"] = line, ["suite"] = suite.Name });
        }
        finally
        {
            File.Delete(path);
        }

        var timeout = Whole(arguments, "timeoutMinutes", 1, 10) ?? 2;
        var paused = StepMove(new JsonObject { ["move"] = "over", ["timeoutMinutes"] = timeout });
        if (page is null)
        {
            // The agent check's first try: paused with no page, step_state had nothing to read.
            if (paused is JsonObject { } noPage && noPage["paused"] is not null && noPage["page"] is null)
            {
                noPage["next"] = "No step made a page current: step_state reads the page that says it is loaded; give 'pageObject' (list_pages names them) to be sure.";
            }

            return paused;
        }

        return paused is not JsonObject { } after || after["lastStepFailed"] is not null || after["paused"] is null
            ? paused
            : StepMove(new JsonObject { ["move"] = "over", ["timeoutMinutes"] = timeout });
    }

    // A paused app's screenshot, copied beside the solution (the runner saves it under its shadow copy, in the temp folder,
    // which an agent may not read); a head without pixels saves the element tree as text or its markup (bUnit), given inline too.
    private object Screenshot(string taken)
    {
        var folder = Path.Combine(_solutionDir, "TestResults", "step-screenshots");
        Directory.CreateDirectory(folder);
        var copy = Path.Combine(folder, Path.GetFileName(taken));
        File.Copy(taken, copy, overwrite: true);
        var tree = copy.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? Cap(File.ReadAllText(copy), 6000)
            : copy.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? Cap(VisibleText(File.ReadAllText(copy)), 6000)
            : null;
        return new { Screenshot = copy, Tree = tree };
    }

    // A page's markup as the text it shows, one piece a line: a bench's markup is mostly attributes (100 KB for 5 KB of text).
    // Blazor's error banner is in every page's markup and hidden by a stylesheet: an agent read it as an error.
    internal static string VisibleText(string markup)
    {
        var shown = System.Text.RegularExpressions.Regex.Replace(markup, @"<(script|style)\b.*?</\1>|<div\b[^>]*\bid=""blazor-error-ui""[^>]*>.*?</div>", string.Empty,
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        shown = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(shown, "<[^>]+>", "\n"));
        return string.Join("\n", shown.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));
    }

    // A value written into a scenario: one line, no table bar or backtick, so it adds no step, cell or code fence.
    private static string OneLine(string value, string name)
        => value.Any(character => char.IsControl(character) || character is '|' or '`')
            ? throw new ToolProblem($"'{name}' must be one line, with no | or `.")
            : value.Trim();

    // A Step-through, waited for: where it paused, or how it ended.
    private object StepStart(JsonObject arguments)
    {
        var uri = FileUri(Required(arguments, "path"));
        var line = ScenarioLine(arguments, uri) ?? throw new ToolProblem("'line' is required.");
        var upcoming = TakeStepEvent();
        var started = _server.StepStartAsync(new StepStartParams(uri, line, (string?)arguments["suite"], null)).GetAwaiter().GetResult();
        return started.Started ? StepEvent(upcoming, TimeSpan.FromMinutes(2), untilEnded: false) : throw new ToolProblem(started.Reason ?? "The Step-through did not start.");
    }

    private object StepMove(JsonObject arguments)
    {
        var move = Required(arguments, "move");
        var wait = TimeSpan.FromMinutes(Whole(arguments, "timeoutMinutes", 1, 10) ?? 2);

        // A pause that came after a Waiting answer is given now; otherwise the next one is waited for.
        if (move == "wait")
        {
            var (count, last) = _server.LastStepEvent;
            if (count > Interlocked.Read(ref _stepEventsSeen) && last is not null)
            {
                Interlocked.Exchange(ref _stepEventsSeen, count);
                return Describe(last);
            }

            return StepEvent(TakeStepEvent(), wait, untilEnded: false);
        }

        var upcoming = TakeStepEvent();
        var moved = _server.StepMove(new StepMoveParams(move));
        return moved.Taken
            ? StepEvent(upcoming, wait, untilEnded: move == "stop")
            : throw new ToolProblem(moved.Reason ?? "The move was not taken.");
    }

    private Task<object> TakeStepEvent()
    {
        Interlocked.Exchange(ref _stepEventsSeen, _server.LastStepEvent.Count);
        return _server.UpcomingStepEvent;
    }

    // The next event; for a stop, the end (a pause raised as the stop came is passed over).
    private object StepEvent(Task<object> upcoming, TimeSpan wait, bool untilEnded)
    {
        var deadline = DateTime.UtcNow + wait;
        while (true)
        {
            var left = deadline - DateTime.UtcNow;
            if (left <= TimeSpan.Zero || !upcoming.Wait(left))
            {
                return new { Waiting = true, Next = "The step is still running: step_move 'wait' waits again, 'stop' ends it." };
            }

            Interlocked.Exchange(ref _stepEventsSeen, _server.LastStepEvent.Count);
            if (!untilEnded || upcoming.Result is StepEndedParams)
            {
                return Describe(upcoming.Result);
            }

            upcoming = _server.UpcomingStepEvent;
        }
    }

    // Where a Step-through paused or how it ended; its null fields go, as every answer's do.
    private JsonObject Describe(object stepEvent)
    {
        JsonObject answer = stepEvent switch
        {
            StepPausedParams paused => new()
            {
                ["paused"] = true,
                ["file"] = paused.At is { } at ? Path.GetRelativePath(_solutionDir, new Uri(at).LocalPath) : null,
                ["line"] = paused.Line,
                ["text"] = paused.Text,
                ["depth"] = paused.Depth,
                ["page"] = paused.Page,
                ["app"] = paused.App,
                ["arguments"] = paused.Arguments is { Count: > 0 } values ? new JsonObject([.. values.Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key, Cap(pair.Value, 300)))]) : null,
                ["lastStepFailed"] = paused.Failed ? true : null,
                ["message"] = Cap(paused.Message, 2000),
            },
            StepEndedParams ended => new() { ["ended"] = true, ["scenario"] = ended.Scenario, ["status"] = ended.Status, ["message"] = Cap(ended.Message, 2000) },
            var other => new() { ["event"] = other.GetType().Name },
        };

        return answer;
    }

    // One scenario through the gate with .NET's code coverage (Microsoft.NET.Test.Sdk brings it): the lines it ran.
    private object Coverage(JsonObject arguments)
    {
        var uri = FileUri(Required(arguments, "path"));
        var line = ScenarioLine(arguments, uri) ?? throw new ToolProblem("'line' is required.");
        var timeout = TimeSpan.FromMinutes(Whole(arguments, "timeoutMinutes", 1, 120) ?? 10);
        var path = new Uri(uri).LocalPath;
        var file = _server.Context(path, (string?)arguments["suite"]);
        var suite = file.Suite ?? throw new ToolProblem("No suite holds or links this feature file.");
        var project = suite.ProjectFile ?? throw new ToolProblem($"{suite.Name} has no project file to run with dotnet test.");
        if (file.Catalogs is null || !suite.HasScenarioTheory)
        {
            throw new ToolProblem(file.Catalogs is null
                ? $"{suite.Name} is not bound: {file.Reason}"
                : $"{suite.Name} runs its scenarios with [Theory], so one has no address to run by. Change it to [UatScenarioTheory].");
        }

        var names = _server.ScenarioNames(uri, line);
        if (names.Count == 0)
        {
            throw new ToolProblem($"There is no scenario heading at line {line}.");
        }

        // The files first: a run is slow, and without them there is nothing to report.
        var wanted = CoverageFiles(arguments);
        var relative = suite.RelativeToConfig(path);
        var filter = string.Join('|', names.Select(name => $"{BddScenarioAddress.Trait}={BddScenarioAddress.Of(relative, name)}"));
        var results = Path.Combine(Path.GetTempPath(), "Bruinet", "coverage", Guid.NewGuid().ToString("N"));
        List<string> test = ["test", project, "--filter", filter, "--collect", "Code Coverage;Format=cobertura", "--results-directory", results, "--logger", "console;verbosity=normal"];
        var built = (bool?)arguments["build"] == true;
        var stale = built ? null : OlderThanSources(suite);
        if (!built)
        {
            test.Add("--no-build");
        }

        Dictionary<string, string> environment = [];
        if ((string?)arguments["head"] is { Length: > 0 } head)
        {
            environment["BRUINET_HEAD"] = head;
        }

        try
        {
            var run = Run("dotnet", test, environment, timeout);
            if (built)
            {
                _server.BuildDone();
            }

            if (run.TimedOut)
            {
                throw new ToolProblem($"The run took longer than {timeout.TotalMinutes} minutes and was ended:{Environment.NewLine}{Cap(run.Tail, 2000, fromEnd: true)}");
            }

            if (run.Tail.Contains("No test matches the given testcase filter", StringComparison.Ordinal) || run.Tail.Contains("Total tests: 0", StringComparison.Ordinal))
            {
                throw new ToolProblem($"No test ran by the filter {filter}: is the suite built with this scenario?");
            }

            var report = Directory.Exists(results) ? Directory.EnumerateFiles(results, "*.cobertura.xml", SearchOption.AllDirectories).FirstOrDefault() : null;
            if (report is null)
            {
                throw new ToolProblem($"No coverage was written (exit code {run.Code}):{Environment.NewLine}{Cap(run.Tail, 2000, fromEnd: true)}");
            }

            var lines = CoverageReport.Read(report);
            var shown = Cut(wanted, arguments);
            return new
            {
                Scenarios = names,
                Passed = run.Code == 0,
                Files = shown.Shown.Select(source => lines.TryGetValue(source, out var hits)
                    ? new { File = Path.GetRelativePath(_solutionDir, source), Hit = Cap(CoverageReport.Ranges(hits.Where(pair => pair.Value).Select(pair => pair.Key)), 300), Missed = Cap(CoverageReport.Ranges(hits.Where(pair => !pair.Value).Select(pair => pair.Key)), 300), Note = (string?)null }
                    : new { File = Path.GetRelativePath(_solutionDir, source), Hit = (string?)null, Missed = (string?)null, Note = (string?)"not in the report: no line of it ran, or the collector left its assembly out" })
                    .ToList(),
                More = shown.More,
                Output = run.Code == 0 ? null : Cap(run.Tail, 2000, fromEnd: true),
                Note = stale,
            };
        }
        finally
        {
            TryDelete(results);
        }
    }

    // A note when the suite's build is older than a source file of its project: the run then used the old code.
    private static string? OlderThanSources(BddSuite suite)
    {
        if (suite.ProjectFile is not { } projectFile)
        {
            return null;
        }

        static bool Built(string path) => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
        var assembly = Path.GetFileNameWithoutExtension(projectFile) + ".dll";
        var output = Directory.Exists(Path.Combine(suite.ProjectDirectory, "bin"))
            ? Directory.EnumerateFiles(Path.Combine(suite.ProjectDirectory, "bin"), assembly, SearchOption.AllDirectories).Select(File.GetLastWriteTimeUtc).DefaultIfEmpty().Max()
            : default;
        if (output == default)
        {
            return $"{suite.Name} is not built: give build: true.";
        }

        var newer = Directory.EnumerateFiles(suite.ProjectDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => !Built(path) && Path.GetExtension(path) is ".cs" or ".razor" or ".xaml" or ".csproj")
            .FirstOrDefault(path => File.GetLastWriteTimeUtc(path) > output);
        return newer is null ? null : $"The build is older than {Path.GetFileName(newer)}: give build: true to cover the code as it is now.";
    }

    // The given files, or the working tree's changed code files that still exist.
    private List<string> CoverageFiles(JsonObject arguments)
    {
        if ((arguments["files"] as JsonArray)?.Select(node => (string?)node).OfType<string>().Select(file => Path.GetFullPath(file, _solutionDir)).ToList() is { Count: > 0 } given)
        {
            return given;
        }

        try
        {
            return [.. Bruinet.Cli.Selection.SelectionCommands.Changed(_solutionDir, "HEAD").Where(file => CoverageReport.IsSource(file) && File.Exists(file))];
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new ToolProblem($"Give 'files': there is no git working tree here to take the changed files from ({exception.Message}).");
        }
    }

    // A temp folder removed; one still held open (a collector closing) is left, and said so.
    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Could not delete {folder}: {exception.Message}");
        }
    }

    // The newest result of each scenario (M2): of a feature file, a suite or every suite; failed ones unless status is all.
    // A suite that cannot load (not built, or built against another Bruinet) is named under notLoaded, and its results,
    // which are files, are still read (M11): one stale build hides no other suite's failures.
    private object ListResults(JsonObject arguments)
    {
        var all = ((string?)arguments["status"])?.Trim().ToLowerInvariant() switch
        {
            null or "" or "failed" => false,
            "all" => true,
            var other => throw new ToolProblem($"status '{other}': failed or all."),
        };
        List<BddSuite> suites;
        List<(BddSuite Suite, string Path)> files;
        if ((string?)arguments["path"] is { Length: > 0 } given)
        {
            var path = new Uri(FileUri(given)).LocalPath;
            var suite = _server.Context(path, (string?)arguments["suite"]).Suite ?? throw new ToolProblem("No suite holds or links this feature file.");
            (suites, files) = ([suite], [(suite, path)]);
        }
        else
        {
            suites = (string?)arguments["suite"] is not null
                ? [Suite(arguments)]
                : _server.Explorer(new ExplorerParams(null, null)).Suites.Select(SuiteNamed).ToList();
            files = [.. suites.SelectMany(suite => suite.FeatureFiles.Concat(suite.LinkedFiles)
                .Where(path => path.EndsWith(BddFileFormat.FeatureExtension, StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => (suite, path)))];
        }

        var notLoaded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var suite in suites)
        {
            try
            {
                if (suite.TryGetCatalogs(out var reason) is null)
                {
                    notLoaded[suite.Name] = reason ?? "It cannot load.";
                }
            }
            catch (Exception exception) when (NotLoaded(exception))
            {
                notLoaded[suite.Name] = $"{exception.GetType().Name}: {exception.Message} Build it again.";
            }
        }

        var rows = new List<(BddSuite Suite, string Path, int? Line, Bruinet.Bdd.Preview.BddScenarioRun Run)>();
        foreach (var (suite, path) in files)
        {
            try
            {
                var headings = File.Exists(path)
                    ? BddMarkdownParser.Parse(File.ReadAllText(path), path).Document?.Scenarios.ToLookup(scenario => scenario.Name, scenario => scenario.Source.LineNumber)
                    : null;
                rows.AddRange(_server.NewestRuns(new Uri(path).AbsoluteUri, suite.Name).Values
                    .Where(run => all || run.Status == "failed")
                    .Select(run => (suite, path, headings?[run.Scenario].Cast<int?>().FirstOrDefault(), run)));
            }
            catch (Exception exception) when (NotLoaded(exception) || exception is BddWorkspaceException)
            {
                notLoaded[suite.Name] = $"{exception.GetType().Name}: {exception.Message} Build it again.";
            }
        }

        var (shown, more) = Cut(rows.OrderByDescending(row => row.Run.At), arguments);
        return new
        {
            Count = rows.Count,
            Results = shown.Select(row => new
            {
                Suite = row.Suite.Name,
                File = Path.GetRelativePath(_solutionDir, row.Path),
                row.Line,
                row.Run.Scenario,
                row.Run.Status,
                At = row.Run.At.ToString("u", CultureInfo.InvariantCulture),
                row.Run.DurationMs,
                Kind = (string?)Failure(row.Run)?["kind"],
                StepLine = row.Run.Steps.FirstOrDefault(step => step.Status == "Failed")?.WrittenLine,
                Message = Cap(row.Run.Steps.FirstOrDefault(step => step.Status == "Failed")?.Message, 300),
            }),
            More = more,
            NotLoaded = notLoaded.Count == 0 ? null : notLoaded.Select(pair => new { Suite = pair.Key, Reason = pair.Value }),
        };
    }

    // A failure's short why (AI9), beside it in run_scenario: where it parted from the last green run and the
    // code changed since that touches it, so the agent needs no call more. Null without a trace or a green run before it.
    private static object? SinceGreen(Bruinet.Bdd.Preview.BddScenarioRun failing, string suiteFolder)
    {
        try
        {
            if (failing.TracePath is not { } path || Bruinet.Uat.UatTraceDiff.LastGreen(path) is not { } green
                || Bruinet.Uat.UatTraceDiff.Read(path) is not { } now || Bruinet.Uat.UatTraceDiff.Read(green) is not { } then)
            {
                return null;
            }

            const int Shown = 5;
            var difference = Bruinet.Uat.UatTraceDiff.Compare(now, then);
            var at = Math.Max(difference.Index, 0);
            var files = ChangedFiles(suiteFolder, (string?)then["commit"], (string?)now["commit"],
                [.. StepNames(now, at, at).Concat(StepNames(then, at, at))], [.. StepNames(now, 0, at - 1)]);
            var changed = files.Touching.Concat(files.Earlier).Concat(files.Other).ToList();
            return new
            {
                difference.Kind,
                difference.Line,
                First = difference.Differences.FirstOrDefault(),
                CodeChanged = changed.Take(Shown),
                More = changed.Count > Shown ? changed.Count - Shown : (int?)null,
                Note = files.Note ?? "diff_since_green gives it in full.",
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    // Since last green (AI9), from what is kept: the newest traced failure against the newest earlier passing run, and the
    // files changed since that run's commit that touch the step where they part. It runs nothing (M2): with no failure or
    // no green run to compare, it says so as its answer, and run_scenario makes one.
    private object DiffSinceGreen(JsonObject arguments)
    {
        var uri = FileUri(Required(arguments, "path"));
        var suite = (string?)arguments["suite"];
        var names = _server.ScenarioNames(uri, ScenarioLine(arguments, uri) ?? throw new ToolProblem("'line' is required: a line of the scenario."));
        if (names.Count == 0)
        {
            throw new ToolProblem("There is no scenario heading at that line.");
        }

        static bool Traced(Bruinet.Bdd.Preview.BddScenarioRun run) => run.TracePath is { } path && File.Exists(path);
        var failed = _server.NewestRuns(uri, suite, run => run.Status == "failed" && Traced(run));
        var (name, failing) = names.Select(name => (name, Run: failed.GetValueOrDefault(name))).Where(found => found.Run is not null).OrderByDescending(found => found.Run!.At).FirstOrDefault();
        if (failing is null)
        {
            return new
            {
                Scenario = names[0],
                Compared = false,
                Note = "No failure of it is traced, so there is nothing to compare: run it with run_scenario (build: true after changing C#); a failure comes with its sinceGreen.",
            };
        }

        var newest = _server.NewestRuns(uri, suite).GetValueOrDefault(name);
        if (Bruinet.Uat.UatTraceDiff.LastGreen(failing.TracePath!) is not { } green)
        {
            return new
            {
                Scenario = name,
                Compared = false,
                Note = $"No run of it passed before the failure of {failing.At.ToString("u", CultureInfo.InvariantCulture)}: there is no green run to compare with. get_trace gives what the failing run did.",
            };
        }

        var (now, then) = (Bruinet.Uat.UatTraceDiff.Read(failing.TracePath!), Bruinet.Uat.UatTraceDiff.Read(green));
        if (now is null || then is null)
        {
            throw new ToolProblem("A trace cannot be read: run the scenario again.");
        }

        var difference = Bruinet.Uat.UatTraceDiff.Compare(now, then);
        var at = Math.Max(difference.Index, 0);
        var files = ChangedFiles(
            SuiteFolder(uri, suite),
            (string?)then["commit"],
            (string?)now["commit"],
            StepNames(now, at, at).Concat(StepNames(then, at, at)).ToList(),
            StepNames(now, 0, at - 1).ToList());
        return new
        {
            Scenario = name,
            Compared = true,
            Failing = new { At = failing.At.ToString("u", CultureInfo.InvariantCulture), Commit = Short((string?)now["commit"]) },
            Green = new { At = File.GetLastWriteTimeUtc(green).ToString("u", CultureInfo.InvariantCulture), Commit = Short((string?)then["commit"]) },
            difference.Kind,
            Step = difference.Index < 0 ? null : new { difference.Line, difference.Text, difference.Index },
            difference.Differences,
            Slower = difference.Slower.Count == 0 ? null : difference.Slower,
            Files = files.Answer,
            Note = string.Join(" ", new[]
            {
                newest is { Status: "passed" } && newest.At > failing.At ? $"Its newest run passed ({newest.At:u}): this is an older failure." : null,
                difference.Kind == Bruinet.Uat.UatTraceDiff.None ? "The failing run's steps match the green one's: get_trace gives the failure." : null,
                Bruinet.Uat.UatTraceDiff.ReadPages(then) ? null : "Page values were not compared: the green run did not read pages (run_scenario's trace: all).",
                files.Note,
            }.OfType<string>()) is { Length: > 0 } note ? note : null,
        };

        static string? Short(string? commit) => commit is { Length: > 8 } ? commit[..8] : commit;
    }

    // The pages and controls the steps from first to last drove: what ties a changed file to them.
    private static IEnumerable<(string Name, bool IsPage)> StepNames(JsonObject trace, int first, int last)
    {
        var steps = trace["steps"]?.AsArray().OfType<JsonObject>().ToList() ?? [];
        for (var index = Math.Max(first, 0); index <= last && index < steps.Count; index++)
        {
            var step = steps[index];
            foreach (var call in step["calls"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                yield return ((string?)call["page"] ?? string.Empty, true);
                yield return ((string?)call["control"] ?? string.Empty, false);
            }

            yield return ((string?)step["page"] ?? string.Empty, true);
        }
    }

    // The files changed since the green run's commit (to the failing run's when HEAD moved on since, else to the working tree,
    // new files too), in the repository the suite is in: those that touch the step where the runs part, those that touch an
    // earlier step of the scenario (a cause can come before its symptom), and how many others. Bounded: 2000 files read
    // at most, 20 named in each list.
    private static WhyFiles ChangedFiles(string folder, string? green, string? failing, IReadOnlyList<(string Name, bool IsPage)> step, IReadOnlyList<(string Name, bool IsPage)> before)
    {
        if (green is null)
        {
            return new WhyFiles([], [], [], [], "The green run's commit is not known (not a git checkout): no files compared.");
        }

        var since = green[..Math.Min(8, green.Length)];
        List<string> changed;
        string root;
        HashSet<string> commentsOnly;
        try
        {
            root = Bruinet.Cli.Sql.SqlCommands.GitText(folder, "rev-parse", "--show-toplevel").Trim();
            var head = Bruinet.Cli.Sql.SqlCommands.GitText(root, "rev-parse", "HEAD").Trim();
            var toFailing = failing is not null && !string.Equals(head, failing, StringComparison.OrdinalIgnoreCase);
            changed = [.. Lines(toFailing
                    ? Bruinet.Cli.Sql.SqlCommands.GitText(root, "-c", "core.quotePath=false", "diff", "--name-only", green, failing!, "--")
                    : Bruinet.Cli.Sql.SqlCommands.GitText(root, "-c", "core.quotePath=false", "diff", "--name-only", green, "--"))
                .Concat(toFailing ? [] : Lines(Bruinet.Cli.Sql.SqlCommands.GitText(root, "-c", "core.quotePath=false", "ls-files", "--others", "--exclude-standard")))
                .Distinct(StringComparer.Ordinal)
                .Take(2000)];

            // A change of comments or blank lines only cannot have made the run go another way: set apart (AI9's check).
            commentsOnly = WhyFiles.CommentsOnlyIn(toFailing
                ? Bruinet.Cli.Sql.SqlCommands.GitText(root, "-c", "core.quotePath=false", "diff", "-U0", "--no-color", green, failing!, "--")
                : Bruinet.Cli.Sql.SqlCommands.GitText(root, "-c", "core.quotePath=false", "diff", "-U0", "--no-color", green, "--"));
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new WhyFiles([], [], [], [], $"The files since {since} could not be listed ({exception.Message}).");
        }

        var code = changed.Where(file => !commentsOnly.Contains(file)).ToList();
        var (touching, earlier, other) = WhyFiles.Sort(code, file => Path.Combine(root, file), step, before);
        return new WhyFiles(touching, earlier, other, [.. changed.Where(commentsOnly.Contains)],
            code.Count == 0 ? $"No code changed since {since}{(changed.Count > 0 ? " (comments only)" : string.Empty)}: the difference is in data, timing or the environment." : null);

        static IEnumerable<string> Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    // A name's words, lower case, without the ones every page has: "TodoDetailPage" and "Todo Detail" give todo, detail.
    internal static HashSet<string> Words(string name)
        => [.. System.Text.RegularExpressions.Regex.Matches(name, "[A-Z]?[a-z]+|[A-Z]+(?![a-z])|[0-9]+")
            .Select(word => word.Value.ToLowerInvariant())
            .Where(word => word is not ("page" or "view" or "test" or "tests" or "control"))];

    // A scenario's newest trace: a row per step and the failed step in full, or one step in full.
    private object GetTrace(JsonObject arguments)
    {
        var uri = FileUri(Required(arguments, "path"));
        var names = _server.ScenarioNames(uri, ScenarioLine(arguments, uri) ?? throw new ToolProblem("line is required: a line of the scenario."));
        if (names.Count == 0)
        {
            throw new ToolProblem("There is no scenario heading at that line.");
        }

        // Of an outline's rows, the newest with a trace: a newer row that passed has none.
        var runs = _server.NewestRuns(uri, (string?)arguments["suite"]);
        var ran = names.Select(name => runs.GetValueOrDefault(name)).OfType<Bruinet.Bdd.Preview.BddScenarioRun>().OrderByDescending(run => run.At).ToList();
        var newest = ran.FirstOrDefault(run => run.TracePath is { } traced && File.Exists(traced));
        if (newest?.TracePath is not { } path)
        {
            throw new ToolProblem(ran.Count == 0
                ? "No run of it has a result yet: run it with run_scenario."
                : $"Its newest run {ran[0].Status} ({ran[0].At:u}) has no trace: it ran with trace: off, or was skipped. Run it again with run_scenario.");
        }

        var (trace, steps) = ReadTrace(path);
        if (Whole(arguments, "step", 1, int.MaxValue) is { } stepLine)
        {
            return TraceStep(steps, stepLine, Path.GetFileName(new Uri(uri).LocalPath));
        }

        return new
        {
            Scenario = (string?)trace["scenario"],
            Status = (string?)trace["status"],
            At = newest.At.ToString("u", CultureInfo.InvariantCulture),
            Steps = steps.Select(TraceRow),
            Failed = steps.FirstOrDefault(step => (string?)step["status"] == "Failed") is { } failed ? Shortened(failed) : null,
        };
    }

    // A failed step without 'step' (M3): its 10 slowest calls, in the order made, and the controls shown after it; what
    // was left out is counted, and get_trace with step gives it in full.
    private static JsonObject Shortened(JsonObject step)
    {
        const int Calls = 10;
        var copy = (JsonObject)step.DeepClone();
        if (copy["calls"] is JsonArray calls && calls.Count > Calls)
        {
            var slowest = calls.OfType<JsonObject>().OrderByDescending(call => (int?)call["ms"] ?? 0).Take(Calls).ToHashSet();
            copy["calls"] = new JsonArray([.. calls.OfType<JsonObject>().Where(slowest.Contains).Select(call => call.DeepClone())]);
            copy["callsLeftOut"] = ((int?)step["callsLeftOut"] ?? 0) + calls.Count - Calls;
        }

        if (copy["pageAfter"]?["controls"] is JsonArray controls && controls.OfType<JsonObject>().Any(control => (bool?)control["shown"] != true))
        {
            var shown = controls.OfType<JsonObject>().Where(control => (bool?)control["shown"] == true).Select(control => control.DeepClone()).ToList();
            copy["pageAfter"]!["controlsNotShown"] = controls.Count - shown.Count;
            copy["pageAfter"]!["controls"] = new JsonArray([.. shown]);
        }

        return copy;
    }

    private (JsonObject Trace, List<JsonObject> Steps) ReadTrace(string path)
    {
        try
        {
            var trace = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? throw new JsonException("empty");
            return (trace, trace["steps"]?.AsArray().OfType<JsonObject>().ToList() ?? throw new JsonException("no steps"));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or IOException)
        {
            throw new ToolProblem($"The trace at {Path.GetRelativePath(_solutionDir, path)} cannot be read ({exception.Message}): run the scenario again.");
        }
    }

    // A step at a line of the feature file: a callable's inner steps share its call line, so the failed one of them.
    private static JsonObject TraceStep(List<JsonObject> steps, int line, string feature)
    {
        var at = steps.Where(step => (int?)step["callLine"] == line).ToList();
        if (at.Count == 0)
        {
            at = [.. steps.Where(step => (int?)step["line"] == line && ((string?)step["file"])?.EndsWith(feature, StringComparison.OrdinalIgnoreCase) != false)];
        }

        return at.FirstOrDefault(step => (string?)step["status"] == "Failed") ?? at.FirstOrDefault()
            ?? throw new ToolProblem($"The trace has no step at line {line}: its steps are at {string.Join(", ", steps.Select(step => (int?)step["callLine"] ?? (int?)step["line"]).Distinct())}.");
    }

    // A step in a line: its page, how many calls, the slowest (a wait), how many app events.
    private static object TraceRow(JsonObject step)
    {
        var calls = step["calls"]?.AsArray().OfType<JsonObject>().ToList() ?? [];
        var slow = calls.MaxBy(call => (int?)call["ms"] ?? 0);
        return new
        {
            Line = (int?)step["callLine"] ?? (int?)step["line"],
            Text = (string?)step["text"],
            Status = (string?)step["status"],
            Ms = (long?)step["durationMs"],
            Page = (string?)step["page"],
            Calls = calls.Count + ((int?)step["callsLeftOut"] ?? 0),
            Slowest = slow is null ? null : $"{slow["page"]}.{slow["control"]}.{slow["action"]} {slow["ms"]}ms" + ((int?)slow["attempts"] > 1 ? $" x{slow["attempts"]}" : string.Empty),
            Events = step["appEvents"]?.AsArray().Count ?? 0,
        };
    }

    // A scenario tool's 'line' as its heading's: agents give the line a failure names, a step's or the tag's, more often than
    // the heading's (half of the benchmark's why calls failed on it).
    private int? ScenarioLine(JsonObject arguments, string uri)
    {
        var line = Whole(arguments, "line", 1, int.MaxValue);
        return line is { } at ? _server.HeadingOf(uri, at) ?? throw new ToolProblem($"There is no scenario at line {at}: scenarios start lower in the file.") : null;
    }

    // The app's sources, their file list kept a minute rather than walked again per call.
    private BddAppSources AppSources()
    {
        if (_appSources is null || DateTime.UtcNow - _appSourcesAt > TimeSpan.FromMinutes(1))
        {
            _appSources = new BddAppSources(_solutionDir);
            _appSourcesAt = DateTime.UtcNow;
        }

        return _appSources;
    }

    // The folder of the suite a feature file runs in; the solution's when none.
    private string SuiteFolder(string uri, string? suite)
        => _server.Context(new Uri(uri).LocalPath, suite).Suite?.ConfigPath is { } configPath ? Path.GetDirectoryName(configPath)! : _solutionDir;

    // Where the app makes the element a failure's locator names: the top three lines, the suite's own app first; null
    // when the failure names none.
    private object? AppSource(JsonNode? failure, string suiteFolder)
    {
        if (failure?["locator"] is not JsonValue locator || BddAppSources.ValueOf(locator.ToString()) is not { } value)
        {
            return null;
        }

        // Only where the value is a test id: a string elsewhere is as likely a test's or the framework's.
        var hits = AppSources().Find(value, max: 3, hint: new BddAppSourceHint(Path.Combine(suiteFolder, "uat.config.md"), null, null))
            .Where(hit => hit.IsAttribute)
            .ToList();
        return hits.Count == 0 ? null : hits.Select(hit => $"{Path.GetRelativePath(_solutionDir, hit.File)}:{hit.Line}: {hit.Text}").ToList();
    }

    // A failed run's failure, as its report wrote it; null for a report written before failures were.
    private static JsonNode? Failure(Bruinet.Bdd.Preview.BddScenarioRun run)
        => run.Steps.FirstOrDefault(step => step.Status == "Failed")?.Failure is { } json ? JsonNode.Parse(json) : null;

    // A whole-number argument in its range, or a tool error saying what it must be; null when left out.
    private static int? Whole(JsonObject arguments, string name, int least, int most)
    {
        if (arguments[name] is not { } node)
        {
            return null;
        }

        return node is JsonValue value && value.TryGetValue<int>(out var number) && number >= least && number <= most
            ? number
            : throw new ToolProblem($"'{name}' must be a whole number from {least} to {most}.");
    }

    // bruinet tests affected for a suite, in process: what an agent runs after a change, with why.
    private object TestsAffected(JsonObject arguments)
    {
        var named = Required(arguments, "suite");
        var folder = Path.GetFullPath(named, _solutionDir) is var path && Directory.Exists(path)
            ? path
            : Suite(arguments).ProjectDirectory;
        var map = Path.Combine(folder, Bruinet.Cli.Selection.TouchMap.FileName);
        if (!File.Exists(map))
        {
            throw new ToolProblem($"No touch map at {map}: run the suite whole once with BRUINET_TIMINGS=1, then 'bruinet tests touch-map <timings> --project {folder}'.");
        }

        List<string> changed;
        try
        {
            changed = (arguments["files"] as JsonArray)?.Select(node => (string?)node).OfType<string>().Select(file => Path.GetFullPath(file, _solutionDir)).ToList() is { Count: > 0 } given
                ? given
                : Bruinet.Cli.Selection.SelectionCommands.Changed(_solutionDir, (string?)arguments["base"] ?? "HEAD");
        }
        catch (InvalidOperationException exception)
        {
            throw new ToolProblem(exception.Message);
        }

        var always = (arguments["always"] as JsonArray)?.Select(node => (string?)node).OfType<string>().ToList();
        var affected = Bruinet.Cli.Selection.Affected.Of(folder, null, changed, Bruinet.Cli.Selection.TouchMap.Load(map), always);
        var picked = Cut(affected.Picked, arguments);
        return new
        {
            Changed = changed.Count,
            affected.Whole,
            affected.Filter,
            Picked = picked.Shown.Select(test => new { test.Name, test.Why }),
            More = picked.More,
            affected.Notes,
            Next = affected.Whole is not null ? "Run the suite whole." : affected.Filter is null ? "Nothing to run." : "Run them with dotnet test <suite project> --filter \"<filter>\", or a scenario with run_scenario.",
        };
    }

    // The language server's rename, as edits an agent reads first; applied, every changed file must still bind.
    private object RenameStep(JsonObject arguments)
    {
        var uri = FileUri(Required(arguments, "path"));
        var line = (int?)arguments["line"] ?? throw new ToolProblem("'line' is required.");
        WorkspaceEdit? edit;
        try
        {
            edit = _server.Rename(new RenameParams(new TextDocumentIdentifier(uri), new Position(line - 1, 0), Required(arguments, "newPhrase")));
        }
        catch (StreamJsonRpc.LocalRpcException exception)
        {
            throw new ToolProblem(exception.Message);
        }

        if (edit?.Changes is not { Count: > 0 } changes)
        {
            throw new ToolProblem($"Line {line} is not a '## Step:' heading or a call of one.");
        }

        // Each file's text after its edits, its lines' before and after; edits applied from the last.
        var files = changes.Select(pair =>
        {
            var path = new Uri(pair.Key).LocalPath;
            var before = File.ReadAllText(path);
            var lines = before.Split('\n');
            var after = lines.ToArray();
            foreach (var change in pair.Value.OrderByDescending(change => change.Range.Start.Line).ThenByDescending(change => change.Range.Start.Character))
            {
                var at = change.Range.Start.Line;
                var text = after[at];
                after[at] = text[..change.Range.Start.Character] + change.NewText + text[change.Range.End.Character..];
            }

            var edits = pair.Value.Select(change => change.Range.Start.Line).Distinct().Order()
                .Select(at => new { Line = at + 1, Before = lines[at].Trim(), After = after[at].Trim() })
                .ToList();
            return (Path: path, Before: before, After: string.Join('\n', after), Edits: edits);
        }).ToList();

        var shown = files.Select(file => new { File = Path.GetRelativePath(_solutionDir, file.Path), file.Edits }).ToList();
        if ((bool?)arguments["confirm"] != true)
        {
            return new { Applied = false, Files = shown, Next = "Call again with confirm: true to write these edits." };
        }

        foreach (var file in files)
        {
            File.WriteAllText(file.Path, file.After);
        }

        var problems = files
            .Select(file => Validate(file.Path, null))
            .Where(check => !check.Valid)
            .Select(check => new { File = Path.GetRelativePath(_solutionDir, check.File), check.Reason, check.Problems })
            .ToList();
        if (problems.Count > 0)
        {
            foreach (var file in files)
            {
                File.WriteAllText(file.Path, file.Before);
            }

            return new { Applied = false, Files = shown, PutBack = true, Problems = problems };
        }

        return new { Applied = true, Files = shown };
    }

    private object RenamePageObject(JsonObject arguments)
    {
        var project = (string?)arguments["project"];
        var page = Required(arguments, "page");
        var name = Required(arguments, "newName");
        if ((bool?)arguments["confirm"] != true)
        {
            return new { Renamed = false, Would = $"Rename the page object of '{page}' to {name}, in the files that declare it; the files that only use it are listed for you to fix.", Next = "Tell the person. Call again with confirm: true once they agree." };
        }

        var result = _server.Scraper.RenamePageObject(project, page, name);
        return new { Renamed = true, result.From, result.To, result.Files, result.UsedElsewhere, Next = result.UsedElsewhere.Count > 0 ? "Change the files that use it, then scrape_build." : "scrape_build." };
    }

    private string FileUri(string path) => new Uri(Path.GetFullPath(path, _solutionDir)).AbsoluteUri;

    private static string? Cap(string? text, int length, bool fromEnd = false)
        => text is null || text.Length <= length ? text
            : fromEnd ? $"({text.Length - length} characters before) …{text[^length..]}"
            : $"{text[..length]}… ({text.Length - length} more characters)";

    // Asks first: it only counts unless the call says confirm: true. A scenario is any line of it, as for the other tools.
    private DeleteResultsResult DeleteResults(JsonObject arguments)
    {
        var uri = (string?)arguments["path"] is { Length: > 0 } path ? FileUri(path) : null;
        var result = _server.DeleteResults(new DeleteResultsParams(
            Required(arguments, "scope"),
            uri,
            uri is null ? Whole(arguments, "line", 1, int.MaxValue) : ScenarioLine(arguments, uri),
            (string?)arguments["suite"],
            (bool?)arguments["confirm"] != true));
        return result.Done ? result : throw new ToolProblem(result.Reason ?? "Nothing to delete.");
    }

    // Build, then the gate run with the recording variables; the video it made, and how the run went.
    private object RecordVideo(JsonObject arguments)
    {
        var path = Path.GetFullPath(Required(arguments, "path"), _solutionDir);
        var recordingsFolder = (string?)arguments["recordingsFolder"] is { Length: > 0 } folder ? InSolution(folder, "recordingsFolder") : null;
        var suite = (string?)arguments["suite"];
        var uri = new Uri(path).AbsoluteUri;
        var line = path.EndsWith(BddFileFormat.FeatureExtension, StringComparison.OrdinalIgnoreCase) ? ScenarioLine(arguments, uri) : Whole(arguments, "line", 1, int.MaxValue);
        var timeout = TimeSpan.FromMinutes(Whole(arguments, "timeoutMinutes", 1, 120) ?? 10);
        var target = _server.RecordTarget(new RecordTargetParams(uri, line, suite, recordingsFolder, (string?)arguments["voice"], null));
        if (!target.CanRecord || target.Project is not { } project || target.Filter is not { } filter)
        {
            throw new ToolProblem($"This cannot be recorded: {target.Reason}");
        }

        var build = Run("dotnet", ["build", project, "-v:minimal", "-nologo"], new Dictionary<string, string>(), timeout);
        if (build.Code != 0)
        {
            throw new ToolProblem($"The build failed, so nothing was recorded:{Environment.NewLine}{build.Tail}");
        }

        var test = Run("dotnet", ["test", project, "--no-build", "--filter", filter, "--logger", "console;verbosity=normal"], target.Environment, timeout);
        if (test.TimedOut)
        {
            throw new ToolProblem($"The recording took longer than {timeout.TotalMinutes} minutes and was ended:{Environment.NewLine}{Cap(test.Tail, 2000, fromEnd: true)}");
        }

        var videos = _server.Recordings(new RecordingsParams(uri, suite, recordingsFolder));
        var video = target.Video is { } expected ? videos.FirstOrDefault(candidate => candidate.Name == expected) : videos.FirstOrDefault();
        if (video is null)
        {
            throw new ToolProblem($"The run made no video (exit code {test.Code}):{Environment.NewLine}{test.Tail}");
        }

        var timeline = Path.ChangeExtension(video.Path, ".timeline.json");
        return new
        {
            Video = video.Path,
            video.Captions,
            Timeline = File.Exists(timeline) ? timeline : null,
            Speech = File.Exists(timeline) ? BddVideoTimeline.Load(timeline).Speech?.Summary : null,
            Passed = test.Code == 0,
            Output = test.Code == 0 ? null : test.Tail,
        };
    }

    // The hand-over page; its videos and path relative to the solution, as every tool's paths are (M5).
    private object WriteReview(JsonObject arguments)
    {
        var videos = (arguments["videos"] as JsonArray)?.Select(node => (string?)node).OfType<string>().Select(video => Path.GetFullPath(video, _solutionDir)).ToList() ?? [];
        if (videos.Count == 0)
        {
            throw new ToolProblem("'videos' is required: the videos record_video made.");
        }

        var missing = videos.Where(video => !File.Exists(video)).ToList();
        if (missing.Count > 0)
        {
            throw new ToolProblem($"No video at {string.Join(", ", missing)}.");
        }

        var page = (string?)arguments["path"] is { Length: > 0 } named
            ? InSolution(named, "path")
            : Path.Combine(Path.GetDirectoryName(videos[0])!, $"review-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.html");
        var changes = (arguments["changes"] as JsonArray)?.Select(node => (string?)node).OfType<string>().ToList() ?? [];
        BddReviewPage.Write(page, Required(arguments, "title"), (string?)arguments["summary"], changes, videos);
        return new { Page = page, Url = new Uri(page).AbsoluteUri };
    }

    // A path an agent gave for a file the tool writes: relative to the solution, and inside it, as write_code's
    // are (review 09, finding 4). An agent following text it found elsewhere cannot write over files outside.
    private string InSolution(string path, string name)
    {
        var full = Path.GetFullPath(path, _solutionDir);
        var relative = Path.GetRelativePath(_solutionDir, full);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ToolProblem($"'{name}' is outside the solution ({full}): give a path inside {_solutionDir}.");
        }

        return full;
    }

    // A process to its end, its output kept: the last lines say why a build or a run failed.
    private static (int Code, string Tail) Run(string command, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> variables)
    {
        var (code, tail, _) = Run(command, arguments, variables, Timeout.InfiniteTimeSpan);
        return (code, tail);
    }

    // As above, ended with its process tree when it runs longer than timeout.
    private static (int Code, string Tail, bool TimedOut) Run(string command, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> variables, TimeSpan timeout)
    {
        var info = new ProcessStartInfo(command) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in variables)
        {
            info.Environment[name] = value;
        }

        var lines = new Queue<string>();
        void Keep(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (lines)
            {
                lines.Enqueue(line);
                if (lines.Count > 40)
                {
                    lines.Dequeue();
                }
            }
        }

        using var process = Process.Start(info) ?? throw new ToolProblem($"{command} did not start.");
        process.OutputDataReceived += (_, e) => Keep(e.Data);
        process.ErrorDataReceived += (_, e) => Keep(e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var timedOut = !process.WaitForExit(timeout);
        if (timedOut)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        lock (lines)
        {
            return (timedOut ? -1 : process.ExitCode, string.Join(Environment.NewLine, lines), timedOut);
        }
    }

    // Every suite, each a row even when it cannot load (M11): its reason says why, and the other suites still answer.
    private object ListSuites(JsonObject arguments)
        => Capped("suites", _server.Explorer(new ExplorerParams(null, null)).Suites.Select(name =>
        {
            try
            {
                var suite = SuiteNamed(name);
                var built = suite.TryGetCatalogs(out var reason) is not null;
                return new { Name = suite.Name, Head = TryHead(suite), Config = (string?)suite.ConfigPath, Built = built, Reason = reason };
            }
            catch (Exception exception) when (NotLoaded(exception) || exception is BddWorkspaceException)
            {
                return new { Name = name, Head = (string?)null, Config = (string?)null, Built = false, Reason = (string?)$"It cannot load ({exception.GetType().Name}: {exception.Message}): build it again." };
            }
        }), arguments);

    // A suite's phrases: those holding the query's words, or one page's; a large suite asked with neither gives its pages
    // and their counts instead, so one call never sends thousands of phrases (M3).
    private static object ListSteps(BddSuite suite, JsonObject arguments)
    {
        const int Large = 200;
        var query = (string?)arguments["query"];
        var page = (string?)arguments["page"];
        var catalogs = Catalogs(suite);
        var words = (query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var steps = catalogs.Default.Patterns
            .Where(pattern => pattern.Callable is null)
            .Select(pattern => new
            {
                Keyword = pattern.Keyword.ToString(),
                pattern.Phrase,
                Parameters = pattern.Parameters.Select(parameter => parameter.ToString()).ToList(),
                From = pattern.ControlVerb is not null ? "control verb, on the current page"
                    : pattern.Page is { } on ? $"page {on}"
                    : pattern.Namespace.Length > 0 ? $"namespace {pattern.Namespace} (the file must use it)"
                    : pattern.CommandId,
                pattern.Page,
            })
            .Concat(suite.StepFiles.SelectMany(path => BddScenarioFiles.ParsePhraseFile(path).Document?.Callables ?? []).Select(callable => new
            {
                Keyword = "Any",
                callable.Phrase,
                Parameters = callable.Declared.Select(parameter => $"{{{parameter.Name}: {parameter.TypeName}}}").ToList(),
                From = $"{callable} in {Path.GetFileName(callable.Source.FilePath)} (a feature file under that folder sees it)",
                Page = (string?)null,
            }))
            .Where(step => words.All(word => step.Phrase.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .Where(step => page is null || string.Equals(step.Page, page, StringComparison.OrdinalIgnoreCase))
            .OrderBy(step => step.Keyword)
            .ThenBy(step => step.Phrase, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (query is null && page is null && steps.Count > Large)
        {
            return new
            {
                Suite = suite.Name,
                Count = steps.Count,
                Pages = steps.GroupBy(step => step.Page ?? "(no page)").OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase).Select(group => new { Page = group.Key, Phrases = group.Count() }),
                Next = $"Over {Large} phrases: ask again with 'query' (words of the phrase) or 'page' (one page's).",
            };
        }

        var (shown, more) = Cut(steps.Select(step => new { step.Keyword, step.Phrase, step.Parameters, step.From }), arguments);
        return new { Suite = suite.Name, Count = steps.Count, Steps = shown, More = more };
    }

    // Without 'page' each page's name and control count; with it, that page's controls (M3).
    private static object ListPages(BddSuite suite, JsonObject arguments)
    {
        var pages = UatPageSet.Of(Catalogs(suite).Default).Values.OrderBy(page => page.Key, StringComparer.OrdinalIgnoreCase).ToList();
        if ((string?)arguments["page"] is not { Length: > 0 } named)
        {
            return Capped("pages", pages.Select(page => new { Page = page.Key, Class = page.PageType.FullName, Controls = page.Controls.Count }), arguments);
        }

        var one = pages.FirstOrDefault(page => string.Equals(page.Key, named, StringComparison.OrdinalIgnoreCase))
            ?? throw new ToolProblem($"No page '{named}' in {suite.Name}. The pages are {string.Join(", ", pages.Select(page => page.Key))}.");
        var (shown, more) = Cut(one.Controls.Select(control => new { control.Name, Type = control.ControlType.Name, control.NavigatesTo }), arguments);
        return new { Page = one.Key, Class = one.PageType.FullName, Controls = shown, More = more };
    }

    // find_step_usages: each step's file relative to the solution and its line one-based, as every tool gives them (M5).
    private object Usages(JsonObject arguments)
    {
        var usages = _server.Usages(new UsagesParams(Required(arguments, "symbol")));
        var (shown, more) = Cut(usages.Usages.Select(usage => new { File = Path.GetRelativePath(_solutionDir, new Uri(usage.Uri).LocalPath), Line = usage.Line + 1, usage.Text, usage.Scenario }), arguments);
        return new { usages.Steps, usages.Scenarios, Usages = shown, More = more };
    }

    // The paused page's controls, at most 'max' of them.
    private object StepState(JsonObject arguments)
    {
        var read = _server.StepReadPageAsync().GetAwaiter().GetResult();
        if (read.State is not { } state)
        {
            throw new ToolProblem($"No page state: {read.Reason}");
        }

        var (shown, more) = Cut(state.Controls.Select(control => new { control.Name, control.Kind, control.Shown, control.Value, control.Unreadable }), arguments);
        return new { state.Page, state.Reason, Controls = shown, More = more };
    }

    // A test recording's steps resolved, at most 'max' of them; the gaps are every unresolved one.
    private object ResolveEvents(JsonObject arguments)
    {
        var test = _server.Scraper.ResolveActions((string?)arguments["project"], Required(arguments, "session"));
        var (shown, more) = Cut(test.Steps, arguments);
        return new { test.Session, test.Name, test.StartPage, test.StartPageObject, Steps = shown, More = more, test.Gaps };
    }

    // Each draft run once, ended after timeoutMinutes.
    private object RunDrafts(JsonObject arguments)
    {
        var minutes = Whole(arguments, "timeoutMinutes", 1, 120) ?? 10;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(minutes));
        try
        {
            return _server.ScraperRunTest(new ScraperTestParams((string?)arguments["project"], Required(arguments, "session"), null, null), timeout.Token).GetAwaiter().GetResult() switch
            {
                { Problem: { } problem } => throw new ScraperProblem(problem),
                var runs => runs.Value!,
            };
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new ToolProblem($"The drafts ran longer than {minutes} minutes and were ended: give timeoutMinutes for longer.");
        }
    }

    // A project's recorded pages: each with its newest capture and how many it has; with 'page', that page's captures (M3).
    private object RecordedPages(JsonObject arguments)
    {
        var project = (string?)arguments["project"];
        var made = _server.Scraper.PageObjects(project);
        var pages = _server.Scraper.Pages(project);
        static object Capture(Bruinet.Scraper.Projects.PageCapture capture) => new { capture.Session, Event = capture.Event.Number, capture.Event.At, capture.Event.Nodes };
        if ((string?)arguments["page"] is { Length: > 0 } key)
        {
            var page = pages.FirstOrDefault(candidate => candidate.Key == key)
                ?? throw new ToolProblem($"No recorded page '{key}'. The pages are {string.Join(", ", pages.Select(candidate => candidate.Key))}.");
            var (shown, more) = Cut(page.Captures.Select(Capture), arguments);
            return new { page.Key, Captures = shown, More = more };
        }

        return Capped("pages", pages.Select(page => new
        {
            page.Key,
            Name = page.Latest.Event.DisplayName,
            page.Latest.Event.Location,
            made.GetValueOrDefault(page.Key).PageObject,
            ChangedSinceGenerated = made.GetValueOrDefault(page.Key).Changed,
            Newest = Capture(page.Latest),
            Captures = page.Captures.Count,
        }), arguments);
    }

    private object MatchControls(JsonObject arguments)
    {
        var matches = _server.Scraper.Match((string?)arguments["project"], Required(arguments, "page"));
        var (shown, more) = Cut(matches.Matches, arguments);
        return new { matches.Page, matches.PageObject, matches.ChangedSinceGenerated, Matches = shown, More = more };
    }

    // A page's changes, at most 'max' of each kind; 'more' counts every one left out (M3).
    private object DiffPage(JsonObject arguments)
    {
        var max = MaxOf(arguments);
        var diff = _server.Scraper.Diff((string?)arguments["project"], Required(arguments, "page"), (string?)arguments["fromSession"], Whole(arguments, "fromEvent", 1, int.MaxValue), (string?)arguments["toSession"], Whole(arguments, "toEvent", 1, int.MaxValue), max);
        var more = diff.AddedCount + diff.RemovedCount + diff.ChangedCount - diff.Added.Count - diff.Removed.Count - diff.Changed.Count;
        return new { diff.AddedCount, diff.RemovedCount, diff.ChangedCount, diff.Added, diff.Removed, diff.Changed, More = more > 0 ? more : (int?)null };
    }

    private object DraftFeature(string path, string? method, string? feature)
    {
        var source = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(_solutionDir, path));
        if (!File.Exists(source))
        {
            throw new ToolProblem($"'{source}' does not exist.");
        }

        var suite = _server.Explorer(new ExplorerParams(null, null)).Suites
            .Select(SuiteNamed)
            .Where(candidate => source.StartsWith(Path.GetDirectoryName(candidate.ConfigPath)! + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.ConfigPath.Length)
            .FirstOrDefault() ?? throw new ToolProblem($"'{source}' is in no suite's folder.");
        var phrases = DraftPhrases(suite);

        FeatureDraft draft;
        try
        {
            draft = CSharpTestDraft.Draft(File.ReadAllText(source), method, phrases);
        }
        catch (ArgumentException exception)
        {
            throw new ToolProblem(exception.Message);
        }

        var suiteDir = Path.GetDirectoryName(suite.ConfigPath)!;
        var moduleDir = draft.Module is null ? null : Path.Combine(suiteDir, "Modules", draft.Module);
        var target = feature is not null
            ? Path.GetFullPath(Path.IsPathRooted(feature) ? feature : Path.Combine(_solutionDir, feature))
            : Path.Combine(moduleDir is not null && Directory.Exists(moduleDir) ? Path.Combine(moduleDir, "Features") : Path.Combine(suiteDir, "Features"), draft.Name + ".feature.md");
        var used = draft.Text.Split('\n').Select(line => line.Trim()).ToHashSet(StringComparer.Ordinal);
        return new
        {
            Feature = target,
            draft.Text,
            Validation = Validate(target, draft.Text),
            Lacks = draft.Gaps,
            Cleanup = draft.Skipped,
            Similar = CSharpTestDraft.Similar(phrases)
                .Where(similar => similar.Phrases.Any(phrase => used.Any(line => line.EndsWith(" " + phrase, StringComparison.Ordinal))))
                .ToList(),
        };
    }

    private static object SimilarSteps(BddSuite suite, JsonObject arguments)
    {
        var words = ((string?)arguments["query"] ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var similar = CSharpTestDraft.Similar(DraftPhrases(suite))
            .Where(group => group.Phrases.Any(phrase => words.All(word => phrase.Contains(word, StringComparison.OrdinalIgnoreCase))))
            .ToList();
        var (shown, more) = Cut(similar, arguments);
        return new { Suite = suite.Name, Count = similar.Count, Groups = shown, More = more };
    }

    // The suite's own phrases: step definitions, not page or control grammar or callable steps.
    private static List<DraftPhrase> DraftPhrases(BddSuite suite)
        => [.. Catalogs(suite).Default.Patterns
            .Where(pattern => pattern.Callable is null && pattern.ControlVerb is null && pattern.Page is null)
            .Select(pattern => new DraftPhrase(pattern.Keyword.ToString(), pattern.Phrase, pattern.Namespace))];

    // The draft checked in the suite that holds its path, as the editor checks unsaved text.
    private Validation Validate(string path, string? text)
    {
        var full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(_solutionDir, path));
        if (text is null && !File.Exists(full))
        {
            throw new ToolProblem($"'{full}' does not exist; give its text.");
        }

        var uri = new Uri(full).AbsoluteUri;
        _server.DidOpen(new DidOpenTextDocumentParams(new TextDocumentItem(uri, "markdown", 1, text ?? File.ReadAllText(full))));
        try
        {
            var file = _server.Context(full);
            var diagnostics = _server.DiagnosticsOf(file);
            return new Validation(
                full,
                file.Suite?.Name,
                file.Catalogs is not null,
                file.Reason,
                diagnostics.All(diagnostic => diagnostic.Severity != 1) && file.Catalogs is not null,
                [.. diagnostics.Select(diagnostic => new ValidationProblem(diagnostic.Range.Start.Line + 1, diagnostic.Severity == 1 ? "error" : "warning", diagnostic.Code, diagnostic.Message))]);
        }
        finally
        {
            _server.DidClose(new DidCloseTextDocumentParams(new TextDocumentIdentifier(uri)));
        }
    }

    private BddSuite Suite(JsonObject arguments)
    {
        var name = (string?)arguments["suite"];
        var names = _server.Explorer(new ExplorerParams(null, null)).Suites;
        if (names.Count == 0)
        {
            throw new ToolProblem($"No suite under {_solutionDir}: no project has a uat.config.md or bdd.config.md.");
        }

        // Left out, the only suite; of several, the agent names one rather than get the first by chance (M7).
        if (name is null && names.Count > 1)
        {
            throw new ToolProblem($"Give 'suite': the solution has {names.Count}, {string.Join(", ", names)}.");
        }

        var found = name is null ? names[0] : names.FirstOrDefault(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
        return found is null
            ? throw new ToolProblem($"No suite '{name}'. The suites are {string.Join(", ", names)}.")
            : SuiteNamed(found);
    }

    private BddSuite SuiteNamed(string name) => _server.SuiteNamed(name) ?? throw new ToolProblem($"No suite '{name}'.");

    // The suite's test sets, with the suite loaded first: its test data is read from the assemblies a load opens, so a
    // fresh server would otherwise answer none (and set_state would refuse every name).
    private static List<string> TestSets(BddSuite suite)
    {
        Catalogs(suite);
        return [.. suite.TestDataDefinitions().SelectMany(definition => definition.TestSetNames).Distinct()];
    }

    private static BddCatalogSet Catalogs(BddSuite suite)
        => suite.TryGetCatalogs(out var reason) ?? throw new ToolProblem($"{suite.Name} is not bound: {reason}");

    private static string? TryHead(BddSuite suite)
    {
        try
        {
            return suite.Head;
        }
        catch (BddWorkspaceException)
        {
            return null;
        }
    }

    private static string Required(JsonObject arguments, string name)
        => (string?)arguments[name] is { Length: > 0 } value ? value : throw new ToolProblem($"'{name}' is required.");

    private static JsonObject Text(string text, bool error)
        => new() { ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }), ["isError"] = error };

    private static JsonObject Error(JsonNode? id, int code, string message)
        => new() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = message } };

    private sealed class McpException(int code, string message) : Exception(message)
    {
        public int Code { get; } = code;
    }

    // check_scenario's answer: whether the file binds, and each problem by line.
    private sealed record Validation(string File, string? Suite, bool Bound, string? Reason, bool Valid, IReadOnlyList<ValidationProblem> Problems);

    private sealed record ValidationProblem(int Line, string Severity, string? Code, string Message);

    // A tool that cannot answer says why, as its result: the agent reads it and can act on it.
    private sealed class ToolProblem(string message) : Exception(message);
}
