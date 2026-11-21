using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Bruinet.Core.Api;
using Bruinet.Core.Testing;

namespace Bruinet.Core.Tests.Api;

/// <summary>
/// The API session: the hosts it may call, the account that never writes, every call kept as an exchange with its
/// secrets masked, a renewed sign-in, and deleting only what carries a run's marker.
/// </summary>
public sealed class ApiSessionTests
{
    private static readonly string Marked = RunMarker.For(new DateTime(2026, 11, 21, 9, 30, 0)) + " Arend";

    [Fact]
    public void A_host_the_suite_did_not_name_is_refused_before_any_call()
    {
        using var service = new FakeService();

        var refused = Assert.Throws<ApiCallException>(() => new ApiSession(Options(service, "https://api.example.com"), "Creator", writes: true));
        Assert.Contains("api.example.com is not a host this suite may call", refused.Message, StringComparison.Ordinal);

        using var session = Session(service);
        Assert.Throws<ApiCallException>(() => session.Get("https://api.example.com/contact/1"));
        Assert.Empty(service.Calls);
    }

    [Fact]
    public void A_star_at_the_start_of_a_host_allows_what_ends_so()
    {
        var guard = new ApiEnvironmentGuard(["*-acc.example.com", "localhost"]);

        Assert.True(guard.Allows("api-acc.example.com"));
        Assert.True(guard.Allows("LOCALHOST"));
        Assert.False(guard.Allows("api.example.com"));
        Assert.Throws<ArgumentException>(() => new ApiEnvironmentGuard([" "]));
    }

    [Fact]
    public void An_account_that_never_writes_sends_no_write()
    {
        using var service = new FakeService();
        using var session = Session(service, writes: false, readPaths: ["/search/*"]);

        var refused = Assert.Throws<ApiCallException>(() => session.Post("/contact", new JsonObject { ["name"] = Marked }));

        Assert.Equal("Reader never writes (TestAccounts.md): POST /contact is refused.", refused.Message);
        Assert.Empty(service.Calls);
        session.Get("/contact/1");
        session.Post("/search/contacts", new JsonObject { ["name"] = "Arend" });
        Assert.Equal(["GET /contact/1", "POST /search/contacts"], service.Calls);
    }

    [Fact]
    public void A_read_of_what_is_not_there_is_null_and_another_error_names_the_call()
    {
        using var service = new FakeService();
        service.On("GET", "/contact/7", HttpStatusCode.NotFound, string.Empty);
        service.On("GET", "/contact/8", HttpStatusCode.InternalServerError, """{"message":"boom"}""");
        using var session = Session(service);

        Assert.Null(session.Get("/contact/7"));
        var failed = Assert.Throws<ApiCallException>(() => session.Get("/contact/8"));

        Assert.Equal("""GET /contact/8 as Creator answered 500: {"message":"boom"}""", failed.Message);
        Assert.Equal(500, failed.Exchange?.StatusCode);
    }

    [Fact]
    public void Every_call_is_kept_as_an_exchange_with_its_secrets_masked()
    {
        var log = Path.Combine(Path.GetTempPath(), $"bruinet-api-{Guid.NewGuid():N}.jsonl");
        try
        {
            using var service = new FakeService();
            service.On("POST", "/login", HttpStatusCode.OK, """{"id":5,"token":"abc","user":{"password":"p"}}""");
            using var session = new ApiSession(Options(service, exchangeLog: log), "Creator", writes: true);

            session.Post("/login?next=home", new JsonObject { ["name"] = "Arend", ["password"] = "geheim" });

            var exchange = Assert.Single(session.Exchanges);
            Assert.Equal("/login", exchange.Path);
            Assert.Equal("Creator", exchange.Account);
            Assert.Equal("""{"name":"Arend","password":"***"}""", exchange.RequestBody);
            Assert.Equal("""{"id":5,"token":"***","user":{"password":"***"}}""", exchange.ResponseBody);
            Assert.DoesNotContain("geheim", File.ReadAllText(log), StringComparison.Ordinal);
            var read = Assert.Single(ApiExchangeLog.Read(log));
            Assert.Equal(exchange.Url, read.Url);
            Assert.Equal(200, read.StatusCode);
        }
        finally
        {
            File.Delete(log);
        }
    }

