using Bruinet.Core.Testing;
using Bruinet.Maui.Configuration;
using Bruinet.Maui.Enums;
using Bruinet.Maui.Headless;
using Bruinet.Maui.Testing;
using Bruinet.Mocking;
using Bruinet.Samples.Todo.App.Services;
using Bruinet.Samples.Todo.Infrastructure;
using Bruinet.Samples.Todo.TestSupport;
using Bruinet.Samples.Todo.UITests.Containers;
using Bruinet.Samples.Todo.UITests.Pages;
using Microsoft.Extensions.DependencyInjection;
using TodoApp = Bruinet.Samples.Todo.App.App;
using TodoAppShell = Bruinet.Samples.Todo.App.AppShell;

namespace Bruinet.Samples.Todo.UITests;

/// <summary>
/// The Todo app on its own database, network-state file and pinned clock, against a backend a
/// subclass provides: the WireMock fake (<see cref="TodoAppFixture"/>, hermetic) or the real API
/// (<see cref="LiveTodoAppFixture"/>, live).
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything is arranged before the app starts</b>, in <see cref="CreateTestContextOptions"/>,
/// which the base constructor calls just before it launches the app. The app's <c>LaunchSettings</c>
/// travel with that one launch (<c>MauiDriverOptions.LaunchSettings</c>, plan item B1): the launched
/// process's environment on Windows, launch-intent extras on Android.
/// </para>
/// <para>
/// <b>Because the base calls that override from its constructor</b>, it runs before any field of
/// a subclass is set. So a fixture describes itself with members that answer constants
/// (<see cref="TestDataName"/>, <see cref="AdjustSettings"/>), and what it builds for the run -
/// the backend included (<see cref="StartBackend"/>) - is created inside the override.
/// </para>
/// <para>
/// <b>One app per collection, shared by its tests.</b> Each test starts with
/// <see cref="StartTest"/>, which puts back what a previous test may have changed outside the app
/// and returns to the list. Tests work on todos of their own, so order does not matter.
/// </para>
/// </remarks>
public abstract class TodoAppFixtureBase : MauiTestFixtureBase
{
    private readonly List<TempDatabase> _retired = [];

    // A field initializer, so it is there when the base constructor composes the app. Replayed headless only: on a
    // device the app runs in a process of its own, on its own file.
    private readonly TodoSqlReplay _replay = new("Bruinet.Samples.Todo.UITests", MauiHeads.IsHeadless ? "headless" : null);
    private TodoHttpReplay? _http;
    private string? _test;
    private Arrangement? _arrangement;
    private LaunchSettings? _settings;
    private LaunchSettings? _unchangedSettings;
    private IDictionary<string, string>? _launchValues;

    /// <summary>Launches the app for <see cref="TestDataName"/> and waits for the list to settle.</summary>
    protected TodoAppFixtureBase()
    {
        WaitForTheList();
        LetTheStartupSyncFinish();
    }

    /// <summary>
    /// The app's HTTP replay (AI11b): made on first use, which is during base construction, so it reads only
    /// <see cref="RecordingsProject"/> and <see cref="SuiteConfig"/>, which answer from constants.
    /// </summary>
    protected TodoHttpReplay HttpReplay => _http ??= new(RecordingsProject, MauiHeads.IsHeadless ? "headless" : null, SuiteConfig);

    /// <summary>The project whose <c>Recordings</c> folder holds the HTTP recordings.</summary>
    protected virtual string RecordingsProject => "Bruinet.Samples.Todo.UITests";

    /// <summary>The suite's <c>uat.config.md</c>, for its <c>backend</c> dependency; null for a suite with none.</summary>
    protected virtual Bruinet.Bdd.BddConfig? SuiteConfig => null;

    /// <summary>
    /// Begins <paramref name="scenario"/>'s HTTP replay session, after the last one's work is quiet, so none of it lands in this
    /// one's recording. Nothing when replay is off.
    /// </summary>
    public void BeginReplay(string scenario)
    {
        if (HttpReplay.Mode != Mocking.Http.HttpReplayMode.Off)
        {
            LetTheLastTestFinish();
        }

        HttpReplay.Begin(scenario);
    }

