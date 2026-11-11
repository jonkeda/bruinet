using System.Net;
using Bruinet.Mocking.Data.Replay;

namespace Bruinet.Mocking.Data.Hook.Tests;

/// <summary>
/// The test-id hook across processes (H2 to H5 of .my/sql-replay/plan.md, step 4): tests in parallel record and replay
/// an app they don't host, naming their test by cookie (as a browser does) or header (as an API client does).
/// </summary>
public sealed class HookTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Bruinet", "HookTests", Guid.NewGuid().ToString("N"));

    public HookTests() => Directory.CreateDirectory(_folder);

    private string Recordings => Path.Combine(_folder, "recordings");

    private string Database => Path.Combine(_folder, "places.db");

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task Hook_RecordsThenReplaysEachTestInParallel_FromAnotherProcess()
    {
        var tests = Enumerable.Range(1, 8).ToList();
        await using (var recording = await HookApp.StartAsync(ReplayMode.Record, Recordings, Database))
        {
            var recorded = await Task.WhenAll(tests.Select(i => Scenario(recording.Address, i)));
            Assert.Equal(tests.Select(i => $"Town{i}:2|409"), recorded);
        }

        File.Delete(Database);
        Assert.Equal(8, Directory.GetFiles(Recordings, "*.json").Length);

        await using var replaying = await HookApp.StartAsync(ReplayMode.Replay, Recordings, Database);
        for (var round = 0; round < 3; round++)
        {
            var replayed = await Task.WhenAll(tests.Select(i => Scenario(replaying.Address, i)));
            Assert.Equal(tests.Select(i => $"Town{i}:2|409"), replayed);
        }

        Assert.False(File.Exists(Database));
    }

    [Fact]
    public async Task Miss_InTheApp_ComesBackToTheTest()
    {
        await using (var recording = await HookApp.StartAsync(ReplayMode.Record, Recordings, Database))
        {
            using var client = Client(recording.Address, "Miss.Test");
            await ReplayHook.BeginAsync(client, recording.Address, "Miss.Test");
            await client.GetAsync("/places/Hoorn");
            await ReplayHook.EndAsync(client, recording.Address, "Miss.Test");
        }

        await using var replaying = await HookApp.StartAsync(ReplayMode.Replay, Recordings, Database);
        using var replay = Client(replaying.Address, "Miss.Test");
        await ReplayHook.BeginAsync(replay, replaying.Address, "Miss.Test");

        var response = await replay.PostAsync("/places/Hoorn", null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var miss = await Assert.ThrowsAsync<ReplayMissException>(() => ReplayHook.EndAsync(replay, replaying.Address, "Miss.Test"));
        Assert.Contains("INSERT INTO Places", miss.Message);
    }

    [Fact]
    public async Task WithAKey_TheHook_AnswersOnlyWhoSendsIt_AsAHeaderOrInTheBeginAddress()
    {
        await using var app = await HookApp.StartAsync(ReplayMode.Record, Recordings, Database, key: "s3cret-key");
        using var client = new HttpClient { BaseAddress = app.Address };

        var without = await client.GetAsync(ReplayHook.BeginPath + "?test=Keyed.Test");
        var wrong = await client.GetAsync(ReplayHook.BeginUrl(app.Address, "Keyed.Test", key: "guess"));
        var inTheAddress = await client.GetAsync(ReplayHook.BeginUrl(app.Address, "Keyed.Test", key: "s3cret-key"));

        Assert.Equal(HttpStatusCode.NotFound, without.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inTheAddress.StatusCode);
        await ReplayHook.EndAsync(client, app.Address, "Keyed.Test", key: "s3cret-key");
    }

    [Fact]
    public async Task Off_TheAppHasNoControlPaths_AndRunsOnItsDatabase()
    {
        await using var app = await HookApp.StartAsync(null, Recordings, Database);
        using var client = new HttpClient { BaseAddress = app.Address };
        client.DefaultRequestHeaders.Add(ReplayHook.Header, "Off.Test");

        var begin = await client.GetAsync(ReplayHook.BeginUrl(app.Address, "Off.Test"));
        await client.PostAsync("/places/Hoorn", null);

        Assert.Equal(HttpStatusCode.NotFound, begin.StatusCode);
        Assert.Equal("Hoorn:0", await client.GetStringAsync("/places/Hoorn"));
        Assert.False(Directory.Exists(Recordings));
    }

    // Half the tests name themselves as a browser does (the begin page sets a cookie), half as an API client (a header).
    private static async Task<string> Scenario(Uri app, int i)
    {
        var test = $"HookTests.Town{i}";
        var cookies = new CookieContainer();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = cookies }) { BaseAddress = app };
        if (i % 2 == 0)
        {
            (await client.GetAsync(ReplayHook.BeginUrl(app, test))).EnsureSuccessStatusCode();
            Assert.Equal(test, cookies.GetCookies(app)[ReplayHook.Cookie]?.Value);
        }
        else
        {
            await ReplayHook.BeginAsync(client, app, test);
            client.DefaultRequestHeaders.Add(ReplayHook.Header, test);
        }

        var name = $"Town{i}";
        (await client.PostAsync($"/places/{name}", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/places/{name}/visit", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/places/{name}/visit", null)).EnsureSuccessStatusCode();
        var place = await client.GetStringAsync($"/places/{name}");
        var again = await client.PostAsync($"/places/{name}", null);
        await ReplayHook.EndAsync(client, app, test);
        return $"{place}|{(int)again.StatusCode}";
    }

    private static HttpClient Client(Uri app, string test)
    {
        var client = new HttpClient { BaseAddress = app };
        client.DefaultRequestHeaders.Add(ReplayHook.Header, test);
        return client;
    }
}
