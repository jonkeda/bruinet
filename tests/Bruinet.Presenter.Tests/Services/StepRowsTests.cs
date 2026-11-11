using Bruinet.Presenter.Models;
using Bruinet.Presenter.Services;
using Bruinet.Presenter.ViewModels;

namespace Bruinet.Presenter.Tests.Services;

public sealed class StepRowsTests
{
    [Fact]
    public void Rows_PutACallAboveItsSteps()
    {
        var scenario = new BddScenarioViewModel(new BddScenarioLoadResult(
            "Scenario",
            "Suite",
            "a.feature.md",
            [],
            [
                new BddStepLoadResult("wait", "Given I am on the Todo List page", "c1", 10),
                new BddStepLoadResult("wait", "  Given the server has todos", "c2", 11, "Given the synced list has todos"),
                new BddStepLoadResult("wait", "  When I tap Sync", "c3", 11, "Given the synced list has todos"),
                new BddStepLoadResult("wait", "Then the list shows them", "c4", 12),
            ]));

        var rows = scenario.Rows;

        Assert.Equal(5, rows.Count);
        Assert.True(rows[1].IsCall);
        Assert.Equal("Given the synced list has todos", rows[1].Label);
        Assert.Equal((1, 3), (rows[1].FirstStep, rows[1].EndStep));
        Assert.True(rows[2].IsInCall);
        Assert.Equal("Given the server has todos", rows[2].Label);
        Assert.False(rows[2].CanBreak);
        Assert.True(rows[4].CanBreak);

        scenario.Steps[1].Status = "pass";
        Assert.Equal("run", rows[1].Status);
        scenario.Steps[2].Status = "fail";
        scenario.Steps[2].Message = "No Sync button";
        Assert.Equal("fail", rows[1].Status);
        Assert.Contains("No Sync button", rows[3].Tooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void Rows_OfATodoScenarioWithACall()
    {
        var folder = RepositoryPath("samples", "Todo", "tests", "Bruinet.Samples.Todo.Uat");
        using var service = new BddWorkspaceService();

        var result = service.LoadFolder(folder);
        var source = result.Scenarios.First(scenario => scenario.Steps.Any(step => step.CallText is not null));
        var rows = new BddScenarioViewModel(source).Rows;

        var call = Assert.Single(rows, row => row.IsCall && row.Label == source.Steps.First(step => step.CallText is not null).CallText);
        Assert.True(rows[call.Index + 1].IsInCall);
        Assert.StartsWith("Given ", rows[0].Label, StringComparison.Ordinal);
    }

    private static string RepositoryPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests")))
        {
            directory = directory.Parent;
        }

        return Path.Combine([directory?.FullName ?? throw new DirectoryNotFoundException("The Bruinet root was not found."), .. parts]);
    }
}