    /// <summary>Ends the scenario's HTTP replay session, its own work finished inside it: its recording saved, or its misses thrown.</summary>
    public void EndReplay()
    {
        if (HttpReplay.Mode != Mocking.Http.HttpReplayMode.Off)
        {
            LetTheLastTestFinish();
        }

        HttpReplay.End();
    }

    /// <summary>The test set the app starts in. Answer with a constant: it is read during base construction.</summary>
    protected abstract string TestDataName { get; }

    /// <summary>The test set, as seeded.</summary>
    public TodoTestSet TestSet => Arranged.TestSet;

    /// <summary>Takes the app offline and back.</summary>
    public NetworkStateFile Network => Arranged.Network;

    /// <summary>The list page, which is where every test starts.</summary>
    public TodoListPage List => new(Context);

    private Arrangement Arranged => _arrangement
        ?? throw new InvalidOperationException("The fixture was used before the app was arranged.");

    /// <summary>
    /// Puts back what earlier tests in the collection may have changed, and returns to the list.
    /// </summary>
    /// <remarks>
    /// Online again, the backend reset as far as the subclass resets it
    /// (<see cref="ResetBackendForTest"/>), the list on screen with the filter on All.
    /// </remarks>
    public TodoListPage StartTest()
    {
        LetTheLastTestFinish();

        // After the last test's work is quiet, so none of it lands in this test's recording.
        _replay.Begin(_test);
        Network.SetOnline();
        ResetBackendForTest();

        var list = ReturnToList();

        // Windows only. On Android a Picker opens a native dialog the selection controls cannot
        // reach yet (the Picker tests are 0/8 there before any change), and a dialog left open
        // would take every later test down with it. Tests that use the filter are Windows-only
        // (and MauiHeadless's, where the Picker is selected in process).
        if (Context.Platform is MauiPlatform.Windows or MauiPlatform.Headless)
        {
            list.Filter.SelectByText("All");
        }

        return list;
    }

    /// <summary>
    /// <see cref="StartTest()"/> for the test <paramref name="output"/> belongs to, in its own SQL replay session
    /// (<c>BRUINET_SQL_REPLAY</c>, .my/sql-replay/plan.md step 8); the test class's <c>Dispose</c> calls <see cref="EndTest"/>.
    /// </summary>
    public TodoListPage StartTest(Xunit.Abstractions.ITestOutputHelper output)
    {
        _test = TodoSqlReplay.TestName(output);
        return StartTest();
    }

    /// <summary>Ends the test's SQL replay session: its recording saved, or its misses thrown into it.</summary>
    public void EndTest()
    {
        // The test's own work finished inside its session, so the next test's starts on a quiet app.
        if (_replay.Mode != Mocking.Data.Replay.ReplayMode.Off)
        {
            LetTheLastTestFinish();
        }

        _test = null;
        _replay.End();
    }

    // Replayed headless, an app that syncs on start finishes that sync (its pushes, pulls and the users' refresh) before the first
    // test: what it writes then belongs to the start, however long its requests take. Waiting on the dispatcher alone is
    // fooled while a request is out.
    private void LetTheStartupSyncFinish()
    {
        if (_replay.Mode == Mocking.Data.Replay.ReplayMode.Off || Context.Driver is not MauiHeadlessDriver head || _settings is not { SyncOnStart: true })
        {
            return;
        }

        var sync = head.TestContext.Services.GetService<Core.Sync.ISyncService>();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (sync is { LastResult: null } && DateTime.UtcNow < deadline)
        {
            head.TestContext.Dispatcher.WaitForQuiet(TimeSpan.FromMilliseconds(150), TimeSpan.FromSeconds(3));
        }
    }

    /// <summary>Whether <see cref="StartOver"/> resets the app in place: on Windows and headless, where the test can write the app's files.</summary>
    public bool CanStartOverInPlace => Context.Platform is MauiPlatform.Windows or MauiPlatform.Headless;

