using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Collection;

/// <summary>
/// A list that loads its first page after its page appears and the next as it nears its end
/// (<c>RemainingItemsThreshold</c>): a search for a row finds it however many pages away, on every head.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "CollectionView")]
public class PagedListTests
{
    private readonly PagedListTestPage _page;

    public PagedListTests(MauiFixture fixture)
    {
        _page = fixture.NavigateToPagedList();
    }

    // Three pages fetched and added on the app's UI thread, between a device's reads of each row. Android swipes a
    // screenful at a time, about two seconds each: row 45 took past 10 s there (R2-T123).
    private const int PagingMs = 30000;

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task FirstPage_ArrivesAfterThePageAppears()
    {
        _page.Rows.Row("Item 1").Text.AssertText("Item 1");

        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.LongTestTimeoutMs)]
    public Task RowOnPageThree_IsFoundByLoadingThePagesBeforeIt()
    {
        _page.Rows.Row("Item 45", PagingMs).Text.AssertText("Item 45");

        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.LongTestTimeoutMs)]
    public Task ScrollToEnd_LoadsEveryPage()
    {
        _page.Rows.Row("Item 1");

        _page.Rows.ScrollToEnd(PagingMs);

        _page.Status.AssertText("60 of 60");
        _page.Rows.Row("Item 60").Text.AssertText("Item 60");

        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task Search_ReplacesTheRows_AndClearingItShowsThemAgain()
    {
        _page.Rows.Row("Item 3");

        _page.Search.SetText("Item 7");
        _page.Rows.AssertItemCount(1);
        _page.ClearSearch.Click();

        _page.Rows.Row("Item 3").Text.AssertText("Item 3");

        return Task.CompletedTask;
    }
}
