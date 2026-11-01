using System.Windows.Input;

namespace Bruinet.Samples.Maui.App.ViewModels;

/// <summary>
/// ViewModel for the container module test view.
/// </summary>
/// <remarks>
/// Container actions record what fired into <see cref="Status"/> so tests assert on
/// observed text rather than on layout, which keeps them free of fixed delays.
/// </remarks>
public class ContainerViewModel : ParentViewModel
{
    private const string NoAction = "none";
    private const string NotRefreshed = "not refreshed";

    private string _status = NoAction;
    private bool _isRefreshing;
    private bool _isWrappedRefreshing;
    private string _refreshText = NotRefreshed;
    private int _refreshCount;

    public ContainerViewModel()
    {
        RecordCommand = new RelayCommand<string>(Record);
        RefreshCommand = new RelayCommand(Refresh);
        ResetCommand = new RelayCommand(Reset);
    }

    /// <summary>The most recent container action, or "none".</summary>
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>
    /// Whether the RefreshView is showing its spinner.
    /// </summary>
    /// <remarks>
    /// Two-way bound so a pull-to-refresh gesture sets it on mobile; the refresh command
    /// clears it either way.
    /// </remarks>
    public bool IsRefreshing
    {
        get => _isRefreshing;

        // Stores the value and nothing else. RefreshView already runs its Command whenever
        // IsRefreshing becomes true - whether from a real pull, from the automation bridge, or
        // from code - so refreshing here as well counted every refresh twice.
        //
        // That went unnoticed because no test could reach the gesture: the RefreshView is not
        // addressable on Windows, and the only route to a refresh was TriggerRefreshButton,
        // which runs the command directly and so fired once. The first gesture that reached this
        // control reported "refreshed 2" from a single pull.
        set => SetProperty(ref _isRefreshing, value);
    }

    /// <summary>The wrapped RefreshView's spinner; its own, so one pull does not start both views.</summary>
    public bool IsWrappedRefreshing
    {
        get => _isWrappedRefreshing;
        set => SetProperty(ref _isWrappedRefreshing, value);
    }

    /// <summary>Text reflecting how many refreshes have completed.</summary>
    public string RefreshText
    {
        get => _refreshText;
        private set => SetProperty(ref _refreshText, value);
    }

    /// <summary>Records a container action.</summary>
    public ICommand RecordCommand { get; }

    /// <summary>Completes a refresh, whether started by gesture or by button.</summary>
    public ICommand RefreshCommand { get; }

    /// <summary>Restores the initial state.</summary>
    public ICommand ResetCommand { get; }

    private void Record(string? action)
    {
        if (string.IsNullOrEmpty(action))
            return;

        Status = action;
    }

    private void Refresh()
    {
        _refreshCount++;
        RefreshText = $"refreshed {_refreshCount}";
        Status = "Refresh";

        // Clear the spinner immediately: there is no real work, and leaving it spinning
        // would make tests wait on a state that never changes.
        if (_isRefreshing)
        {
            SetProperty(ref _isRefreshing, false, nameof(IsRefreshing));
        }

        if (_isWrappedRefreshing)
        {
            SetProperty(ref _isWrappedRefreshing, false, nameof(IsWrappedRefreshing));
        }
    }

    private void Reset()
    {
        Status = NoAction;
        RefreshText = NotRefreshed;
        _refreshCount = 0;

        if (_isRefreshing)
        {
            SetProperty(ref _isRefreshing, false, nameof(IsRefreshing));
        }

        if (_isWrappedRefreshing)
        {
            SetProperty(ref _isWrappedRefreshing, false, nameof(IsWrappedRefreshing));
        }
    }
}
