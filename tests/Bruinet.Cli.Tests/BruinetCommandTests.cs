using Bruinet.Scraper.Projects;

namespace Bruinet.Cli.Tests;

// The command tree: help, wrong calls, the documented examples, and the commands that need no browser.
public sealed class BruinetCommandTests : IDisposable
{
    private readonly string _solution = Path.Combine(Path.GetTempPath(), "bruinet-cli-tests", Guid.NewGuid().ToString("N"));
    private readonly string _project;

    public BruinetCommandTests()
    {
        _project = Path.Combine(_solution, "tests", "Shop.UITests");
        Directory.CreateDirectory(_project);
        File.WriteAllText(Path.Combine(_solution, "Shop.sln"), "");
        File.WriteAllText(Path.Combine(_project, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(_project, ScraperConfig.FileName), "## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | https://shop.example/menu |\n");
    }

    public void Dispose()
    {
        if (Directory.Exists(_solution))
            Directory.Delete(_solution, recursive: true);
    }

    private async Task<(int Exit, string Output, string Errors)> Run(string currentDirectory, params string[] args)
    {
        var output = new StringWriter();
        var errors = new StringWriter();
        var exit = await BruinetCommand.RunAsync(args, new CliIo(TextReader.Null, output, errors, currentDirectory, Interactive: false));
        return (exit, output.ToString(), errors.ToString());
    }

    [Fact]
    public async Task Sql_TakesItsConnection_FromAVariable_AndSaysWhichIsMissing()
    {
        var unset = await Run(_solution, "sql", "capture", "start", "--connection-from", "BRUINET_TEST_NO_SUCH_VARIABLE");
        var neither = await Run(_solution, "sql", "capture", "start");
        var both = await Run(_solution, "sql", "capture", "start", "--connection", "Server=.", "--connection-from", "PATH");

        Assert.Equal(2, unset.Exit);
        Assert.Contains("BRUINET_TEST_NO_SUCH_VARIABLE is not set", unset.Errors, StringComparison.Ordinal);
        Assert.Equal(2, neither.Exit);
        Assert.Contains("one of them", neither.Errors, StringComparison.Ordinal);
        Assert.Equal(2, both.Exit);
    }

    [Theory]
    [InlineData(new string[0], "scrape")]
    [InlineData(new[] { "scrape" }, "install-browser")]
    [InlineData(new[] { "scenario", "results" }, "delete")]
    [InlineData(new[] { "data", "validate", "--help" }, "--fix")]
    public async Task A_group_on_its_own_or_help_shows_what_comes_next(string[] args, string shown)
    {
        var (exit, output, _) = await Run(_solution, args);

        Assert.Equal(0, exit);
        Assert.Contains(shown, output);
    }

    [Theory]
    [InlineData(new[] { "nope" }, "See 'bruinet --help'.")]
    [InlineData(new[] { "scrape", "explore", "--max-pages", "0" }, "See 'bruinet scrape explore --help'.")]
    [InlineData(new[] { "data", "validate", "--format" }, "See 'bruinet data validate --help'.")]
    [InlineData(new[] { "controls", "generate" }, "--input")]
    public async Task A_wrong_call_exits_2_and_says_where_help_is(string[] args, string message)
    {
        var (exit, _, errors) = await Run(_solution, args);

        Assert.Equal(2, exit);
        Assert.Contains(message, errors);
    }

    // The examples in the design, the guides and the scripts: each parses without an error.
    [Theory]
    [InlineData("scrape new Shop.UITests --url https://shop.example --platform blazor --test-id data-testid --test-id data-test --references packages --yes")]
    [InlineData("scrape explore --project Bruinet.Html.React --solution Scraper.Sites --headless --yes --json --dry-run --max-pages 5 --max-depth 2 --max-minutes 3 --delay-ms 100 --url https://x")]
    [InlineData("scrape draft -p Shop.UITests --page /menu --page /orders --json")]
    [InlineData("scrape verify --headless --json")]
    [InlineData("scrape config set Explore MaxPages 20")]
    [InlineData("scrape config add \"Explore hosts\" shop.example")]
    [InlineData("scrape record --by-hand --session first --url https://x")]
    [InlineData("data validate Features/TOD.01.feature.md --format json --fix --assembly a.dll --assembly b.dll --config uat.config.md")]
    [InlineData("data phrases --keyword When --json")]
    [InlineData("data snippet Features/TOD.01.feature.md:14")]
    [InlineData("data schema --check")]
    [InlineData("scenario preview Features/TOD.01.feature.md --suite Todo --out page.html --host")]
    [InlineData("scenario results delete Features/TOD.01.feature.md --line 12 --yes")]
    [InlineData("controls generate -i src/Controls;more --members state")]
    [InlineData("mcp --solution .")]
    [InlineData("lsp")]
    public void A_documented_example_parses(string line)
    {
        var parsed = BruinetCommand.Build(new CliIo(TextReader.Null, TextWriter.Null, TextWriter.Null, _solution, false)).Parse(line);

        Assert.Empty(parsed.Errors.Select(e => e.Message));
    }

    [Fact]
    public async Task Status_from_inside_a_project_s_folder_needs_no_project_and_no_solution()
    {
        var pages = Directory.CreateDirectory(Path.Combine(_project, "Pages")).FullName;

        var (exit, output, _) = await Run(pages, "scrape", "status");

        Assert.Equal(0, exit);
        Assert.StartsWith("Shop.UITests", output);
        Assert.Contains("Next: Check that the Scraper can reach the app (step 2)", output);
    }

    [Fact]
    public async Task List_finds_the_solution_upwards_and_prints_json()
    {
        var (exit, output, _) = await Run(_project, "scrape", "list", "--json");

        Assert.Equal(0, exit);
        Assert.Contains("\"folder\": \"tests/Shop.UITests\"", output);
        Assert.Contains("\"startUrl\": \"https://shop.example/menu\"", output);
    }

    [Fact]
    public async Task Config_set_changes_the_file_and_a_wrong_change_exits_2()
    {
        var (exit, output, _) = await Run(_solution, "scrape", "config", "set", "Explore", "MaxPages", "9");

        Assert.Equal(0, exit);
        Assert.Contains("MaxPages", output);
        Assert.Equal(9, ScraperConfig.Parse(File.ReadAllText(Path.Combine(_project, ScraperConfig.FileName))).Explore.MaxPages);

        (exit, _, var errors) = await Run(_solution, "scrape", "config", "set", "Explore", "ClickPolicy", "everything");
        Assert.Equal(2, exit);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task New_without_a_terminal_and_without_yes_makes_nothing()
    {
        var (exit, _, errors) = await Run(_solution, "scrape", "new", "Orders.UITests", "--url", "https://shop.example/orders");

        Assert.Equal(2, exit);
        Assert.Contains("pass --yes", errors);
        Assert.False(Directory.Exists(Path.Combine(_solution, "Orders.UITests")));
    }

    [Fact]
    public async Task New_with_yes_makes_the_project()
    {
        var (exit, output, _) = await Run(_solution, "scrape", "new", "Orders.UITests", "--url", "https://shop.example/orders", "--references", "packages", "--yes", "--json");

        Assert.Equal(0, exit);
        Assert.Contains("\"name\": \"Orders.UITests\"", output);
        var (_, list, _) = await Run(_solution, "scrape", "list");
        Assert.Contains("Orders.UITests", list);
    }

    [Fact]
    public async Task Version_prints_bruinet_and_its_version()
    {
        var (exit, output, _) = await Run(_solution, "version");

        Assert.Equal(0, exit);
        Assert.Matches(@"^bruinet \d+\.\d+", output);
    }

    [Fact]
    public async Task Generate_writes_each_template_s_output_and_exits_2_for_a_missing_input()
    {
        var (exit, _, errors) = await Run(_solution, "controls", "generate", "--input", Path.Combine(_solution, "nowhere"));

        Assert.Equal(2, exit);
        Assert.Contains("Input not found", errors);
    }
}
