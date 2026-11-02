using Bruinet.Core.Testing;
using Bruinet.Html.Testing;
using Bruinet.Mocking;
using Bruinet.Samples.Todo.Infrastructure;
using Bruinet.Samples.Todo.TestSupport.Uat;
using Bruinet.Samples.Todo.Web.UITests.Pages;

namespace Bruinet.Samples.Todo.Web.UITests;

/// <summary>
/// The Todo web head, hermetic: a seeded SQLite file, the WireMock fake of <c>Todo.Api</c>, a pinned
/// clock, and one browser. The same arrangement as the MAUI fixtures (<c>TodoAppFixture</c>), on
/// the web head's own host (<see cref="WebAppHost"/>).
/// </summary>
/// <remarks>
/// One device per fixture (<see cref="TodoWebDevice"/>): the host process is one device, so tests
/// in a collection share its database, as the MAUI tests share their app. Each test starts from the
/// list, online, with the backend reset (<see cref="StartTest"/>). A fixture that runs several
/// devices on the one backend starts them with <see cref="StartDeviceAsync"/> and makes one
/// <see cref="Device"/>.
/// </remarks>
public abstract class TodoWebFixture : IAsyncLifetime, ITodoArrangement
{
    /// <summary>The name this app sends in <c>X-Todo-Client</c>.</summary>
    public const string ClientId = "web-hermetic";

    private MockApiServer? _backend;
    private TodoWebDevice? _main;
    private TodoWebDevice? _device;
    private TodoHttpReplay? _http;

    /// <summary>The test set the fixture seeds from.</summary>
    protected abstract string TestDataName { get; }

    /// <summary>Changes the launch settings before the app starts.</summary>
    protected virtual LaunchSettings AdjustSettings(LaunchSettings settings) => settings;

    public TodoTestSet TestSet { get; private set; } = null!;

    public MockApiServer Backend => _backend ?? throw new InvalidOperationException("The fixture is not started.");

    public TodoServerStub Server { get; private set; } = null!;

    /// <summary>The device the fixture started with.</summary>
    public TodoWebDevice MainDevice => _main ?? throw new InvalidOperationException("The fixture is not started.");

    /// <summary>The device the members below act on: <see cref="MainDevice"/> unless a subclass chose another.</summary>
    public TodoWebDevice Device
    {
        get => _device ?? throw new InvalidOperationException("The fixture is not started.");
        protected set => _device = value;
    }

    /// <summary>The device's network: the app reads it from a file.</summary>
    public NetworkStateFile Network => Device.Network;

    /// <summary>The app's SQLite file.</summary>
    public string DatabasePath => Device.DatabasePath;

    public IHtmlTestContext Context => Device.Context;

    public string BaseUrl => Device.BaseUrl;

    public TodoListPage List => Device.List;

    /// <summary>The suite's <c>uat.config.md</c>, for its <c>backend</c> dependency; null for a suite with none.</summary>
    protected virtual Bruinet.Bdd.BddConfig? SuiteConfig => null;

    /// <summary>
    /// The main device's HTTP replay (AI11b), on bUnit, where the app is composed here. Another device talks to the stub:
    /// a scenario with two apps reads the backend they share, so it needs the stub (<c>@needs-stub</c>).
    /// </summary>
    public TodoHttpReplay HttpReplay => _http ?? throw new InvalidOperationException("The fixture is not started.");

    /// <inheritdoc />
    public IReadOnlyList<Mocking.Http.HttpCall>? ReplayedCalls => _http?.ReplayedCalls;

    public virtual async Task InitializeAsync()
    {
        TestSet = TodoTestSet.Load(TestDataName);
        _backend = MockApiServer.Start();
        Server = TestSet.StubServer(_backend);
        _http = new TodoHttpReplay(BrowserTodoWebHead.Project, WebHeads.IsBunit ? "bunit" : null, SuiteConfig);
        _http.BeginStartup(GetType().Name);
        _main = await TodoWebDevice.StartAsync("todo-web", TestSet, Backend.BaseUrl, ClientId, AdjustSettings, httpHandler: _http.Handler);
        _device = _main;
    }

    /// <summary>Begins <paramref name="scenario"/>'s HTTP replay session on the main device. Nothing when replay is off.</summary>
    public void BeginReplay(string scenario) => HttpReplay.Begin(scenario);

    /// <summary>Ends the scenario's HTTP replay session: its recording saved, or its misses thrown into it.</summary>
    public void EndReplay() => HttpReplay.End();

    public virtual async Task DisposeAsync()
    {
        if (_main != null)
        {
            await _main.DisposeAsync();
        }

        _backend?.Dispose();
    }

