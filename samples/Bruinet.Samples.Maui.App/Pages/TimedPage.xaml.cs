namespace Bruinet.Samples.Maui.App;

public partial class TimedPage : ContentPage
{
    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(300);

    private readonly IDispatcherTimer _debounce;
    private int _searches;

    public TimedPage()
    {
        InitializeComponent();
        _debounce = Dispatcher.CreateTimer();
        _debounce.Interval = Pause;
        _debounce.IsRepeating = false;
        _debounce.Tick += (_, _) => SearchStatus.Text = $"searched {++_searches}: {Search.Text}";
    }

    // An awaited delay: a runtime timer, then the rest on the UI thread.
    private async void OnSave(object? sender, EventArgs e)
    {
        SaveStatus.Text = "saving";
        await Task.Delay(Pause);
        SaveStatus.Text = "saved";
    }

    // Work the app dispatches with a delay.
    private void OnSaveLater(object? sender, EventArgs e)
    {
        SaveLaterStatus.Text = "saving";
        Dispatcher.DispatchDelayed(Pause, () => SaveLaterStatus.Text = "saved");
    }

    // A search once typing pauses: each change starts the timer again.
    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    // A message that hides itself a second after it shows.
    private void OnShowMessage(object? sender, EventArgs e)
    {
        Message.IsVisible = true;
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1), () => Message.IsVisible = false);
    }
}
