using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Record;
#if WINDOWS || MACCATALYST
using CommunityToolkit.Maui.Storage;
#endif

namespace Bruinet.Recorder;

/// <summary>
/// The Recorder's window: every control in a column on the left, the site in a webview beside it. The page holds
/// no recording logic: it hands the webview to the engine's <see cref="Scraper.Record.Recorder"/> and shows what it says.
/// </summary>
public sealed class MainPage : ContentPage
{
    private readonly StartOptions _options;
    private readonly WebViewPage _page = new();
    private readonly Scraper.Record.Recorder _recorder;
    private readonly CancellationTokenSource _closing = new();

    private readonly WebView _view = new() { AutomationId = "webSite" };
    private readonly Button _back = Icon("←", "btnBack", "Back");
    private readonly Button _forward = Icon("→", "btnForward", "Forward");
    private readonly Button _reload = Icon("⟳", "btnReload", "Reload");
    private readonly Button _home = Icon("⌂", "btnHome", "The project's start address");
    private readonly Entry _address = new() { AutomationId = "txtAddress", Placeholder = "https://", Keyboard = Keyboard.Url, ReturnType = ReturnType.Go };
    private readonly Picker _previous = new() { AutomationId = "ddAddress", Title = "Earlier addresses" };
    private readonly Entry _folder = new() { AutomationId = "txtFolder", Placeholder = "A folder with a scraper.config.md" };
    private readonly Button _browse = new() { AutomationId = "btnBrowse", Text = "…", WidthRequest = 44 };
    private readonly Button _record = new() { AutomationId = "btnRecord", Text = "Start recording" };
    private readonly Switch _values = new() { AutomationId = "swValues" };
    private readonly Label _state = new() { AutomationId = "lblState", FontAttributes = FontAttributes.Bold };
    private readonly Label _pages = new() { AutomationId = "lblPages" };
    private readonly Label _problem = new() { AutomationId = "lblProblem", TextColor = Colors.Firebrick };
    private readonly Label _help = new() { AutomationId = "txtPrompt" };
    private readonly Button _copy = new() { AutomationId = "btnCopy", Text = "Copy" };

    private RecorderSettings _settings = new();
    private string? _projectFolder;
    private string _current = "";
    private int _waiting;
    private bool _told;
    private bool _filling;

    /// <summary>The window's page, opened with what the app was started with.</summary>
    public MainPage(StartOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _recorder = new Scraper.Record.Recorder(_page);
        Content = Layout();

        _back.Clicked += (_, _) => _view.GoBack();
        _forward.Clicked += (_, _) => _view.GoForward();
        _reload.Clicked += (_, _) => _view.Reload();
        _home.Clicked += (_, _) => Run(() => GoAsync(StartAddress(), byHand: false));
        _address.Completed += (_, _) => Run(() => GoAsync(_address.Text, byHand: true));
        _previous.SelectedIndexChanged += (_, _) => PickedEarlier();
        _folder.Completed += (_, _) => OpenFolder(_folder.Text);
        _folder.Unfocused += (_, _) => OpenFolder(_folder.Text);
        _browse.Clicked += (_, _) => Run(BrowseAsync);
        _record.Clicked += (_, _) => Run(ToggleRecordingAsync);
        _values.Toggled += (_, e) => Run(() => SetValuesAsync(e.Value));
        _copy.Clicked += (_, _) => Run(() => Clipboard.Default.SetTextAsync(_help.Text));

        _view.Navigated += (_, e) => At(e.Url);
        _page.AddressChanged += (_, address) => MainThread.BeginInvokeOnMainThread(() => At(address));
        _recorder.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
        _view.HandlerChanged += (_, _) => Run(AttachAsync);
        Unloaded += (_, _) => Close();
    }

