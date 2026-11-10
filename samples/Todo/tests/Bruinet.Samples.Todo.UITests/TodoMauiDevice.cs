using Bruinet.Core.Configuration;
using Bruinet.Core.Services;
using Bruinet.Core.Testing;
using Bruinet.Maui.Configuration;
using Bruinet.Maui.Enums;
using Bruinet.Mocking;
using Bruinet.Samples.Todo.Infrastructure;
using Bruinet.Samples.Todo.TestSupport;
using Bruinet.Samples.Todo.UITests.Pages;

namespace Bruinet.Samples.Todo.UITests;

/// <summary>
/// One Todo app on Windows or Android, as one of several in a test: its own seeded database,
/// network-state file and launch settings, on a backend its owner shares between apps. The MAUI
/// twin of <c>TodoWebDevice</c>.
/// </summary>
/// <remarks>
/// The <c>Platform</c> column of its <see cref="Definition"/> says where it runs, <c>Windows</c>
/// unless it says <c>Android</c>. On Android the device's database is the app's own, so only the
/// test set's server half reaches it, through its first sync.
/// </remarks>
public sealed class TodoMauiDevice : ITestApp
{
    private readonly TempDatabase _database;
    private readonly MauiTestContext _context;
    private IScreenshotService? _screenshots;

    private TodoMauiDevice(TestAppDefinition definition, TempDatabase database, NetworkStateFile network, LaunchSettings settings, MauiTestContext context)
    {
        Definition = definition;
        _database = database;
        Network = network;
        Settings = settings;
        _context = context;
    }

    /// <inheritdoc />
    public TestAppDefinition Definition { get; }

    /// <summary>The app's test context, named after the app.</summary>
    public IMauiTestContext Context => _context;

    /// <summary>Where the app runs.</summary>
    public MauiPlatform Platform => _context.Platform;

    /// <summary>The name the app sends in <c>X-Todo-Client</c>.</summary>
    public string? ClientId => Settings.ClientId;

    /// <summary>The settings the app was launched with.</summary>
    public LaunchSettings Settings { get; }

    /// <summary>The app's network: it reads it from a file. Windows only.</summary>
    public NetworkStateFile Network { get; }

    /// <summary>The app's database file on this machine. Windows only.</summary>
    public string DatabasePath => _database.Path;

    /// <summary>The list page.</summary>
    public TodoListPage List => new(Context);

    /// <inheritdoc />
    /// <remarks>Into a folder named after the app, beside the run's other screenshots.</remarks>
    public IScreenshotService Screenshots => _screenshots ??= new ScreenshotService(
        _context,
        _context.Logger,
        new ScreenshotSettings { OutputDirectory = Path.Combine(new ScreenshotSettings().OutputDirectory, FolderName(Definition.Name)) });

    /// <summary>
    /// Seeds a database from the local half of <paramref name="testSet"/>, launches the app
    /// against <paramref name="apiBaseUrl"/>, and waits for its list.
    /// </summary>
    /// <param name="definition">The app, as a row of an <c>Apps</c> table: its <c>Platform</c> column says where it runs.</param>
    /// <param name="testSet">The test set to seed from.</param>
    /// <param name="apiBaseUrl">The backend, as this machine reaches it; rewritten for an emulator.</param>
    /// <param name="clientId">What the app sends in <c>X-Todo-Client</c>.</param>
    /// <param name="placement">Where the window goes on Windows (<c>left-half</c>, <c>right-half</c>); null for the default.</param>
    /// <param name="adjust">Changes the launch settings before the app starts.</param>
    public static TodoMauiDevice Start(
        TestAppDefinition definition,
        TodoTestSet testSet,
        string apiBaseUrl,
        string clientId,
        string? placement = null,
        Func<LaunchSettings, LaunchSettings>? adjust = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(testSet);

        var platform = PlatformOf(definition);
        var database = new TempDatabase($"maui-{FolderName(definition.Name)}-{testSet.Name}");
        testSet.SeedDatabaseAsync(database.Path).GetAwaiter().GetResult();
        var network = NetworkStateFile.Create(Path.Combine(database.Folder, "network-state.txt"));

        var settings = new LaunchSettings
        {
            DatabasePath = database.Path,
            ApiBaseUrl = apiBaseUrl,
            FixedNow = testSet.Now,
            NetworkStateFile = network.Path,
            ApiTimeout = TimeSpan.FromSeconds(5),
            ClientId = clientId,
        };
        settings = adjust?.Invoke(settings) ?? settings;

        var configuration = BruinetMauiConfiguration.Load();
        var options = MauiDriverOptions.FromConfiguration(configuration.Maui);
        options.Platform = platform;
        options.AppPath = TodoMauiLaunch.AppPath(platform);
        options.Placement = placement;
        if (platform == MauiPlatform.Android)
        {
            options.AppiumServerUri = new Uri(configuration.Maui.ServerUri ?? "http://127.0.0.1:4723");
            options.DeviceName = configuration.Maui.DeviceName ?? "emulator-5554";
            TodoMauiLaunch.ConfigureAndroid(options);
        }

        foreach (var (name, value) in TodoMauiLaunch.LaunchValues(settings, database.Folder, platform))
        {
            options.LaunchSettings[name] = value;
        }

        MauiTestContext? context = null;
        try
        {
            context = new MauiTestContext(new MauiTestContextOptions
            {
                DriverOptions = options,
                Timeouts = TodoMauiLaunch.Timeouts,
                Name = definition.Name,
            });
            TodoMauiLaunch.WaitForTheList(context);
            return new TodoMauiDevice(definition, database, network, settings, context);
        }
        catch
        {
            context?.Dispose();
            network.Dispose();
            database.Dispose();
            throw;
        }
    }

    /// <summary>Where <paramref name="definition"/> runs: its <c>Platform</c> column, Windows when it has none.</summary>
    /// <param name="definition">The app.</param>
    public static MauiPlatform PlatformOf(TestAppDefinition definition)
        => definition["Platform"] is { } platform
            ? Enum.Parse<MauiPlatform>(platform, ignoreCase: true)
            : MauiPlatform.Windows;

    /// <inheritdoc />
    /// <remarks>Online, and back on the list; on Windows with the filter on All.</remarks>
    public ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        Network.SetOnline();
        var list = TodoMauiLaunch.ReturnToList(Context);

        // As in TodoAppFixtureBase.StartTest: a Picker on Android opens a dialog the selection
        // controls cannot reach yet.
        if (Platform is MauiPlatform.Windows or MauiPlatform.Headless)
        {
            list.Filter.SelectByText("All");
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>The app first, then the files it had open.</remarks>
    public ValueTask DisposeAsync()
    {
        _context.Dispose();
        Network.Dispose();
        _database.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string FolderName(string app)
        => string.Concat(app.Select(character => Path.GetInvalidFileNameChars().Contains(character) || character == ' ' ? '_' : character));
}
