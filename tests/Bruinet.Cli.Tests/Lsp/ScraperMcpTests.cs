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
        Assert.Contains("\"name\":\"Shop.UITests\"", CallText("scrape_list_projects", []));
        var pages = CallText("scrape_list_pages", []);

        Assert.Contains("\"key\":\"/orders\"", pages);
        Assert.Contains("\"key\":\"/archive\"", pages);
    }

    [Fact]
    public void A_page_comes_as_an_outline_and_a_node_s_kind_is_found_on_every_page()
    {
        var outline = CallText("scrape_get_page", new JsonObject { ["page"] = "/orders" });
        var similar = CallText("scrape_find_similar", new JsonObject { ["page"] = "/orders", ["node"] = 2 });
        var found = CallText("scrape_find_text", new JsonObject { ["query"] = "grace" });

        Assert.StartsWith("#1 table [data-testid=orders]\n  #2 tr .order-row\n    #3 td \"Ada\"", outline);
        Assert.Equal(3, JsonNode.Parse(similar)!["nodes"]!.AsArray().Count); // two rows on /orders, one on /archive
        Assert.Contains("\\\"Grace\\\"", found);
    }

    [Fact]
    public void What_cannot_be_done_is_a_tool_error_saying_why()
    {
        Assert.Contains("No recorded page '/nope'", ErrorText("scrape_get_page", new JsonObject { ["page"] = "/nope" }));
        Assert.Contains("'session' is required", ErrorText("scrape_list_events", []));
    }

    [Fact]
    public void Explore_asks_first_with_the_host_guards_and_budget_and_refuses_a_host_that_is_not_an_explore_host()
    {
        var asked = JsonNode.Parse(CallText("scrape_explore", []))!;
        var refused = JsonNode.Parse(CallText("scrape_explore", new JsonObject { ["confirm"] = true }))!;

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

        var said = JsonNode.Parse(CallText("scrape_update_config", (JsonObject)arguments.DeepClone()))!;
        Assert.Equal(before, File.ReadAllText(config));
        arguments["confirm"] = true;
        var written = JsonNode.Parse(CallText("scrape_update_config", arguments))!;

        Assert.Equal(("Added ## Explore hosts with shop.example.", false), ((string?)said["description"], (bool)said["written"]!));
        Assert.True((bool)written["written"]!);
        Assert.Equal(before.TrimEnd() + "\n\n## Explore hosts\n\n| Host |\n| --- |\n| shop.example |\n", File.ReadAllText(config));
        Assert.DoesNotContain("REFUSED", (string)JsonNode.Parse(CallText("scrape_explore", []))!["summary"]!);
    }

    [Fact]
    public void A_config_change_that_would_not_read_is_not_written()
    {
        var config = Path.Combine(_solution, "Shop.UITests", ScraperConfig.FileName);
        var before = File.ReadAllText(config);

        var error = ErrorText("scrape_update_config", new JsonObject { ["action"] = "set", ["section"] = "Explore", ["values"] = new JsonArray("ClickPolicy", "everything"), ["confirm"] = true });

        Assert.Contains("ClickPolicy is links, safe, not-unsafe or all, not 'everything'", error);
        Assert.Equal(before, File.ReadAllText(config));
    }

    [Fact]
    public void An_agent_makes_a_project_only_once_the_person_agreed()
    {
        var arguments = new JsonObject { ["name"] = "Agent.UITests", ["startUrl"] = "http://localhost:5001/" };

        var asked = JsonNode.Parse(CallText("scrape_create_project", (JsonObject)arguments.DeepClone()))!;
        Assert.False(Directory.Exists(Path.Combine(_solution, "Agent.UITests")));
        arguments["confirm"] = true;
        var made = JsonNode.Parse(CallText("scrape_create_project", arguments))!;

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
