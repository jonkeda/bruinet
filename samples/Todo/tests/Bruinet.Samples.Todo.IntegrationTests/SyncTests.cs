namespace Bruinet.Samples.Todo.IntegrationTests;

/// <summary>
/// The sync over real HTTP and real SQLite, against the WireMock fake of the API.
/// </summary>
[Trait("Module", "Todo")]
[Trait("Pyramid", "Integration")]
public sealed class SyncTests
{
    private const string AnyTodo = TodoApi.TodosPath + "/*";

    [Fact]
    [Trait("Journey", "TOD.11.9")]
    public async Task AUserAddedElsewhere_IsCachedAfterTheNextSync()
    {
        using var host = await TodoHost.StartAsync("team");
        var eve = host.TestSet.ServerUsers[1] with { Id = Guid.NewGuid(), Name = "Eve" };
        host.Server.StoreUser(eve);

        var result = await host.Sync.SyncAsync();

        Assert.True(result.Succeeded);
        var cached = await host.Get<IUserRepository>().GetAllAsync();
        Assert.Equal(["Daan", "Eva", "Eve", "Finn", "Gus", "Hana"], cached.Select(user => user.Name).Order());
        Assert.Equal(1250.75m, cached.Single(user => user.Name == "Eve").HourlyRate);
    }

