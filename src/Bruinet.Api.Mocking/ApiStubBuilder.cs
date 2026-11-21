using System.ComponentModel;
using System.Text.Json;
using WireMock;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Types;
using WireMock.Util;

namespace Bruinet.Api.Mocking;

/// <summary>
/// Describes one stub on a <see cref="MockApiServer"/>: which requests it answers, and how.
/// </summary>
/// <remarks>
/// <para>
/// Conditions (<see cref="WithHeader"/>, <see cref="WithQuery"/>), timing (<see cref="WithDelay"/>)
/// and precedence (<see cref="AtPriority"/>) come first; a <c>Returns…</c>, <c>Fails</c> or
/// <see cref="RespondsWith"/> call registers the stub and ends the chain.
/// </para>
/// <para>
/// <b>Priority: a lower number wins.</b> Register a test's defaults at the default priority and
/// override one endpoint for one test with <c>AtPriority(1)</c> - the way
/// <c>Exact.Construction.UITests</c> overrides project settings for a single test.
/// </para>
/// </remarks>
public sealed class ApiStubBuilder
{
    /// <summary>The priority a stub gets when none is set. Lower numbers win.</summary>
    public const int DefaultPriority = 10;

    private readonly MockApiServer _owner;
    private readonly IRequestBuilder _request;
    private readonly string _description;
    private int _priority = DefaultPriority;
    private TimeSpan? _delay;
    private int _failuresFirst;
    private int _failureStatus;

    internal ApiStubBuilder(MockApiServer owner, string method, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _owner = owner;
        _request = MockApiServer.CreateMatcher(method, path);
        _description = $"{method.ToUpperInvariant()} {path}";
    }

    /// <summary>
    /// Answers only requests carrying this header value; others fall through to the next stub,
    /// or to WireMock's 404.
    /// </summary>
    public ApiStubBuilder WithHeader(string name, string value)
    {
        _request.WithHeader(name, value, MatchBehaviour.AcceptOnMatch);
        return this;
    }

    /// <summary>Answers only requests with this query parameter value.</summary>
    public ApiStubBuilder WithQuery(string name, string value)
    {
        _request.WithParam(name, value);
        return this;
    }

    /// <summary>Sets the precedence; a lower number wins over the default of <see cref="DefaultPriority"/>.</summary>
    public ApiStubBuilder AtPriority(int priority)
    {
        _priority = priority;
        return this;
    }

    /// <summary>Delays every response of this stub, to show loading states or to trip timeouts.</summary>
    public ApiStubBuilder WithDelay(TimeSpan delay)
    {
        _delay = delay;
        return this;
    }

    /// <summary>
    /// Fails the first <paramref name="times"/> requests with <paramref name="statusCode"/>, then
    /// answers as the rest of the chain says: "the server is down, then it recovers".
    /// </summary>
    public ApiStubBuilder FailsFirst(int times, int statusCode = 500)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(times);

