using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;

namespace Bruinet.Scraper.Tests.Projects;

public sealed class ProjectMakerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));

    public ProjectMakerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static NewProject Request(string name = "Shop.UITests", string folder = "tests/Shop.UITests", string platform = "html", string url = "https://shop.example/", BruinetReferences references = BruinetReferences.Packages) =>
        new(name, folder, platform, url, ["data-testid", "data-test"], references);

    [Fact]
    public void A_new_project_has_its_csproj_its_config_and_an_empty_pages_folder()
    {
        var folder = ProjectMaker.Create(_root, Request());

        Assert.True(Directory.Exists(Path.Combine(folder, "Pages")));
        var csproj = File.ReadAllText(Path.Combine(folder, "Shop.UITests.csproj"));
        Assert.Contains("<PackageReference Include=\"Bruinet.Html\" Version=\"", csproj);
        Assert.DoesNotContain("Bruinet.Blazor", csproj);
        var project = ScrapeProject.Open(folder);
        Assert.Equal(("Shop.UITests", "html", "https://shop.example/", "Shop.UITests.Pages"), (project.Name, project.Config.Platform, project.Config.StartUrl, project.Config.Namespace));
        Assert.Equal(["data-testid", "data-test"], project.Config.TestIds);
        Assert.Contains("shop.example", project.Config.Explore.Hosts); // a start that is not localhost can be explored
    }

    [Fact]
    public void Inside_bruinet_s_repository_it_references_the_sources_and_goes_in_tests()
    {
        var html = Path.Combine(_root, "src", "Bruinet.Html");
        Directory.CreateDirectory(html);
        File.WriteAllText(Path.Combine(html, "Bruinet.Html.csproj"), "<Project />");

        var defaults = ProjectMaker.Defaults(_root, "Shop.UITests");
        var folder = ProjectMaker.Create(_root, Request(folder: defaults.Folder, platform: "blazor", references: defaults.References));

        Assert.Equal((Path.Combine("tests", "Shop.UITests"), BruinetReferences.Projects), (defaults.Folder, defaults.References));
        var csproj = File.ReadAllText(Path.Combine(folder, "Shop.UITests.csproj"));
        Assert.Contains($"<ProjectReference Include=\"{Path.Combine("..", "..", "src", "Bruinet.Html", "Bruinet.Html.csproj")}\" />", csproj);
        Assert.Contains($"<ProjectReference Include=\"{Path.Combine("..", "..", "src", "Bruinet.Blazor", "Bruinet.Blazor.csproj")}\" />", csproj);
    }

    [Fact]
    public void What_is_wrong_is_said_by_field_and_nothing_is_written()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Taken"));
        File.WriteAllText(Path.Combine(_root, "Taken", "Taken.csproj"), "<Project />");

        var problems = ProjectMaker.Problems(_root, new NewProject("Taken", "../outside", "wpf", "ftp://x", ["data testid"], BruinetReferences.Packages));

        Assert.Equal(["folder", "name", "platform", "startUrl", "testIds"], problems.Keys.Order(StringComparer.Ordinal));
        Assert.Throws<ArgumentException>(() => ProjectMaker.Create(_root, Request(url: "localhost")));
        Assert.False(Directory.Exists(Path.Combine(_root, "tests")));
    }

    [Fact]
    public async Task Two_projects_of_one_name_are_shown_with_their_folders_and_asked_for_by_folder()
    {
        ProjectMaker.Create(_root, Request(folder: Path.Combine("a", "Shop.UITests")));
        ProjectMaker.Create(_root, Request(name: "Other", folder: Path.Combine("b", "Other")));
        // A copy made by hand: the same project name in another folder.
        var copy = Path.Combine(_root, "c", "Shop.UITests");
        Directory.CreateDirectory(copy);
        foreach (var file in Directory.GetFiles(Path.Combine(_root, "a", "Shop.UITests")))
            File.Copy(file, Path.Combine(copy, Path.GetFileName(file)));
        await using var service = new ScraperService(_root);
        var all = service.Projects();

        Assert.Equal(["Other", "Shop.UITests (a/Shop.UITests)", "Shop.UITests (c/Shop.UITests)"], all.Select(p => service.Label(p, all)).Order(StringComparer.Ordinal));
        var problem = Assert.Throws<ScraperProblem>(() => service.Project("Shop.UITests"));
        Assert.Contains("a/Shop.UITests, c/Shop.UITests", problem.Message);
        Assert.Equal(copy, service.Project("c/Shop.UITests").Folder);
        Assert.Equal(copy, service.Project("Shop.UITests (c/Shop.UITests)").Folder);
    }
}
