using System.Text.Json.Nodes;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Snapshots;
using Xunit;

namespace Bruinet.Bdd.LanguageServer.Tests;

/// <summary>
/// The MCP server's tool list as a whole (D2, review 03): the names, the schema, what the read-only tools may touch,
/// the cap every list has, and a suite that cannot load. Over a temp solution with the counter suite, a suite built
/// against another Bruinet, and a scraped project with one recording.
/// </summary>
public sealed class McpToolsTests : IDisposable
{
    private readonly string _solution = Path.Combine(Path.GetTempPath(), "Bruinet", "McpToolsTests", Guid.NewGuid().ToString("N"));
    private readonly string _session;
    private readonly BddMcpServer _server;

    public McpToolsTests()
    {
        Directory.CreateDirectory(Path.Combine(Counter, "Features"));
        File.WriteAllText(Path.Combine(_solution, "Tools.sln"), string.Empty);
        File.WriteAllText(Path.Combine(Counter, "uat.config.md"), Config(nameof(StepCounterFixture), string.Empty));
        File.WriteAllText(Feature, """
            # Feature: Counter

            ## Scenario: Counts

            ```gherkin
            Given the counter counts
            Then the counter has counted 1 times
            ```

            ## Scenario: Fails

            ```gherkin
            Given the counter counts
            Then the counter has counted 5 times
            ```
            """);

        var shop = Path.Combine(_solution, "Shop.UITests");
        Directory.CreateDirectory(shop);
        File.WriteAllText(Path.Combine(shop, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(shop, ScraperConfig.FileName), "## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | https://shop.example/ |\n");
        var session = ScrapeProject.Open(shop).StartSession("Orders", new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero));
        session.AddPage(Orders("https://shop.example/orders", "Ada", "Grace"));
        session.AddPage(Orders("https://shop.example/archive", "Linus"));
        _session = session.Id;
        _server = new BddMcpServer(_solution);
    }

    // Named as its assembly, as a built suite is: results carry the assembly's name as their suite.
    private string Counter => Path.Combine(_solution, typeof(StepCounterFixture).Assembly.GetName().Name!);

    private string Feature => Path.Combine(Counter, "Features", "counter.feature.md");

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
    public void TheToolList_IsTheCheckedInOne()
    {
        var listed = File.ReadAllLines(Path.Combine(Repository, "tests", "Bruinet.Cli.Tests", "Lsp", "mcp-tools.txt")).Where(line => line.Length > 0);

        Assert.Equal(listed, Listed().Select(tool => (string)tool["name"]!));
    }

