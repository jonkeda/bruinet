using Bruinet.Core.Composition;
using Bruinet.Core.Testing;
using Nerdbank.Streams;
using StreamJsonRpc;
using Xunit;

namespace Bruinet.Bdd.LanguageServer.Tests;

/// <summary>The server over JSON-RPC with LSP framing, on an in-memory stream, as a client talks to it.</summary>
[Collection(FfmpegVariable.Name)]
public sealed class LanguageServerTests : IAsyncLifetime
{
    private readonly string _solution = Path.Combine(Path.GetTempPath(), "Bruinet", "LanguageServerTests", Guid.NewGuid().ToString("N"));
    private readonly BddLanguageServer _server = new();
    private JsonRpc _client = null!;
    private JsonRpc _serverRpc = null!;
    private readonly TaskCompletionSource<string> _resultsChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly PublishedDiagnostics _published = new();

    private string Shelf => Path.Combine(_solution, "Shelf.Steps");

    private string Linked => Path.Combine(_solution, "Shelf.Web");

    private string Feature => Path.Combine(Shelf, "Features", "shelf.feature.md");

    private string Built => Path.Combine(Shelf, "bin", "Debug", "net10.0", "Shelf.Steps.dll");

    public Task InitializeAsync()
    {
        // Two suites: one holds the feature file, the other links it.
        Directory.CreateDirectory(Path.Combine(Shelf, "Features"));
        Directory.CreateDirectory(Linked);
        File.WriteAllText(Path.Combine(Shelf, "bdd.config.md"), Config("bin/Debug/net10.0/Shelf.Steps.dll"));
        File.WriteAllText(Path.Combine(Linked, "uat.config.md"), Config("bin/Debug/net10.0/Shelf.Web.dll"));
        File.WriteAllText(Path.Combine(Linked, "Shelf.Web.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <None Include="..\Shelf.Steps\Features\**\*.feature.md" Link="Features\%(RecursiveDir)%(Filename)%(Extension)" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Feature, """
            # Feature: Shelf

            ## Scenario: One

            ```gherkin
            When I shelve "Dune"
            ```
            """);

        var (clientStream, serverStream) = FullDuplexStream.CreatePair();
        _serverRpc = _server.Attach(serverStream, serverStream);
        var formatter = new SystemTextJsonFormatter { JsonSerializerOptions = { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase } };
        _client = new JsonRpc(new HeaderDelimitedMessageHandler(clientStream, clientStream, formatter));
        _client.AddLocalRpcMethod("bruinet/resultsChanged", new Action<string>(uri => _resultsChanged.TrySetResult(uri)));
        _client.AddLocalRpcTarget(_published, new JsonRpcTargetOptions { UseSingleObjectParameterDeserialization = true });
        _client.StartListening();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _serverRpc.Dispose();
        _server.Dispose();
        try
        {
            Directory.Delete(_solution, recursive: true);
        }
        catch (IOException)
        {
            // A load context not collected yet; the temp folder is cleaned by the OS.
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Initialize_ListsTheSuites()
    {
        var result = await InitializeAsync(_client);

        Assert.Equal(1, result.Capabilities.TextDocumentSync.Change);
        Assert.True(result.Capabilities.TextDocumentSync.OpenClose);
        Assert.Equal(["Shelf.Steps", "Shelf.Web"], result.Suites.Select(suite => suite.Name).Order());
    }

    [Fact]
    public async Task Preview_BeforeABuild_ParsesOnly_AndSaysWhy()
    {
        await InitializeAsync(_client);

        var preview = await PreviewAsync();

        Assert.Equal("parse-only", preview.BindState);
        Assert.Contains("Build the UAT project first", preview.Reason);
        Assert.Contains("Parsed only, not bound", preview.Html);
    }

    [Fact]
    public async Task BuildDone_ReloadsTheCatalog_AndThePreviewBinds()
    {
        await InitializeAsync(_client);
        Assert.Equal("parse-only", (await PreviewAsync()).BindState);

        Build();
        await _client.NotifyWithParameterObjectAsync("bruinet/buildDone", new { });
        var preview = await PreviewAsync();

        Assert.Equal("bound", preview.BindState);
        Assert.Equal("Shelf.Steps", preview.Suite);
        Assert.Contains("uat-bound", preview.Html);
    }

    [Fact]
    public async Task OpenAndChange_ThePreviewShowsTheEditorsText()
    {
        Build();
        await InitializeAsync(_client);
        var uri = new Uri(Feature).AbsoluteUri;

        await _client.NotifyWithParameterObjectAsync("textDocument/didOpen", new { textDocument = new { uri, languageId = "markdown", version = 1, text = File.ReadAllText(Feature) } });
        await _client.NotifyWithParameterObjectAsync("textDocument/didChange", new
        {
            textDocument = new { uri, version = 2 },
            contentChanges = new[] { new { text = "# Feature: Shelf\n\n## Scenario: One\n\n```gherkin\nWhen I dust the shelf\n```\n" } },
        });
        var preview = await PreviewAsync();

        Assert.Equal(2, preview.Version);
        Assert.Contains("I dust the shelf", preview.Html);
        Assert.Contains("data-code=\"UATB001\"", preview.Html);
    }

    [Fact]
    public async Task ALinkedFile_OffersEachSuite_AndBindsInThePickedOne()
    {
        Build();
        await InitializeAsync(_client);

        var first = await PreviewAsync();
        var picked = await PreviewAsync("Shelf.Web");

        Assert.Equal(["Shelf.Steps", "Shelf.Web"], first.Suites);
        Assert.Equal("Shelf.Steps", first.Suite);
        Assert.Equal("Shelf.Web", picked.Suite);
        Assert.Equal("parse-only", picked.BindState);
    }

    [Fact]
    public async Task AFileNoSuiteHolds_ParsesOnly_SayingSo()
    {
        await InitializeAsync(_client);
        var loose = Path.Combine(_solution, "loose.feature.md");
        File.WriteAllText(loose, "# Feature: Loose\n");

        var preview = await _client.InvokeWithParameterObjectAsync<PreviewResult>("bruinet/preview", new { uri = new Uri(loose).AbsoluteUri });

        Assert.Empty(preview.Suites);
        Assert.Contains("No suite holds or links this file", preview.Reason);
    }

    [Fact]
    public async Task PreviewHost_IsThePageTheWebviewLoads()
    {
        await InitializeAsync(_client);

        var host = await _client.InvokeWithParameterObjectAsync<PreviewHostResult>("bruinet/previewHost", new { });

        Assert.Contains("<main id=\"uat-root\"></main>", host.Html);
    }

    [Fact]
    public async Task Preview_ShowsTheNewestRun_FoundThroughTheManifests()
    {
        Build();
        File.WriteAllText(Path.Combine(_solution, "Shelf.sln"), string.Empty);
        WriteRun("20260101-000000-older1", "failed", "Failed");
        WriteRun("20260102-000000-newer2", "passed", "Passed");
        await InitializeAsync(_client);

        var preview = await PreviewAsync();

        Assert.Equal(Path.Combine(_solution, "TestResults"), preview.ResultsRoot);
        Assert.Contains("<span class=\"uat-result\" data-status=\"passed\"", preview.Html);
        Assert.Contains("<span class=\"uat-status\" data-status=\"passed\"", preview.Html);
    }

    // A solution in a sub-folder with its own .sln lists a suite outside it; the
    // suite's runs write TestResults above the suite's project, not beside the open solution.
    [Fact]
    public async Task Preview_FindsTheRuns_WhereTheSuitesRunsWriteThem_NotBesideTheOpenSolution()
    {
        Build();
        File.WriteAllText(Path.Combine(_solution, "Shelf.sln"), string.Empty);
        var src = Directory.CreateDirectory(Path.Combine(_solution, "src")).FullName;
        File.WriteAllText(Path.Combine(src, "Only.sln"), "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Shelf.Steps\", \"..\\Shelf.Steps\\Shelf.Steps.csproj\", \"{11111111-1111-1111-1111-111111111111}\"\nEndProject\n");
        WriteRun("20260102-000000-newer2", "passed", "Passed");
        await InitializeAsync(_client, solutionDir: src);

        var preview = await PreviewAsync();

        Assert.Equal(Path.Combine(_solution, "TestResults"), preview.ResultsRoot);
        Assert.Contains("<span class=\"uat-status\" data-status=\"passed\"", preview.Html);
    }

    [Fact]
    public async Task ANewRun_TellsTheClientTheResultsChanged()
    {
        Build();
        File.WriteAllText(Path.Combine(_solution, "Shelf.sln"), string.Empty);
        Directory.CreateDirectory(Path.Combine(_solution, "TestResults"));
        await InitializeAsync(_client);
        await _client.NotifyWithParameterObjectAsync("textDocument/didOpen", new { textDocument = new { uri = new Uri(Feature).AbsoluteUri, version = 1, text = File.ReadAllText(Feature) } });

        WriteRun("20260103-000000-latest", "passed", "Passed");

        Assert.Equal(new Uri(Feature).AbsoluteUri, await _resultsChanged.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    // A run as UatScenarioTestBase writes it: the report, and its manifest record naming suite and file.
    private void WriteRun(string runId, string status, string stepStatus)
    {
        var run = Path.Combine(_solution, "TestResults", runId);
        var uat = Directory.CreateDirectory(Path.Combine(run, "suites", "testhost", "uat")).FullName;
        File.WriteAllText(Path.Combine(uat, "shelf__One__result.json"), $$"""
            { "scenario": "One", "file": "Features/shelf.feature.md", "line": 3, "status": "{{status}}", "durationMs": 12,
              "steps": [ { "line": 6, "file": "Features/shelf.feature.md", "callLine": null, "status": "{{stepStatus}}", "message": null } ] }
            """);
        File.WriteAllText(Path.Combine(run, "manifest.json"), $$"""
            { "RunId": "{{runId}}", "Suites": [ { "Name": "testhost", "Artifacts": [ {
                "Kind": "uat-scenario", "Name": "One", "Path": "suites\\testhost\\uat\\shelf__One__result.json", "Reason": "{{status}}", "Status": "{{status}}",
                "CreatedAtUtc": "2026-01-01T00:00:00Z", "Metadata": { "suite": "Shelf.Steps", "file": "Features/shelf.feature.md", "line": "3" } } ] } ] }
            """);
        File.SetLastWriteTimeUtc(Path.Combine(run, "manifest.json"), DateTime.UtcNow.AddMinutes(runId.EndsWith("older1", StringComparison.Ordinal) ? -10 : 0));
    }

    [Fact]
    public async Task RunTarget_ForAScenarioHeading_IsTheSuitesProject_AndItsAddress()
    {
        Build();
        File.WriteAllText(Path.Combine(Shelf, "Shelf.Steps.csproj"), "<Project />");
        await InitializeAsync(_client);

        var target = await _client.InvokeWithParameterObjectAsync<RunTargetResult>("bruinet/runTarget", new { uri = new Uri(Feature).AbsoluteUri, line = 3 });

        Assert.True(target.CanRun, target.Reason);
        Assert.Equal(Path.Combine(Shelf, "Shelf.Steps.csproj"), target.Project);
        Assert.Equal("BddScenario=Features/shelf.feature.md#One", target.Filter);
        Assert.Equal(["One"], target.Scenarios);
    }

    [Fact]
    public async Task RunTarget_ForTheFile_MatchesEachOfItsScenarios()
    {
        Build();
        File.WriteAllText(Path.Combine(Shelf, "Shelf.Steps.csproj"), "<Project />");
        await InitializeAsync(_client);

        var target = await _client.InvokeWithParameterObjectAsync<RunTargetResult>("bruinet/runTarget", new { uri = new Uri(Feature).AbsoluteUri });

        Assert.Equal("BddScenario~Features/shelf.feature.md#", target.Filter);
    }

    [Fact]
    public async Task RecordTarget_ForAVideoScript_RunsItsScenes_WithTheRecordingVariables()
    {
        Build();
        File.WriteAllText(Path.Combine(Shelf, "Shelf.Steps.csproj"), "<Project />");
        var script = Path.Combine(Shelf, "Features", "shelf.video.md");
        File.WriteAllText(script, "# Video: The shelf\n\n## Scene: Shelving\n\nScenario: Features/shelf.feature.md#One\n");
        await InitializeAsync(_client);

        var target = await WithFfmpegAsync(() => _client.InvokeWithParameterObjectAsync<RecordTargetResult>("bruinet/recordTarget", new { uri = new Uri(script).AbsoluteUri, recordingsFolder = Path.Combine(_solution, "Videos"), beat = 250, touchColor = "#00A0FF" }));

        Assert.True(target.CanRecord, target.Reason);
        Assert.Equal(Path.Combine(Shelf, "Shelf.Steps.csproj"), target.Project);
        Assert.Equal("BddScenario=Features/shelf.feature.md#One", target.Filter);
        Assert.Equal(script, target.Environment["BRUINET_RECORD"]);
        Assert.Equal(Path.Combine(_solution, "Videos"), target.Environment["BRUINET_RECORD_DIR"]);
        Assert.Equal("250", target.Environment["BRUINET_BEAT"]);
        Assert.Equal("#00A0FF", target.Environment["BRUINET_TOUCH_COLOR"]);
        Assert.Equal(Path.Combine(_solution, "Videos", "shelf.run"), target.Environment["BRUINET_RECORD_OUT"]);
        Assert.Equal("shelf.mp4", target.Video);
    }

    [Fact]
    public async Task RecordTarget_ForAScenario_RecordsItOnItsOwn()
    {
        Build();
        File.WriteAllText(Path.Combine(Shelf, "Shelf.Steps.csproj"), "<Project />");
        await InitializeAsync(_client);

        var target = await WithFfmpegAsync(() => _client.InvokeWithParameterObjectAsync<RecordTargetResult>("bruinet/recordTarget", new { uri = new Uri(Feature).AbsoluteUri, line = 3 }));

        Assert.True(target.CanRecord, target.Reason);
        Assert.Equal("BddScenario=Features/shelf.feature.md#One", target.Filter);
        Assert.Equal("1", target.Environment["BRUINET_RECORD"]);
        Assert.Null(target.Video);
        Assert.EndsWith(Path.Combine("TestResults", "recordings"), target.Folder, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecordTarget_WithoutFfmpeg_SaysHowToGetIt()
    {
        Build();
        await InitializeAsync(_client);
        var original = Environment.GetEnvironmentVariable("BRUINET_FFMPEG");
        Environment.SetEnvironmentVariable("BRUINET_FFMPEG", Path.Combine(_solution, "no-ffmpeg.exe"));
        try
        {
            var target = await _client.InvokeWithParameterObjectAsync<RecordTargetResult>("bruinet/recordTarget", new { uri = new Uri(Feature).AbsoluteUri, line = 3 });

            Assert.False(target.CanRecord);
            Assert.Contains("winget install", target.Reason, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BRUINET_FFMPEG", original);
        }
    }

    [Fact]
    public async Task Recordings_OfAVideoScript_AreItsVideos_NewestFirst()
    {
        var script = Path.Combine(Shelf, "Features", "shelf.video.md");
        File.WriteAllText(script, "# Video: The shelf\n");
        var videos = Directory.CreateDirectory(Path.Combine(_solution, "Videos")).FullName;
        File.WriteAllText(Path.Combine(videos, "shelf-20260101-090000.mp4"), string.Empty);
        File.SetLastWriteTimeUtc(Path.Combine(videos, "shelf-20260101-090000.mp4"), new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc));
        File.WriteAllText(Path.Combine(videos, "shelf-20260102-090000.mp4"), string.Empty);
        File.WriteAllText(Path.Combine(videos, "shelf-20260102-090000.srt"), string.Empty);
        File.WriteAllText(Path.Combine(videos, "other-20260102-090000.mp4"), string.Empty);
        await InitializeAsync(_client);

        var recordings = await _client.InvokeWithParameterObjectAsync<RecordingEntry[]>("bruinet/recordings", new { uri = new Uri(script).AbsoluteUri, recordingsFolder = videos });

        Assert.Equal(["shelf-20260102-090000.mp4", "shelf-20260101-090000.mp4"], recordings.Select(video => video.Name));
        Assert.Equal(Path.Combine(videos, "shelf-20260102-090000.srt"), recordings[0].Captions);
    }

    // Record checks for ffmpeg first: any file named by BRUINET_FFMPEG passes that check.
    private static async Task<T> WithFfmpegAsync<T>(Func<Task<T>> action)
    {
        var original = Environment.GetEnvironmentVariable("BRUINET_FFMPEG");
        Environment.SetEnvironmentVariable("BRUINET_FFMPEG", Environment.ProcessPath);
        try
        {
            return await action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("BRUINET_FFMPEG", original);
        }
    }

    [Fact]
    public async Task RunTarget_BeforeABuild_SaysWhy_AndNamesTheProjectToBuild()
    {
        File.WriteAllText(Path.Combine(Shelf, "Shelf.Steps.csproj"), "<Project />");
        await InitializeAsync(_client);

        var target = await _client.InvokeWithParameterObjectAsync<RunTargetResult>("bruinet/runTarget", new { uri = new Uri(Feature).AbsoluteUri, line = 3 });

        Assert.False(target.CanRun);
        Assert.Contains("Build the UAT project first", target.Reason);
        Assert.Equal(Path.Combine(Shelf, "Shelf.Steps.csproj"), target.Project);
        Assert.Null(target.Filter);
    }

    [Fact]
    public async Task AnOpenFile_GetsTheGatesDiagnostics_AtTheStepsLine()
    {
        Build();
        await InitializeAsync(_client, editor: true);
        var uri = new Uri(Feature).AbsoluteUri;

        await _client.NotifyWithParameterObjectAsync("textDocument/didOpen", new
        {
            textDocument = new { uri, version = 1, text = "# Feature: Shelf\n\n## Scenario: One\n\n```gherkin\n  When I dust the shelf\n```\n" },
        });
        var published = await _published.WaitAsync(uri, diagnostics => diagnostics.Count > 0);

        var diagnostic = Assert.Single(published.Diagnostics, d => d.Code == "UATB001");
        Assert.Equal(1, diagnostic.Severity);
        Assert.Equal("Bruinet BDD", diagnostic.Source);
        Assert.Equal(new Range(new Position(5, 2), new Position(5, 23)), diagnostic.Range);
        Assert.Equal(1, published.Version);
    }

    [Fact]
    public async Task FixingTheStep_ClearsItsDiagnostic()
    {
        Build();
        await InitializeAsync(_client, editor: true);
        var uri = new Uri(Feature).AbsoluteUri;
        await _client.NotifyWithParameterObjectAsync("textDocument/didOpen", new { textDocument = new { uri, version = 1, text = "# Feature: Shelf\n\n## Scenario: One\n\n```gherkin\nWhen I dust the shelf\n```\n" } });
        await _published.WaitAsync(uri, diagnostics => diagnostics.Count > 0);

        await _client.NotifyWithParameterObjectAsync("textDocument/didChange", new
        {
            textDocument = new { uri, version = 2 },
            contentChanges = new[] { new { text = File.ReadAllText(Feature) } },
        });

        Assert.Empty((await _published.WaitAsync(uri, diagnostics => diagnostics.Count == 0)).Diagnostics);
    }

    [Fact]
    public async Task Save_ChecksTheWholeSuite_ClosedFilesToo()
    {
        Build();
        var other = Path.Combine(Shelf, "Features", "other.feature.md");
        File.WriteAllText(other, "# Feature: Other\n\n## Scenario: Two\n\n```gherkin\nWhen I lose the shelf\n```\n");
        await InitializeAsync(_client, editor: true);

        await _client.NotifyWithParameterObjectAsync("textDocument/didSave", new { textDocument = new { uri = new Uri(Feature).AbsoluteUri } });

        var published = await _published.WaitAsync(new Uri(other).AbsoluteUri, diagnostics => diagnostics.Count > 0);
        Assert.Contains(published.Diagnostics, d => d.Code == "UATB001" && d.Range.Start.Line == 5);
        Assert.Null(published.Version);
    }

    [Fact]
    public async Task AStepThatFailedInTheLastRun_IsAnErrorAtItsLine()
    {
        Build();
        File.WriteAllText(Path.Combine(_solution, "Shelf.sln"), string.Empty);
        WriteRun("20260104-000000-failed", "failed", "Failed");
        await InitializeAsync(_client, editor: true);
        var uri = new Uri(Feature).AbsoluteUri;

        await _client.NotifyWithParameterObjectAsync("textDocument/didOpen", new { textDocument = new { uri, version = 1, text = File.ReadAllText(Feature) } });

        var published = await _published.WaitAsync(uri, diagnostics => diagnostics.Count > 0);
        var failed = Assert.Single(published.Diagnostics);
        Assert.Equal("UATRUN", failed.Code);
        Assert.Equal(5, failed.Range.Start.Line);
        Assert.StartsWith("Failed in the last run of 'One'", failed.Message);
    }

    [Fact]
    public async Task Definition_OfACSharpStep_IsItsMethod_ThroughThePdb()
    {
        Build();
        await InitializeAsync(_client, editor: true);
        var uri = new Uri(Feature).AbsoluteUri;
        await _client.NotifyWithParameterObjectAsync("textDocument/didOpen", new { textDocument = new { uri, version = 1, text = File.ReadAllText(Feature) } });

        var locations = await _client.InvokeWithParameterObjectAsync<IReadOnlyList<Location>>("textDocument/definition", new { textDocument = new { uri }, position = new { line = 5, character = 3 } });

        var location = Assert.Single(locations);
        Assert.EndsWith("LanguageServerTests.cs", new Uri(location.Uri).LocalPath);
        Assert.Contains("public void Shelve(", File.ReadAllLines(new Uri(location.Uri).LocalPath)[location.Range.Start.Line]);
    }

    [Fact]
    public async Task Hover_ShowsWhatTheStepBindsTo_AndItsArgumentsAsRead()
    {
        Build();
        await InitializeAsync(_client, editor: true);
        var uri = new Uri(Feature).AbsoluteUri;
        await _client.NotifyWithParameterObjectAsync("textDocument/didOpen", new { textDocument = new { uri, version = 1, text = "# Feature: Shelf\n\n## Scenario: One\n\n```gherkin\nWhen I lend \"Dune\" until 3-Mar-2027\n```\n" } });

        var hover = await _client.InvokeWithParameterObjectAsync<Hover>("textDocument/hover", new { textDocument = new { uri }, position = new { line = 5, character = 3 } });

        Assert.Equal("markdown", hover.Contents.Kind);
        Assert.Contains("**binds to** `Bruinet.Bdd.LanguageServer.Tests.ShelfSteps.Lend`", hover.Contents.Value);
        Assert.Contains("| `title` | \"Dune\" | Dune |", hover.Contents.Value);
        Assert.Contains("| `due` | 3-Mar-2027 | 03-Mar-2027 |", hover.Contents.Value);
    }

    [Fact]
    public async Task Explorer_Results_ShowsTheLastRunOfEachScenario_OpeningItsHeading()
    {
        Build();
        File.WriteAllText(Path.Combine(_solution, "Shelf.sln"), string.Empty);
        WriteRun("20260105-000000-explorer", "failed", "Failed");
        await InitializeAsync(_client);

        var results = await _client.InvokeWithParameterObjectAsync<ExplorerResult>("bruinet/explorer", new { view = "results", suite = "Shelf.Steps" });

        Assert.Contains("1 scenarios with a run in Shelf.Steps, 1 failed", results.Html);
        Assert.Contains($"data-open=\"{new Uri(Feature).AbsoluteUri}\" data-line=\"3\"><span class=\"uat-result\" data-status=\"failed\"", results.Html);
    }

    [Fact]
    public async Task Scenarios_GivesEachSuitesTests_WithTheAddressEachRunsBy()
    {
        Build();
        File.WriteAllText(Path.Combine(Shelf, "Shelf.Steps.csproj"), "<Project />");
        await InitializeAsync(_client);

        var suites = await _client.InvokeWithParameterObjectAsync<IReadOnlyList<SuiteScenarios>>("bruinet/scenarios", new { });

        var shelf = Assert.Single(suites, suite => suite.Suite == "Shelf.Steps");
        Assert.True(shelf.Runnable, shelf.Reason);
        var file = Assert.Single(shelf.Files);
        Assert.Equal(new Uri(Feature).AbsoluteUri, file.Uri);
        Assert.Equal(new ScenarioTest("One", 3, "BddScenario=Features/shelf.feature.md#One"), Assert.Single(file.Scenarios));
        Assert.False(Assert.Single(suites, suite => suite.Suite == "Shelf.Web").Runnable);
    }

    [Fact]
    public async Task LastRuns_GivesEachScenariosStatus_AndItsFailedStep()
    {
        Build();
        File.WriteAllText(Path.Combine(_solution, "Shelf.sln"), string.Empty);
        WriteRun("20260106-000000-lastruns", "failed", "Failed");
        await InitializeAsync(_client);

        var runs = await _client.InvokeWithParameterObjectAsync<IReadOnlyList<ScenarioResult>>("bruinet/lastRuns", new { uri = new Uri(Feature).AbsoluteUri });

        Assert.Equal(new ScenarioResult("One", "failed", 12, null, 6), Assert.Single(runs));
    }

    private Task<InitializeResult> InitializeAsync(JsonRpc client, bool editor = false, string? solutionDir = null)
        => client.InvokeWithParameterObjectAsync<InitializeResult>("initialize", new
        {
            processId = (int?)null,
            initializationOptions = new { solutionDir = solutionDir ?? _solution },
            capabilities = editor ? new { textDocument = new { publishDiagnostics = new { } } } : null,
        });

    private Task<PreviewResult> PreviewAsync(string? suite = null)
        => _client.InvokeWithParameterObjectAsync<PreviewResult>("bruinet/preview", new { uri = new Uri(Feature).AbsoluteUri, suite });

    // What a build does: the suite's assembly (this test assembly, holding its fixture and steps) appears.
    private void Build()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Built)!);
        File.Copy(typeof(LanguageServerTests).Assembly.Location, Built, overwrite: true);
        File.Copy(Path.ChangeExtension(typeof(LanguageServerTests).Assembly.Location, ".pdb"), Path.ChangeExtension(Built, ".pdb"), overwrite: true);
    }

    private static string Config(string assembly) => $"""
        # BDD Config

        ## Runtime

        | Field | Value |
        | --- | --- |
        | Fixture | {nameof(ShelfFixture)} |

        ## Assemblies

        | Kind | Assembly |
        | --- | --- |
        | Steps | {assembly} |
        """;
}

public sealed class ShelfFixture
{
    public TestComposition Composition => throw new NotSupportedException("The server reads the fixture's type only.");
}

// The suite's test class, as a suite that runs by address has it.
public sealed class ShelfScenarioTests
{
    [Bruinet.Uat.UatScenarioTheory]
    [InlineData("x.feature.md", "x")]
    public void Scenario_Passes(string filePath, string scenarioName)
    {
        Assert.NotNull(filePath);
        Assert.NotNull(scenarioName);
    }
}

[BddStepDefinitions]
public sealed class ShelfSteps : BddStepDefinitionsBase
{
    [BddWhen("I shelve {title}")]
    public void Shelve(string title)
    {
    }

    [BddWhen("I lend {title} until {due}")]
    public void Lend(string title, DateOnly due)
    {
    }
}

/// <summary>The diagnostics the server publishes, per file, as an editor's LSP client takes them.</summary>
public sealed class PublishedDiagnostics
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PublishDiagnosticsParams> _latest = [];
    private event Action? Published;

    [JsonRpcMethod("textDocument/publishDiagnostics")]
    public void Publish(PublishDiagnosticsParams published)
    {
        lock (_gate)
        {
            _latest[published.Uri] = published;
        }

        Published?.Invoke();
    }

    /// <summary>The file's latest diagnostics once they meet the condition.</summary>
    public async Task<PublishDiagnosticsParams> WaitAsync(string uri, Func<IReadOnlyList<Diagnostic>, bool> condition)
    {
        var met = new TaskCompletionSource<PublishDiagnosticsParams>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Check()
        {
            lock (_gate)
            {
                if (_latest.TryGetValue(uri, out var latest) && condition(latest.Diagnostics))
                {
                    met.TrySetResult(latest);
                }
            }
        }

        Published += Check;
        try
        {
            Check();
            return await met.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }
        finally
        {
            Published -= Check;
        }
    }
}
