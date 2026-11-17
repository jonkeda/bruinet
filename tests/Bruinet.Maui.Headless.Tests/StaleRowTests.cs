using System.Collections.ObjectModel;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using MauiHeadless;

namespace Bruinet.Maui.Headless.Tests;

/// <summary>
/// A list row whose item left the source goes stale, as a device's recycled row does: a container
/// that cached it (a row asked to be gone) must not keep finding it.
/// </summary>
public sealed class StaleRowTests : IDisposable
{
    private readonly MauiTestContext _ctx = new();
    private readonly ObservableCollection<string> _items = ["Kept", "Leaving"];
    private readonly MauiHeadlessDriver _driver;

    public StaleRowTests()
    {
        var list = new CollectionView
        {
            AutomationId = "Rows",
            ItemsSource = _items,
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label();
                label.SetBinding(Label.TextProperty, ".");
                label.SetBinding(Element.AutomationIdProperty, ".");
                return label;
            }),
        };
        _driver = new MauiHeadlessDriver(_ctx.Render(new ContentPage { Content = list }));
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void A_row_whose_item_left_the_source_is_stale()
    {
        var row = _driver.FindElements(Locator.ByAutomationId("Leaving"))[0];
        _ = row.InstanceKey;

        _driver.TestContext.Dispatcher.Run(() => _items.Remove("Leaving"));

        Assert.Throws<StaleElementException>(() => row.InstanceKey);
    }

    [Fact]
    public void A_row_whose_item_stays_keeps_its_identity()
    {
        var row = _driver.FindElements(Locator.ByAutomationId("Kept"))[0];
        var key = row.InstanceKey;

        _driver.TestContext.Dispatcher.Run(() => _items.Remove("Leaving"));

        Assert.Equal(key, row.InstanceKey);
    }

    [Fact]
    public void A_row_of_a_replaced_source_is_stale()
    {
        var list = (CollectionView)((ContentPage)_driver.Window.Page!).Content;
        var row = _driver.FindElements(Locator.ByAutomationId("Leaving"))[0];

        _driver.TestContext.Dispatcher.Run(() => list.ItemsSource = new ObservableCollection<string> { "Kept" });

        Assert.Throws<StaleElementException>(() => row.InstanceKey);
    }
}
