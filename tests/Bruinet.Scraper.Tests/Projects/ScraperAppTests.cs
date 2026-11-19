using Bruinet.Scraper.Host;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Tests.Projects;

// An app's own Scraper project beside its suite: the config names the suite, and the Scraper's own files stay with it.
public sealed class ScraperAppTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private (string Scraper, string Suite) MakeApp(string projectFile = "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><ProjectReference Include=\"Bruinet.Scraper.Host.csproj\" /></ItemGroup></Project>")
    {
        var suite = Path.Combine(_root, "Shop.UITests");
        var scraper = Path.Combine(_root, "Shop.Scraper");
        Directory.CreateDirectory(suite);
        Directory.CreateDirectory(scraper);
        File.WriteAllText(Path.Combine(suite, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(scraper, "Shop.Scraper.csproj"), projectFile);
        File.WriteAllText(Path.Combine(scraper, ScraperConfig.FileName), """
            # Scraper Config

            ## App

            | Field | Value |
            | --- | --- |
            | Platform | html |
            | StartUrl | https://shop.example/ |
            | Suite | ../Shop.UITests |
            """);
        return (scraper, suite);
    }

    [Fact]
    public void A_config_that_names_a_suite_keeps_the_scraper_s_files_apart_from_the_code()
    {
        var (scraper, suite) = MakeApp();

        var project = ScrapeProject.Open(scraper);

        Assert.Equal("Shop.UITests", project.Name);
        Assert.Equal(suite, project.Folder);
        Assert.Equal(scraper, project.Home);
        Assert.Equal(Path.Combine(scraper, ScrapeProject.RecordingsFolderName), project.RecordingsFolder);
    }

    [Fact]
    public void A_project_without_a_suite_row_is_one_folder_as_before()
    {
        var (scraper, _) = MakeApp();
        File.WriteAllText(Path.Combine(scraper, ScraperConfig.FileName), "# Scraper Config\n\n## App\n\n| Field | Value |\n| --- | --- |\n| Platform | html |\n| StartUrl | https://shop.example/ |\n");

        var project = ScrapeProject.Open(scraper);

        Assert.Equal(project.Folder, project.Home);
        Assert.Equal("Shop.Scraper", project.Name);
    }

    [Fact]
    public void A_suite_that_is_not_there_is_said_with_the_config_that_names_it()
    {
        var (scraper, suite) = MakeApp();
        Directory.Delete(suite, recursive: true);

        var e = Assert.Throws<FileNotFoundException>(() => ScrapeProject.Open(scraper));

        Assert.Contains("names the suite ../Shop.UITests", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_scraper_s_project_is_found_from_inside_it_and_not_from_the_suite()
    {
        var (scraper, suite) = MakeApp();
        var inside = Directory.CreateDirectory(Path.Combine(scraper, "Records")).FullName;

        Assert.Equal(Path.Combine(scraper, "Shop.Scraper.csproj"), ScraperAppProject.Find(inside));
        Assert.Null(ScraperAppProject.Find(suite));
    }

    [Fact]
    public void A_project_that_does_not_name_the_host_is_not_the_scraper_s()
    {
        var (scraper, _) = MakeApp("<Project Sdk=\"Microsoft.NET.Sdk\" />");

        Assert.Null(ScraperAppProject.Find(scraper));
    }

    [Fact]
    public async Task The_app_s_own_command_line_lists_its_project_by_the_suite_s_name()
    {
        var (scraper, _) = MakeApp();
        using var output = new StringWriter();
        using var errors = new StringWriter();

        var exit = await ScraperApp.RunAsync(["list"], io: new HostIo(TextReader.Null, output, errors, scraper, Interactive: false));

        Assert.Equal(0, exit);
        Assert.Contains("Shop.UITests", output.ToString(), StringComparison.Ordinal);
        Assert.Equal("", errors.ToString());
    }

    [Fact]
    public async Task What_the_app_adds_reaches_the_commands_and_a_wrong_command_exits_2()
    {
        var (scraper, _) = MakeApp();
        using var output = new StringWriter();
        using var errors = new StringWriter();
        var io = new HostIo(TextReader.Null, output, errors, scraper, Interactive: false);

        var exit = await ScraperApp.RunAsync(["nothing-like-this"], app => app.AddRecord(new NoRecord()), io);

        Assert.Equal(2, exit);
        Assert.Contains("nothing-like-this", errors.ToString(), StringComparison.Ordinal);
    }

    private sealed class NoRecord : IRecordKind
    {
        public string Name => "none";

        public Task<string> MakeAsync(string marker, CancellationToken cancellationToken = default) => Task.FromResult("/none");

        public Task<IReadOnlyList<string>> FindAsync(string marker, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<int> DeleteAsync(string marker, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
