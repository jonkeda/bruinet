namespace Bruinet.NativeIos.UITests.Tests;

/// <summary>A table view of 200 rows: rows keyed by their item, found far down, and followed after a delete.</summary>
[Collection(GalleryCollection.Name)]
public sealed class InvoicesTests(GalleryFixture gallery)
{
    private InvoicesPage Open()
    {
        gallery.Open("invoices");
        var page = new InvoicesPage(gallery.Context);
        page.AssertIdle();
        page.Count.AssertText("200 invoices");
        return page;
    }

    [Fact]
    public void ARow_IsKeyedByItsName_AndReadByItsViews()
    {
        var page = Open();

        var row = page.Invoices.Item("Invoice 3");

        Assert.Equal("Invoice 3", row.Key.Value);
        row.Text("amount").AssertText("€ 30,00");
    }

    /// <summary>A table keeps all its rows in the tree: a row far down is found by its key, then scrolled into view to be tapped.</summary>
    [Fact]
    public void ARowFarDown_IsFound_AndScrolledIntoView()
    {
        var page = Open();

        var row = page.Invoices.Item("Invoice 150", timeoutMs: 30000);

        row.Text("amount").AssertText("€ 1500,00");
        row.Button("delete").Click();
        page.Count.AssertText("199 invoices");
    }

    /// <summary>
    /// Deleting a row rebinds the cells below it to other items. The row object of a later item
    /// follows its item; it never answers for the item now in its old place.
    /// </summary>
    [Fact]
    public void AfterADelete_ARowFollowsItsItem()
    {
        var page = Open();
        var fourth = page.Invoices.Item("Invoice 4");

        page.Invoices.Item("Invoice 2").Button("delete").Click();

        page.Count.AssertText("199 invoices");
        fourth.Text("amount").AssertText("€ 40,00");
    }

    /// <summary>Pulling to refresh makes the page busy; the next call waits it out.</summary>
    [Fact]
    public void PullToRefresh_IsWaitedOut_ThroughTheBusySignal()
    {
        var page = Open();

        page.Refresh.PullToRefresh();

        page.Count.AssertText("200 invoices, refreshed");
    }
}