    // Headless (MauiHeadless) the app works only when a test lets it, so a sync the last test started
    // would otherwise finish in this one, after its backend was reset, and show in its request log.
    // On a device the app has finished it by itself.
    // Longer than a page's IsIdle: a sync's requests and continuations come in a few rounds.
    private void LetTheLastTestFinish()
    {
        if (Context.Driver is MauiHeadlessDriver head)
        {
            head.TestContext.Dispatcher.WaitForQuiet(TimeSpan.FromMilliseconds(150), TimeSpan.FromSeconds(3));
        }
    }

    /// <summary>
    /// Puts the app back to where its test set starts, with no restart, and returns to the list:
    /// what <see cref="StartTest"/> does, and the app's own data too.
    /// </summary>
    /// <remarks>
    /// The database is restored in place (<see cref="TodoTestSet.RestoreDatabaseAsync"/>) and the app
    /// told to start over on it (<see cref="TodoResetFile"/>): the last sync and the list's state
    /// forgotten, the rows reloaded. Elsewhere than on Windows it is <see cref="StartTest"/>.
    /// </remarks>
    public TodoListPage StartOver()
    {
        if (!CanStartOverInPlace)
        {
            return StartTest();
        }

        LetTheLastTestFinish();
        Network.SetOnline();
        ResetBackendForTest();
        Arranged.TestSet.RestoreDatabaseAsync(Arranged.Database.Path).GetAwaiter().GetResult();
        // Headless the app's main thread runs only when the test lets it: the wait lets it.
        Arranged.Reset.Request(whileWaiting: MauiHeads.IsHeadless ? () => Context.Driver.IsIdle() : null);
        return StartTest();
    }

    /// <summary>
    /// Closes the app and starts it again with the same launch settings - the same database and
    /// backend on Windows - and waits for the list.
    /// </summary>
    /// <remarks>
    /// On Android a new session reinstalls the app (<c>enforceAppInstall</c>), which clears its
    /// data: a restart there is a fresh install, not a restart. TOD.09.3 is Windows-only for that
    /// reason.
    /// </remarks>
    public TodoListPage Restart()
    {
        RestartApp();
        OnAppRestarted();
        return WaitForTheList();
    }

    /// <summary>
    /// Restarts the app with changed launch settings, e.g. an unreachable backend. The change holds
    /// until <see cref="RestoreLaunchSettings"/>.
    /// </summary>
    /// <param name="change">Derives the new settings from the current ones.</param>
    /// <returns>The list, loaded.</returns>
    public TodoListPage RestartWith(Func<LaunchSettings, LaunchSettings> change)
    {
        _unchangedSettings ??= _settings;
        ApplySettings(change(_settings!));
        return Restart();
    }

    /// <summary>Undoes <see cref="RestartWith"/>, restarting the app. Nothing happens when nothing was changed.</summary>
    public void RestoreLaunchSettings()
    {
        if (_unchangedSettings is not { } unchanged)
        {
            return;
        }

        _unchangedSettings = null;
        ApplySettings(unchanged);
        Restart();
    }

    /// <summary>
    /// Saves a screenshot for the Reviews pass, into the run's artifacts
    /// (<c>TestResults/&lt;run-id&gt;/suites/TodoUITests/screenshots</c>, AD-007).
    /// </summary>
    /// <param name="screen">Which screen, e.g. <c>list</c>; it becomes part of the file name.</param>
    /// <returns>The file written.</returns>
    public string CaptureReview(string screen) => ScreenshotService.Capture("Review", screen, "review");

    /// <summary>
    /// Starts (or connects to) the backend for this run and returns the base URL the app should use.
    /// </summary>
    /// <remarks>
    /// Runs during base construction, after the database is seeded and before the app launches.
    /// Anything it starts is the subclass's to stop in <see cref="StopBackend"/>.
    /// </remarks>
    /// <param name="testSet">The test set: its server half is what the backend should hold.</param>
    /// <param name="runFolder">A folder of this run's own, for files the backend needs.</param>
    protected abstract string StartBackend(TodoTestSet testSet, string runFolder);

    /// <summary>Stops whatever <see cref="StartBackend"/> started. Called after the app has closed.</summary>
    protected abstract void StopBackend();

    /// <summary>Puts the backend back between tests. Nothing, unless the subclass can and should.</summary>
    protected virtual void ResetBackendForTest()
    {
    }

