using Bruinet.Maui.UITests.Containers;
using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Container;

/// <summary>
/// UI tests for Grid-based container scoping and fluent behaviour.
/// </summary>
/// <remarks>
/// The typed locals here (<c>ProductFormContainer result = ...</c>) are load-bearing:
/// they assert the compile-time fluent contract as well as the runtime behaviour.
/// Replacing them with looser existence checks would drop half the coverage.
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "GridContainer")]
public class GridContainerTests
{
    private readonly MauiFixture _fixture;

    public GridContainerTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _fixture.NavigateToGridCollectionDemo();
    }

    private GridCollectionDemoPage Page => _fixture.GridCollectionDemoPage;

    #region Scoping

    /// <summary>1. Form children resolve inside the form container.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "Scoping")]
    public Task ProductForm_FindsItsOwnControls()
    {
        Page.ProductForm.Title.AssertExists();
        Page.ProductForm.NameEntry.AssertExists();
        Page.ProductForm.PriceEntry.AssertExists();
        Page.ProductForm.AddButton.AssertExists();
        return Task.CompletedTask;
    }

    /// <summary>2. A page-level control cannot be reached through the form scope.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "NoParentFallback")]
    public Task ProductForm_DoesNotFindControlsOutsideItself()
    {
        // PageTitle exists on the page, but not inside the product form.
        var titleInForm = Page.ProductForm.Label("PageTitle");

        Page.PageTitle.AssertExists();
        Assert.False(titleInForm.Exists());
        return Task.CompletedTask;
    }

    /// <summary>3. Nested options controls resolve inside the options container.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "NestedScoping")]
    public Task ProductOptions_ScopesInsideProductForm()
    {
        Page.ProductForm.Options.AssertExists();
        Page.ProductForm.Options.InStockCheckBox.AssertExists();
        Page.ProductForm.Options.InStockCaption.AssertExists();
        return Task.CompletedTask;
    }

    /// <summary>4. The nested scope cannot reach a sibling from its parent.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "NestedScoping")]
    public Task ProductOptions_DoesNotFindParentControls()
    {
        var addButtonInOptions =
            Page.ProductForm.Options.Button("ProductAddButton");

        Page.ProductForm.AddButton.AssertExists();
        Assert.False(addButtonInOptions.Exists());
        return Task.CompletedTask;
    }

    #endregion

    #region Fluent contract

    /// <summary>5. A child control action returns the owning container.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "FluentReturn")]
    public Task ControlAction_ReturnsContainer_NotPage()
    {
        // One reference throughout: page and container objects are created per access, so two
        // reads of Page.ProductForm are deliberately different instances. What this test is
        // about is that the call hands back the container it was called on.
        var page = Page;
        var form = page.ProductForm;

        ProductFormContainer result = form.NameEntry.SetText("Widget");

        Assert.Same(form, result);
        return Task.CompletedTask;
    }

    /// <summary>6. A container assertion returns the same container instance.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "FluentReturn")]
    public Task ContainerAssertion_ReturnsSameContainer()
    {
        var form = Page.ProductForm;

        ProductFormContainer result = form.AssertVisible(true);

        Assert.Same(form, result);
        return Task.CompletedTask;
    }

    /// <summary>7. Parent exits one scope level at a time.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Property", "Parent")]
    public Task Parent_ExitsOneLevelAtATime()
    {
        var page = Page;
        var form = page.ProductForm;

        Assert.Same(form, form.Options.Parent);
        Assert.Same(page, form.Options.Parent.Parent);
        Assert.Same(page, form.Parent);
        return Task.CompletedTask;
    }

    #endregion

    #region State and interaction

    /// <summary>8. Cache invalidation permits subsequent child resolution.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "InvalidateCache")]
    public Task InvalidateCache_ControlsStillResolve()
    {
        Page.ProductForm.Title.AssertExists();

        Page.ProductForm.InvalidateCache();

        Page.ProductForm.Title.AssertExists();
        return Task.CompletedTask;
    }

    /// <summary>9. Filling and submitting the form takes the logical count from 3 to 4.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "FluentChain")]
    public Task AddProduct_RaisesLogicalCountToFour()
    {
        Page.Products.AssertLogicalCount(ProductCollection.SeedCount);

        Page.ProductForm
                .FillProduct("Chained Widget", "12.34", inStock: true)
                .AddButton.Click()
                .Parent
            .Products
                .AssertLogicalCount(ProductCollection.SeedCount + 1);

        return Task.CompletedTask;
    }

    /// <summary>10. The nested checkbox toggles without leaving its scope.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SetChecked")]
    public Task ProductOptions_CheckBox_TogglesWithinScope()
    {
        var form = Page.ProductForm;

        ProductOptionsContainer options = form.Options.InStockCheckBox.SetChecked(false);
        Assert.Same(form, options.Parent);
        Assert.False(options.InStockCheckBox.IsChecked());

        options.InStockCheckBox.SetChecked(true);
        Assert.True(options.InStockCheckBox.IsChecked());

        return Task.CompletedTask;
    }

    #endregion
}
