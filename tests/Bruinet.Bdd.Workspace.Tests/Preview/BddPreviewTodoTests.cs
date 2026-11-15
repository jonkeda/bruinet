using System.Net;
using Bruinet.Bdd.Workspace;
using Xunit;

namespace Bruinet.Bdd.Preview.Tests;

/// <summary>
/// The Todo MAUI suite's files, bound against its catalog as the language server loads it. Needs
/// the Todo sample built (<c>dotnet build samples/Todo/Bruinet.Samples.Todo.slnx</c>).
/// </summary>
public sealed class BddPreviewTodoTests : IClassFixture<BddPreviewTodoTests.TodoSuite>
{
    private readonly TodoSuite _todo;

    public BddPreviewTodoTests(TodoSuite todo) => _todo = todo;

    /// <summary>The MAUI suite's own feature and step files, relative to its project.</summary>
    public static IEnumerable<object[]> Files => Directory
        .EnumerateFiles(TodoSuite.Project, "*.md", SearchOption.AllDirectories)
        .Where(file => file.EndsWith(BddFileFormat.FeatureExtension, StringComparison.OrdinalIgnoreCase) ||
                       file.EndsWith(BddFileFormat.StepsExtension, StringComparison.OrdinalIgnoreCase))
        .Select(file => Path.GetRelativePath(TodoSuite.Project, file))
        .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
        .Order(StringComparer.Ordinal)
        .Select(file => new object[] { file.Replace('\\', '/') });

    [Theory]
    [MemberData(nameof(Files))]
    public void TodoFile_Bound_RendersAsItsSnapshot(string file)
    {
        var path = Path.Combine(TodoSuite.Project, file);

        var result = BddPreview.Render(File.ReadAllText(path), path, new BddPreviewOptions { Catalogs = _todo.Catalogs });

        Snapshot.Match(result.Html, "bound." + file.Replace('/', '.'));
    }

    [Fact]
    public void TheWithoutUsingFile_ShowsUatb010_WordForWordAsTheGateReportsIt()
    {
        var path = Path.Combine(TodoSuite.Project, "ExpectedFailures", "TOD.11-without-using.feature.md");
        var gate = UatScenarioCheck.CheckFile(path, _todo.Catalogs).Where(d => d.Code == "BDDB010").ToList();

        var html = WebUtility.HtmlDecode(BddPreview.Render(File.ReadAllText(path), path, new BddPreviewOptions { Catalogs = _todo.Catalogs }).Html);

        Assert.NotEmpty(gate);
        Assert.All(gate, diagnostic => Assert.Contains($"data-line=\"{diagnostic.Location.LineNumber}\"><span class=\"uat-diag-code\">BDDB010</span> {diagnostic.Message}", html));
    }

    [Fact]
    public void TheTodoVideo_ShowsEverySceneWithItsSteps_AndNoDiagnostics()
    {
        var path = Path.Combine(TodoSuite.Project, "VideoScripts", "how-todo-works.video.md");

        var result = BddPreview.Render(File.ReadAllText(path), path);
        var html = WebUtility.HtmlDecode(result.Html);

        Assert.Empty(result.Diagnostics);
        Assert.Contains("data-kind=\"video\"", html);
        Assert.Equal(11, html.Split("<span class=\"uat-kw\">Scene ").Length - 1);
        Assert.Contains("<span class=\"uat-kw\">When</span><span class=\"uat-text\">I tap Sync<span class=\"uat-video-say\">You tap Sync to fetch it; while the app waits, it tells you it is loading.</span>", html);
        Assert.Contains("<span class=\"uat-video-silent\">silent</span>", html);
    }

    [Fact]
    public void AVideoScript_OffersRecord_AndListsItsRecordingsToWatch()
    {
        var path = Path.Combine(TodoSuite.Project, "VideoScripts", "how-todo-works.video.md");
        var options = new BddPreviewOptions
        {
            Record = new BddPreviewRunState(false, "Recording needs ffmpeg."),
            Recordings = [new BddPreviewRecording(@"C:\Videos\how-todo-works-20260929-101500.mp4", "how-todo-works-20260929-101500.mp4", new DateTime(2026, 9, 29, 10, 15, 0))],
        };

        var html = WebUtility.HtmlDecode(BddPreview.Render(File.ReadAllText(path), path, options).Html);

        Assert.Contains("class=\"uat-run uat-record\" data-record-file disabled title=\"Recording needs ffmpeg.\">\u23FA Record</button>", html);
        Assert.Contains("<a href=\"#\" data-watch=\"C:\\Videos\\how-todo-works-20260929-101500.mp4\"", html);
        Assert.Contains("2026-09-29 10:15", html);
    }

    [Fact]
    public void AScenarioHeading_OffersRecord_BesideRunAndDebug()
    {
        var path = Path.Combine(TodoSuite.Project, "Features", "TOD.01-see-my-todos.feature.md");
        var options = new BddPreviewOptions { Run = new BddPreviewRunState(true), Record = new BddPreviewRunState(true) };

        var html = BddPreview.Render(File.ReadAllText(path), path, options).Html;

        Assert.Contains("data-record=\"15\" title=\"Record this scenario on video\">\u23FA Record</button>", html);
    }

    [Fact]
    public void AVideoScript_IsCheckedLikeAFeatureFile()
    {
        var path = Path.Combine(TodoSuite.Project, "Features", "broken.video.md");

        var analysis = BddFileAnalysis.Analyze("# Video: V\n## Scene: S\nScenario: Features/TOD.01-see-my-todos.feature.md#TOD.01.99-None\n", path, catalogs: null);

        Assert.Equal("VID012", Assert.Single(analysis.Diagnostics).Code);
    }

    /// <summary>The MAUI suite, loaded once for the class.</summary>
    public sealed class TodoSuite : IDisposable
    {
        public static readonly string Project = Path.Combine(FindTodo(), "tests", "Bruinet.Samples.Todo.Uat");

        private readonly BddSuite _suite = BddSuite.Open(Path.Combine(Project, BddFileFormat.UiConfigFileName));

        public BddCatalogSet Catalogs => _suite.TryGetCatalogs(out var reason)
            ?? throw new InvalidOperationException($"The Todo MAUI suite cannot be bound: {reason} Build samples/Todo first.");

        public void Dispose() => _suite.Dispose();

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
}