    [Fact]
    public void EveryTool_SaysItInUnder500Characters_WithItsRequiredParametersInTheSchema()
    {
        foreach (var tool in Listed())
        {
            var name = (string)tool["name"]!;
            Assert.True(((string)tool["description"]!).Length < 500, $"{name}'s description is {((string)tool["description"]!).Length} characters.");
            var properties = tool["inputSchema"]!["properties"]!.AsObject();
            Assert.All(tool["inputSchema"]!["required"]?.AsArray() ?? [], required => Assert.True(properties.ContainsKey((string)required!), $"{name} requires {required}, which it does not take."));
            Assert.All(properties, property => Assert.DoesNotContain("required", (string)property.Value!["description"]!, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(tool["annotations"]!["readOnlyHint"]);
        }
    }

    [Fact]
    public void TheHints_MarkTheReaders_TheOneThatDeletes_AndTheThreeThatOpenABrowser()
    {
        var tools = Listed().ToDictionary(tool => (string)tool["name"]!, tool => tool["annotations"]!);

        Assert.Equal(["delete_results"], tools.Where(tool => (bool?)tool.Value["destructiveHint"] == true).Select(tool => tool.Key));
        Assert.Equal(["scrape_start", "scrape_explore", "scrape_check_page_objects"], tools.Where(tool => (bool)tool.Value["openWorldHint"]!).Select(tool => tool.Key));
        Assert.Contains("list_results", tools.Where(tool => (bool)tool.Value["readOnlyHint"]!).Select(tool => tool.Key));
        Assert.DoesNotContain("step_screenshot", tools.Where(tool => (bool)tool.Value["readOnlyHint"]!).Select(tool => tool.Key));
    }

    [Fact]
    public void EveryList_TakesMax()
    {
        var lists = Listed().Where(tool => (string)tool["name"]! is var name && (name.Contains("list_", StringComparison.Ordinal) || name.Contains("find_", StringComparison.Ordinal)));

        Assert.All(lists, tool => Assert.True(tool["inputSchema"]!["properties"]!.AsObject().ContainsKey("max"), $"{tool["name"]} takes no max."));
    }

    [Fact]
    public void EveryReadOnlyTool_WritesNoFile()
    {
        Call("run_scenario", new JsonObject { ["path"] = Feature });
        Dictionary<string, JsonObject> arguments = new()
        {
            ["check_scenario"] = new() { ["path"] = Feature },
            ["draft_feature"] = new() { ["path"] = Feature },
            ["find_step_usages"] = new() { ["symbol"] = $"{typeof(StepCounterFixture).FullName}.Counts" },
            ["list_results"] = new() { ["status"] = "all" },
            ["get_trace"] = new() { ["path"] = Feature, ["line"] = 10 },
            ["diff_since_green"] = new() { ["path"] = Feature, ["line"] = 10 },
            ["get_http_calls"] = new() { ["path"] = Feature, ["line"] = 3 },
            ["list_affected_tests"] = new() { ["suite"] = Path.GetFileName(Counter) },
            ["scrape_get_page"] = new() { ["page"] = "/orders" },
            ["scrape_find_similar"] = new() { ["page"] = "/orders", ["node"] = 2 },
            ["scrape_find_text"] = new() { ["query"] = "grace" },
            ["scrape_get_control_brief"] = new() { ["signature"] = "tr" },
            ["scrape_match_controls"] = new() { ["page"] = "/orders" },
            ["scrape_diff_page"] = new() { ["page"] = "/orders" },
            ["scrape_suggest_controls"] = new() { ["page"] = "/orders" },
            ["scrape_get_header"] = new() { ["page"] = "/orders" },
            ["scrape_list_events"] = new() { ["session"] = _session },
            ["scrape_resolve_events"] = new() { ["session"] = _session },
            ["scrape_gap_brief"] = new() { ["session"] = _session },
        };
        var before = Files();

        var readers = Listed().Where(tool => (bool)tool["annotations"]!["readOnlyHint"]!).Select(tool => (string)tool["name"]!).ToList();
        foreach (var reader in readers)
        {
            Call(reader, arguments.GetValueOrDefault(reader) ?? []);
        }

        Assert.True(readers.Count > 25);
        Assert.Equal(before, Files());
    }

    [Fact]
    public void EveryList_GivesAtMostMax_AndCountsTheRestAsMore()
    {
        Call("run_scenario", new JsonObject { ["path"] = Feature });

        var pages = Answer(Call("scrape_list_pages", new JsonObject { ["max"] = 1 }));
        var similar = Answer(Call("scrape_find_similar", new JsonObject { ["page"] = "/orders", ["node"] = 2, ["max"] = 1 }));
        var results = Answer(Call("list_results", new JsonObject { ["status"] = "all", ["max"] = 1 }));
        var steps = Answer(Call("list_steps", new JsonObject { ["max"] = 2 }));
        var all = Answer(Call("scrape_find_similar", new JsonObject { ["page"] = "/orders", ["node"] = 2 }));

        Assert.Equal((1, 1), (pages["pages"]!.AsArray().Count, (int)pages["more"]!));
        Assert.Equal((1, 2), (similar["nodes"]!.AsArray().Count, (int)similar["more"]!));
        Assert.Equal((1, 1), (results["results"]!.AsArray().Count, (int)results["more"]!));
        Assert.Equal(2, steps["steps"]!.AsArray().Count);
        Assert.Equal((int)steps["count"]! - 2, (int)steps["more"]!);
        Assert.Equal(3, all["nodes"]!.AsArray().Count);
        Assert.Null(all["more"]);
    }

    [Fact]
    public void ASuiteBuiltAgainstAnotherBruinet_IsARow_NotAnError()
    {
        var stale = Path.Combine(_solution, "Stale.Uat");
        Directory.CreateDirectory(Path.Combine(stale, "Features"));
        File.WriteAllText(Path.Combine(stale, "uat.config.md"), Config(nameof(StaleBuildFixture), "\n## Apps\n\n| Name | Head |\n| --- | --- |\n| Web | web |\n"));
        Call("run_scenario", new JsonObject { ["path"] = Feature });
        using var server = new BddMcpServer(_solution);

        var suites = Answer(Call(server, "list_suites", []))["suites"]!.AsArray();
        var failing = Call(server, "list_results", []);

        var row = suites.Single(suite => (string?)suite!["name"] == "Stale.Uat")!;
        Assert.False((bool)row["built"]!);
        Assert.Contains("does not fit this Bruinet", (string?)row["reason"], StringComparison.Ordinal);
        Assert.True((bool)suites.Single(suite => (string?)suite!["name"] != "Stale.Uat")!["built"]!);
        Assert.False((bool)failing["isError"]!, Text(failing));
        var answer = Answer(failing);
        Assert.Equal("Fails", (string?)answer["results"]![0]!["scenario"]);
        Assert.Equal(10, (int?)answer["results"]![0]!["line"]);
        Assert.Contains(answer["notLoaded"]!.AsArray(), suite => (string?)suite!["suite"] == "Stale.Uat");
    }

    [Fact]
    public void AnAnswer_LeavesOutItsNulls_AndGivesPathsRelativeToTheSolution()
    {
        var suite = Answer(Call("list_suites", []))["suites"]![0]!.AsObject();

        Assert.DoesNotContain(suite, field => field.Value is null);
        Assert.Equal(Path.Combine(Path.GetFileName(Counter), "uat.config.md"), (string?)suite["config"]);
    }

    private static string Config(string fixture, string more) => $"""
        # UAT Config

        ## Runtime

        | Field | Value |
        | --- | --- |
        | Target | MAUI |
        | Fixture | {fixture} |

        ## Assemblies

        | Kind | Assembly |
        | --- | --- |
        | Pages | {typeof(StepCounterFixture).Assembly.Location} |
        {more}
        """;

    private static ScrapeSnapshot Orders(string location, params string[] customers)
    {
        var nodes = new List<ScrapeNode> { new() { Number = 1, Parent = 0, Type = "table", Attributes = new Dictionary<string, string> { ["data-testid"] = "orders" } } };
        foreach (var customer in customers)
        {
            var row = nodes.Count + 1;
            nodes.Add(new() { Number = row, Parent = 1, Type = "tr", Attributes = new Dictionary<string, string> { ["class"] = "order-row" } });
            nodes.Add(new() { Number = row + 1, Parent = row, Type = "td", Text = customer });
        }

        return new ScrapeSnapshot { Platform = "html", Location = location, Title = "Orders", Nodes = nodes };
    }

    private static string Repository => Path.GetDirectoryName(Path.GetDirectoryName(TodoPaths.Todo))!;

    // Each file of the solution: its size and when it was written.
    private SortedDictionary<string, (long, DateTime)> Files()
        => new(Directory.EnumerateFiles(_solution, "*", SearchOption.AllDirectories).ToDictionary(path => path, path => (new FileInfo(path).Length, File.GetLastWriteTimeUtc(path))), StringComparer.OrdinalIgnoreCase);

    private List<JsonObject> Listed()
        => [.. _server.Handle("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""")!["result"]!["tools"]!.AsArray().Select(tool => tool!.AsObject())];

    private JsonObject Call(string tool, JsonObject arguments) => Call(_server, tool, arguments);

    private static JsonObject Call(BddMcpServer server, string tool, JsonObject arguments)
        => server.Handle(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = arguments },
        }.ToJsonString())!["result"]!.AsObject();

    private static string Text(JsonObject result) => (string)result["content"]![0]!["text"]!;

    private static JsonNode Answer(JsonObject result) => JsonNode.Parse(Text(result))!;
}

#pragma warning disable BRUINET010 // The sessions host is experimental; this fixture only fails to give its modules.

/// <summary>A fixture as a suite built against an older Bruinet loads: asking its head's modules throws <see cref="TypeLoadException"/>.</summary>
public sealed class StaleBuildFixture : Bruinet.Uat.IUatAppsHost
{
    /// <inheritdoc />
    public Bruinet.Core.Testing.TestAppSet Apps => throw new NotSupportedException();

    /// <inheritdoc />
    public Bruinet.Core.Composition.TestCompositionDiscoveryOptions? ModulesFor(Bruinet.Core.Testing.TestAppDefinition app)
        => throw new TypeLoadException("Could not load type 'Bruinet.Html.Pages.PageObjectBase`1' from assembly 'Bruinet.Html'.");

    /// <inheritdoc />
    public void AddAppServices(Bruinet.Core.Testing.ITestApp app, Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
    }
}

#pragma warning restore BRUINET010
