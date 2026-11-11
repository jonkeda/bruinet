using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Bruinet.Mocking.Tests.Api;

public sealed class MockApiServerTests : IDisposable
{
    private readonly MockApiServer _server = MockApiServer.Start();
    private readonly HttpClient _client;

    public MockApiServerTests()
    {
        _client = new HttpClient { BaseAddress = new Uri(_server.BaseUrl) };
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Fact]
    public void Start_PicksAFreePort_AndReportsItsAddress()
    {
        Assert.True(_server.IsStarted);
        Assert.True(_server.Port > 0);
        Assert.Equal($"http://127.0.0.1:{_server.Port}", _server.BaseUrl);
    }

    [Fact]
    public async Task ReturnsJson_SerializesWithWebDefaults()
    {
        _server.Get("/api/items").ReturnsJson(new[] { new Item(1, "First") });

        var response = await _client.GetAsync("/api/items");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("""[{"id":1,"name":"First"}]""", body);
    }

    [Fact]
    public async Task ReturnsSample_ServesTheRecordedFileAsItIs()
    {
        _server.Get("/api/items").ReturnsSample("items/list.json");

        var body = await _client.GetStringAsync("/api/items");

        Assert.Equal(SampleFiles.Load("ContractSamples", "items/list.json"), body);
        Assert.Contains("\"recorded\": true", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ReturnsSample_WithAMissingFile_NamesWhereItLooked()
    {
        var failure = Assert.Throws<FileNotFoundException>(
            () => _server.Get("/api/items").ReturnsSample("items/missing.json"));

        Assert.Contains("items/missing.json", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wildcards_MatchEveryIdUnderAPath()
    {
        _server.Delete("/api/items/*").ReturnsStatus(204);

        var first = await _client.DeleteAsync("/api/items/1");
        var second = await _client.DeleteAsync("/api/items/abc");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task WithDelay_HoldsTheResponseBack()
    {
        var delay = TimeSpan.FromMilliseconds(300);
        _server.Get("/api/slow").WithDelay(delay).ReturnsText("done");

        var clock = Stopwatch.StartNew();
        var body = await _client.GetStringAsync("/api/slow");

        Assert.Equal("done", body);
        Assert.True(clock.Elapsed >= delay - TimeSpan.FromMilliseconds(20), $"Answered after {clock.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task FailsFirst_FailsTheGivenNumberOfTimes_ThenRecovers()
    {
        _server.Get("/api/flaky").FailsFirst(2, 503).ReturnsText("ok");

        var codes = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            codes.Add((await _client.GetAsync("/api/flaky")).StatusCode);
        }

        Assert.Equal(
            [HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK, HttpStatusCode.OK],
            codes);
    }

    [Fact]
    public async Task AtPriority_OverridesADefaultForOneTest()
    {
        _server.Get("/api/settings").ReturnsJson(new { mode = "default" });
        _server.Get("/api/settings").AtPriority(1).ReturnsJson(new { mode = "override" });

        var body = await _client.GetStringAsync("/api/settings");

        Assert.Equal("""{"mode":"override"}""", body);
    }

    [Fact]
    public async Task WithHeader_AnswersOnlyRequestsCarryingIt()
    {
        _server.Get("/api/secure").WithHeader("X-Api-Key", "secret").ReturnsText("in");
        _server.Get("/api/secure").AtPriority(20).ReturnsStatus(401);

        using var withKey = new HttpRequestMessage(HttpMethod.Get, "/api/secure");
        withKey.Headers.Add("X-Api-Key", "secret");

        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(withKey)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/secure")).StatusCode);
    }

    [Fact]
    public async Task WithQuery_AnswersOnlyThatParameterValue()
    {
        _server.Get("/api/search").WithQuery("q", "milk").ReturnsText("found");
        _server.Get("/api/search").AtPriority(20).ReturnsStatus(404);

        Assert.Equal("found", await _client.GetStringAsync("/api/search?q=milk"));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/search?q=bread")).StatusCode);
    }

    [Fact]
    public async Task EchoesRequestBody_ReturnsWhatWasSent()
    {
        _server.Put("/api/items/*").EchoesRequestBody();

        var response = await _client.PutAsJsonAsync("/api/items/7", new Item(7, "Seven"));

        Assert.Equal(new Item(7, "Seven"), await response.Content.ReadFromJsonAsync<Item>());
    }

    [Fact]
    public async Task RespondsWith_CanKeepStateBetweenRequests()
    {
        var stored = new List<string>();
        _server.Post("/api/notes").RespondsWith(request =>
        {
            lock (stored)
            {
                stored.Add(request.Body ?? string.Empty);
                return MockResponse.Status(201);
            }
        });
        _server.Get("/api/notes").RespondsWith(_ =>
        {
            lock (stored)
            {
                return MockResponse.Json(stored.ToArray());
            }
        });

        await _client.PostAsync("/api/notes", new StringContent("a", Encoding.UTF8, "text/plain"));
        await _client.PostAsync("/api/notes", new StringContent("b", Encoding.UTF8, "text/plain"));

        Assert.Equal(["a", "b"], await _client.GetFromJsonAsync<string[]>("/api/notes") ?? []);
    }

    [Fact]
    public async Task RequestQueries_CountByMethodPathAndBody()
    {
        _server.Put("/api/items/*").ReturnsStatus(200);

        await _client.PutAsJsonAsync("/api/items/1", new Item(1, "One"));
        await _client.PutAsJsonAsync("/api/items/2", new Item(2, "Two"));
        await _client.GetAsync("/api/items/1");

        Assert.Equal(2, _server.RequestCount("PUT", "/api/items/*"));
        Assert.Equal(3, _server.RequestCount(null, "/api/items/*"));
        Assert.Equal(1, _server.JsonRequestCount("PUT", "/api/items/*", json => json.GetProperty("name").GetString() == "Two"));
        Assert.Equal(["/api/items/1", "/api/items/2"], _server.Requests("PUT", "/api/items/*").Select(request => request.Path));
        Assert.Equal(new Item(1, "One"), _server.Requests("PUT", "/api/items/1").Single().BodyAs<Item>());
    }

    [Fact]
    public async Task FormRequestCount_ReadsUrlEncodedFields()
    {
        _server.Post("/login").ReturnsStatus(200);

        await _client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["user"] = "ann smith",
            ["remember"] = "true",
        }));

        Assert.Equal(1, _server.FormRequestCount("POST", "/login", form => form["user"] == "ann smith"));
    }

    [Fact]
    public async Task RecordedRequest_KeepsHeaders()
    {
        _server.Get("/api/items").ReturnsStatus(200);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/items");
        request.Headers.Add("X-Api-Key", "secret");
        await _client.SendAsync(request);

        Assert.Equal("secret", _server.Requests("GET", "/api/items").Single().Headers["x-api-key"]);
    }

    [Fact]
    public async Task Reset_RemovesStubsAndForgetsRequests()
    {
        _server.Get("/api/items").ReturnsText("before");
        await _client.GetAsync("/api/items");

        _server.Reset();

        Assert.Empty(_server.Requests());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/items")).StatusCode);
    }

    [Fact]
    public async Task Reset_RestartsFailFirstScenarios()
    {
        _server.Get("/api/flaky").FailsFirst(1).ReturnsText("ok");
        await _client.GetAsync("/api/flaky");

        _server.Reset();
        _server.Get("/api/flaky").FailsFirst(1).ReturnsText("ok");

        Assert.Equal(HttpStatusCode.InternalServerError, (await _client.GetAsync("/api/flaky")).StatusCode);
    }

    [Fact]
    public async Task WaitForRequest_ReturnsARequestThatArrivesLater()
    {
        _server.Put("/api/items/*").ReturnsStatus(200);

        var sender = Task.Run(async () =>
        {
            await Task.Delay(150);
            await _client.PutAsJsonAsync("/api/items/9", new Item(9, "Late"));
        });

        var request = _server.WaitForRequest("PUT", "/api/items/*", timeout: TimeSpan.FromSeconds(5));
        await sender;

        Assert.Equal("/api/items/9", request.Path);
    }

    [Fact]
    public async Task WaitForRequest_AppliesThePredicateAndSkip()
    {
        _server.Put("/api/items/*").ReturnsStatus(200);
        await _client.PutAsJsonAsync("/api/items/1", new Item(1, "A"));
        await _client.PutAsJsonAsync("/api/items/2", new Item(2, "B"));
        await _client.PutAsJsonAsync("/api/items/3", new Item(3, "B"));

        var second = _server.WaitForRequest(
            "PUT",
            "/api/items/*",
            request => request.BodyAs<Item>()?.Name == "B",
            TimeSpan.FromSeconds(1),
            skip: 1);

        Assert.Equal("/api/items/3", second.Path);
    }

    [Fact]
    public async Task WaitForRequest_WhenNothingArrives_ListsWhatDid()
    {
        _server.Get("/api/items").ReturnsStatus(200);
        await _client.GetAsync("/api/items");

        var failure = Assert.Throws<TimeoutException>(
            () => _server.WaitForRequest("PUT", "/api/items/*", timeout: TimeSpan.FromMilliseconds(100)));

        Assert.Contains("PUT /api/items/*", failure.Message, StringComparison.Ordinal);
        Assert.Contains("GET /api/items", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WaitForRequestAsync_ReturnsARequestThatArrivesLater()
    {
        _server.Delete("/api/items/*").ReturnsStatus(204);

        var waiting = _server.WaitForRequestAsync("DELETE", "/api/items/*", timeout: TimeSpan.FromSeconds(5));
        await _client.DeleteAsync("/api/items/4");

        Assert.Equal("/api/items/4", (await waiting).Path);
    }

    [Fact]
    public async Task WritesSince_GivesEveryWriteAfterTheMoment_NoGets_OldestFirst()
    {
        _server.Post("/api/items").ReturnsStatus(201);
        _server.Get("/api/items").ReturnsJson(Array.Empty<Item>());
        _server.Delete("/api/items/*").ReturnsStatus(204);
        var before = DateTime.UtcNow.AddSeconds(-1);

        await _client.PostAsJsonAsync("/api/items", new Item(1, "First"));
        await _client.GetAsync("/api/items");
        await _client.DeleteAsync("/api/items/1");

        var writes = _server.WritesSince(before);
        Assert.Equal([("POST", "/api/items"), ("DELETE", "/api/items/1")], writes.Select(write => (write.Method, write.Path)));
        Assert.Equal(("application/json", """{"id":1,"name":"First"}"""), (writes[0].ContentType, writes[0].Body));
        Assert.Empty(_server.WritesSince(DateTime.UtcNow.AddMinutes(1)));
    }

    private sealed record Item(int Id, string Name);
}
