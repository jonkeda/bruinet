using Bruinet.Cli;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.TestSite;

namespace Bruinet.Cli.Tests.Scrape;

// `bruinet scrape explore` against the test site, headless, and its exit codes.
[Trait("Category", "Browser")]
public sealed class ScrapeCommandTests : IDisposable
{
    private readonly TestSiteServer _site = new();
    private readonly string _solution = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));
    private readonly StringWriter _output = new();
    private readonly StringWriter _errors = new();

    public void Dispose()
    {
        _site.Dispose();
        if (Directory.Exists(_solution))
            Directory.Delete(_solution, recursive: true);
    }

    private void MakeProject(string startPath, string explore = "")
    {
        var folder = Path.Combine(_solution, "tests", "Shop.UITests");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName),
            $"## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | {_site.Url(startPath)} |\n\n## Explore\n\n| Field | Value |\n| --- | --- |\n| DelayMs | 1 |\n{explore}");
    }

    private Task<int> RunAsync(string input, params string[] args) =>
        BruinetCommand.RunAsync(["scrape", "explore", "--solution", _solution, .. args], new CliIo(new StringReader(input), _output, _errors, _solution, Interactive: true) { SourceFor = _ => new PlaywrightScrapeSource() });

    [Fact]
    public async Task A_run_within_its_guards_exits_0_with_the_summary_first_and_the_report_last()
    {
        MakeProject("pages/account.html", "| MaxDepth | 1 |\n");

        var exit = await RunAsync("", "--project", "Shop.UITests", "--headless", "--yes");

        Assert.Equal(0, exit);
        var errors = _errors.ToString();
        Assert.StartsWith("Explore Shop.UITests from " + _site.Url("pages/account.html"), errors);
        Assert.Contains("  clicks      ClickPolicy safe", errors);
        Assert.Contains("captured /pages/account.html", errors);
        Assert.StartsWith("Done: ", _output.ToString());
        Assert.Contains("Report: ", _output.ToString());
        Assert.All(ExploreSettings.Categories, c => Assert.Equal(0, _site.Counter(c)));
    }

    [Fact]
    public async Task A_sign_in_page_in_an_unattended_run_exits_1()
    {
        MakeProject("index.html", "| LoginUrl | /pages/support.html |\n| MaxDepth | 1 |\n");

        var exit = await RunAsync("", "--headless", "--yes", "--json");

        Assert.Equal(1, exit);
        Assert.Contains("\"stop\": \"SignIn\"", _output.ToString());
    }

    [Fact]
    public async Task A_start_that_is_not_an_explore_host_exits_1_without_opening_it()
    {
        MakeProject("index.html");

        var exit = await RunAsync("", "--url", _site.OtherOriginUrl("index.html").Replace("127.0.0.1", "127.0.0.2", StringComparison.Ordinal), "--headless", "--yes");

        Assert.Equal(1, exit);
        Assert.Contains("REFUSED", _errors.ToString());
        Assert.StartsWith("HostRefused: ", _output.ToString());
        Assert.Empty(_site.Requests);
    }

    [Fact]
    public async Task Without_a_yes_to_the_question_it_does_not_start_and_exits_2()
    {
        MakeProject("index.html");

        var exit = await RunAsync("n\n", "--headless");

        Assert.Equal(2, exit);
        Assert.Contains("Go on? [y/N] Not started.", _errors.ToString());
        Assert.Empty(_site.Requests);
    }

    [Theory]
    [InlineData(new[] { "--project", "Nope", "--yes" }, "No scraped project 'Nope'")]
    [InlineData(new[] { "--max-pages", "zero", "--yes" }, "'zero' for option '--max-pages'")]
    [InlineData(new[] { "--max-pages", "0", "--yes" }, "--max-pages takes a whole number above 0.")]
    public async Task A_wrong_call_exits_2_saying_why(string[] args, string message)
    {
        MakeProject("index.html");

        var exit = await RunAsync("", args);

        Assert.Equal(2, exit);
        Assert.Contains(message, _errors.ToString());
    }

    [Fact]
    public async Task Without_a_terminal_and_without_yes_it_does_not_start_and_exits_2()
    {
        MakeProject("index.html");

        var exit = await BruinetCommand.RunAsync(["scrape", "explore", "--solution", _solution], new CliIo(TextReader.Null, _output, _errors, _solution, Interactive: false) { SourceFor = _ => new PlaywrightScrapeSource() });

        Assert.Equal(2, exit);
        Assert.Contains("pass --yes", _errors.ToString());
        Assert.Empty(_site.Requests);
    }
}
