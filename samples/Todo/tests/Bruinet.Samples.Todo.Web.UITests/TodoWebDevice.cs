using Bruinet.Core.Configuration;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Services;
using Bruinet.Core.Testing;
using System.Diagnostics;
using Bruinet.Mocking;
using Bruinet.Samples.Todo.Infrastructure;
using Bruinet.Samples.Todo.Infrastructure.Data;
using Bruinet.Samples.Todo.Web.UITests.Pages;
using Bruinet.Samples.Todo.Web.UITests.Spike;

namespace Bruinet.Samples.Todo.Web.UITests;

/// <summary>
/// One device running the Todo web head: its own seeded SQLite file, network-state file, and
/// app on this run's head (<see cref="ITodoWebHead"/>: a host process and browser context, or bUnit). Devices share a backend; the fixture that owns the backend owns
/// the devices (<see cref="TodoWebFixture"/>).
/// </summary>
/// <remarks>
/// A restart stops the app and starts it again with the same database. In the browser the host
/// gets a new port and the browser context stays, so page objects built on <see cref="Context"/>
/// keep working; on bUnit the context is new, so page objects are made again from it.
/// </remarks>
public sealed class TodoWebDevice : ITestApp
{
    private readonly List<TempDatabase> _retired = [];
    private readonly ITodoWebHead _head;
    private TempDatabase _database;
    private readonly NetworkStateFile _network;
    private LaunchSettings _settings;
    private LaunchSettings? _unchangedSettings;
    private IScreenshotService? _screenshots;

    private TodoWebDevice(TestAppDefinition definition, string name, TempDatabase database, NetworkStateFile network, LaunchSettings settings, ITodoWebHead head)
    {
        Definition = definition;
        Name = name;
        _database = database;
        _network = network;
        _settings = settings;
        _head = head;
    }

    /// <summary>The device's name: its browser context's, for the call log.</summary>
    public string Name { get; }

    /// <inheritdoc />
    public TestAppDefinition Definition { get; }

    /// <inheritdoc />
    /// <remarks>Into a folder named after the app, beside the run's other screenshots.</remarks>
    public IScreenshotService Screenshots => _screenshots ??= new ScreenshotService(
        Context,
        Context.Logger,
        new ScreenshotSettings { OutputDirectory = Path.Combine(new ScreenshotSettings().OutputDirectory, FolderName(Definition.Name)) });

    /// <summary>The name the app sends in <c>X-Todo-Client</c>.</summary>
    public string? ClientId => _settings.ClientId;

    public IHtmlTestContext Context => _head.Context;

    public string BaseUrl => _head.BaseUrl;

    /// <summary>The app's time in its repositories, on a head that measures it (bUnit); else null.</summary>
    public DatabaseClock? Clock => _head.Clock;

    /// <summary>SQL replay of the device's file, per test (<see cref="TodoSqlReplay"/>).</summary>
    public TodoSqlReplay Replay => _head.Replay;

    /// <summary>The device's network: the app reads it from a file.</summary>
    public NetworkStateFile Network => _network;

    /// <summary>The app's SQLite file.</summary>
    public string DatabasePath => _database.Path;

    public TodoListPage List => new(Context);

