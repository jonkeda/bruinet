using System.Collections;
using System.Windows.Input;

namespace Bruinet.Samples.Maui.App.Controls;

/// <summary>
/// A list control as a component library writes one: its <see cref="RefreshView"/> private, two levels
/// down (a Grid, a Border), with only the items, the refresh command and the spinner bindable. A test
/// pulls the control itself, as a finger pulls the list; Bruinet finds the RefreshView inside.
/// </summary>
public class RefreshingList : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(RefreshingList));

    public static readonly BindableProperty RefreshCommandProperty =
        BindableProperty.Create(nameof(RefreshCommand), typeof(ICommand), typeof(RefreshingList));

    public static readonly BindableProperty IsRefreshingProperty =
        BindableProperty.Create(nameof(IsRefreshing), typeof(bool), typeof(RefreshingList), false, BindingMode.TwoWay);

    public RefreshingList()
    {
        var list = new CollectionView
        {
            ItemTemplate = new DataTemplate(() =>
            {
                var row = new Label();
                row.SetBinding(Label.TextProperty, new Binding("."));
                return row;
            }),
        };
        list.SetBinding(ItemsView.ItemsSourceProperty, new Binding(nameof(ItemsSource), source: this));

        var refresh = new RefreshView { Content = list };
        refresh.SetBinding(RefreshView.CommandProperty, new Binding(nameof(RefreshCommand), source: this));
        refresh.SetBinding(RefreshView.IsRefreshingProperty, new Binding(nameof(IsRefreshing), BindingMode.TwoWay, source: this));

        Content = new Grid { Children = { new Border { Content = refresh } } };
    }

    /// <summary>The rows.</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>What a pull runs.</summary>
    public ICommand? RefreshCommand
    {
        get => (ICommand?)GetValue(RefreshCommandProperty);
        set => SetValue(RefreshCommandProperty, value);
    }

    /// <summary>Whether the spinner shows.</summary>
    public bool IsRefreshing
    {
        get => (bool)GetValue(IsRefreshingProperty);
        set => SetValue(IsRefreshingProperty, value);
    }
}
