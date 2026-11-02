using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using Bruinet.Samples.Shared.Commands;
using Bruinet.Samples.Shared.ViewModels;
using Bruinet.Samples.Todo.Core.Rules;
using Bruinet.Samples.Todo.Core.Services;
using Bruinet.Samples.Todo.Core.Sync;
using System.Globalization;

namespace Bruinet.Samples.Todo.Core.ViewModels;

/// <summary>How the app behaves at startup.</summary>
/// <param name="SyncOnStart">Whether the list syncs the first time it appears.</param>
public sealed record TodoAppOptions(bool SyncOnStart = true);

/// <summary>
/// The list page: rows, filter, sync, and the Loading / Empty / Error / Content state.
/// </summary>
public sealed partial class TodoListViewModel : ParentViewModel
{
    private readonly TodoService _todos;
    private readonly ISyncService _sync;
    private readonly IConnectivityState _connectivity;
    private readonly INavigator _navigator;
    private readonly TodoAppOptions _options;

    private int _filterIndex;
    private bool _isLoading = true;
    private bool _hasAppeared;
    private SyncError _lastSyncError;
    private ListState _state = ListState.Loading;
    private string _syncLine = string.Empty;
    private bool _isOnline;
    private TodoRowViewModel? _selectedItem;
    private readonly ILogger<TodoListViewModel> _logger;

    /// <summary>Creates the view model.</summary>
    public TodoListViewModel(
        TodoService todos,
        ISyncService sync,
        IConnectivityState connectivity,
        INavigator navigator,
        TodoAppOptions options,
        ILogger<TodoListViewModel>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TodoListViewModel>.Instance;
        _todos = todos;
        _sync = sync;
        _connectivity = connectivity;
        _navigator = navigator;
        _options = options;

        AddCommand = new TargetAsyncCommand(this, () => _navigator.GoToAsync(TodoRoutes.Edit));
        UsersCommand = new TargetAsyncCommand(this, () => _navigator.GoToAsync(UserRoutes.List));
        OpenCommand = new TargetAsyncCommand<TodoRowViewModel>(this, OpenAsync);
        SyncCommand = new TargetAsyncCommand(this, SyncAsync, () => IsOnline);
        RetryCommand = new TargetAsyncCommand(this, SyncAsync);

        _isOnline = _connectivity.IsOnline;
        _connectivity.Changed += OnConnectivityChanged;
        UpdateSyncLine();
    }

    /// <summary>The rows, filtered and ordered.</summary>
    public ObservableCollection<TodoRowViewModel> Items { get; } = [];

    /// <summary>The filter picker's options.</summary>
    public IReadOnlyList<string> FilterNames => TodoListRules.FilterNames;

    /// <summary>The picker's selected index; changing it reloads the rows.</summary>
    public int FilterIndex
    {
        get => _filterIndex;
        set
        {
            if (value < 0 || !SetProperty(ref _filterIndex, value))
            {
                return;
            }

            Reloading = ReloadAsync();
        }
    }

    /// <summary>The filter the picker stands for.</summary>
    public TodoFilter Filter => (TodoFilter)_filterIndex;

