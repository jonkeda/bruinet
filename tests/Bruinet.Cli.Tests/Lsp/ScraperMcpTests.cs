using System.Text.Json.Nodes;
using Bruinet.Scraper.Snapshots;
using Bruinet.Scraper.Projects;
using Xunit;

namespace Bruinet.Bdd.LanguageServer.Tests;

/// <summary>The Scraper's MCP tools over a recording made beforehand: no browser.</summary>
public sealed class ScraperMcpTests : IDisposable
{
    private readonly string _solution = Path.Combine(Path.GetTempPath(), "bruinet-scraper-mcp", Guid.NewGuid().ToString("N"));
    private readonly BddMcpServer _server;

    public ScraperMcpTests()
    {
        var folder = Path.Combine(_solution, "Shop.UITests");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), "## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | https://shop.example/ |\n");
        var session = ScrapeProject.Open(folder).StartSession("Orders", new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero));
        session.AddPage(Orders("https://shop.example/orders", "Ada", "Grace"));
        session.AddPage(Orders("https://shop.example/archive", "Linus"));
        _server = new BddMcpServer(_solution);
    }

    public void Dispose()
    {
        _server.Dispose();
        Directory.Delete(_solution, recursive: true);
    }

    private static ScrapeSnapshot Orders(string location, params string[] customers)
    {
        var nodes = new List<ScrapeNode>
        {
            new() { Number = 1, Parent = 0, Type = "table", Attributes = new Dictionary<string, string> { ["data-testid"] = "orders" } },
        };
        foreach (var customer in customers)
        {
            var row = nodes.Count + 1;
            nodes.Add(new() { Number = row, Parent = 1, Type = "tr", Attributes = new Dictionary<string, string> { ["class"] = "order-row" } });
            nodes.Add(new() { Number = row + 1, Parent = row, Type = "td", Text = customer });
        }
        return new ScrapeSnapshot { Platform = "html", Location = location, Title = "Orders", Nodes = nodes };
    }

    [Fact]
    public void The_projects_and_their_recorded_pages_are_listed()
    {
        Assert.Contains("\"name\":\"Shop.UITests\"", CallText("list_scraped_projects", []));
        var pages = CallText("list_recorded_pages", []);

        Assert.Contains("\"key\":\"/orders\"", pages);
        Assert.Contains("\"key\":\"/archive\"", pages);
    }

    [Fact]
    public void A_page_comes_as_an_outline_and_a_node_s_kind_is_found_on_every_page()
    {
        var outline = CallText("get_page_snapshot", new JsonObject { ["page"] = "/orders" });
        var similar = CallText("find_similar_elements", new JsonObject { ["page"] = "/orders", ["node"] = 2 });
        var found = CallText("search_recordings", new JsonObject { ["text"] = "grace" });

        Assert.StartsWith("#1 table [data-testid=orders]\n  #2 tr .order-row\n    #3 td \"Ada\"", outline);
        Assert.Equal(3, JsonNode.Parse(similar)!.AsArray().Count); // two rows on /orders, one on /archive
        Assert.Contains("\\\"Grace\\\"", found);
    }

    [Fact]
    public async Task The_view_reads_the_same_over_json_rpc()
    {
        await using var harness = await ServerHarness.StartAsync(_solution);

        var projects = await harness.Client.InvokeWithParameterObjectAsync<JsonNode>("bruinet/scraper/projects", new { });
        var pages = await harness.Client.InvokeWithParameterObjectAsync<JsonNode>("bruinet/scraper/pages", new { project = "Shop.UITests" });
        var outline = await harness.Client.InvokeWithParameterObjectAsync<JsonNode>("bruinet/scraper/outline", new { page = "/archive" });
        var capture = await harness.Client.InvokeWithParameterObjectAsync<JsonNode>("bruinet/scraper/capture", new { });
        var host = await harness.Client.InvokeWithParameterObjectAsync<JsonNode>("bruinet/scraper/host", new { });
        var similar = await harness.Client.InvokeWithParameterObjectAsync<JsonNode>("bruinet/scraper/similar", new { page = "/orders", node = 2 });
        var header = await harness.Client.InvokeWithParameterObjectAsync<JsonNode>("bruinet/scraper/header", new { page = "/orders" });

        Assert.Contains("<title>Scraper</title>", (string?)host["html"]);
        // The workflow: the project picker with its label and +, the next action, the steps and their panel.
        Assert.Contains("<label for=\"sc-project\">Project</label>", (string?)host["html"]);
        Assert.Contains("id=\"sc-new-project\"", (string?)host["html"]);
        Assert.Contains("id=\"sc-next\"", (string?)host["html"]);
        Assert.Contains("id=\"sc-steps\"", (string?)host["html"]);
        Assert.Contains("id=\"sc-panel\"", (string?)host["html"]);
        Assert.Equal(3, similar["value"]!.AsArray().Count);
        Assert.True(similar.AsObject().ContainsKey("problem")); // sent even as null: the view tells a result by it
        Assert.StartsWith("// <scraped page=\"/orders\"", (string?)header["value"]);

        Assert.Equal("Shop.UITests", (string?)projects[0]!["name"]);
        Assert.Equal("Shop.UITests", (string?)projects[0]!["label"]);
        Assert.Equal(["/archive", "/orders"], pages["value"]!.AsArray().Select(page => (string?)page!["key"]));
        Assert.Contains("#3 td \"Linus\"", (string?)outline["value"]);
        Assert.Contains("No recording is in progress", (string?)capture["problem"]);
    }

    [Fact]
    public async Task The_view_reads_and_edits_the_explore_settings_over_json_rpc()
    {
        Directory.CreateDirectory(Path.Combine(_solution, "Admin.UITests"));
        File.WriteAllText(Path.Combine(_solution, "Admin.UITests", "Admin.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        await using var harness = await ServerHarness.StartAsync(_solution);
        Task<JsonNode> Call(string method, object parameters) => harness.Client.InvokeWithParameterObjectAsync<JsonNode>("bruinet/scraper/" + method, parameters);

        var before = await Call("config", new { });
        var said = await Call("configEdit", new { action = "set", section = "Danger words", values = new[] { "delete", "false" } });
        var after = await Call("config", new { });
        var summary = await Call("exploreSummary", new { dryRun = true });
        var match = await Call("elementMatch", new { page = "/orders", node = 1 });
        var candidates = await Call("configCandidates", new { });
        var created = await Call("createConfig", new { folder = Path.Combine(_solution, "Admin.UITests"), startUrl = "http://localhost:5000/" });
        var refused = await Call("explore", new { project = "Admin.UITests", url = "https://elsewhere.example/" });

        Assert.Equal("safe", (string?)before["value"]!["clickPolicy"]);
        Assert.True((bool)before["value"]!["dangerWords"]!["delete"]!);
        Assert.Equal("Set delete to false in ## Danger words.", (string?)said["value"]);
        Assert.False((bool)after["value"]!["dangerWords"]!["delete"]!);
        Assert.Contains("danger words off: delete", (string?)summary["value"]);
        Assert.Contains("shop.example: REFUSED", (string?)summary["value"]);
        Assert.Equal("selector: [data-testid='orders']", (string?)match["value"]);
        Assert.Equal([Path.Combine(_solution, "Admin.UITests")], candidates.AsArray().Select(c => (string?)c));
        Assert.Equal("Admin.UITests", (string?)created["value"]);
        Assert.True(File.Exists(Path.Combine(_solution, "Admin.UITests", ScraperConfig.FileName)));
        // Explore starts in the background: a start that is refused comes back as its Explored change, not here.
        Assert.True((bool)refused["value"]!);
    }

    [Fact]
    public void What_cannot_be_done_is_a_tool_error_saying_why()
    {
        Assert.Contains("No recording is in progress", ErrorText("capture_page", []));
        Assert.Contains("No recorded page '/nope'", ErrorText("get_page_snapshot", new JsonObject { ["page"] = "/nope" }));
        Assert.Contains("'session' is required", ErrorText("start_recording", []));
    }

    [Fact]
    public void Explore_asks_first_with_the_host_guards_and_budget_and_refuses_a_host_that_is_not_an_explore_host()
    {
        var asked = JsonNode.Parse(CallText("explore_app", []))!;
        var refused = JsonNode.Parse(CallText("explore_app", new JsonObject { ["confirm"] = true }))!;

        Assert.False((bool)asked["started"]!);
        var summary = (string)asked["summary"]!;
        Assert.Contains("Explore Shop.UITests (dry run: links only, nothing clicked) from https://shop.example/", summary);
        Assert.Contains("shop.example: REFUSED", summary);
        Assert.Contains("ClickPolicy safe; danger words off: none", summary);
        Assert.Equal("HostRefused", (string?)refused["stop"]);
        Assert.False((bool)refused["withinGuards"]!);
    }

    [Fact]
    public void A_config_change_is_said_first_and_written_once_confirmed_keeping_the_rest_of_the_file()
    {
        var config = Path.Combine(_solution, "Shop.UITests", ScraperConfig.FileName);
        var before = File.ReadAllText(config);
        var arguments = new JsonObject { ["action"] = "add", ["section"] = "Explore hosts", ["values"] = new JsonArray("shop.example") };

        var said = JsonNode.Parse(CallText("update_scraper_config", (JsonObject)arguments.DeepClone()))!;
        Assert.Equal(before, File.ReadAllText(config));
        arguments["confirm"] = true;
        var written = JsonNode.Parse(CallText("update_scraper_config", arguments))!;

        Assert.Equal(("Added ## Explore hosts with shop.example.", false), ((string?)said["description"], (bool)said["written"]!));
        Assert.True((bool)written["written"]!);
        Assert.Equal(before.TrimEnd() + "\n\n## Explore hosts\n\n| Host |\n| --- |\n| shop.example |\n", File.ReadAllText(config));
        Assert.DoesNotContain("REFUSED", (string)JsonNode.Parse(CallText("explore_app", []))!["summary"]!);
    }

    [Fact]
    public void A_config_change_that_would_not_read_is_not_written()
    {
        var config = Path.Combine(_solution, "Shop.UITests", ScraperConfig.FileName);
        var before = File.ReadAllText(config);

        var error = ErrorText("update_scraper_config", new JsonObject { ["action"] = "set", ["section"] = "Explore", ["values"] = new JsonArray("ClickPolicy", "everything"), ["confirm"] = true });

        Assert.Contains("ClickPolicy is links, safe, not-unsafe or all, not 'everything'", error);
        Assert.Equal(before, File.ReadAllText(config));
    }

    [Fact]
    public async Task The_view_checks_and_makes_a_new_project_over_json_rpc()
    {
        await using var harness = await ServerHarness.StartAsync(_solution);
        var fields = new { name = "Admin.UITests", folder = "Admin.UITests", platform = "blazor", startUrl = "https://admin.example/", testIds = new[] { "data-testid" }, references = "packages" };

        var defaults = await harness.Client.InvokeWithParameterObjectAsync<JsonNode>("bruinet/scraper/projectDefaults", new { name = "Admin.UITests" });
        var wrong = await harness.Client.InvokeWithParameterObjectAsync<JsonNode>("bruinet/scraper/newProjectCheck", new { name = "Shop.UITests", folder = "Shop.UITests", startUrl = "nowhere" });
        var right = await harness.Client.InvokeWithParameterObjectAsync<JsonNode>("bruinet/scraper/newProjectCheck", fields);
        var made = await harness.Client.InvokeWithParameterObjectAsync<JsonNode>("bruinet/scraper/newProject", fields);
        var projects = await harness.Client.InvokeWithParameterObjectAsync<JsonNode>("bruinet/scraper/projects", new { });

        Assert.Equal(("Admin.UITests", "packages"), ((string?)defaults["value"]!["folder"], (string?)defaults["value"]!["references"]));
        Assert.Equal(["folder", "name", "startUrl"], wrong["value"]!.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal));
        Assert.Empty(right["value"]!.AsObject());
        Assert.Equal(Path.Combine(_solution, "Admin.UITests", "Admin.UITests.csproj"), (string?)made["value"]);
        Assert.Contains("Bruinet.Blazor", File.ReadAllText((string)made["value"]!));
        Assert.Contains(projects.AsArray(), p => (string?)p!["name"] == "Admin.UITests");
    }

    [Fact]
    public async Task The_view_reads_the_workflow_over_json_rpc()
    {
        await using var harness = await ServerHarness.StartAsync(_solution);

        var progress = await harness.Client.InvokeWithParameterObjectAsync<JsonNode>("bruinet/scraper/progress", new { });
        var checks = await harness.Client.InvokeWithParameterObjectAsync<JsonNode>("bruinet/scraper/controlChecks", new { });
        var saved = await harness.Client.InvokeWithParameterObjectAsync<JsonNode>("bruinet/scraper/configEdits", new { changes = new[] { new { action = "set", section = "Explore", values = new[] { "MaxPages", "7" } }, new { action = "add", section = "Explore hosts", values = new[] { "shop.example" } } } });

        var steps = progress["value"]!["steps"]!.AsArray();
        Assert.Equal(8, steps.Count);
        Assert.Equal(("Project", "Done"), ((string?)steps[0]!["name"], (string?)steps[0]!["state"]));
        Assert.Equal("Collect pages", (string?)steps[2]!["name"]);
        Assert.NotNull(progress["value"]!["next"]);
        Assert.Empty(checks["value"]!.AsArray());
        Assert.Equal(2, saved["value"]!.AsArray().Count);
        Assert.Contains("| MaxPages | 7 |", File.ReadAllText(Path.Combine(_solution, "Shop.UITests", ScraperConfig.FileName)));
    }

    [Fact]
    public void An_agent_makes_a_project_only_once_the_person_agreed()
    {
        var arguments = new JsonObject { ["name"] = "Agent.UITests", ["startUrl"] = "http://localhost:5001/" };

        var asked = JsonNode.Parse(CallText("create_scraped_project", (JsonObject)arguments.DeepClone()))!;
        Assert.False(Directory.Exists(Path.Combine(_solution, "Agent.UITests")));
        arguments["confirm"] = true;
        var made = JsonNode.Parse(CallText("create_scraped_project", arguments))!;

        Assert.False((bool)asked["written"]!);
        Assert.True((bool)made["written"]!);
        Assert.True(File.Exists(Path.Combine(_solution, "Agent.UITests", ScraperConfig.FileName)));
    }

    private string CallText(string tool, JsonObject arguments)
    {
        var result = Call(tool, arguments);
        Assert.False((bool)result["isError"]!, (string?)result["content"]![0]!["text"]);
        return (string)result["content"]![0]!["text"]!;
    }

    private string ErrorText(string tool, JsonObject arguments)
    {
        var result = Call(tool, arguments);
        Assert.True((bool)result["isError"]!);
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
