using Xunit;

namespace Bruinet.Bdd.Workspace.Tests;

/// <summary>The Todo sample's suites, found and told apart by their files, without a build.</summary>
public sealed class BddWorkspaceTests
{
    private static readonly string Todo = FindTodo();

    [Fact]
    public void FindConfigs_FindsEveryTodoSuite_UiAndService()
    {
        var configs = BddWorkspace.FindConfigs(Todo).Select(path => Path.GetRelativePath(Todo, path).Replace('\\', '/')).ToList();

        Assert.Equal(
            [
                "tests/Bruinet.Samples.Todo.IntegrationTests/bdd.config.md",
                "tests/Bruinet.Samples.Todo.Mixed.Uat/bdd.config.md",
                "tests/Bruinet.Samples.Todo.Uat/bdd.config.md",
                "tests/Bruinet.Samples.Todo.Web.Uat/bdd.config.md",
            ],
            configs);
    }

    // A solution in a folder of its own that lists the Todo projects, which are not under it.
    [Fact]
    public void FindConfigs_FindsTheSuitesOfTheProjectsASolutionLists_OutsideItsFolder()
    {
        var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bruinet-sln-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            string Line(string project) =>
                $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{project}\", \"{Path.Combine(Todo, "tests", project, project + ".csproj")}\", \"{{{Guid.NewGuid().ToString().ToUpperInvariant()}}}\"\nEndProject\n";
            File.WriteAllText(
                Path.Combine(folder, "Outside.sln"),
                "Microsoft Visual Studio Solution File, Format Version 12.00\n" + Line("Bruinet.Samples.Todo.Uat") + Line("Bruinet.Samples.Todo.IntegrationTests"));

            var configs = BddWorkspace.FindConfigs(folder).Select(path => Path.GetRelativePath(Todo, path).Replace('\\', '/')).ToList();

            Assert.Equal(["tests/Bruinet.Samples.Todo.IntegrationTests/bdd.config.md", "tests/Bruinet.Samples.Todo.Uat/bdd.config.md"], configs);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void FindConfigs_ReadsAnSlnx_AndCountsASuiteOnce()
    {
        var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bruinet-slnx-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var project = Path.GetRelativePath(folder, Path.Combine(Todo, "tests", "Bruinet.Samples.Todo.Web.Uat", "Bruinet.Samples.Todo.Web.Uat.csproj"));
            File.WriteAllText(Path.Combine(folder, "Only.slnx"), $"<Solution><Folder Name=\"/web/\"><Project Path=\"{project}\" /></Folder><Project Path=\"{project}\" /></Solution>");

            var config = Assert.Single(BddWorkspace.FindConfigs(folder));

            Assert.Equal(Path.Combine(Todo, "tests", "Bruinet.Samples.Todo.Web.Uat", "bdd.config.md"), config, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void SuitesFor_AFileLinkedIntoSeveralSuites_NamesEachOfThem()
    {
        using var workspace = BddWorkspace.Open(Todo);

        var suites = workspace.SuitesFor(Feature("TOD.01-see-my-todos.feature.md")).Select(suite => suite.Name).ToList();

        Assert.Contains("Bruinet.Samples.Todo.Uat", suites);
        Assert.Contains("Bruinet.Samples.Todo.Web.Uat", suites);
        Assert.Contains("Bruinet.Samples.Todo.IntegrationTests", suites);
    }

    [Fact]
    public void SuitesFor_AFileTheWebSuiteExcludes_IsNotTheWebSuites()
    {
        using var workspace = BddWorkspace.Open(Todo);

        var suites = workspace.SuitesFor(Feature("TOD.11-manage-users.feature.md")).Select(suite => suite.Name).ToList();

        Assert.Contains("Bruinet.Samples.Todo.Uat", suites);
        Assert.DoesNotContain("Bruinet.Samples.Todo.Web.Uat", suites);
    }

    [Fact]
    public void SuitesFor_AStepFile_FollowsItsLinks()
    {
        using var workspace = BddWorkspace.Open(Todo);

        var suites = workspace.SuitesFor(Feature("Todo.steps.md")).Select(suite => suite.Name).ToList();

        Assert.Contains("Bruinet.Samples.Todo.Web.Uat", suites);
    }

    private static string Feature(string name) => Path.Combine(Todo, "tests", "Bruinet.Samples.Todo.Uat", "Features", name);

    private static string FindTodo()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var todo = Path.Combine(folder.FullName, "samples", "Todo");
            if (Directory.Exists(todo))
            {
                return todo;
            }
        }

        throw new DirectoryNotFoundException("samples/Todo is not above the test's folder.");
    }
}