        _failuresFirst = times;
        _failureStatus = statusCode;
        return this;
    }

    /// <summary>Answers with <paramref name="body"/>, a contract type of the API, serialized as JSON.</summary>
    /// <typeparam name="TBody">The API's DTO (or a collection of them). Not an anonymous type: see AD-012.</typeparam>
    /// <param name="body">The body; serialized with <paramref name="options"/> as its runtime type.</param>
    /// <param name="statusCode">The status code; 200 by default.</param>
    /// <param name="options">Serializer options; <see cref="MockApiServer.DefaultJsonOptions"/> when omitted.</param>
    public MockApiServer ReturnsJson<TBody>(TBody body, int statusCode = 200, JsonSerializerOptions? options = null)
        => ReturnsJson((object?)body, statusCode, options);

    /// <summary>Answers with <paramref name="body"/> serialized as JSON.</summary>
    /// <param name="body">The body; serialized with <paramref name="options"/>.</param>
    /// <param name="statusCode">The status code; 200 by default.</param>
    /// <param name="options">Serializer options; <see cref="MockApiServer.DefaultJsonOptions"/> when omitted.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public MockApiServer ReturnsJson(object? body, int statusCode = 200, JsonSerializerOptions? options = null)
        => Register(Response.Create()
            .WithStatusCode(statusCode)
            .WithHeader("Content-Type", "application/json")
            .WithBody(JsonSerializer.Serialize(body, body?.GetType() ?? typeof(object), options ?? MockApiServer.DefaultJsonOptions)));

    /// <summary>Answers with <paramref name="body"/> as it is.</summary>
    public MockApiServer ReturnsText(string body, int statusCode = 200, string contentType = "text/plain")
        => Register(Response.Create()
            .WithStatusCode(statusCode)
            .WithHeader("Content-Type", contentType)
            .WithBody(body));

    /// <summary>Answers with a status code and no body.</summary>
    public MockApiServer ReturnsStatus(int statusCode)
        => Register(Response.Create().WithStatusCode(statusCode));

    /// <summary>Answers with a server error. The same as <see cref="ReturnsStatus"/>, named for intent.</summary>
    public MockApiServer Fails(int statusCode = 500) => ReturnsStatus(statusCode);

    /// <summary>
    /// Answers with a recorded response file, served as it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file is looked up under <see cref="MockApiServerOptions.SamplesDirectory"/>, first in the
    /// test output folder and then in each folder above it, so a sample works whether it is copied
    /// to the output or only exists in the source tree.
    /// </para>
    /// <para>
    /// A recorded response is what the real API actually sent, which a hand-written body only
    /// approximates. It can still go stale; a contract test that checks each sample against the
    /// real API is what keeps it honest.
    /// </para>
    /// </remarks>
    /// <param name="relativePath">The file, relative to the samples folder, e.g. <c>todos/list.json</c>.</param>
    /// <param name="statusCode">The status code; 200 by default.</param>
    public MockApiServer ReturnsSample(string relativePath, int statusCode = 200)
        => Register(Response.Create()
            .WithStatusCode(statusCode)
            .WithHeader("Content-Type", "application/json")
            .WithBody(SampleFiles.Load(_owner.Options.SamplesDirectory, relativePath)));

    /// <summary>Answers with the request's own body, as a PUT that stores what it is sent would.</summary>
    public MockApiServer EchoesRequestBody(int statusCode = 200)
        => RespondsWith(request => new MockResponse(statusCode, request.Body));

    /// <summary>
    /// Answers with whatever <paramref name="respond"/> computes from the request.
    /// </summary>
    /// <remarks>
    /// For a stub with state of its own - a fake backend that remembers what was stored and
    /// returns it from the next <c>GET</c>. Called on WireMock's request thread; keep it quick
    /// and lock any state it shares.
    /// </remarks>
    public MockApiServer RespondsWith(Func<RecordedRequest, MockResponse> respond)
    {
        ArgumentNullException.ThrowIfNull(respond);

        return Register(Response.Create().WithCallback(message =>
        {
            var answer = respond(RecordedRequest.From(message));
            var response = new ResponseMessage
            {
                StatusCode = answer.StatusCode,
            };

            if (answer.Body is not null)
            {
                response.BodyData = new BodyData
                {
                    BodyAsString = answer.Body,
                    DetectedBodyType = BodyType.String,
                };
                response.AddHeader("Content-Type", answer.ContentType);
            }

            return response;
        }));
    }

    private MockApiServer Register(IResponseBuilder response)
    {
        if (_delay is { } delay)
        {
            response = response.WithDelay(delay);
        }

        var server = _owner.Server;

        if (_failuresFirst == 0)
        {
            server.Given(_request).AtPriority(_priority).RespondWith(response);
            return _owner;
        }

        // A private scenario with one named state per failure and a final "recovered" state, set
        // explicitly to the first failure. Every mapping names the state it answers in: a mapping
        // relying on WireMock's "not started" state was measured to match again after recovery,
        // failing every other request. A fresh name per stub keeps two such stubs apart.
        var scenario = $"{_description} fails first {Guid.NewGuid():N}";
        const string recovered = "recovered";

        for (var failure = 1; failure <= _failuresFirst; failure++)
        {
            var failing = Response.Create().WithStatusCode(_failureStatus);
            server.Given(_request)
                .AtPriority(_priority)
                .InScenario(scenario)
                .WhenStateIs(FailingState(failure))
                .WillSetStateTo(failure == _failuresFirst ? recovered : FailingState(failure + 1))
                .RespondWith(_delay is { } failureDelay ? failing.WithDelay(failureDelay) : failing);
        }

        // Sets its own state again: WireMock applies a mapping's next state after every match, and
        // a mapping without one clears the state, so a bare "recovered" answer works exactly once.
        server.Given(_request)
            .AtPriority(_priority)
            .InScenario(scenario)
            .WhenStateIs(recovered)
            .WillSetStateTo(recovered)
            .RespondWith(response);

        server.SetScenarioState(scenario, FailingState(1));

        return _owner;

        static string FailingState(int failure) => $"failing-{failure}";
    }
}

/// <summary>
/// What a <see cref="ApiStubBuilder.RespondsWith"/> callback answers.
/// </summary>
/// <param name="StatusCode">The HTTP status code.</param>
/// <param name="Body">The body, or <c>null</c> for none.</param>
/// <param name="ContentType">The body's content type.</param>
public sealed record MockResponse(int StatusCode, string? Body = null, string ContentType = "application/json")
{
    /// <summary>A JSON body of a contract type, serialized with <see cref="MockApiServer.DefaultJsonOptions"/> unless given others.</summary>
    /// <typeparam name="TBody">The API's DTO (or a collection of them). Not an anonymous type: see AD-012.</typeparam>
    public static MockResponse Json<TBody>(TBody body, int statusCode = 200, JsonSerializerOptions? options = null)
        => Json((object?)body, statusCode, options);

    /// <summary>A JSON body, serialized with <see cref="MockApiServer.DefaultJsonOptions"/> unless given others.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static MockResponse Json(object? body, int statusCode = 200, JsonSerializerOptions? options = null)
        => new(statusCode, JsonSerializer.Serialize(body, body?.GetType() ?? typeof(object), options ?? MockApiServer.DefaultJsonOptions));

    /// <summary>A status code with no body.</summary>
    public static MockResponse Status(int statusCode) => new(statusCode);
}