    [Fact]
    public void A_refused_sign_in_is_renewed_once_and_the_call_made_again()
    {
        using var service = new FakeService { Accepts = "second" };
        var tokens = new Queue<string>(["first", "second"]);
        using var session = new ApiSession(Options(service), "Creator", writes: true, new BearerSignIn(tokens.Dequeue));

        var answer = session.Get("/contact/1");

        Assert.NotNull(answer);
        Assert.Equal(["GET /contact/1", "GET /contact/1"], service.Calls);
        Assert.Equal([401, 200], session.Exchanges.Select(exchange => exchange.StatusCode ?? 0));
    }

    [Fact]
    public async Task A_generated_client_on_the_sessions_http_client_is_guarded_signed_and_kept_too()
    {
        using var service = new FakeService { Accepts = "token" };
        using var session = new ApiSession(Options(service), "Reader", writes: false, new BearerSignIn(() => "token"));

        using var answer = await session.Client.GetAsync(new Uri("/contact/1", UriKind.Relative));
        var body = await answer.Content.ReadAsStringAsync();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        await Assert.ThrowsAsync<ApiCallException>(() => session.Client.PostAsync(new Uri("/contact", UriKind.Relative), content));
        await Assert.ThrowsAsync<ApiCallException>(() => session.Client.GetAsync(new Uri("https://api.example.com/contact/1")));

        Assert.Equal("{}", body);
        Assert.Equal(["GET /contact/1"], service.Calls);
        Assert.Equal("GET /contact/1 as Reader answered 200", Assert.Single(session.Exchanges).Describe());
    }

    [Fact]
    public void A_token_is_fetched_again_a_minute_before_it_runs_out()
    {
        var now = new DateTimeOffset(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);
        var fetched = 0;
        var signIn = new BearerSignIn(() => Token(now.AddMinutes(10 * ++fetched)), () => now);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api-acc.example.com/");

        signIn.Apply(request);
        signIn.Apply(request);
        Assert.Equal(1, fetched);

        now = now.AddMinutes(9).AddSeconds(30);
        signIn.Apply(request);
        Assert.Equal(2, fetched);
        Assert.Null(BearerSignIn.Expiry("not-a-token"));
    }

    [Fact]
    public void Only_a_record_the_service_says_is_marked_is_deleted()
    {
        using var service = new FakeService();
        service.On("GET", "/contact/1", HttpStatusCode.OK, $$"""{"id":1,"name":"{{Marked}}"}""");
        service.On("GET", "/contact/2", HttpStatusCode.OK, """{"id":2,"name":"Bouwbedrijf Jansen"}""");
        service.On("GET", "/contact/3", HttpStatusCode.NotFound, string.Empty);
        using var session = Session(service);

        Assert.True(session.DeleteMarked(Contacts(), "1"));
        Assert.False(session.DeleteMarked(Contacts(), "3"));
        var refused = Assert.Throws<ApiCallException>(() => session.DeleteMarked(Contacts(), "2"));

        Assert.Equal("The contact 2 is 'Bouwbedrijf Jansen', not one a test made: it is not deleted.", refused.Message);
        Assert.Equal(["DELETE /contact/1"], service.Calls.Where(call => call.StartsWith("DELETE", StringComparison.Ordinal)));
    }

    [Fact]
    public void A_sweep_deletes_the_marked_and_names_what_must_stay()
    {
        using var service = new FakeService();
        service.On("GET", "/contacts", HttpStatusCode.OK, $$"""[{"id":1,"name":"{{Marked}}"},{"id":2,"name":"Bouwbedrijf Jansen"},{"id":4,"name":"{{Marked}} kept"}]""");
        service.On("GET", "/contact/1", HttpStatusCode.OK, $$"""{"id":1,"name":"{{Marked}}"}""");
        service.On("GET", "/contact/4", HttpStatusCode.OK, $$"""{"id":4,"name":"{{Marked}} kept"}""");
        using var session = Session(service);
        var contacts = Contacts(keep: record => (long?)record["id"] == 4 ? "a project refers to it" : null);

        var deleted = session.Sweep(contacts);

        Assert.Equal([Marked], deleted);
        Assert.Equal([$"{Marked} kept (#4): a project refers to it"], session.Kept);
        Assert.DoesNotContain("GET /contact/2", service.Calls);
    }