    /// <summary>
    /// Changes the launch settings a fixture needs beyond the test set. Answer from constants: this
    /// runs during base construction.
    /// </summary>
    protected virtual LaunchSettings AdjustSettings(LaunchSettings settings) => settings;

    /// <inheritdoc />
    protected override MauiTestContextOptions CreateTestContextOptions(MauiTestContextOptions loaded)
    {
        var settings = Arrange();

        var options = base.CreateTestContextOptions(loaded);
        _launchValues = options.DriverOptions!.LaunchSettings;
        foreach (var (name, value) in LaunchValuesFor(settings, Arranged.Database.Folder))
        {
            _launchValues[name] = value;
        }

        return options;
    }

    /// <summary>
    /// The MauiHeadless head (<c>BRUINET_HEAD=headless</c>): the same arrangement as on a device (database,
    /// backend, network-state and reset files), and the app composed as its <c>MauiProgram</c> composes
    /// it (<see cref="TodoAppServices.AddTodoApp"/>), with the launch settings handed over directly
    /// rather than as launch values, and MauiHeadless's dispatcher as the main thread. Asked again on every
    /// restart, so a restart is a new app on the settings of that moment.
    /// </summary>
    protected override MauiTestContextOptions CreateHeadlessOptions()
    {
        var settings = _settings ?? Arrange();

        // What the app does before the first test is recorded too: the first list load reads the file, the first sync calls.
        _replay.BeginStartup(GetType().Name);
        HttpReplay.BeginStartup(GetType().Name);

        // What UseMauiCommunityToolkit() does first on a device: the toolkit's assembly loaded. In
        // Debug, XAML is parsed at run time, and the parser knows only the XAML namespaces of loaded
        // assemblies, so without this toolkit:StateContainer on the list was "not found".
        _ = typeof(CommunityToolkit.Maui.Layouts.StateContainer).Assembly;
        return MauiHeadlessHead.Options(
            services => services.GetRequiredService<TodoAppShell>(),
            options => options.Application = services => new TodoApp(services.GetRequiredService<TodoAppShell>()),
            context =>
            {
                context.Services.AddTodoApp(
                    _ => settings,
                    new TodoPlatform(Dispatch: action => context.Dispatcher.Dispatch(action), HttpHandler: HttpReplay.Handler));

                // The app's file through the test's SQL replay session, when there is one.
                context.Services.AddSingleton(new Infrastructure.Data.TodoDatabase(settings.DatabasePath) { Wrap = _replay.Wrap });
            });
    }

    // The test set, its database and backend, the network-state and reset files, and the launch settings.
    private LaunchSettings Arrange()
    {
        var testSet = TodoTestSet.Load(TestDataName);
        var database = new TempDatabase("ui-" + testSet.Name);
        testSet.SeedDatabaseAsync(database.Path).GetAwaiter().GetResult();

        var network = NetworkStateFile.Create(Path.Combine(database.Folder, "network", "network-state.txt"));
        var reset = new TodoResetFile(Path.Combine(database.Folder, "reset.txt"));
        var apiBaseUrl = StartBackend(testSet, database.Folder);

        var settings = AdjustSettings(new LaunchSettings
        {
            DatabasePath = database.Path,
            ApiBaseUrl = apiBaseUrl,
            FixedNow = testSet.Now,
            NetworkStateFile = network.Path,
            ResetFile = reset.Path,
            ApiTimeout = TimeSpan.FromSeconds(5),
        });

        _arrangement = new Arrangement(testSet, database, network, reset);
        _settings = settings;
        return settings;
    }