    /// <summary>Starts another device on the fixture's backend and test set.</summary>
    /// <param name="name">The device's name, for its browser context and the call log.</param>
    /// <param name="clientId">What the app sends in <c>X-Todo-Client</c>.</param>
    /// <param name="definition">The app, as a row of an <c>Apps</c> table, when it is one.</param>
    protected Task<TodoWebDevice> StartDeviceAsync(string name, string clientId, TestAppDefinition? definition = null)
        => TodoWebDevice.StartAsync(name, TestSet, Backend.BaseUrl, clientId, AdjustSettings, definition);

    /// <summary>Online, the backend back to the test set, and the list open, showing every todo.</summary>
    /// <summary>
    /// <see cref="StartTest()"/> for the test <paramref name="output"/> belongs to, in its own SQL replay session; the
    /// test class's <c>Dispose</c> calls <see cref="EndTest"/>.
    /// </summary>
    public TodoListPage StartTest(Xunit.Abstractions.ITestOutputHelper output)
    {
        Device.Replay.Begin(TodoSqlReplay.TestName(output));
        return StartTest();
    }

    /// <summary>Ends the test's SQL replay session: its recording saved, or its misses thrown into it.</summary>
    public void EndTest() => Device.Replay.End();

    public virtual TodoListPage StartTest()
    {
        Spike.DatabaseShare.Start(Device.Clock);
        Network.SetOnline();
        ResetBackend();
        return OpenList();
    }

    /// <summary>The fake server back to the test set's server half, with nothing arranged on it.</summary>
    public void ResetBackend()
    {
        Backend.Reset();
        Server = TestSet.StubServer(Backend);
    }

    /// <summary>Opens the list in a fresh page load, waits until it is interactive, and shows every todo.</summary>
    public TodoListPage OpenList()
    {
        var list = Device.OpenList();

        // The list's view model outlives a page load, and with it the filter a test chose.
        list.Filter.SelectByText("All");
        return list;
    }

    /// <summary>Stops the app and starts it again on the same database and backend, then opens the list.</summary>
    public TodoListPage Restart() => Device.Restart();

    /// <summary>Restarts with changed launch settings; <see cref="RestoreLaunchSettings"/> puts them back.</summary>
    public TodoListPage RestartWith(Func<LaunchSettings, LaunchSettings> change) => Device.RestartWith(change);

    /// <summary>Undoes <see cref="RestartWith"/>, restarting only when something was changed.</summary>
    public void RestoreLaunchSettings() => Device.RestoreLaunchSettings();

    /// <summary>Moves the app to another test set: a new seeded database, the backend reset, a restart.</summary>
    public virtual TodoListPage UseTestSet(string name)
    {
        if (name == TestSet.Name)
        {
            return List;
        }

        TestSet = TodoTestSet.Load(name);
        ResetBackend();
        return MainDevice.UseTestSet(TestSet);
    }

    /// <summary>A todo on the server only, with a unique title; the app has it after a sync.</summary>
    public string ServerHasTodo(string title, TodoStatusDto status = TodoStatusDto.Open, DateOnly? dueDate = null)
    {
        var unique = $"{title} {Guid.NewGuid().ToString("N")[..6]}";
        var yesterday = TestSet.Now.AddDays(-1);
        Server.Store(new TodoDto(Guid.NewGuid(), unique, null, dueDate, status, yesterday, yesterday));
        return unique;
    }
}

/// <summary>Where the built web head is.</summary>
public static class WebApp
{
    public static string Assembly() => WebAppHost.FindBuiltApp("src/Bruinet.Samples.Todo.Web");
}

public sealed class ThreeTodosWebFixture : TodoWebFixture
{
    protected override string TestDataName => "three-todos";
}

public sealed class EmptyWebFixture : TodoWebFixture
{
    protected override string TestDataName => "empty";
}

public sealed class PendingChangesWebFixture : TodoWebFixture
{
    protected override string TestDataName => "pending-local-change";

    protected override LaunchSettings AdjustSettings(LaunchSettings settings) => settings with { SyncOnStart = false };
}

[CollectionDefinition(Name)]
public sealed class ThreeTodosWebCollection : ICollectionFixture<ThreeTodosWebFixture>
{
    public const string Name = "Todo web: three todos";
}

[CollectionDefinition(Name)]
public sealed class EmptyWebCollection : ICollectionFixture<EmptyWebFixture>
{
    public const string Name = "Todo web: empty";
}

[CollectionDefinition(Name)]
public sealed class PendingChangesWebCollection : ICollectionFixture<PendingChangesWebFixture>
{
    public const string Name = "Todo web: pending changes";
}