    [Fact]
    public void What_was_made_through_the_session_goes_when_it_is_disposed()
    {
        using var service = new FakeService();
        service.On("GET", "/contact/1", HttpStatusCode.OK, $$"""{"id":1,"name":"{{Marked}}"}""");
        var session = Session(service);

        session.Made(Contacts(), "1");
        Assert.DoesNotContain("DELETE /contact/1", service.Calls);
        session.Dispose();

        Assert.Contains("DELETE /contact/1", service.Calls);
        Assert.Empty(session.CleanUp.Failures);
    }

    [Fact]
    public void A_marker_in_any_text_field_counts_when_the_resource_says_so()
    {
        var absences = new MarkedResource("absence", _ => [], (_, _) => null, null) { MarkerField = "*" };

        Assert.Equal($"{Marked} off", absences.MarkedText(new JsonObject { ["id"] = 9, ["note"] = "x", ["description"] = $"{Marked} off" }));
        Assert.Null(absences.MarkedText(new JsonObject { ["id"] = 9, ["description"] = "holiday" }));
        Assert.Equal("9", absences.Id(new JsonObject { ["id"] = 9 }));
        Assert.Equal("a-9", absences.Id(new JsonObject { ["id"] = "a-9" }));
    }

    [Fact]
    public void A_seed_makes_what_is_not_there_and_leaves_what_is()
    {
        using var service = new FakeService();
        using var session = Session(service);
        var there = new HashSet<string> { "Seed client" };
        SeedRecord[] seed =
        [
            new("Seed client", _ => there.Contains("Seed client"), _ => there.Add("Seed client")),
            new("Seed project", _ => there.Contains("Seed project"), _ => there.Add("Seed project")),
        ];

        Assert.Equal(["Seed project"], ApiSeed.Ensure(session, seed));
        Assert.Empty(ApiSeed.Ensure(session, seed));
    }

    private static MarkedResource Contacts(Func<JsonObject, string?>? keep = null)
        => new(
            "contact",
            session => [.. (session.Get("/contacts")?.AsArray() ?? []).OfType<JsonObject>()],
            (session, id) => session.Get($"/contact/{id}") as JsonObject,
            (session, record) => session.Delete($"/contact/{record["id"]}"))
        {
            Keep = keep is null ? null : (_, record) => keep(record),
        };

    private static ApiSession Session(FakeService service, bool writes = true, IReadOnlyList<string>? readPaths = null)
        => new(Options(service, readPaths: readPaths), writes ? "Creator" : "Reader", writes);

    private static ApiSessionOptions Options(FakeService service, string address = "https://api-acc.example.com", string? exchangeLog = null, IReadOnlyList<string>? readPaths = null)
        => new()
        {
            BaseAddress = new Uri(address),
            AllowedHosts = ["*-acc.example.com"],
            Handler = service,
            ExchangeLog = exchangeLog,
            ReadPaths = readPaths ?? [],
        };

    private static string Token(DateTimeOffset until)
    {
        static string Part(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{Part("""{"alg":"none"}""")}.{Part($$"""{"exp":{{until.ToUnixTimeSeconds()}}}""")}.sig";
    }

    /// <summary>A service that answers what a test told it to, and <c>{}</c> otherwise.</summary>
    private sealed class FakeService : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _answers = new(StringComparer.Ordinal);

        public List<string> Calls { get; } = [];

        /// <summary>The bearer token the service takes; any when null.</summary>
        public string? Accepts { get; init; }

        public void On(string method, string path, HttpStatusCode status, string body) => _answers[$"{method} {path}"] = (status, body);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = $"{request.Method} {request.RequestUri!.AbsolutePath}";
            Calls.Add(call);
            if (Accepts is not null && request.Headers.Authorization?.Parameter != Accepts)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent(string.Empty) });
            }

            var (status, body) = _answers.TryGetValue(call, out var answer) ? answer : (HttpStatusCode.OK, "{}");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
