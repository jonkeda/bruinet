using Bruinet.Maui.UITests.Containers;

namespace Bruinet.Maui.UITests.Pages;

/// <summary>
/// Page object for the Grid + CollectionView demo page.
/// </summary>
/// <remarks>
/// The form and collection are constructed once, not exposed as <c>=&gt; new()</c>
/// properties, so their cached container roots survive between calls.
/// </remarks>
public class GridCollectionDemoPage : PageObjectBase<GridCollectionDemoPage>
{
    public GridCollectionDemoPage(IMauiTestContext context)
        : base(context)
    {
        ProductForm = new ProductFormContainer(this, "ProductFormContainer");
        Products = new ProductCollection(this, "ProductListContainer");
        Shortcuts = new ShortcutRows(this);
    }

    /// <inheritdoc />
    public override string Name => "GridCollectionPage";


    /// <summary>The page title label.</summary>
    public Label<GridCollectionDemoPage> PageTitle => new(this, "PageTitle");

    /// <summary>The Grid-based product entry form.</summary>
    public ProductFormContainer ProductForm { get; }

    /// <summary>The product collection.</summary>
    public ProductCollection Products { get; }

    /// <summary>Rows opened by a tap on the row.</summary>
    public ShortcutRows Shortcuts { get; }

    /// <summary>Which shortcut was opened last.</summary>
    public Label<GridCollectionDemoPage> OpenedProduct => new(this, "OpenedProductLabel");
}
