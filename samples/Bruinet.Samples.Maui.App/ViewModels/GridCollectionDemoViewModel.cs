using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Globalization;

namespace Bruinet.Samples.Maui.App.ViewModels;

/// <summary>
/// ViewModel for the Grid + CollectionView demo page.
/// </summary>
/// <remarks>
/// <see cref="ProductItem"/> deliberately has no Id or AutomationId property and the
/// commands do no reindexing. Rows are addressed by scope, not by a unique id, so the
/// model stays a plain model. Repeating row AutomationIds are an acceptance condition
/// of this demo, not an oversight.
/// </remarks>
public class GridCollectionDemoViewModel : ParentViewModel
{
    private const int BulkAddCount = 60;

    private string _newProductName = "";
    private string _newProductPrice = "";
    private bool _newProductInStock = true;
    private ProductItem? _selectedProduct;

    public GridCollectionDemoViewModel()
    {
        Products = [];
        Products.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ProductCount));
            OnPropertyChanged(nameof(IsProductListEmpty));
        };

        AddProductCommand = new AsyncRelayCommand(this, AddProductAsync);
        DeleteProductCommand = new RelayCommand<ProductItem>(DeleteProduct);
        ClearProductsCommand = new RelayCommand(() => Products.Clear());
        BulkAddProductsCommand = new RelayCommand(BulkAddProducts);
        ResetDemoCommand = new RelayCommand(ResetDemo);
        OpenProductCommand = new RelayCommand<ProductItem>(product => OpenedProduct = $"Opened {product?.Name}");
        Shortcuts = [new ProductItem { Name = "Excavator" }, new ProductItem { Name = "Crane" }];

        ResetDemo();
    }

    #region Grid form section

    public string NewProductName
    {
        get => _newProductName;
        set => SetProperty(ref _newProductName, value);
    }

    public string NewProductPrice
    {
        get => _newProductPrice;
        set => SetProperty(ref _newProductPrice, value);
    }

    public bool NewProductInStock
    {
        get => _newProductInStock;
        set => SetProperty(ref _newProductInStock, value);
    }

    public IAsyncRelayCommand AddProductCommand { get; }

    private async Task AddProductAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProductName))
            return;

        await Task.Delay(50);

        _ = decimal.TryParse(NewProductPrice, out var price);
        Products.Add(new ProductItem
        {
            Name = NewProductName,
            Price = price,
            InStock = NewProductInStock
        });

        NewProductName = "";
        NewProductPrice = "";
    }

    #endregion

    #region Collection section

    public ObservableCollection<ProductItem> Products { get; }

    public ProductItem? SelectedProduct
    {
        get => _selectedProduct;
        set => SetProperty(ref _selectedProduct, value);
    }

    /// <summary>
    /// The logical number of products in the data source.
    /// </summary>
    /// <remarks>
    /// Not the same as the number of rows realized in the automation tree: a
    /// virtualizing CollectionView materializes only what is near the viewport.
    /// Tests read this for logical count and the row APIs for materialization.
    /// </remarks>
    public int ProductCount => Products.Count;

    public bool IsProductListEmpty => Products.Count == 0;

    /// <summary>Rows opened by a tap on the row (a recognizer bound through x:Reference), as Construction's.</summary>
    public ObservableCollection<ProductItem> Shortcuts { get; }

    public ICommand OpenProductCommand { get; }

    public string OpenedProduct
    {
        get => _openedProduct;
        private set => SetProperty(ref _openedProduct, value);
    }

    private string _openedProduct = string.Empty;

    public ICommand DeleteProductCommand { get; }
    public ICommand ClearProductsCommand { get; }
    public ICommand BulkAddProductsCommand { get; }
    public ICommand ResetDemoCommand { get; }

    private void DeleteProduct(ProductItem? product)
    {
        if (product == null)
            return;

        Products.Remove(product);
        // No reindexing: rows are scoped, not id-addressed.
    }

    /// <summary>
    /// Adds enough rows to force virtualization, so scroll-and-observe has a real target.
    /// </summary>
    private void BulkAddProducts()
    {
        for (var i = 0; i < BulkAddCount; i++)
        {
            Products.Add(new ProductItem
            {
                Name = $"Bulk Product {i:D2}",
                Price = 10m + i,
                InStock = i % 3 != 0
            });
        }
    }

    /// <summary>
    /// Restores the three seed products and clears form state.
    /// </summary>
    /// <remarks>
    /// Required because the UI test collection shares one fixture and one Shell across
    /// test classes, and Shell may retain page instances. Navigation alone does not
    /// guarantee clean state, so every test resets before asserting.
    /// </remarks>
    private void ResetDemo()
    {
        Products.Clear();
        Products.Add(new ProductItem { Name = "Keyboard", Price = 49.99m, InStock = true });
        Products.Add(new ProductItem { Name = "Mouse", Price = 24.50m, InStock = true });
        Products.Add(new ProductItem { Name = "Monitor", Price = 199.00m, InStock = false });

        NewProductName = "";
        NewProductPrice = "";
        NewProductInStock = true;
        SelectedProduct = null;
    }

    #endregion
}

/// <summary>
/// A product row. Has no Id or AutomationId property - item scoping makes them unnecessary.
/// </summary>
public class ProductItem : INotifyPropertyChanged
{
    private string _name = "";
    private decimal _price;
    private bool _inStock;
    private bool _isSelected;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public decimal Price
    {
        get => _price;
        set { _price = value; OnPropertyChanged(); OnPropertyChanged(nameof(PriceText)); }
    }

    public bool InStock
    {
        get => _inStock;
        set { _inStock = value; OnPropertyChanged(); OnPropertyChanged(nameof(StockText)); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public string PriceText => Price.ToString("C", CultureInfo.CurrentCulture);

    public string StockText => InStock ? "In stock" : "Out of stock";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
