using Bruinet.Maui.Containers;
using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Containers;

/// <summary>
/// The product collection.
/// </summary>
/// <remarks>
/// A collection is a container that also hands out items, so it scopes its own
/// non-item controls (<see cref="Title"/>, <see cref="EmptyLabel"/>,
/// <see cref="CountLabel"/>) alongside <c>Item(i)</c>. Rows are discovered by the
/// repeating "ProductRow" automation id.
/// </remarks>
public class ProductCollection : CollectionObjectBase<GridCollectionDemoPage, ProductCollection, ProductRow>
{
    /// <summary>The number of products the demo seeds on reset.</summary>
    public const int SeedCount = 3;

    public ProductCollection(IMauiScope<GridCollectionDemoPage> parentScope, string automationId)
        : base(parentScope,
               automationId,
               ItemStrategy.ByAutomationId("ProductRow"),
               (collection, itemRoot, index) => new ProductRow(collection, itemRoot, index))
    {
    }

    #region Collection-level controls

    /// <summary>The collection heading.</summary>
    public Label<ProductCollection> Title => new(this, "ProductListTitle");

    /// <summary>The empty-state label, shown only when there are no products.</summary>
    public Label<ProductCollection> EmptyLabel => new(this, "ProductListEmptyLabel");

    /// <summary>
    /// The logical product count reported by the view model.
    /// </summary>
    /// <remarks>
    /// This is the data-source count, which under virtualization is not the same as the
    /// number of realized row roots. Use this for logical count and the row APIs for
    /// materialization.
    /// </remarks>
    public Label<ProductCollection> CountLabel => new(this, "ProductCountLabel");

    /// <summary>Removes every product.</summary>
    public Button<ProductCollection> ClearButton => new(this, "ProductClearButton");

    /// <summary>Restores the seed products and clears form state.</summary>
    public Button<ProductCollection> ResetButton => new(this, "ProductResetButton");

    /// <summary>Adds enough products to force virtualization.</summary>
    public Button<ProductCollection> BulkAddButton => new(this, "ProductBulkAddButton");

    #endregion

    /// <summary>
    /// The CollectionView is the element that scrolls; the container root is the
    /// AutomationContainer wrapper around it, which does not.
    /// </summary>
    protected override IMauiElement? ScrollTarget
        => TryFindElement(Locator.ByAutomationId("ProductCollectionView"));

    #region Domain helpers

    /// <summary>
    /// Reads the logical product count from the count label.
    /// </summary>
    /// <returns>The count, or -1 when the label is unreadable.</returns>
    public int GetLogicalCount()
    {
        var text = CountLabel.GetText();
        return int.TryParse(text?.Trim(), out var count) ? count : -1;
    }

    /// <summary>
    /// Waits until the logical count reaches <paramref name="expected"/>.
    /// </summary>
    public bool WaitLogicalCount(int expected, int? timeoutMs = null)
        => RunWait(() => GetLogicalCount() == expected, timeoutMs);

    /// <summary>
    /// Asserts the logical count, returning the collection for chaining.
    /// </summary>
    public ProductCollection AssertLogicalCount(int expected, string? message = null, int? timeoutMs = null)
    {
        if (!WaitLogicalCount(expected, timeoutMs))
        {
            throw new ControlAssertionException(
                message ?? $"Expected logical count {expected} but the label reported {GetLogicalCount()}.");
        }

        return Self;
    }

    /// <summary>
    /// Restores the seeded state and waits for it to take effect.
    /// </summary>
    /// <remarks>
    /// The UI test collection shares one fixture and one Shell, and Shell may retain
    /// page instances, so navigation alone does not guarantee clean state.
    /// </remarks>
    public ProductCollection Reset(int? timeoutMs = null)
    {
        ResetButton.Click();
        WaitLogicalCount(SeedCount, timeoutMs);
        return Self;
    }

    /// <summary>
    /// Finds a row by its visible product name.
    /// </summary>
    public ProductRow ByName(string name)
        => ItemWhere(row => row.Name.GetText() == name);

    #endregion
}
