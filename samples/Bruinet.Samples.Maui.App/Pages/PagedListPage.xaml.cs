using Bruinet.Samples.Maui.App.ViewModels;

namespace Bruinet.Samples.Maui.App;

public partial class PagedListPage : ContentPage
{
    private readonly PagedListViewModel _viewModel;

    public PagedListPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = new PagedListViewModel(Dispatcher);
    }

    // The first page after the page appears, as an app's list loads on its page's appearing.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.LoadMoreAsync();
    }
}
