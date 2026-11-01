using System.Net;
using Xunit;

namespace Bruinet.Bdd.Preview.Tests;

/// <summary>The last run in the preview: each step's status, a failure's message and screenshot, the scenario's duration.</summary>
public sealed class BddPreviewRunTests : IDisposable
{
    private const string Feature = """
        # Feature: Shelf

        ## Scenario: Shelve

        ```gherkin
        When I shelve "Dune"
        And I shelve "Emma"
        Then the shelf holds 2 books
        ```
        """;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Bruinet", "BddPreviewRunTests", Guid.NewGuid().ToString("N"));

    public BddPreviewRunTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void EachStep_ShowsItsStatus_WithATextAlternative_AndStepsAfterAFailure_AreNotRun()
    {
        var html = Render(Run("failed", 1500, null, (6, "Passed", null), (7, "Failed", "Emma is already out")));

        Assert.Contains("data-line=\"6\"><span class=\"uat-kw\">When</span><span class=\"uat-text\"><span class=\"uat-status\" data-status=\"passed\" role=\"img\" aria-label=\"Passed\"", html);
        Assert.Contains("data-line=\"7\"><span class=\"uat-kw\">And</span><span class=\"uat-text\"><span class=\"uat-status\" data-status=\"failed\" role=\"img\" aria-label=\"Failed\"", html);
        Assert.Contains("data-line=\"8\"><span class=\"uat-kw\">Then</span><span class=\"uat-text\"><span class=\"uat-status\" data-status=\"notrun\" role=\"img\" aria-label=\"Not run\"", html);
    }

    [Fact]
    public void TheScenario_ShowsItsStatusAndDuration()
    {
        var html = Render(Run("passed", 1500, null, (6, "Passed", null), (7, "Passed", null), (8, "Passed", null)));

        Assert.Contains("<span class=\"uat-result\" data-status=\"passed\"", html);
        Assert.Contains("passed · 1.5 s</span></h2>", WebUtility.HtmlDecode(html));
    }

    [Fact]
    public void AFailedStep_ShowsItsMessage_AndTheScreenshot_WhichOpensFullSize()
    {
        var screenshot = Path.Combine(_folder, "failure.png");
        File.WriteAllBytes(screenshot, [0x89, 0x50, 0x4E, 0x47]);

        var html = WebUtility.HtmlDecode(Render(Run("failed", 900, screenshot, (6, "Passed", null), (7, "Failed", "Emma is already out"))));

        Assert.Contains("<div class=\"uat-failure\" data-line=\"7\">Emma is already out", html);
        Assert.Contains($"data-open=\"{screenshot}\"", html);
        Assert.Contains($"<img src=\"{new Uri(screenshot).AbsoluteUri}\" alt=\"The screen when the step failed\">", html);
    }

    [Fact]
    public void ACallableCall_TakesItsStepsWorstStatus()
    {
        var run = new BddScenarioRun("Shelve", "failed", 10, null,
            [new BddStepRun(6, "Features/shelf.feature.md", null, "Passed", null),
             new BddStepRun(3, "Features/Shelf.steps.md", 7, "Passed", null),
             new BddStepRun(4, "Features/Shelf.steps.md", 7, "Failed", "no room")],
            DateTimeOffset.Now);

        var html = Render(run);

        Assert.Contains("data-line=\"7\"><span class=\"uat-kw\">And</span><span class=\"uat-text\"><span class=\"uat-status\" data-status=\"failed\"", html);
    }

    [Fact]
    public void AReport_ParsesItsStepsLines()
    {
        var run = BddScenarioRun.Parse("""
            { "scenario": "Shelve", "status": "failed", "durationMs": 42, "evidencePath": null,
              "steps": [ { "line": 6, "file": "Features/shelf.feature.md", "callLine": null, "status": "Passed", "message": null },
                         { "line": 4, "file": "Features/Shelf.steps.md", "callLine": 7, "status": "Failed", "message": "no room" } ] }
            """, DateTimeOffset.Now);

        Assert.NotNull(run);
        Assert.Equal(42, run.DurationMs);
        Assert.Equal([6, 7], run.Steps.Select(step => step.WrittenLine));
        Assert.Equal("no room", run.Steps[1].Message);
        Assert.Equal((null, null), (run.Steps[1].DurationMs, run.Steps[1].Failure));
    }

    [Fact]
    public void AReport_ReadsEachStepsTime_AndTheFailedStepsFailure()
    {
        var run = BddScenarioRun.Parse("""
            { "scenario": "Shelve", "status": "failed", "durationMs": 42, "evidencePath": null,
              "steps": [ { "line": 6, "status": "Passed", "durationMs": 3 },
                         { "line": 7, "status": "Failed", "message": "no room", "durationMs": 9,
                           "failure": { "kind": "assertion", "expected": "2", "actual": "1" } } ] }
            """, DateTimeOffset.Now);

        Assert.Equal([3L, 9L], run!.Steps.Select(step => step.DurationMs));
        Assert.Null(run.Steps[0].Failure);
        Assert.Contains("\"kind\": \"assertion\"", run.Steps[1].Failure);
    }

    private string Render(BddScenarioRun run)
        => BddPreview.Render(Feature, Path.Combine(_folder, "shelf.feature.md"), new BddPreviewOptions { Runs = new Dictionary<string, BddScenarioRun> { [run.Scenario] = run } }).Html;

    private static BddScenarioRun Run(string status, long duration, string? evidence, params (int Line, string Status, string? Message)[] steps)
        => new("Shelve", status, duration, evidence, steps.Select(step => new BddStepRun(step.Line, "Features/shelf.feature.md", null, step.Status, step.Message)).ToList(), DateTimeOffset.Now);
}
