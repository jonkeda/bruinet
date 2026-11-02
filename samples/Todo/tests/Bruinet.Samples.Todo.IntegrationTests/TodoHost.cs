global using Bruinet.Mocking;
global using Bruinet.Samples.Todo.Contracts;
global using Bruinet.Samples.Todo.Core.Models;
global using Bruinet.Samples.Todo.Core.Services;
global using Bruinet.Samples.Todo.Core.Sync;
global using Bruinet.Samples.Todo.Infrastructure;
global using Bruinet.Samples.Todo.TestSupport;
global using Xunit;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Bruinet.Samples.Todo.IntegrationTests;

/// <summary>
/// The app without its UI: the real composition, a database file of its own and a WireMock
/// backend of its own, per test.
/// </summary>
/// <remarks>
/// <para>
/// The container is built with <see cref="TodoServiceCollectionExtensions.AddTodoServices"/>,
/// the same call <c>MauiProgram</c> makes, so these tests check the app's wiring, not a copy of
/// it. Only navigation and dialogs are substitutes: they are Shell and DisplayAlert in the app.
/// </para>
/// <para>
/// <see cref="Restart"/> throws the container away and builds a new one on the same file, which is
/// what an app restart does to everything below the UI (TOD.09.3).
/// </para>
/// </remarks>
internal sealed class TodoHost : IDisposable
{
    private ServiceProvider _services;

    private readonly HttpMessageHandler? _handler;

    private TodoHost(TodoTestSet testSet, LaunchSettings settings, MockApiServer mock, TempDatabase database, TodoServerStub server, NetworkStateFile network, HttpMessageHandler? handler)
    {
        _handler = handler;
        TestSet = testSet;
        Settings = settings;
        Mock = mock;
        Database = database;
        Server = server;
        Network = network;
        _services = Build(settings, handler);
    }

    public TodoTestSet TestSet { get; }

    public LaunchSettings Settings { get; }

    /// <summary>The WireMock server, for request assertions and per-test overrides.</summary>
    public MockApiServer Mock { get; }

    /// <summary>The fake backend's state.</summary>
    public TodoServerStub Server { get; private set; }

    /// <summary>The device's network, as the app reads it: online until a test says otherwise.</summary>
    public NetworkStateFile Network { get; }

    public TempDatabase Database { get; }

    public ISyncService Sync => Get<ISyncService>();

    public TodoService Todos => Get<TodoService>();

    public ITodoRepository Repository => Get<ITodoRepository>();

    /// <summary>Starts a host from a test set: local half in the database, server half in WireMock.</summary>
    public static async Task<TodoHost> StartAsync(
        string testSetName = "empty",
        Func<LaunchSettings, LaunchSettings>? adjust = null,
        HttpMessageHandler? handler = null)
    {
        var testSet = TodoTestSet.Load(testSetName);
        var database = new TempDatabase("integration");
        var mock = MockApiServer.Start();
        var network = NetworkStateFile.Create();

        await testSet.SeedDatabaseAsync(database.Path);
        var server = testSet.StubServer(mock);

        var settings = new LaunchSettings
        {
            DatabasePath = database.Path,
            ApiBaseUrl = mock.BaseUrl,
            FixedNow = testSet.Now,
            ApiTimeout = TimeSpan.FromSeconds(5),
            NetworkStateFile = network.Path,
        };

        return new TodoHost(testSet, adjust?.Invoke(settings) ?? settings, mock, database, server, network, handler);
    }

    public T Get<T>()
        where T : notnull
        => _services.GetRequiredService<T>();

    /// <summary>A new container on the same database file: an app restart, below the UI.</summary>
    public void Restart()
    {
        _services.Dispose();
        _services = Build(Settings, _handler);
    }

    /// <summary>Drops every per-test override on the fake server and puts back the test set's server half.</summary>
    public void ResetBackend()
    {
        Mock.Reset();
        Server = TestSet.StubServer(Mock);
    }

    /// <summary>Every local todo, tombstones included.</summary>
    public Task<IReadOnlyList<TodoItem>> LocalAsync() => Repository.GetAllAsync();

    public void Dispose()
    {
        _services.Dispose();
        Mock.Dispose();
        Network.Dispose();
        Database.Dispose();
    }

    private static ServiceProvider Build(LaunchSettings settings, HttpMessageHandler? handler)
    {
        var services = new ServiceCollection();
        services.AddTodoServices(settings, new TodoPlatform(HttpHandler: handler));
        services.AddSingleton(Substitute.For<INavigator>());
        services.AddSingleton(Substitute.For<IDialogs>());

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
