using Bruinet.Mocking.Http;

namespace Bruinet.Samples.Todo.IntegrationTests;

/// <summary>
/// HTTP replay on the app's real wiring (AI11): its sync recorded against the backend once, then replayed with the
/// backend gone, the replay hermetic, so nothing leaves the process. The app's API key is never written.
/// </summary>
public sealed class HttpReplaySyncTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Bruinet.Samples.Todo.IntegrationTests", Guid.NewGuid().ToString("N"));

    private string Recording => HttpReplaySession.PathFor(_folder, "A todo created on the server appears after sync");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task A_sync_recorded_against_the_backend_replays_with_the_backend_gone()
    {
        var elsewhere = new TodoDto(Guid.Parse("6a1f9d2e-0000-4000-8000-000000000001"), "From my phone", null, null, TodoStatusDto.Open, default, default);

        var record = new HttpReplaySession(HttpReplayMode.Record, Recording);
        using (var host = await TodoHost.StartAsync("three-todos", settings => settings with { ApiKey = "sk-proj-not-a-real-key-0123456789abcdef" }, HttpReplayHandler.Create(record)))
        {
            host.Mock.Reset();
            host.TestSet.StubServer(host.Mock, apiKey: "sk-proj-not-a-real-key-0123456789abcdef").Store(elsewhere with { CreatedAt = host.TestSet.Now, UpdatedAt = host.TestSet.Now });
            Assert.True((await host.Sync.SyncAsync()).Succeeded);
        }

        record.Finish();
        Assert.DoesNotContain("sk-proj-not-a-real-key", File.ReadAllText(Recording), StringComparison.Ordinal);

        var replay = new HttpReplaySession(HttpReplayMode.Replay, Recording, new HttpReplayOptions { Hermetic = true });
        using var replayed = await TodoHost.StartAsync("three-todos", settings => settings with { ApiKey = "sk-proj-not-a-real-key-0123456789abcdef" }, HttpReplayHandler.Create(replay));
        replayed.Mock.Dispose();

        var result = await replayed.Sync.SyncAsync();

        Assert.True(result.Succeeded, result.Error + ": " + string.Join(" | ", replay.Misses));
        Assert.Equal(1, result.Pulled);
        Assert.Equal("From my phone", (await replayed.Repository.GetAsync(elsewhere.Id))!.Title);
        replay.ThrowIfMissed();
        Assert.All(replay.Calls, call => Assert.Equal("matched", call.Outcome));
    }
}