    private Grid Layout()
    {
        var navigation = new HorizontalStackLayout { Spacing = 4, Children = { _back, _forward, _reload, _home } };
        var folder = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 4 };
        folder.Add(_folder, 0);
        folder.Add(_browse, 1);
        var values = new HorizontalStackLayout { Spacing = 8, Children = { _values, new Label { Text = "Record values", VerticalTextAlignment = TextAlignment.Center } } };
        var column = new VerticalStackLayout
        {
            Padding = 12,
            Spacing = 10,
            Children =
            {
                navigation,
                new Label { Text = "Address" }, _address, _previous,
                new Label { Text = "Save in" }, folder,
                new BoxView { HeightRequest = 1, Color = Colors.LightGray },
                _record, values, _state, _pages, _problem,
                new BoxView { HeightRequest = 1, Color = Colors.LightGray },
                new Label { Text = "What to tell the AI", FontAttributes = FontAttributes.Bold, AutomationId = "lblHelp" }, _help, _copy,
            },
        };
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(300), new ColumnDefinition(GridLength.Star) } };
        grid.Add(new ScrollView { Content = column }, 0);
        grid.Add(_view, 1);
        return grid;
    }

    private static Button Icon(string glyph, string id, string says)
    {
        var button = new Button { Text = glyph, AutomationId = id, WidthRequest = 44, FontSize = 18, Padding = 0 };
        SemanticProperties.SetDescription(button, says);
        ToolTipProperties.SetText(button, says);
        return button;
    }

    // The webview exists: the watcher goes in before the first page, then the folder and the address the app was
    // started with open, and the line to the chat is there.
    private async Task AttachAsync()
    {
        if (_view.Handler is null)
            return;
        await _page.AttachAsync(_view).ConfigureAwait(true);
        OpenFolder(_options.Folder);
        await GoAsync(_options.Url ?? _settings.Address ?? StartAddress(), byHand: _options.Url is not null).ConfigureAwait(true);
        Refresh();
        _ = RecorderLink.ServeAsync(AnswerAsync, _closing.Token);
    }

    private void OpenFolder(string? folder)
    {
        folder = string.IsNullOrWhiteSpace(folder) ? null : folder.Trim();
        if (_recorder.Recording || string.Equals(folder, _projectFolder, StringComparison.OrdinalIgnoreCase))
            return;
        var isProject = folder is not null && File.Exists(Path.Combine(folder, ScraperConfig.FileName));
        _projectFolder = isProject ? folder : null;
        _settings = isProject ? RecorderSettings.Load(folder!) : new RecorderSettings();
        _folder.Text = folder ?? "";
        _filling = true;
        _values.IsToggled = _settings.RecordValues;
        _filling = false;
        ShowEarlier();
        Refresh();
    }

    private void ShowEarlier()
    {
        _filling = true;
        _previous.ItemsSource = _settings.Addresses.ToList();
        _previous.SelectedIndex = -1;
        _filling = false;
    }

    private void PickedEarlier()
    {
        if (_filling || _previous.SelectedItem is not string address)
            return;
        Run(() => GoAsync(address, byHand: true));
    }

    // An address opened by hand (typed, picked, passed in) is remembered in the project; one a click led to is not.
    private Task GoAsync(string? address, bool byHand)
    {
        if (string.IsNullOrWhiteSpace(address))
            return Task.CompletedTask;
        address = address.Trim();
        if (!address.Contains("://", StringComparison.Ordinal))
            address = "https://" + address;
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            _problem.Text = $"'{address}' is no address of a page.";
            return Task.CompletedTask;
        }

        _view.Source = new UrlWebViewSource { Url = uri.AbsoluteUri };
        At(uri.AbsoluteUri);
        if (byHand && _projectFolder is not null)
        {
            _settings = _settings.Opened(uri.AbsoluteUri);
            _settings.Save(_projectFolder);
            ShowEarlier();
        }

        return Task.CompletedTask;
    }

    private void At(string? address)
    {
        if (string.IsNullOrEmpty(address) || address == _current)
            return;
        _current = address;
        if (!_address.IsFocused)
            _address.Text = address;
        Refresh();
    }

    private string? StartAddress()
    {
        if (_projectFolder is null)
            return null;
        try
        {
            return ScrapeProject.Open(_projectFolder).Config.StartUrl;
        }
        catch (FormatException)
        {
            return null; // the config cannot be read: Start says so
        }
    }

    private async Task BrowseAsync()
    {
#if WINDOWS || MACCATALYST
        var picked = await FolderPicker.Default.PickAsync(_closing.Token).ConfigureAwait(true);
        if (picked.IsSuccessful)
            OpenFolder(picked.Folder.Path);
#else
        await Task.CompletedTask.ConfigureAwait(true);
#endif
    }

    private async Task ToggleRecordingAsync()
    {
        if (_recorder.Recording)
        {
            await _recorder.StopAsync().ConfigureAwait(true);
        }
        else if (Scraper.Record.Recorder.Refusal(_projectFolder, _current) is null)
        {
            _told = false;
            await _recorder.StartAsync(_projectFolder!, _current).ConfigureAwait(true);
        }

        Refresh();
    }

    private async Task SetValuesAsync(bool values)
    {
        await _recorder.SetRecordValuesAsync(values).ConfigureAwait(true);
        if (_filling || _projectFolder is null)
            return;
        _settings = _settings with { RecordValues = values };
        _settings.Save(_projectFolder);
    }

    private void Refresh()
    {
        var recording = _recorder.Recording;
        var refusal = recording ? null : Scraper.Record.Recorder.Refusal(_projectFolder, _current);
        _record.Text = recording ? "Stop recording" : "Start recording";
        _record.IsEnabled = recording || refusal is null;
        _folder.IsEnabled = _browse.IsEnabled = !recording;
        _state.Text = !recording ? "Not recording" : _recorder.RecordValues ? "Recording, with values" : "Recording";
        _state.TextColor = recording && _recorder.RecordValues ? Colors.OrangeRed : null!;
        _pages.Text = $"Pages: {_recorder.Pages}   Presses: {_recorder.Presses}";
        _problem.Text = recording ? _recorder.Problem ?? "" : refusal ?? "";
        _back.IsEnabled = _view.CanGoBack;
        _forward.IsEnabled = _view.CanGoForward;
        _home.IsEnabled = _projectFolder is not null;
        var help = RecorderHelp.For(_projectFolder, _current, recording, _recorder.Session?.Id, _recorder.Pages, chatWaits: _waiting > 0 || _told);
        _help.Text = help.Text;
        _copy.IsVisible = help.ForTheChat;
    }

    private void Close()
    {
        if (_projectFolder is not null && _current.Length > 0)
            (_settings with { Address = _current }).Save(_projectFolder);
        _closing.Cancel();
    }

    // What the chat asks over the pipe. Everything but waiting touches the window, so it runs where the window lives.
    private async Task<RecorderStatus> AnswerAsync(RecorderRequest request, CancellationToken cancellationToken)
    {
        if (request.Do != "wait")
            return await MainThread.InvokeOnMainThreadAsync(() => DoAsync(request)).ConfigureAwait(false);

        Interlocked.Increment(ref _waiting);
        MainThread.BeginInvokeOnMainThread(Refresh);
        var ended = false;
        try
        {
            ended = await _recorder.WaitEndedAsync(TimeSpan.FromMinutes(Math.Clamp(request.Minutes, 0.01, 60)), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _waiting);
            _told = ended;
            MainThread.BeginInvokeOnMainThread(Refresh);
        }

        return await MainThread.InvokeOnMainThreadAsync(() => Status(ended)).ConfigureAwait(false);
    }

    private async Task<RecorderStatus> DoAsync(RecorderRequest request)
    {
        string? problem = null;
        switch (request.Do)
        {
            case "status":
                break;
            case "open":
                if (request.Folder is { Length: > 0 } folder)
                {
                    if (_recorder.Recording && !string.Equals(folder, _projectFolder, StringComparison.OrdinalIgnoreCase))
                        problem = "A recording is on: stop it before another folder is opened.";
                    else
                        OpenFolder(folder);
                }

                if (problem is null && request.Url is { Length: > 0 })
                    await GoAsync(request.Url, byHand: true).ConfigureAwait(true);
                break;
            case "start":
                problem = _recorder.Recording ? "A recording is on already." : Scraper.Record.Recorder.Refusal(_projectFolder, _current);
                if (problem is null)
                    await ToggleRecordingAsync().ConfigureAwait(true);
                break;
            case "stop":
                if (_recorder.Recording)
                    await ToggleRecordingAsync().ConfigureAwait(true);
                break;
            default:
                problem = $"The Recorder does not know '{request.Do}': status, open, start, stop or wait.";
                break;
        }

        return Status(ended: false) with { Problem = problem ?? _recorder.Problem };
    }

    private RecorderStatus Status(bool ended) =>
        new(_recorder.Recording, _projectFolder, _current, _recorder.Session?.Id, _recorder.Pages, _recorder.Presses, _recorder.RecordValues, ended, _recorder.Problem);

    // A handler's work: what goes wrong in it is shown in the column, not thrown at the window.
    private void Run(Func<Task> work) => _ = RunAsync(work);

    private async Task RunAsync(Func<Task> work)
    {
        try
        {
            await work().ConfigureAwait(true);
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException or FormatException)
        {
            _problem.Text = e.Message;
        }
    }
}