    /// <summary>What the page shows.</summary>
    public ListState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(StateKey));
            }
        }
    }

    /// <summary>
    /// The StateContainer key for <see cref="State"/>; <c>null</c> for the rows, which are the
    /// container's default content.
    /// </summary>
    public string? StateKey => State == ListState.Content ? null : State.ToString();

    /// <summary>The Error state's message.</summary>
    public string ErrorMessage => TodoListRules.ErrorMessage(_lastSyncError);

    /// <summary>"Synced 09:41", "Offline", "Server unreachable", ...</summary>
    public string SyncLine
    {
        get => _syncLine;
        private set => SetProperty(ref _syncLine, value);
    }

    /// <summary>Whether the app is online; Sync is disabled when it is not.</summary>
    public bool IsOnline
    {
        get => _isOnline;
        private set
        {
            if (SetProperty(ref _isOnline, value))
            {
                SyncCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// The row the list has selected. Selecting a row opens it, and the selection is cleared so
    /// the same row can be opened again after coming back.
    /// </summary>
    public TodoRowViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (!SetProperty(ref _selectedItem, value) || value is null)
            {
                return;
            }

            _ = OpenCommand.ExecuteAsync(value);
            SetProperty(ref _selectedItem, null);
        }
    }

    /// <summary>Opens a new, empty edit page.</summary>
    public IAsyncRelayCommand AddCommand { get; }

    /// <summary>Opens the users list.</summary>
    public IAsyncRelayCommand UsersCommand { get; }

    /// <summary>Opens a row's detail page.</summary>
    public IAsyncRelayCommand<TodoRowViewModel> OpenCommand { get; }

    /// <summary>Syncs with the server. Disabled while offline, and while a sync runs.</summary>
    public IAsyncRelayCommand SyncCommand { get; }

    /// <summary>The Error state's Retry: another sync.</summary>
    public IAsyncRelayCommand RetryCommand { get; }

    /// <summary>The last reload started by the filter or by the page appearing. For tests.</summary>
    public Task Reloading { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Reloads the rows each time the page appears (so an edit shows on return) and, the first
    /// time only, syncs if the app is configured to.
    /// </summary>
    public override void OnViewAppearing()
    {
        base.OnViewAppearing();
        Reloading = AppearAsync();
    }

    /// <summary>
    /// Starts over as a fresh launch does, on the database as it is now: no sync error, the filter
    /// on All, then the first appearance (the rows reloaded, and a sync if the app syncs on start).
    /// A test's reset without a restart; <see cref="ISyncService.Forget"/> goes with it.
    /// </summary>
    public Task StartOverAsync()
    {
        _lastSyncError = default;
        OnPropertyChanged(nameof(ErrorMessage));
        _isLoading = true;
        _hasAppeared = false;
        if (_filterIndex != 0)
        {
            _filterIndex = 0;
            OnPropertyChanged(nameof(FilterIndex));
            OnPropertyChanged(nameof(Filter));
        }

        UpdateSyncLine();
        Reloading = AppearAsync();
        return Reloading;
    }

    /// <summary>What <see cref="OnViewAppearing"/> starts, awaitable for tests.</summary>
    public async Task AppearAsync()
    {
        var first = !_hasAppeared;
        _hasAppeared = true;

        await ReloadAsync();

        if (first && _options.SyncOnStart && IsOnline)
        {
            await SyncCommand.ExecuteAsync(null);
            return;
        }

        _isLoading = false;
        UpdateState();
    }

    private async Task SyncAsync()
    {
        _isLoading = true;
        UpdateState();

        try
        {
            var result = await _sync.SyncAsync();
            _lastSyncError = result.Error;
            if (!result.Succeeded)
            {
                LogSyncFailed(_logger, result.Error);
            }

            OnPropertyChanged(nameof(ErrorMessage));
            UpdateSyncLine();
            await ReloadAsync();
        }
        finally
        {
            _isLoading = false;
            UpdateState();
        }
    }

    private async Task ReloadAsync()
    {
        var today = _todos.Today;
        var visible = TodoListRules.Arrange(await _todos.GetVisibleAsync(), Filter);

        Items.Clear();
        foreach (var item in visible)
        {
            Items.Add(new TodoRowViewModel(item, today));
        }

        UpdateState();
    }

    private Task OpenAsync(TodoRowViewModel? row)
        => row is null
            ? Task.CompletedTask
            : _navigator.GoToAsync(TodoRoutes.Detail, TodoRoutes.ForTodo(row.Id));

    private void OnConnectivityChanged(object? sender, EventArgs e)
    {
        IsOnline = _connectivity.IsOnline;
        UpdateSyncLine();
    }

    private void UpdateSyncLine() => SyncLine = TodoFormatting.SyncLine(IsOnline, _sync.LastResult, culture: CultureInfo.CurrentCulture);

    private void UpdateState() => State = TodoListRules.StateFor(_isLoading, Items.Count, _lastSyncError);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sync failed: {Error}")]
    private static partial void LogSyncFailed(ILogger logger, SyncError error);
}