    [Fact]
    [Trait("Journey", "TOD.11.9")]
    public async Task AFailedUserFetch_StopsTheSync_AndKeepsTheCachedUsers()
    {
        using var host = await TodoHost.StartAsync("team");
        host.Server.StoreUser(host.TestSet.ServerUsers[0] with { Id = Guid.NewGuid(), Name = "Ivo" });
        host.Mock.Get(TodoApi.UsersPath).AtPriority(1).Fails(500);

        var result = await host.Sync.SyncAsync();

        Assert.Equal(SyncError.ServerError, result.Error);
        Assert.Equal(5, (await host.Get<IUserRepository>().GetAllAsync()).Count);
        Assert.DoesNotContain(host.Mock.Requests(), request => request.Path.StartsWith(TodoApi.TodosPath, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Journey", "TOD.07.2")]
    public async Task AfterASuccessfulSync_NothingIsPending_AndTheServerHasTheChanges()
    {
        using var host = await TodoHost.StartAsync("pending-local-change");

        await host.Sync.SyncAsync();

        Assert.All(await host.LocalAsync(), todo => Assert.Equal(SyncState.Synced, todo.SyncState));
        Assert.Contains(host.Server.Todos, todo => todo.Title == "Buy oat milk");
        Assert.Contains(host.Server.Todos, todo => todo.Title == "Call plumber");
    }

    [Fact]
    [Trait("Journey", "TOD.07.3")]
    public async Task ATodoCreatedOnTheServer_AppearsAfterSync()
    {
        using var host = await TodoHost.StartAsync("three-todos");
        var elsewhere = new TodoDto(Guid.NewGuid(), "From my phone", null, null, TodoStatusDto.Open, host.TestSet.Now, host.TestSet.Now);
        host.Server.Store(elsewhere);

        var result = await host.Sync.SyncAsync();

        Assert.Equal(1, result.Pulled);
        var stored = (await host.Repository.GetAsync(elsewhere.Id))!;
        Assert.Equal("From my phone", stored.Title);
        Assert.Equal(SyncState.Synced, stored.SyncState);
    }

    [Fact]
    [Trait("Journey", "TOD.07.3")]
    public async Task ATodoDeletedOnTheServer_DisappearsAfterSync()
    {
        using var host = await TodoHost.StartAsync("three-todos");
        var milk = host.TestSet.Local[0];
        host.Server.Remove(milk.Id);

        var result = await host.Sync.SyncAsync();

        Assert.Equal(1, result.Removed);
        Assert.Null(await host.Repository.GetAsync(milk.Id));
    }

    [Fact]
    [Trait("Journey", "TOD.07.5")]
    public async Task ADeletedTodo_IsDeletedOnTheServer_ThenPurged()
    {
        using var host = await TodoHost.StartAsync("three-todos");
        var milk = host.TestSet.Local[0];
        await host.Todos.DeleteAsync(milk.Id);

        var result = await host.Sync.SyncAsync();

        Assert.Equal(1, result.Deleted);
        Assert.Equal(1, host.Mock.RequestCount("DELETE", TodoApi.TodoPath(milk.Id)));
        Assert.DoesNotContain(host.Server.Todos, todo => todo.Id == milk.Id);
        Assert.Null(await host.Repository.GetAsync(milk.Id));
    }

    [Fact]
    [Trait("Journey", "TOD.06.5")]
    public async Task ATodoDeletedBeforeItWasEverSent_CausesNoServerCall()
    {
        using var host = await TodoHost.StartAsync();
        var draft = await host.Todos.CreateAsync("Never sent", null, null, TodoStatus.Open);

        await host.Todos.DeleteAsync(draft.Id);
        await host.Sync.SyncAsync();

        Assert.Equal(0, host.Mock.RequestCount("DELETE", AnyTodo));
        Assert.Equal(0, host.Mock.RequestCount("PUT", AnyTodo));
        Assert.Empty(await host.LocalAsync());
    }

    [Fact]
    [Trait("Journey", "TOD.07.7")]
    public async Task ASecondSyncWithNoChanges_SendsNoWrites_AndAddsNoRows()
    {
        using var host = await TodoHost.StartAsync("pending-local-change");
        await host.Sync.SyncAsync();
        var rows = (await host.LocalAsync()).Count;
        host.Mock.ResetRequests();

        var second = await host.Sync.SyncAsync();

        Assert.Equal(0, second.Pushed + second.Pulled + second.Deleted + second.Removed);
        Assert.Equal(0, host.Mock.RequestCount("PUT", AnyTodo));
        Assert.Equal(0, host.Mock.RequestCount("DELETE", AnyTodo));
        Assert.Equal(rows, (await host.LocalAsync()).Count);
    }

    [Fact]
    [Trait("Journey", "TOD.07.8")]
    public async Task EveryRequest_CarriesTheApiKey()
    {
        using var host = await TodoHost.StartAsync("pending-local-change", settings => settings with { ApiKey = "secret-key" });
        host.Mock.Reset();
        host.TestSet.StubServer(host.Mock, apiKey: "secret-key");

        var result = await host.Sync.SyncAsync();

        Assert.True(result.Succeeded);
        Assert.NotEmpty(host.Mock.Requests());
        Assert.All(host.Mock.Requests(), request => Assert.Equal("secret-key", request.Headers[TodoApi.ApiKeyHeader]));
    }

    [Fact]
    [Trait("Journey", "TOD.07.10")]
    public async Task EveryRequest_NamesTheClient_WhenOneIsSet()
    {
        using var host = await TodoHost.StartAsync("pending-local-change", settings => settings with { ClientId = "web-alice" });

        var result = await host.Sync.SyncAsync();

        Assert.True(result.Succeeded);
        Assert.NotEmpty(host.Mock.Requests());
        Assert.All(host.Mock.Requests(), request => Assert.Equal("web-alice", request.Headers[TodoApi.ClientHeader]));
    }

    [Fact]
    [Trait("Journey", "TOD.07.10")]
    public async Task WithoutAClientId_NoClientHeaderIsSent()
    {
        using var host = await TodoHost.StartAsync("pending-local-change");

        await host.Sync.SyncAsync();

        Assert.NotEmpty(host.Mock.Requests());
        Assert.All(host.Mock.Requests(), request => Assert.False(request.Headers.ContainsKey(TodoApi.ClientHeader)));
    }

    [Fact]
    [Trait("Journey", "TOD.07.6")]
    public async Task AConflict_TheNewerServerVersionWins_AndTheOlderLocalOneIsReplaced()
    {
        using var host = await TodoHost.StartAsync("pending-local-change");
        var milk = host.TestSet.Server[0];
        host.Server.Store(milk with { Title = "Buy almond milk", UpdatedAt = host.TestSet.Now });

        await host.Sync.SyncAsync();

        Assert.Equal("Buy almond milk", (await host.Repository.GetAsync(milk.Id))!.Title);
        Assert.Equal("Buy almond milk", host.Server.Todos.Single(todo => todo.Id == milk.Id).Title);
    }

    [Fact]
    [Trait("Journey", "TOD.08.1")]
    public async Task AServerError_KeepsTheChangesPending_AndTheDataLocal()
    {
        using var host = await TodoHost.StartAsync("pending-local-change");
        host.Mock.Put(AnyTodo).AtPriority(1).Fails(500);

        var result = await host.Sync.SyncAsync();

        Assert.Equal(SyncError.ServerError, result.Error);
        var local = await host.LocalAsync();
        Assert.Equal(3, local.Count);
        Assert.Equal(SyncState.Pending, local.Single(todo => todo.Title == "Buy oat milk").SyncState);
        Assert.Equal(SyncState.LocalOnly, local.Single(todo => todo.Title == "Call plumber").SyncState);
    }

    [Fact]
    [Trait("Journey", "TOD.08.2")]
    public async Task AfterTheServerRecovers_TheNextSyncSucceeds()
    {
        using var host = await TodoHost.StartAsync("pending-local-change");
        host.Mock.Get(TodoApi.TodosPath).AtPriority(1).FailsFirst(1, 503).ReturnsJson(Array.Empty<TodoDto>());

        Assert.Equal(SyncError.ServerError, (await host.Sync.SyncAsync()).Error);
        host.Mock.Reset();
        host.TestSet.StubServer(host.Mock);

        Assert.True((await host.Sync.SyncAsync()).Succeeded);
        Assert.All(await host.LocalAsync(), todo => Assert.Equal(SyncState.Synced, todo.SyncState));
    }

    [Fact]
    [Trait("Journey", "TOD.08.4")]
    public async Task ATimeout_KeepsTheChangesPending_AndSaysSo()
    {
        using var host = await TodoHost.StartAsync(
            "pending-local-change",
            settings => settings with { ApiTimeout = TimeSpan.FromMilliseconds(300) });
        host.Mock.Put(AnyTodo).AtPriority(1).WithDelay(TimeSpan.FromSeconds(3)).ReturnsStatus(200);

        var result = await host.Sync.SyncAsync();

        Assert.Equal(SyncError.Timeout, result.Error);
        Assert.Contains(await host.LocalAsync(), todo => todo.SyncState != SyncState.Synced);
    }

    [Fact]
    [Trait("Journey", "TOD.08.5")]
    public async Task AWrongApiKey_IsReportedAsUnauthorised()
    {
        using var host = await TodoHost.StartAsync("three-todos", settings => settings with { ApiKey = "wrong" });

        var result = await host.Sync.SyncAsync();

        Assert.Equal(SyncError.Unauthorized, result.Error);
        Assert.Equal(3, (await host.LocalAsync()).Count);
    }

    [Fact]
    [Trait("Journey", "TOD.09.4")]
    public async Task AnUnreachableServer_IsReported_AndLocalTodosStay()
    {
        using var host = await TodoHost.StartAsync("three-todos", settings => settings with { ApiBaseUrl = "http://127.0.0.1:1" });

        var result = await host.Sync.SyncAsync();

        Assert.Equal(SyncError.Unreachable, result.Error);
        Assert.Equal(3, (await host.Todos.GetVisibleAsync()).Count);
    }

    [Fact]
    [Trait("Journey", "TOD.08.8")]
    public async Task Offline_TheSyncSendsNothing_UntilBackOnline()
    {
        using var network = NetworkStateFile.Create();
        using var host = await TodoHost.StartAsync("pending-local-change", settings => settings with { NetworkStateFile = network.Path });
        network.SetOffline();

        Assert.Equal(SyncError.Offline, (await host.Sync.SyncAsync()).Error);
        Assert.Empty(host.Mock.Requests());

        network.SetOnline();

        Assert.True((await host.Sync.SyncAsync()).Succeeded);
    }
}
