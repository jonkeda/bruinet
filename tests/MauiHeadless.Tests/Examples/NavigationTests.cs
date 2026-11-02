using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using MauiHeadless.Assertions;
using MauiHeadless.Interaction;
using MauiHeadless.Samples.App;
using MauiHeadless.Samples.App.Pages;
using MauiHeadless.Samples.App.Services;
using Xunit;

namespace MauiHeadless.Tests.Examples;

/// <summary>6. Navigation: MAUI's own NavigationPage and Shell, driven by the app's handlers.</summary>
public sealed class NavigationTests
{
    private static readonly Product Water = new("w", "Water", 2.5m);
    private static readonly Product Rope = new("r", "Rope", 7m);

    [Fact]
    public void Choosing_a_product_pushes_its_page_and_back_returns()
    {
        using var ctx = new MauiTestContext();
        ctx.Services.AddSingleton<IProductCatalog>(new FakeCatalog(Water, Rope));
        var shop = ctx.Render<ProductsPage>(o => o.WithNavigation());

        shop.Find<Label>("Summary").Should().HaveText("2 products");
        shop.Find<CollectionView>("Products").Select(Rope);

        var product = shop.Navigation.ShouldBeOn<ProductPage>();
        Assert.Same(Rope, product.Product);
        shop.Find<Label>("Title").Should().HaveText("Rope");
        Assert.Equal(2, shop.Navigation.Stack.Count);

        shop.Navigation.GoBack();

        shop.Navigation.ShouldBeOn<ProductsPage>();
    }

    [Fact]
    public void Back_raises_the_navigation_pages_Popped_as_a_device_does()
    {
        using var ctx = new MauiTestContext();
        ctx.Services.AddSingleton<IProductCatalog>(new FakeCatalog(Water, Rope));
        var shop = ctx.Render<ProductsPage>(o => o.WithNavigation());
        var navigation = (NavigationPage)shop.Instance.Parent;
        Page? popped = null;
        navigation.Popped += (_, e) => popped = e.Page;
        shop.Find<CollectionView>("Products").Select(Rope);
        var product = shop.Navigation.ShouldBeOn<ProductPage>();

        shop.Navigation.GoBack();

        Assert.Same(product, popped);
    }

    [Fact]
    public void Rows_are_realized_from_the_item_template()
    {
        using var ctx = new MauiTestContext();
        ctx.Services.AddSingleton<IProductCatalog>(new FakeCatalog(Water, Rope));
        var shop = ctx.Render<ProductsPage>(o => o.WithNavigation());

        var names = shop.FindAll<Label>("ProductName").Select(label => label.Text);
        var prices = shop.FindAll<Label>("ProductPrice").Select(label => label.Text);

        Assert.Equal(["Water", "Rope"], names);
        Assert.Equal(["2.50", "7.00"], prices);
    }

    [Fact]
    public void The_toolbar_reloads_and_an_empty_list_shows_its_empty_view()
    {
        using var ctx = new MauiTestContext();
        var catalog = new FakeCatalog(Water);
        ctx.Services.AddSingleton<IProductCatalog>(catalog);
        var shop = ctx.Render<ProductsPage>(o => o.WithNavigation());
        catalog.Products.Clear();

        shop.Find<ToolbarItem>("Reload").Click();

        shop.Find<Label>("Summary").Should().HaveText("0 products");
        shop.FindByText<Label>("Nothing for sale").Should().BeVisible();
        Assert.Equal(2, catalog.Calls);
    }

    [Fact]
    public void Shell_routes_through_the_apps_own_GoToAsync()
    {
        using var ctx = new MauiTestContext();
        var app = ctx.Render<AppShell>();
        app.Find<Label>("Welcome").Should().HaveText("Welcome home");

        app.Find<Button>("Help").Click();

        app.Navigation.ShouldBeOn<HelpPage>();
        Assert.Equal("//home/help", app.Navigation.Location);
        app.Navigation.GoBack();
        app.Navigation.ShouldBeOn<HomePage>();

        app.Navigation.GoTo("//about");
        app.Find<Label>("Version").Should().HaveText("Version 1.0");
    }
}
