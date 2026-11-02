namespace Bruinet.Samples.Maui.App;

public partial class CoveredPage : ContentPage
{
    private bool _loaded;

    public CoveredPage()
    {
        InitializeComponent();
    }

    // Busy until the first load ends: a fetch off the UI thread, as an app's backend call is.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
            return;

        _loaded = true;
        var status = await Task.Run(() => "loaded");
        StatusLabel.Text = status;
        BusySignal.Text = "False";
    }

    private async void OnPushDetail(object? sender, EventArgs e)
        => await Navigation.PushAsync(Covering("CoveredDetailPage", "Detail", page => page.Navigation.PopAsync()));

    private async void OnOpenModal(object? sender, EventArgs e)
        => await Navigation.PushModalAsync(Covering("CoveredModalPage", "Modal", page => page.Navigation.PopModalAsync()));

    // A page over this one, with a button that closes it the way it was opened.
    private static ContentPage Covering(string automationId, string title, Func<Page, Task> close)
    {
        var page = new ContentPage { AutomationId = automationId, Title = title };
        var closeButton = new Button { AutomationId = $"{automationId}_Close", Text = "Close" };
        closeButton.Clicked += async (_, _) => await close(page);
        page.Content = new VerticalStackLayout
        {
            Padding = 20,
            Children = { new Label { AutomationId = $"{automationId}_Title", Text = title }, closeButton },
        };
        return page;
    }
}