    /// <summary>
    /// Starts the app over on the test set called <paramref name="name"/>: a fresh database seeded
    /// from it, the backend reset to its server half, the clock at its now. Nothing happens when it
    /// is the test set already loaded.
    /// </summary>
    /// <remarks>
    /// The app restarts, so page objects built before are stale. The network state carries over.
    /// On Android only the server half and the clock change: the device's database is the app's own.
    /// </remarks>
    /// <param name="name">The test set, e.g. <c>one-todo</c>.</param>
    /// <returns>The list, loaded.</returns>
    public TodoListPage UseTestSet(string name)
    {
        if (name == Arranged.TestSet.Name)
        {
            return List;
        }

        if (!CanChangeTestSet)
        {
            throw new NotSupportedException($"{GetType().Name} cannot change its test set: its backend is seeded once, at start.");
        }

        // Drop a RestartWith change; the restart below applies every launch value again.
        _settings = _unchangedSettings ?? _settings;
        _unchangedSettings = null;

        var testSet = TodoTestSet.Load(name);
        var database = new TempDatabase("ui-" + testSet.Name);
        testSet.SeedDatabaseAsync(database.Path).GetAwaiter().GetResult();

        _settings = _settings! with { DatabasePath = database.Path, FixedNow = testSet.Now };
        PassToTheLaunch(_settings, database.Folder);

        // The first database's folder holds the network-state file, so earlier ones stay until Dispose.
        _retired.Add(Arranged.Database);
        _arrangement = new Arrangement(testSet, database, Arranged.Network, Arranged.Reset);
        ResetBackendForTest();

        RestartApp();
        OnAppRestarted();
        return WaitForTheList();
    }

    private void ApplySettings(LaunchSettings settings)
    {
        _settings = settings;
        PassToTheLaunch(settings, Arranged.Database.Folder);
    }

    // On a device the settings reach the app as launch values; headless (MauiHeadless) the next app is
    // composed with them directly (CreateHeadlessOptions), and there are no launch values.
    private void PassToTheLaunch(LaunchSettings settings, string runFolder)
    {
        if (_launchValues is not { } launch)
        {
            return;
        }

        foreach (var (key, value) in LaunchValuesFor(settings, runFolder))
        {
            launch[key] = value;
        }
    }

    /// <summary>Whether <see cref="UseTestSet"/> can reseed the backend. A live backend is seeded once.</summary>
    protected virtual bool CanChangeTestSet => true;

    /// <summary>The app's database file on this machine, for read-only checks of what it stored.</summary>
    public string DatabasePath => Arranged.Database.Path;

    /// <summary>Called after the app has restarted, when <see cref="MauiTestFixtureBase.Context"/> is a new object.</summary>
    protected virtual void OnAppRestarted()
    {
    }

    // Per launch, not per process: two fixtures no longer share one environment.
    private Dictionary<string, string> LaunchValuesFor(LaunchSettings settings, string runFolder)
        => TodoMauiLaunch.LaunchValues(settings, runFolder, Platform);

    /// <inheritdoc />
    /// <remarks>The capabilities the Bruinet sample fixture uses, for this app's package.</remarks>
    protected override void ConfigureAndroidOptions(MauiDriverOptions options)
    {
        base.ConfigureAndroidOptions(options);
        TodoMauiLaunch.ConfigureAndroid(options);
    }

    /// <summary>The Todo app's Android package name (<c>ApplicationId</c>).</summary>
    public const string AndroidPackage = TodoMauiLaunch.AndroidPackage;

    /// <inheritdoc />
    /// <remarks>
    /// The app built by the Todo solution, Debug preferred: the Windows exe, or the signed Android
    /// APK. Found from the test output by walking up to the folder holding
    /// <c>Bruinet.Samples.Todo.slnx</c>: the base's own search looks for a <c>.sln</c>.
    /// </remarks>
    protected override string GetDefaultAppPath(MauiPlatform platform) => TodoMauiLaunch.AppPath(platform);

    /// <inheritdoc />
    /// <remarks>The app first, then the backend, then the files the app had open.</remarks>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && _arrangement is { } arranged)
        {
            StopBackend();
            arranged.Network.Dispose();
            arranged.Database.Dispose();
            _retired.ForEach(database => database.Dispose());
        }
    }

    private TodoListPage WaitForTheList() => TodoMauiLaunch.WaitForTheList(Context);

    private TodoListPage ReturnToList() => TodoMauiLaunch.ReturnToList(Context);

    private sealed class Arrangement(TodoTestSet testSet, TempDatabase database, NetworkStateFile network, TodoResetFile reset)
    {
        public TodoTestSet TestSet { get; } = testSet;

        public TodoResetFile Reset { get; } = reset;

        public TempDatabase Database { get; } = database;

        public NetworkStateFile Network { get; } = network;
    }
}
