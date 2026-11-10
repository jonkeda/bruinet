using Bruinet.Maui.UITests.Containers;
using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Collection;

/// <summary>
/// UI tests for CollectionView item scoping, retrieval, mutation, and virtualization.
/// </summary>
/// <remarks>
/// Logical count is read from <c>ProductCountLabel</c>; row APIs report what is
/// materialized in the automation tree. Logical indexes come from UI Automation set metadata,
/// so <c>Item(i)</c> keeps its data-source meaning as the realized window moves.
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "CollectionView")]
public class ProductCollectionTests
{
    private const int BulkTotal = ProductCollection.SeedCount + 60;

    private readonly MauiFixture _fixture;

    public ProductCollectionTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _fixture.NavigateToGridCollectionDemo();
    }

    private GridCollectionDemoPage Page => _fixture.GridCollectionDemoPage;

    #region Item retrieval

    /// <summary>1. Item, the indexer, and TryItem return typed rows with correct indices.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Item")]
    public Task Item_Indexer_AndTryItem_ReturnTypedRows()
    {
        ProductRow first = Page.Products.Item(0);
        ProductRow viaIndexer = Page.Products[0];
        ProductRow? viaTry = Page.Products.TryItem(1);

        Assert.Equal(0, first.Index);
        Assert.Equal(0, viaIndexer.Index);
        Assert.NotNull(viaTry);
        Assert.Equal(1, viaTry!.Index);
        Assert.Equal(first.Name.GetText(), viaIndexer.Name.GetText());

        return Task.CompletedTask;
    }

    /// <summary>2. Out-of-range TryItem returns null; Item throws.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "TryItem")]
    public Task OutOfRange_TryItemReturnsNull_ItemThrows()
    {
        Assert.Null(Page.Products.TryItem(9999));
        // Item(i) waits for the row (Q10); a short budget keeps the expected failure quick.
        Assert.Throws<ElementNotFoundException>(() => Page.Products.Item(9999, timeoutMs: 500));

        return Task.CompletedTask;
    }

    #endregion

    #region Item scoping

    /// <summary>
    /// 3. Rows 0-2 return the seed products despite every row sharing child ids.
    /// This is the behaviour the whole design exists to provide.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "ItemScoping")]
    public Task Rows_WithRepeatingIds_AreIndependentlyScoped()
    {
        Assert.Equal("Keyboard", Page.Products.Item(0).Name.GetText());
        Assert.Equal("Mouse", Page.Products.Item(1).Name.GetText());
        Assert.Equal("Monitor", Page.Products.Item(2).Name.GetText());

        return Task.CompletedTask;
    }

    /// <summary>4. Every control in a row resolves relative to that row.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "ItemScoping")]
    public Task Row_AllTemplateControls_ResolveWithinTheRow()
    {
        var row = Page.Products.Item(2);

        row.Name.AssertText("Monitor");
        row.Stock.AssertText("Out of stock");
        row.Price.AssertExists();
        row.Selected.AssertExists();
        row.Delete.AssertExists();

        return Task.CompletedTask;
    }

    /// <summary>5. Acting on one row does not act on another.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "ItemScoping")]
    public Task Row_Interaction_DoesNotLeakToOtherRows()
    {
        Page.Products.Item(0).Selected.SetChecked(true);

        Assert.Equal(true, Page.Products.Item(0).Selected.IsChecked());
        Assert.Equal(false, Page.Products.Item(1).Selected.IsChecked());

        return Task.CompletedTask;
    }

    /// <summary>6. A row cannot reach collection-level controls.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "NoParentFallback")]
    public Task Row_DoesNotFindCollectionLevelControls()
    {
        var countInRow = Page.Products.Item(0).Label("ProductCountLabel");

        Page.Products.CountLabel.AssertExists();
        Assert.False(countInRow.Exists());

        return Task.CompletedTask;
    }

    #endregion

    #region Collection as a scope

    /// <summary>7. Collection-level controls resolve.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "CollectionIsScope")]
    public Task Collection_ResolvesItsOwnControls()
    {
        Page.Products.Title.AssertExists();
        Page.Products.CountLabel.AssertExists();
        Page.Products.ClearButton.AssertExists();
        Page.Products.ResetButton.AssertExists();
        Page.Products.BulkAddButton.AssertExists();

        return Task.CompletedTask;
    }

    /// <summary>8. Clear shows the empty state; reset restores the seed state.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "EmptyView")]
    public Task Clear_ShowsEmptyState_ResetRestoresSeed()
    {
        Page.Products.ClearButton.Click();

        Assert.True(Page.Products.WaitLogicalCount(0, TestConstants.DefaultTestTimeoutMs));
        Page.Products.AssertEmpty(true);

        // IsVisible drives presence in the automation tree on Windows, so the empty
        // label appears and disappears rather than merely changing visibility. Wait for
        // it to arrive before asserting on it.
        Assert.True(Page.Products.EmptyLabel.WaitExists(true, TestConstants.DefaultTestTimeoutMs));

        Page.Products.Reset(TestConstants.DefaultTestTimeoutMs);

        Page.Products.AssertLogicalCount(ProductCollection.SeedCount);

        // IsVisible="false" removes the label from the automation tree on Windows.
        Page.Products.EmptyLabel.AssertExists(false);
        Page.Products.AssertEmpty(false);

        return Task.CompletedTask;
    }

    /// <summary>9. Deleting shifts remaining rows with no app-side reindexing.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "ItemScoping")]
    public Task DeletingRow_ShiftsRemainingRowsCorrectly()
    {
        Page.Products.Item(0).Delete.Click();

        Assert.True(Page.Products.WaitLogicalCount(
            ProductCollection.SeedCount - 1, TestConstants.DefaultTestTimeoutMs));

        Assert.Equal("Mouse", Page.Products.Item(0).Name.GetText());
        Assert.Equal("Monitor", Page.Products.Item(1).Name.GetText());

        return Task.CompletedTask;
    }

    #endregion

    #region Search by content

    /// <summary>10. FindItem, ItemWhere, and ByName search by row content.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "ItemWhere")]
    public Task SearchByContent_FindsTheRightRow()
    {
        var found = Page.Products.FindItem(r => r.Name.GetText() == "Monitor");
        Assert.NotNull(found);
        Assert.Equal(2, found!.Index);

        Assert.Equal(1, Page.Products.ItemWhere(r => r.Name.GetText() == "Mouse").Index);
        Assert.Equal(0, Page.Products.ByName("Keyboard").Index);

        Assert.Null(Page.Products.FindItem(r => r.Name.GetText() == "Nonexistent"));
        Assert.Throws<ElementNotFoundException>(
            () => Page.Products.ItemWhere(r => r.Name.GetText() == "Nonexistent", timeoutMs: 500));

        return Task.CompletedTask;
    }

    /// <summary>10b. ByName targets the right row for a mutation.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "DomainHelper")]
    public Task ByName_DeletesTheRightRow()
    {
        Page.Products.ByName("Mouse").Delete.Click();

        Assert.True(Page.Products.WaitLogicalCount(
            ProductCollection.SeedCount - 1, TestConstants.DefaultTestTimeoutMs));
        Assert.Null(Page.Products.FindItem(r => r.Name.GetText() == "Mouse"));

        return Task.CompletedTask;
    }

    #endregion

    #region Fluent contract

    /// <summary>11. Collection assertions and actions return the collection instance.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "FluentReturn")]
    public Task CollectionMembers_ReturnTheCollection()
    {
        // One reference throughout: page and container objects are created per access, so each
        // read of Page.Products is a different instance by design. The contract under test is
        // that a call hands back the scope it was called on.
        var page = Page;
        var products = page.Products;

        ProductCollection afterAssert = products.AssertLogicalCount(ProductCollection.SeedCount);
        ProductCollection afterSelect = products.SelectItem(1);

        Assert.Same(products, afterAssert);
        Assert.Same(products, afterSelect);

        ProductRow row = products.Item(0);
        ProductRow afterCheck = row.Selected.SetChecked(true);

        Assert.Same(row, afterCheck);
        Assert.Same(products, afterCheck.Parent);
        Assert.Same(page, afterCheck.Parent.Parent);

        return Task.CompletedTask;
    }

    #endregion

    #region Virtualization

    /// <summary>12. Bulk add reports logical count 63 through the count label.</summary>
    [Fact(Timeout = TestConstants.LongTestTimeoutMs)]
    [Trait("Pattern", "Virtualization")]
    public Task BulkAdd_ReportsLogicalCountThroughLabel()
    {
        Page.Products.BulkAddButton.Click();

        // Logical count comes from the data source, not from realized rows.
        Assert.True(Page.Products.WaitLogicalCount(BulkTotal, TestConstants.LongTestTimeoutMs));

        return Task.CompletedTask;
    }

    /// <summary>13. ScrollToItem materializes an off-screen row without a fixed delay.</summary>
    [WindowsOnlyFact("Android and iOS publish no logical row index (PositionInSet), so a row is found by position among the realised ones", Timeout = TestConstants.LongTestTimeoutMs)]
    [Trait("Pattern", "Virtualization")]
    public Task ScrollToItem_MaterializesOffscreenRow()
    {
        Page.Products.BulkAddButton.Click();
        Assert.True(Page.Products.WaitLogicalCount(BulkTotal, TestConstants.LongTestTimeoutMs));

        Page.Products.ScrollToItem(BulkTotal - 3);

        Assert.NotNull(Page.Products.TryItem(BulkTotal - 3));

        return Task.CompletedTask;
    }

    /// <summary>14. Content search finds a bulk row that starts off-screen.</summary>
    [Fact(Timeout = TestConstants.LongTestTimeoutMs)]
    [Trait("Pattern", "Virtualization")]
    public Task ItemWhere_ScrollsToFindOffscreenRow()
    {
        Page.Products.BulkAddButton.Click();
        Assert.True(Page.Products.WaitLogicalCount(BulkTotal, TestConstants.LongTestTimeoutMs));

        // A scroll through 60 rows, reading each one's name, takes longer than the default wait
        // (10.4 s in the 2026-09-19 baseline): the search gets a budget to match (Q9).
        var row = Page.Products.FindItem(
            r => r.Name.GetText() == "Bulk Product 55",
            timeoutMs: TestConstants.DefaultTestTimeoutMs);

        Assert.NotNull(row);

        return Task.CompletedTask;
    }

    #endregion
}
