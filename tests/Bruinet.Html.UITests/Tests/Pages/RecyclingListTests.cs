using Bruinet.Core.Collections;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Scopes;
using Bruinet.Html.Collections;
using Bruinet.Html.UITests.PageObjects;
using Bruinet.Html.UITests.TestBase;

namespace Bruinet.Html.UITests.Tests.Pages;

/// <summary>
/// A row object stands for its item, not for a DOM node or a position (design 7.6, R0): on a
/// list that reuses its nodes, it still acts on its own item, and it never acts on another.
/// </summary>
public sealed class RecyclingListTests : BlazorSampleTestBase
{
    [Fact]
    public void ARow_FollowsItsItem_WhenTheListReusesItsNodes()
    {
        NavigateToPage("/recycling-list");
        var page = new RecyclingListPage(Context);
        var row = page.Rows.Item(3);
        var title = row.Title.GetText();
        Assert.Equal(ItemKeyKind.Logical, row.Key.Kind);

        page.Recycle.Click();
        page.Status.AssertText("Recycled");
        Assert.NotEqual(title, page.Rows.Item(3).Title.GetText());

        row.Delete.Click();

        page.Status.AssertText($"Deleted {title}");
        Assert.Null(page.Rows.TryItem(row.Key.Value));
    }

    [Fact]
    public void ARowWhoseItemIsGone_FailsNamingIt_AndActsOnNothingElse()
    {
        NavigateToPage("/recycling-list");
        var page = new RecyclingListPage(Context);
        var row = page.Rows.Item(0);
        var title = row.Title.GetText();
        row.Delete.Click();
        page.Status.AssertText($"Deleted {title}");
        var countAfterDelete = page.Rows.GetItemCount();

        var error = Record.Exception(() => row.Delete.Click(timeoutMs: 500));

        var notReady = Assert.IsType<ScopeNotReadyException>(error);
        Assert.Equal(ScopeReadinessState.ItemChanged, notReady.Readiness.State);
        page.Status.AssertText($"Deleted {title}");
        Assert.Equal(countAfterDelete, page.Rows.GetItemCount());
    }
}
