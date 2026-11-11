using Bruinet.Presenter.Models;
using Bruinet.Presenter.Services;
using Bruinet.Presenter.ViewModels;

namespace Bruinet.Presenter.Tests.Services;

public sealed class PresenterSteppingTests
{
    // Rows: 0 Given (step 0) | 1 call (steps 1-2) | 2 inner (1) | 3 inner (2) | 4 Then (3)
    private static readonly IReadOnlyList<BddStepRowViewModel> Rows = new BddScenarioViewModel(new BddScenarioLoadResult(
        "Scenario",
        "Suite",
        "a.feature.md",
        [],
        [
            new BddStepLoadResult("wait", "Given I am on the list", "c1", 10),
            new BddStepLoadResult("wait", "  Given the server has todos", "c2", 11, "Given the synced list has todos"),
            new BddStepLoadResult("wait", "  When I tap Sync", "c3", 11, "Given the synced list has todos"),
            new BddStepLoadResult("wait", "Then the list shows them", "c4", 12),
        ])).Rows;

    [Fact]
    public void Step_RunsAStep_OrACallWhole()
    {
        Assert.Equal(new StepPlan(1, false, null), PresenterStepping.Plan(Rows, 0, PresenterMove.Step));
        Assert.Equal(1, PresenterStepping.PositionAfter(Rows, 1, 0));

        Assert.Equal(new StepPlan(3, false, null), PresenterStepping.Plan(Rows, 1, PresenterMove.Step));
        Assert.Equal(4, PresenterStepping.PositionAfter(Rows, 3, 1));
    }

    [Fact]
    public void Into_GoesInsideACall_RunningNothing()
    {
        Assert.Equal(new StepPlan(1, false, 2), PresenterStepping.Plan(Rows, 1, PresenterMove.Into));
        Assert.Null(PresenterStepping.Plan(Rows, 0, PresenterMove.Into));
    }

    [Fact]
    public void Out_RunsTheRestOfTheCall()
    {
        Assert.Equal(new StepPlan(3, false, null), PresenterStepping.Plan(Rows, 2, PresenterMove.Out));
        Assert.Equal(4, PresenterStepping.PositionAfter(Rows, 3, 2));
        Assert.Null(PresenterStepping.Plan(Rows, 0, PresenterMove.Out));
        Assert.Null(PresenterStepping.Plan(Rows, 1, PresenterMove.Out));
    }

    [Fact]
    public void Step_InsideACall_StopsAtTheNextInnerStep()
    {
        Assert.Equal(new StepPlan(2, false, null), PresenterStepping.Plan(Rows, 2, PresenterMove.Step));
        Assert.Equal(3, PresenterStepping.PositionAfter(Rows, 2, 2));
    }

    [Fact]
    public void RunUntil_PausesBeforeTheRow_OnlyAhead()
    {
        Assert.Equal(new StepPlan(2, false, 3), PresenterStepping.PlanUntil(Rows, 0, 3));
        Assert.Null(PresenterStepping.PlanUntil(Rows, 3, 1));
    }

    [Fact]
    public void Play_StopsAtACallsBreakpoint_BeforeItsFirstStep()
    {
        Rows[1].HasBreakpoint = true;
        Rows[4].HasBreakpoint = true;
        try
        {
            Assert.Same(Rows[1], PresenterStepping.BreakBefore(Rows, 1));
            Assert.Null(PresenterStepping.BreakBefore(Rows, 2));
            Assert.Same(Rows[4], PresenterStepping.BreakBefore(Rows, 3));
        }
        finally
        {
            Rows[1].HasBreakpoint = false;
            Rows[4].HasBreakpoint = false;
        }
    }

    [Fact]
    public void BreakpointStore_TogglesAFilesLine_AndKeepsTheRest()
    {
        var path = Path.Combine(Path.GetTempPath(), "BruinetPresenterUat", Guid.NewGuid().ToString("N"), "bdd-breakpoints.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{ "files": { "C:\\other.feature.md": [3] }, "breakOnFailure": true }""");
        try
        {
            var store = new PresenterBreakpointStore(path);

            Assert.True(store.Toggle(@"C:\suite\a.feature.md", 12));
            Assert.Equal([12], store.Lines(@"C:\suite\a.feature.md"));
            Assert.Equal([3], store.Lines(@"C:\other.feature.md"));
            Assert.Contains("breakOnFailure", File.ReadAllText(path), StringComparison.Ordinal);

            Assert.False(store.Toggle(@"C:\SUITE\a.feature.md", 12));
            Assert.Empty(store.Lines(@"C:\suite\a.feature.md"));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void Play_StopsAtAnIdesBreakpointInAStepFile_TheRowsCoverTheFeatureFile()
    {
        var folder = Path.Combine(Path.GetTempPath(), "BruinetPresenterUat", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "bdd-breakpoints.json");
        var feature = Path.Combine(folder, "shelf.feature.md");
        var steps = Path.Combine(folder, "shelf.steps.md");
        Directory.CreateDirectory(folder);
        File.WriteAllText(path, $$"""{ "files": { {{System.Text.Json.JsonSerializer.Serialize(steps)}}: [6], {{System.Text.Json.JsonSerializer.Serialize(feature)}}: [9] } }""");
        try
        {
            Bruinet.Bdd.Runner.BddRunnerPosition At(string file, int line) => new(file, line, "a step", 1, []);

            Assert.True(PresenterStepping.BreaksInStepFile(At(steps, 6), feature, path));
            Assert.False(PresenterStepping.BreaksInStepFile(At(steps, 7), feature, path));
            Assert.False(PresenterStepping.BreaksInStepFile(At(feature, 9), feature, path));
            Assert.False(PresenterStepping.BreaksInStepFile(null, feature, path));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
