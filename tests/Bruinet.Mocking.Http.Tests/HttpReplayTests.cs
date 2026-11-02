using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Bruinet.Mocking.Http.Tests;

/// <summary>Record against a real service (a WireMock server), then replay with it stopped: nothing leaves the process.</summary>
public sealed class HttpReplayTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Bruinet.Mocking.Http.Tests", Guid.NewGuid().ToString("N"));
    private readonly WireMockServer _service = WireMockServer.Start();

    private string Recording => HttpReplaySession.PathFor(_folder, "A test");

    public void Dispose()
    {
        _service.Stop();
        _service.Dispose();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task A_recorded_call_is_answered_in_replay_with_the_service_stopped()
    {
        _service.Given(Request.Create().WithPath("/todos").UsingGet())
            .RespondWith(Response.Create().WithHeader("Content-Type", "application/json").WithBody("""[{"id":1,"title":"Milk"}]"""));
        await RecordAsync(client => client.GetStringAsync(_service.Url + "/todos?done=false"));
        _service.Stop();

        var (session, client) = Replaying();
        var body = await client.GetStringAsync("http://elsewhere.test/todos?done=false");

        Assert.Equal("""[{"id":1,"title":"Milk"}]""", body);
        Assert.Equal("matched", Assert.Single(session.Calls).Outcome);
    }

    [Fact]
    public async Task A_call_never_recorded_misses_naming_the_nearest_and_how_it_differs()
    {
        _service.Given(Request.Create().WithPath("/todos").UsingPost()).RespondWith(Response.Create().WithStatusCode(201));
        await RecordAsync(client => client.PostAsync(_service.Url + "/todos", Json("""{"title":"Milk"}""")));

        var (session, client) = Replaying();
        var miss = await Assert.ThrowsAsync<HttpReplayMissException>(() => client.PostAsync("http://x.test/todos", Json("""{"title":"Bread"}""")));

        Assert.Contains("POST /todos", miss.Message, StringComparison.Ordinal);
        Assert.Contains("$.title is '\"Bread\"' where '\"Milk\"' was recorded", miss.Message, StringComparison.Ordinal);
        Assert.Throws<HttpReplayMissException>(session.ThrowIfMissed);
    }

    [Fact]
    public async Task Values_that_differ_between_two_recordings_are_volatile_and_match_anything()
    {
        _service.Given(Request.Create().WithPath("/chat").UsingPost()).RespondWith(Response.Create().WithBody("hello"));
        await RecordAsync(client => client.PostAsync(_service.Url + "/chat?ts=1", Json("""{"request_id":"a1","text":"hi"}""")));
        await RecordAsync(client => client.PostAsync(_service.Url + "/chat?ts=2", Json("""{"request_id":"b2","text":"hi"}""")));

        var exchange = Assert.Single(HttpRecording.Load(Recording)!.Exchanges);
        Assert.Equal(["$.request_id", "?ts"], exchange.Volatile);

        var (_, client) = Replaying();
        var answer = await client.PostAsync("http://x.test/chat?ts=3", Json("""{"request_id":"c3","text":"hi"}"""));
        Assert.Equal("hello", await answer.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_model_preset_matches_a_model_call_on_its_messages_from_one_recording()
    {
        _service.Given(Request.Create().WithPath("/openai/deployments/gpt/chat/completions").UsingPost())
            .RespondWith(Response.Create().WithHeader("Content-Type", "application/json").WithBody("""{"choices":[{"message":{"content":"Rain"}}]}"""));
        await RecordAsync(client => client.PostAsync(_service.Url + "/openai/deployments/gpt/chat/completions",
            Json("""{"messages":[{"role":"user","content":"Weather?"}],"temperature":0.7,"seed":1,"user":"u1"}""")));

        var options = new HttpReplayOptions().WithPreset(HttpReplayPresets.Model);
        var (_, client) = Replaying(options);
        var answer = await client.PostAsync("http://x.test/openai/deployments/gpt/chat/completions",
            Json("""{"messages":[{"role":"user","content":"Weather?"}],"temperature":0.2,"seed":9,"user":"u2"}"""));

        Assert.Contains("Rain", await answer.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var (_, plain) = Replaying();
        await Assert.ThrowsAsync<HttpReplayMissException>(() => plain.PostAsync("http://x.test/openai/deployments/gpt/chat/completions",
            Json("""{"messages":[{"role":"user","content":"Weather?"}],"temperature":0.2,"seed":9,"user":"u2"}""")));
    }

    [Fact]
    public async Task The_same_call_is_answered_in_recorded_order_and_the_last_answer_repeats()
    {
        _service.Given(Request.Create().WithPath("/job").UsingGet()).InScenario("job").WillSetStateTo("done")
            .RespondWith(Response.Create().WithBody("pending"));
        _service.Given(Request.Create().WithPath("/job").UsingGet()).InScenario("job").WhenStateIs("done")
            .RespondWith(Response.Create().WithBody("done"));
        await RecordAsync(async client =>
        {
            await client.GetStringAsync(_service.Url + "/job");
            await client.GetStringAsync(_service.Url + "/job");
        });

        var (_, client) = Replaying();
        Assert.Equal("pending", await client.GetStringAsync("http://x.test/job"));
        Assert.Equal("done", await client.GetStringAsync("http://x.test/job"));
        Assert.Equal("done", await client.GetStringAsync("http://x.test/job"));
    }

    [Fact]
    public async Task Keys_are_never_written_and_the_redacted_call_still_matches()
    {
        _service.Given(Request.Create().WithPath("/maps").UsingGet()).RespondWith(Response.Create().WithBody("tile"));
        await RecordAsync(async client =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _service.Url + "/maps?api-key=real-key-123&z=3");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "real-token-456");
            using var answer = await client.SendAsync(request);
        });

        var text = File.ReadAllText(Recording);
        Assert.DoesNotContain("real-key-123", text, StringComparison.Ordinal);
        Assert.DoesNotContain("real-token-456", text, StringComparison.Ordinal);
        Assert.Contains("api-key=***", text, StringComparison.Ordinal);

        var (_, client) = Replaying();
        Assert.Equal("tile", await client.GetStringAsync("http://x.test/maps?api-key=another-key&z=3"));
    }

    [Fact]
    public async Task A_recording_holding_what_looks_like_a_key_is_not_saved()
    {
        _service.Given(Request.Create().WithPath("/config").UsingGet())
            .RespondWith(Response.Create().WithBody("""{"token":"sk-proj-abcdefghijklmnopqrstuvwxyz0123"}"""));

        var session = new HttpReplaySession(HttpReplayMode.Record, Recording);
        using (var client = new HttpClient(HttpReplayHandler.Create(session)))
        {
            await client.GetStringAsync(_service.Url + "/config");
        }

        var refused = Assert.Throws<HttpRecordingSecretException>(session.Finish);
        Assert.Contains("response body", refused.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Recording));
    }

    [Fact]
    public async Task A_streamed_answer_replays_part_by_part_at_recorded_pace_when_asked()
    {
        new HttpRecording
        {
            Exchanges =
            [
                new HttpExchange
                {
                    Method = "POST",
                    Url = "https://model.test/chat",
                    ResponseHeaders = { ["Content-Type"] = "text/event-stream" },
                    Chunks =
                    [
                        new HttpChunk { Data = "data: {\"delta\":\"Hel\"}\n\n" },
                        new HttpChunk { DelayMs = 200, Data = "data: {\"delta\":\"lo\"}\n\n" },
                        new HttpChunk { Data = "data: [DONE]\n\n" },
                    ],
                },
            ],
        }.Save(Recording);

        var (_, client) = Replaying(new HttpReplayOptions { Pace = HttpReplayPace.Recorded });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://model.test/chat");
        using var answer = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        using var reader = new StreamReader(await answer.Content.ReadAsStreamAsync());
        var clock = Stopwatch.StartNew();
        var first = await reader.ReadLineAsync();
        var firstAt = clock.ElapsedMilliseconds;
        var rest = await reader.ReadToEndAsync();

        Assert.Equal("data: {\"delta\":\"Hel\"}", first);
        Assert.True(firstAt < 150, $"The first part came after {firstAt} ms, not at once.");
        Assert.True(clock.ElapsedMilliseconds >= 180, "The second part came without its recorded gap.");
        Assert.EndsWith("data: [DONE]\n\n", rest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_streamed_answer_is_recorded_as_parts()
    {
        _service.Given(Request.Create().WithPath("/stream").UsingPost())
            .RespondWith(Response.Create().WithHeader("Content-Type", "text/event-stream").WithBody("data: a\n\ndata: b\n\n"));
        await RecordAsync(client => client.PostAsync(_service.Url + "/stream", Json("{}")));

        var exchange = Assert.Single(HttpRecording.Load(Recording)!.Exchanges);
        Assert.NotNull(exchange.Chunks);
        Assert.Null(exchange.ResponseBody);
        Assert.Equal("data: a\n\ndata: b\n\n", string.Concat(exchange.Chunks!.Select(chunk => chunk.Data)));
    }

    [Fact]
    public async Task A_binary_answer_round_trips()
    {
        byte[] image = [0x89, 0x50, 0x4E, 0x47, 0x00, 0xFF, 0xFE];
        _service.Given(Request.Create().WithPath("/logo.png").UsingGet())
            .RespondWith(Response.Create().WithHeader("Content-Type", "image/png").WithBody(image));
        await RecordAsync(client => client.GetByteArrayAsync(_service.Url + "/logo.png"));

        var (_, client) = Replaying();
        Assert.Equal(image, await client.GetByteArrayAsync("http://x.test/logo.png"));
    }

    [Fact]
    public async Task A_hermetic_replay_refuses_a_host_the_recording_never_called()
    {
        _service.Given(Request.Create().WithPath("/todos").UsingGet()).RespondWith(Response.Create().WithBody("[]"));
        await RecordAsync(client => client.GetStringAsync(_service.Url + "/todos"));

        var (_, client) = Replaying(new HttpReplayOptions { Hermetic = true });
        var refused = await Assert.ThrowsAsync<HttpReplayMissException>(() => client.GetStringAsync("http://tracker.test/todos"));
        Assert.Contains("Hermetic", refused.Message, StringComparison.Ordinal);

        using var bare = new HttpClient(HermeticGuard.Handler());
        var violation = await Assert.ThrowsAnyAsync<HttpRequestException>(() => bare.GetStringAsync("http://example.test/"));
        Assert.True(HermeticGuard.IsViolation(violation));
        Assert.Contains("example.test", violation.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_factory_client_goes_through_the_session_after_the_apps_own_handlers()
    {
        _service.Given(Request.Create().WithPath("/todos").UsingGet().WithHeader("X-Tenant", "t1")).RespondWith(Response.Create().WithBody("[1]"));
        var recordSession = new HttpReplaySession(HttpReplayMode.Record, Recording);
        using (var provider = Services(recordSession))
        {
            var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("todos");
            Assert.Equal("[1]", await client.GetStringAsync(_service.Url + "/todos"));
        }

        recordSession.Finish();
        _service.Stop();

        var replay = new HttpReplaySession(HttpReplayMode.Replay, Recording, new HttpReplayOptions { Hermetic = true });
        using var replayProvider = Services(replay);
        var replayed = replayProvider.GetRequiredService<IHttpClientFactory>().CreateClient("todos");
        Assert.Equal("[1]", await replayed.GetStringAsync(_service.Url + "/todos"));
    }

    [Fact]
    public async Task Refresh_says_which_answers_changed()
    {
        _service.Given(Request.Create().WithPath("/price").UsingGet()).RespondWith(Response.Create().WithBody("""{"price":10}"""));
        await RecordAsync(client => client.GetStringAsync(_service.Url + "/price"));
        _service.Reset();
        _service.Given(Request.Create().WithPath("/price").UsingGet()).RespondWith(Response.Create().WithBody("""{"price":12}"""));

        var session = new HttpReplaySession(HttpReplayMode.Refresh, Recording);
        using (var client = new HttpClient(HttpReplayHandler.Create(session)))
        {
            await client.GetStringAsync(_service.Url + "/price");
        }

        session.Finish();
        var change = Assert.Single(session.Changes);
        Assert.Contains("GET /price", change, StringComparison.Ordinal);
        Assert.Contains("$.price", change, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_names_new_and_changed_recordings_and_stale_ones_after_a_replay()
    {
        _service.Given(Request.Create().WithPath("/a").UsingGet()).RespondWith(Response.Create().WithBody("1"));
        _service.Given(Request.Create().WithPath("/b").UsingGet()).RespondWith(Response.Create().WithBody("2"));
        await RecordAsync(async client =>
        {
            await client.GetStringAsync(_service.Url + "/a");
            await client.GetStringAsync(_service.Url + "/b");
        });

        Assert.Equal(["new http/A test.json"], HttpRecordingReview.Unreviewed(_folder));
        HttpRecordingReview.Accept(_folder);
        Assert.Empty(HttpRecordingReview.Unreviewed(_folder));

        var (session, client) = Replaying();
        await client.GetStringAsync("http://x.test/a");
        session.Finish();
        var stale = Assert.Single(HttpRecordingReview.Stale(_folder));
        Assert.Contains("1 recorded calls no longer made", stale, StringComparison.Ordinal);

        var recording = HttpRecording.Load(Recording)!;
        recording.Exchanges[0].ResponseBody = "changed";
        recording.Save(Recording);
        Assert.Equal(["changed http/A test.json"], HttpRecordingReview.Unreviewed(_folder));
    }

    [Fact]
    public void The_mode_comes_from_the_environment_variable()
    {
        Assert.Equal(HttpReplayMode.Off, HttpReplaySession.ModeFromEnvironment(_ => null));
        Assert.Equal(HttpReplayMode.Replay, HttpReplaySession.ModeFromEnvironment(_ => "replay"));
        Assert.Throws<ArgumentException>(() => HttpReplaySession.ModeFromEnvironment(_ => "sometimes"));
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private async Task RecordAsync(Func<HttpClient, Task> calls)
    {
        var session = new HttpReplaySession(HttpReplayMode.Record, Recording);
        using (var client = new HttpClient(HttpReplayHandler.Create(session)))
        {
            await calls(client);
        }

        session.Finish();
    }

    private (HttpReplaySession Session, HttpClient Client) Replaying(HttpReplayOptions? options = null)
    {
        var session = new HttpReplaySession(HttpReplayMode.Replay, Recording, options);
        return (session, new HttpClient(new HttpReplayHandler(session, new NeverCalled())));
    }

    private static ServiceProvider Services(HttpReplaySession session)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("todos").AddHttpMessageHandler(() => new TenantHeader());
        services.AddBruinetHttpReplay(session);
        return services.BuildServiceProvider();
    }

    // The app's own handler: what it adds is part of the request the replay sees.
    private sealed class TenantHeader : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Add("X-Tenant", "t1");
            return base.SendAsync(request, cancellationToken);
        }
    }

    // In replay nothing reaches the network.
    private sealed class NeverCalled : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException($"Replay reached the network: {request.RequestUri}");
    }
}
