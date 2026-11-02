using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Bruinet.Samples.Maui.App.ViewModels;

/// <summary>
/// ViewModel for the paged list: a list that loads its first page after the page appears and the
/// next one when the list nears its end, with a search that replaces the list's source.
/// </summary>
/// <remarks>
/// A page is fetched off the UI thread and added on it, as an app's call to a backend is: the
/// list is empty, then has rows, without the test waiting on anything but the rows.
/// </remarks>
public class PagedListViewModel : ParentViewModel
{
    /// <summary>The rows in a page.</summary>
    public const int PageSize = 20;

    /// <summary>The rows the source has in all.</summary>
    public const int Total = 60;

    private readonly IDispatcher _dispatcher;
    private readonly List<string> _loaded = [];
    private ObservableCollection<string> _rows = [];
    private string _searchText = string.Empty;
    private bool _isLoading;
    private bool _askedWhileLoading;

    public PagedListViewModel(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        LoadMoreCommand = new RelayCommand(() => _ = LoadMoreAsync());
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
    }

    /// <summary>The rows shown: every loaded row, or those matching the search.</summary>
    public ObservableCollection<string> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    /// <summary>The header's rows, which are not items.</summary>
    public IReadOnlyList<string> Recent { get; } = ["Recent Alpha", "Recent Bravo"];

    /// <summary>Whether a page is being fetched.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>How many rows are loaded, of how many.</summary>
    public string Status => $"{_loaded.Count} of {Total}";

    /// <summary>The search; a change replaces <see cref="Rows"/>, and an empty one shows every loaded row.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
                Rows = new ObservableCollection<string>(_loaded.Where(row => row.Contains(_searchText, StringComparison.OrdinalIgnoreCase)));
        }
    }

    /// <summary>The next page, as the list's <c>RemainingItemsThresholdReachedCommand</c>.</summary>
    public ICommand LoadMoreCommand { get; }

    /// <summary>Empties the search.</summary>
    public ICommand ClearSearchCommand { get; }

    /// <summary>
    /// Fetches the next page and adds it; nothing while a search is on or all are loaded. Asked while a
    /// page loads, it loads the next one after: a list at its end asks only once, as it is scrolled
    /// there, and does not ask again until it is scrolled.
    /// </summary>
    public async Task LoadMoreAsync()
    {
        if (_searchText.Length > 0 || _loaded.Count >= Total)
            return;

        if (IsLoading)
        {
            _askedWhileLoading = true;
            return;
        }

        IsLoading = true;
        var first = _loaded.Count + 1;
        var page = await Task.Run(() => Enumerable.Range(first, PageSize).Select(i => $"Item {i}").ToList());
        await _dispatcher.DispatchAsync(() =>
        {
            _loaded.AddRange(page);
            foreach (var row in page)
                Rows.Add(row);
            IsLoading = false;
            OnPropertyChanged(nameof(Status));
        });

        if (_askedWhileLoading)
        {
            _askedWhileLoading = false;
            await LoadMoreAsync();
        }
    }
}
