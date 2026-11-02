using System.Net;
using System.Text;
using Bruinet.Mocking.Http;

namespace Bruinet.Mocking.Tests.Api;

/// <summary>
/// HTTP replay for an app in a process of its own: the server records by proxying to the real service and replays from
/// the recording, in the in-process handler's format.
/// </summary>
public sealed class MockApiServerReplayTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Bruinet.Mocking.Tests", Guid.NewGuid().ToString("N"));
    private readonly MockApiServer _service = MockApiServer.Start();

    private string Recording => HttpReplaySession.PathFor(_folder, "Out of process");

    public void Dispose()
    {
        _service.Dispose();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task Calls_recorded_through_the_proxy_are_answered_in_replay_with_the_service_gone()
    {
        _service.Get("/todos").ReturnsText("""[{"id":1}]""", contentType: "application/json");
        _service.Post("/todos").ReturnsText("""{"id":2}""", 201, "application/json");

        using (var proxy = MockApiServer.Start())
        using (var app = new HttpClient { BaseAddress = new Uri(proxy.BaseUrl) })
        {
            proxy.Record(_service.BaseUrl);
            Assert.Equal("""[{"id":1}]""", await app.GetStringAsync("/todos?done=false"));
            using var created = await app.PostAsync("/todos", new StringContent("""{"title":"Milk"}""", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            var session = new HttpReplaySession(HttpReplayMode.Record, Recording);
            proxy.CopyRecordedTo(session);
            session.Finish();
        }

        _service.Dispose();
        using var replay = MockApiServer.Start();
        replay.Replay(HttpRecording.Load(Recording)!);
        using var client = new HttpClient { BaseAddress = new Uri(replay.BaseUrl) };

        Assert.Equal("""[{"id":1}]""", await client.GetStringAsync("/todos?done=false"));
        using var again = await client.PostAsync("/todos", new StringContent("""{"title":"Milk"}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        Assert.Empty(replay.ReplayMisses());
    }

    [Fact]
    public async Task A_call_not_recorded_gets_the_miss_status_and_is_listed()
    {
        new HttpRecording { Exchanges = [Exchange("POST", "/todos", """{"title":"Milk"}""", 201, "{}")] }.Save(Recording);
        using var replay = MockApiServer.Start();
        replay.Replay(HttpRecording.Load(Recording)!);
        using var client = new HttpClient { BaseAddress = new Uri(replay.BaseUrl) };

        using var answer = await client.PostAsync("/todos", new StringContent("""{"title":"Bread"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(MockApiServer.ReplayMissStatus, (int)answer.StatusCode);
        Assert.Equal(["POST /todos"], replay.ReplayMisses());
    }

    [Fact]
    public async Task A_recording_made_in_process_replays_in_order_with_volatile_values_matching_anything()
    {
        var pending = Exchange("GET", "/job?ts=1", null, 200, "pending");
        pending.Volatile = ["?ts"];
        var done = Exchange("GET", "/job?ts=2", null, 200, "done");
        done.Volatile = ["?ts"];
        using var replay = MockApiServer.Start();
        replay.Replay(new HttpRecording { Exchanges = [pending, done] });
        using var client = new HttpClient { BaseAddress = new Uri(replay.BaseUrl) };

        Assert.Equal("pending", await client.GetStringAsync("/job?ts=7"));
        Assert.Equal("done", await client.GetStringAsync("/job?ts=8"));
        Assert.Equal("done", await client.GetStringAsync("/job?ts=9"));
    }

    private static HttpExchange Exchange(string method, string url, string? body, int status, string answer)
        => new()
        {
            Method = method,
            Url = "https://api.example.test" + url,
            RequestBody = body,
            Status = status,
            ResponseBody = answer,
        };
}
