using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Container;

/// <summary>
/// A list control that keeps its RefreshView private, two levels down, as a component library's list
/// does: pulling the control refreshes it, and the refresh awaits its service and replaces the rows.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "RefreshView")]
public class NestedRefreshTests
{
    private readonly ContainerTestPage _page;

    public NestedRefreshTests(MauiFixture fixture)
    {
        _page = fixture.NavigateToContainerModule();
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task Pull_ReachesTheRefreshViewInside_AndTheFetchedRowsReplaceTheOld()
    {
        _page.NestedRefreshStatus.AssertText("not fetched");

        _page.NestedRefreshList.PullToRefresh();

        _page.NestedRefreshStatus.AssertText("fetched 1: Fresh A, Fresh B, Fresh C");

        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task HeaderRefresh_RunsTheSameFetch_AndAPullAfterItFetchesAgain()
    {
        _page.NestedRefreshButton.Click();
        _page.NestedRefreshStatus.AssertText("fetched 1: Fresh A, Fresh B, Fresh C");

        _page.NestedRefreshList.PullToRefresh();

        _page.NestedRefreshStatus.AssertText("fetched 2: Fresh A, Fresh B, Fresh C");

        return Task.CompletedTask;
    }
}
