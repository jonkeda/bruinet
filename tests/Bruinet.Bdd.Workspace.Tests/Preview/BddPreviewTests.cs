using Xunit;

namespace Bruinet.Bdd.Preview.Tests;

/// <summary>The parse-only preview: every Todo feature and step file, and what a broken file shows.</summary>
public sealed class BddPreviewTests
{
    private static readonly string Todo = FindTodo();

    /// <summary>Every feature and step file of the Todo suites, relative to <c>samples/Todo/tests</c>.</summary>
    public static IEnumerable<object[]> TodoFiles => Directory
        .EnumerateFiles(Path.Combine(Todo, "tests"), "*.md", SearchOption.AllDirectories)
        .Where(file => file.EndsWith(BddFileFormat.FeatureExtension, StringComparison.OrdinalIgnoreCase) ||
                       file.EndsWith(BddFileFormat.StepsExtension, StringComparison.OrdinalIgnoreCase))
        .Select(file => Path.GetRelativePath(Path.Combine(Todo, "tests"), file))
        .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
        .Order(StringComparer.Ordinal)
        .Select(file => new object[] { file.Replace('\\', '/') });

    [Theory]
    [MemberData(nameof(TodoFiles))]
    public void TodoFile_RendersAsItsSnapshot(string file)
    {
        var path = Path.Combine(Todo, "tests", file);

        var result = BddPreview.Render(File.ReadAllText(path), path);

        Snapshot.Match(result.Html, file.Replace('/', '.'));
    }

    [Fact]
    public void AParseError_ShowsAtItsLine_AndTheRestStillRenders()
    {
        var result = BddPreview.Render("""
            # Feature: Broken

            ## Scenario: One

            ```gherkin
            Given I am on the Todo List page
            Wonder whether this is a step
            Then I should be on the Todo List page
            ```
            """, "broken.feature.md");

        Assert.Contains("<li class=\"uat-step uat-not-a-step\" data-line=\"7\">", result.Html);
        Assert.Contains("data-code=\"BDD011\" data-line=\"7\"", result.Html);
        Assert.Contains("<li class=\"uat-step\" data-line=\"8\"><span class=\"uat-kw\">Then</span>", result.Html);
        Assert.True(
            result.Html.IndexOf("data-code=\"BDD011\"", StringComparison.Ordinal) < result.Html.IndexOf("data-line=\"8\"", StringComparison.Ordinal),
            "The diagnostic shows under its own step, before the next one.");
    }

    [Fact]
    public void AFileWithNoFeatureHeading_ShowsWhy_NotABlank()
    {
        var result = BddPreview.Render("Just some notes.\n", "notes.feature.md");

        Assert.Contains("Just some notes.", result.Html);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains("class=\"uat-diag\"", result.Html);
    }

    [Fact]
    public void AFileInTheOldForm_ShowsUat029_WithItsFix()
    {
        var result = BddPreview.Render("""
            # UAT: Old

            ## Scenario: One

            ```gherkin
            Given I am on the Todo List page
            ```
            """, "old.uat.md");

        Assert.Contains("'old.uat.md' is the old name of 'old.feature.md'", System.Net.WebUtility.HtmlDecode(result.Html));
        Assert.Contains("'# UAT: Old' is the old form of '# Feature: Old'", System.Net.WebUtility.HtmlDecode(result.Html));
        Assert.Contains("<span class=\"uat-kw\">Feature</span> Old", result.Html);
    }

    [Fact]
    public void RawHtml_InTheMarkdown_IsShownAsText()
    {
        var result = BddPreview.Render("""
            # Feature: Html

            <script>alert(1)</script>
            """, "html.feature.md");

        Assert.DoesNotContain("<script>", result.Html);
    }

    [Fact]
    public void ParseOnly_SaysSo_AndWhy()
    {
        var result = BddPreview.Render("# Feature: Plain\n", "plain.feature.md", new BddPreviewOptions { ParseOnlyReason = "Nothing is built yet." });

        Assert.Contains("Parsed only, not bound: Nothing is built yet.", result.Html);
    }

    [Fact]
    public void TheStylesheet_HasLightDarkAndHighContrast()
    {
        Assert.Contains("--uat-bg", BddPreview.Stylesheet);
        Assert.Contains("prefers-color-scheme: dark", BddPreview.Stylesheet);
        Assert.Contains("data-theme=\"high-contrast\"", BddPreview.Stylesheet);
        Assert.Contains("forced-colors", BddPreview.Stylesheet);
    }

    [Fact]
    public void RunButtons_OnTheTitleAndEachScenario_WhenTheSuiteCanRun()
    {
        var result = BddPreview.Render(Scenario, "shelf.feature.md", new BddPreviewOptions { Run = new BddPreviewRunState(true) });

        Assert.Contains("<button type=\"button\" class=\"uat-run\" data-run-file title=\"Run every scenario of the file in the suite's runner, from the source: no build when only scenario files changed\">", result.Html);
        Assert.Contains("<button type=\"button\" class=\"uat-run\" data-run=\"3\" title=\"Run this scenario in the suite's runner, from the source: no build when only scenario files changed\">", result.Html);
        Assert.Contains("<button type=\"button\" class=\"uat-run\" data-test data-run=\"3\" title=\"Run this scenario as a test (dotnet test), as CI runs it\">▷ Run as test</button>", result.Html);
        Assert.Contains("<button type=\"button\" class=\"uat-run\" data-test data-run-file title=\"Run every scenario of the file as a test (dotnet test), as CI runs it\">▷ Run file as test</button>", result.Html);
        Assert.Contains("<button type=\"button\" class=\"uat-run\" data-debug data-run=\"3\" title=\"Debug this scenario, stopping at step breakpoints\">", result.Html);
        Assert.Contains("<button type=\"button\" class=\"uat-run\" data-debug data-run-file title=\"Debug every scenario of the file, stopping at step breakpoints\">", result.Html);
        Assert.Contains("<button type=\"button\" class=\"uat-run\" data-step=\"3\" title=\"Step through this scenario, paused before each step\">", result.Html);
        Assert.DoesNotContain("data-step-file", result.Html);
    }

    [Fact]
    public void RunButtons_AreDisabled_WithTheReason_WhenTheSuiteCannotRun()
    {
        var result = BddPreview.Render(Scenario, "shelf.feature.md", new BddPreviewOptions { Run = new BddPreviewRunState(false, "Use [BddScenarioTheory].") });

        Assert.Contains("data-run=\"3\" disabled title=\"Use [BddScenarioTheory].\"", result.Html);
    }

    [Fact]
    public void NoRunButtons_OnAPageOfItsOwn()
        => Assert.DoesNotContain("uat-run", BddPreview.Render(Scenario, "shelf.feature.md").Html);

    private const string Scenario = """
        # Feature: Shelf

        ## Scenario: Shelve

        ```gherkin
        When I shelve "Dune"
        ```
        """;

    [Fact]
    public void TheHostPage_HasTheRootTheBarAndTheScript_AndNoRemoteContent()
    {
        var page = BddPreview.HostPage;

        Assert.Contains("<main id=\"uat-root\"></main>", page);
        Assert.Contains("id=\"uat-suite\"", page);
        Assert.Contains("case \"update\": update(message); break;", page);
        Assert.Contains("default-src 'none'", page);
        Assert.DoesNotContain("<script src=", page);
    }

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
