using Bruinet.Presenter.Services;

namespace Bruinet.Presenter.Tests.Services;

/// <summary>
/// How a <c>Runtime Project</c> row resolves to the Pages assembly, and what it reports when
/// it cannot. The evaluator is faked so these stay fast; <see cref="MsBuildProjectEvaluatorTests" />
/// covers the real one.
/// </summary>
public sealed class ProjectResolutionTests
{
    [Fact]
    public void Inspect_ProjectRow_ResolvesPagesFromTheProjectOutput()
    {
        using var workspace = new Workspace(
            """
            | Field | Value |
            | --- | --- |
            | Target | MAUI |
            | Fixture | TodoUatFixture |
            | Project | ./Sample.csproj |
            """);

        var output = workspace.CreateFile("bin/Debug/net10.0/Sample.dll");
        var result = workspace.Inspect(new FakeEvaluator(output, ["net10.0"]));

        var pages = Assert.Single(result.Assemblies);
        Assert.Equal("Pages", pages.Kind);
        Assert.Equal(output, pages.ResolvedPath);
        Assert.True(pages.Exists);
        Assert.False(result.HasErrors);
        Assert.Equal(output, result.Project?.PagesAssemblyPath);
    }

    [Fact]
    public void Inspect_ProjectNotBuilt_ReportsWhereTheOutputBelongs()
    {
        using var workspace = new Workspace(
            """
            | Field | Value |
            | --- | --- |
            | Target | MAUI |
            | Fixture | TodoUatFixture |
            | Project | ./Sample.csproj |
            """);

        var expected = Path.Combine(workspace.Root, "bin", "Debug", "net10.0", "Sample.dll");
        var result = workspace.Inspect(new FakeEvaluator(expected, ["net10.0"]));

        Assert.True(result.Project?.NeedsBuild);
        Assert.Contains(
            result.Diagnostics,
            line => line.Contains("is not built", StringComparison.Ordinal) &&
                    line.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void Inspect_MultiTargetedProjectWithoutAFramework_AsksWhichOne()
    {
        using var workspace = new Workspace(
            """
            | Field | Value |
            | --- | --- |
            | Target | MAUI |
            | Fixture | TodoUatFixture |
            | Project | ./Sample.csproj |
            """);

        var result = workspace.Inspect(new FakeEvaluator(string.Empty, ["net9.0", "net10.0"]));

        Assert.Contains(
            result.Diagnostics,
            line => line.Contains("net9.0, net10.0", StringComparison.Ordinal) &&
                    line.Contains("ProjectFramework", StringComparison.Ordinal));
    }

    [Fact]
    public void Inspect_ProjectFrameworkRow_IsPassedToTheEvaluator()
    {
        using var workspace = new Workspace(
            """
            | Field | Value |
            | --- | --- |
            | Target | MAUI |
            | Fixture | TodoUatFixture |
            | Project | ./Sample.csproj |
            | ProjectFramework | net10.0 |
            | Configuration | Release |
            """);

        var evaluator = new FakeEvaluator(workspace.CreateFile("bin/Release/net10.0/Sample.dll"), ["net10.0"]);
        workspace.Inspect(evaluator);

        Assert.Equal("net10.0", evaluator.LastTargetFramework);
        Assert.Equal("Release", evaluator.LastConfiguration);
    }

    [Fact]
    public void Inspect_MissingProjectFile_IsAnError()
    {
        using var workspace = new Workspace(
            """
            | Field | Value |
            | --- | --- |
            | Target | MAUI |
            | Fixture | TodoUatFixture |
            | Project | ./Gone.csproj |
            """,
            createProjectFile: false);

        var result = workspace.Inspect(new FakeEvaluator(string.Empty, []));

        Assert.Contains(
            result.Diagnostics,
            line => line.Contains("Runtime Project was not found", StringComparison.Ordinal));
    }

    [Fact]
    public void Inspect_ExplicitPagesRow_OverridesTheProject()
    {
        using var workspace = new Workspace(
            """
            | Field | Value |
            | --- | --- |
            | Target | MAUI |
            | Fixture | TodoUatFixture |
            | Project | ./Sample.csproj |
            """,
            assemblies: """
            | Kind | Assembly |
            | --- | --- |
            | Pages | Explicit.dll |
            """);

        workspace.CreateFile("Explicit.dll");
        var result = workspace.Inspect(new FakeEvaluator(workspace.CreateFile("bin/Debug/net10.0/Sample.dll"), ["net10.0"]));

        var pages = Assert.Single(result.Assemblies, assembly => assembly.Kind == "Pages");
        Assert.Equal("Explicit.dll", pages.Assembly);
    }

    /// <summary>
    /// Every diagnostic knows which field produced it, and the flat list the Diagnostics tab
    /// prints is a projection of the same records - so the two can never disagree.
    /// </summary>
    [Fact]
    public void Inspect_Problems_AreAttachedToTheirFieldAndProjectToTheFlatList()
    {
        using var workspace = new Workspace(
            """
            | Field | Value |
            | --- | --- |
            | Fixture | TodoUatFixture |
            | Project | ./Sample.csproj |
            | AppPath | ./nope.exe |
            """);

        var result = workspace.Inspect(new FakeEvaluator(string.Empty, ["net9.0", "net10.0"]));

        var appPath = Assert.Single(result.ProblemsFor("Runtime", "AppPath"));
        Assert.Contains("was not found", appPath.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Error:", appPath.Message, StringComparison.Ordinal);

        Assert.Single(result.ProblemsFor("Runtime", "ProjectFramework"));
        Assert.Empty(result.ProblemsFor("Runtime", "Fixture"));

        Assert.Equal(
            [.. result.FieldProblems.Select(problem => problem.Line)],
            result.Diagnostics);
    }

    [Fact]
    public void Inspect_MissingConfig_AttributesTheProblemToTheFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "BruinetNoConfig", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var result = new BddWorkspaceService().LoadFolder(root).Config;

            var problem = Assert.Single(result.FieldProblems);
            Assert.Equal("File", problem.Section);
            Assert.Equal(result.Diagnostics[0], problem.Line);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Inspect_NeitherProjectNorAssemblies_IsAnError()
    {
        using var workspace = new Workspace(
            """
            | Field | Value |
            | --- | --- |
            | Target | MAUI |
            | Fixture | TodoUatFixture |
            """,
            createProjectFile: false);

        var result = workspace.Inspect(new FakeEvaluator(string.Empty, []));

        Assert.Contains(
            result.Diagnostics,
            line => line.Contains("must", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("is required", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeEvaluator(string targetPath, IReadOnlyList<string> frameworks) : IProjectEvaluator
    {
        public string? LastTargetFramework { get; private set; }

        public string? LastConfiguration { get; private set; }

        public ProjectEvaluation Evaluate(
            string projectPath,
            string? targetFramework = null,
            string? configuration = null)
        {
            LastTargetFramework = targetFramework;
            LastConfiguration = configuration;
            return new ProjectEvaluation(true, projectPath, targetPath, string.Empty, frameworks, string.Empty);
        }
    }

    private sealed class Workspace : IDisposable
    {
        public Workspace(string runtimeTable, string? assemblies = null, bool createProjectFile = true)
        {
            Root = Path.Combine(Path.GetTempPath(), "BruinetProjectResolution", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);

            if (createProjectFile)
            {
                File.WriteAllText(Path.Combine(Root, "Sample.csproj"), "<Project />");
            }

            File.WriteAllText(
                Path.Combine(Root, "uat.config.md"),
                string.Join(
                    Environment.NewLine,
                    "# UAT Config",
                    string.Empty,
                    "## Runtime",
                    string.Empty,
                    runtimeTable,
                    string.Empty,
                    assemblies is null ? string.Empty : "## Assemblies",
                    string.Empty,
                    assemblies ?? string.Empty));
        }

        public string Root { get; }

        public string CreateFile(string relativePath)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
            return path;
        }

        public Bruinet.Presenter.Models.BddWorkspaceConfigLoadResult Inspect(IProjectEvaluator evaluator) =>
            new BddWorkspaceService(evaluator).LoadFolder(Root).Config;

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