    /// <summary>
    /// Seeds a database from the local half of <paramref name="testSet"/>, starts the app against
    /// <paramref name="apiBaseUrl"/>, and opens a browser context for it.
    /// </summary>
    /// <param name="name">The device's name.</param>
    /// <param name="testSet">The test set to seed from.</param>
    /// <param name="apiBaseUrl">The backend.</param>
    /// <param name="clientId">What the app sends in <c>X-Todo-Client</c>.</param>
    /// <param name="adjust">Changes the launch settings before the app starts.</param>
    /// <param name="definition">The app, as a row of an <c>Apps</c> table; one named <paramref name="name"/> when null.</param>
    /// <param name="httpHandler">The app's HTTP handler on bUnit (the fixture's HTTP replay); the network when null.</param>
    public static async Task<TodoWebDevice> StartAsync(
        string name,
        TodoTestSet testSet,
        string apiBaseUrl,
        string clientId,
        Func<LaunchSettings, LaunchSettings>? adjust = null,
        TestAppDefinition? definition = null,
        HttpMessageHandler? httpHandler = null)
    {
        var database = await SeedAsync(name, testSet);
        var network = NetworkStateFile.Create(Path.Combine(database.Folder, "network", "network-state.txt"));

        var settings = new LaunchSettings
        {
            DatabasePath = database.Path,
            ApiBaseUrl = apiBaseUrl,
            FixedNow = testSet.Now,
            NetworkStateFile = network.Path,
            ApiTimeout = TimeSpan.FromSeconds(5),
            ClientId = clientId,
        };

        var head = await TodoWebHeads.CreateAsync(name, httpHandler);
        definition ??= new TestAppDefinition(name, name, "web", new Dictionary<string, string>());
        var device = new TodoWebDevice(definition, name, database, network, adjust?.Invoke(settings) ?? settings, head);
        device.StartApp();
        return device;
    }

    /// <inheritdoc />
    /// <remarks>Online, and the list open in a fresh page load, showing every todo.</remarks>
    public ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        _network.SetOnline();

        // The list's view model outlives a page load, and with it the filter a test chose.
        OpenList().Filter.SelectByText("All");
        return ValueTask.CompletedTask;
    }

    /// <summary>Opens the list in a fresh page load and waits until it is interactive.</summary>
    public TodoListPage OpenList()
    {
        Context.NavigateTo(BaseUrl + "/");
        var list = List;
        list.AssertIdle();
        return list;
    }

    /// <summary>Stops the app and starts it again on the same database and backend, then opens the list.</summary>
    public TodoListPage Restart()
    {
        StartApp();
        return OpenList();
    }

    /// <summary>Restarts with changed launch settings; <see cref="RestoreLaunchSettings"/> puts them back.</summary>
    public TodoListPage RestartWith(Func<LaunchSettings, LaunchSettings> change)
    {
        _unchangedSettings ??= _settings;
        _settings = change(_settings);
        return Restart();
    }

    /// <summary>Undoes <see cref="RestartWith"/>, restarting only when something was changed.</summary>
    public void RestoreLaunchSettings()
    {
        if (_unchangedSettings is not { } unchanged)
        {
            return;
        }

        _unchangedSettings = null;
        _settings = unchanged;
        Restart();
    }

    /// <summary>Moves the device to another test set: a new seeded database, then a restart.</summary>
    public TodoListPage UseTestSet(TodoTestSet testSet)
    {
        var database = SeedAsync(Name, testSet).GetAwaiter().GetResult();

        // The first database's folder holds the network-state file, so earlier ones stay until Dispose.
        _retired.Add(_database);
        _database = database;
        _settings = (_unchangedSettings ?? _settings) with { DatabasePath = database.Path, FixedNow = testSet.Now };
        _unchangedSettings = null;
        return Restart();
    }

    public async ValueTask DisposeAsync()
    {
        await _head.DisposeAsync();
        TodoDatabase.ReleaseFileHandles();
        _network.Dispose();
        _database.Dispose();
        _retired.ForEach(database => database.Dispose());
    }

    private static string FolderName(string app)
        => string.Concat(app.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));

    private void StartApp() => _head.Start(_settings);

    // A fresh database from the test set's local half; its time is the fixture's seed in the SQL replay spike.
    private static async Task<TempDatabase> SeedAsync(string name, TodoTestSet testSet)
    {
        var database = new TempDatabase($"web-{name}-{testSet.Name}");
        var start = Stopwatch.GetTimestamp();
        await testSet.SeedDatabaseAsync(database.Path);
        DatabaseShare.Seeded($"{name}/{testSet.Name}", Stopwatch.GetElapsedTime(start));
        return database;
    }
}
