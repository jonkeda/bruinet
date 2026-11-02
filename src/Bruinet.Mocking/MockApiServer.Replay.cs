using System.Globalization;
using System.Text;
using Bruinet.Mocking.Http;
using WireMock;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Types;
using WireMock.Util;

namespace Bruinet.Mocking;

/// <summary>
/// HTTP replay for an app in a process of its own (AI11): its base URLs point here, and the server records by proxying
/// to the real service or answers from a recording. The recording is the in-process handler's format, so one made either
/// way replays either way.
/// </summary>
public sealed partial class MockApiServer
{
    /// <summary>The priority of the record proxy and the replay's miss answer: below every stub, so a stub still wins.</summary>
    private const int CatchAllPriority = int.MaxValue - 1;

    /// <summary>The status a replay answers a call it has no recording for, with the miss as its body.</summary>
    public const int ReplayMissStatus = 599;

    private readonly Lock _replayGate = new();
    private string? _recordTarget;
    private Guid _recordMapping = Guid.NewGuid();

    /// <summary>
    /// Records: every call no stub answers goes on to <paramref name="target"/> (the real service) and its answer comes back;
    /// <see cref="CopyRecordedTo"/> hands the calls to a test's session.
    /// </summary>
    /// <param name="target">The real service's base URL, such as <c>https://api.example.com</c>.</param>
    public void Record(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        Guid mapping;
        lock (_replayGate)
        {
            // Recording again replaces the proxy: one catch-all, to the newest target.
            _server.DeleteMapping(_recordMapping);
            _recordTarget = target.TrimEnd('/');
            _recordMapping = mapping = Guid.NewGuid();
        }

        _server.Given(Request.Create().UsingAnyMethod().WithPath(new WildcardMatcher("*")))
            .WithGuid(mapping)
            .AtPriority(CatchAllPriority)
            .RespondWith(Response.Create().WithProxy(target.TrimEnd('/')));
    }

    /// <summary>The calls the proxy answered since <see cref="Record"/>, in order, added to <paramref name="session"/>; then call its <c>Finish</c>.</summary>
    public void CopyRecordedTo(HttpReplaySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        string target;
        Guid mapping;
        lock (_replayGate)
        {
            target = _recordTarget ?? throw new InvalidOperationException("Nothing was recorded: call Record(target) first.");
            mapping = _recordMapping;
        }

        // Only what the proxy answered: a call a hand-written stub answered is the test's, not the service's.
        foreach (var entry in _server.LogEntries.Where(entry => entry.RequestMessage is not null && entry.ResponseMessage is not null && entry.MappingGuid == mapping)
                     .OrderBy(entry => entry.RequestMessage!.DateTime))
        {
            var request = entry.RequestMessage!;
            var response = entry.ResponseMessage!;
            var exchange = new HttpExchange
            {
                Method = request.Method,
                Url = target + new Uri(request.Url).PathAndQuery,
                Status = Convert.ToInt32(response.StatusCode, CultureInfo.InvariantCulture),
                RequestBody = request.Body,
            };
            foreach (var (name, values) in request.Headers ?? new Dictionary<string, WireMock.Types.WireMockList<string>>())
            {
                if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) || session.Options.RedactedHeaders.Contains(name))
                {
                    exchange.RequestHeaders[name] = string.Join(", ", values);
                }
            }

            foreach (var (name, values) in response.Headers ?? new Dictionary<string, WireMock.Types.WireMockList<string>>())
            {
                if (!name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) && !name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
                    && !name.Equals("Date", StringComparison.OrdinalIgnoreCase))
                {
                    exchange.ResponseHeaders[name] = string.Join(", ", values);
                }
            }

            if (response.BodyData?.BodyAsString is { } text)
            {
                exchange.ResponseBody = text;
            }
            else if (response.BodyData?.BodyAsBytes is { Length: > 0 } bytes)
            {
                exchange.ResponseBody = Convert.ToBase64String(bytes);
                exchange.ResponseBodyEncoding = "base64";
            }

            session.Add(exchange);
        }
    }

    /// <summary>
    /// Replays <paramref name="recording"/>: each recorded call answered as recorded, the same call in recorded order (the
    /// last answer repeating), matched as the in-process handler matches. A call it cannot answer gets
    /// <see cref="ReplayMissStatus"/> with the miss as its body, and is listed by <see cref="ReplayMisses"/>.
    /// </summary>
    public void Replay(HttpRecording recording, HttpReplayOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(recording);
        var ignored = (IReadOnlyCollection<string>)(options ?? new HttpReplayOptions()).IgnoredFields;
        var exchanges = recording.Exchanges;
        // Each exchange's group: the first recorded exchange it is the same call as.
        var groups = exchanges.GroupBy(exchange => exchanges.FindIndex(first => HttpMatching.Matches(exchange, first, ignored)));
        foreach (var group in groups)
        {
            // One mapping per call, its answers handed out in recorded order by a counter of its own, not WireMock's
            // scenarios: with those, the second of two calls made one after the other still got the first answer.
            var answers = group.ToList();
            var next = -1;
            _server.Given(RequestFor(answers[0], ignored)).RespondWith(Response.Create().WithCallback(_ =>
                ResponseFor(answers[Math.Min(Interlocked.Increment(ref next), answers.Count - 1)])));
        }

        _server.Given(Request.Create().UsingAnyMethod().WithPath(new WildcardMatcher("*")))
            .AtPriority(CatchAllPriority)
            .RespondWith(Response.Create().WithStatusCode(ReplayMissStatus).WithBody(request =>
                $"HTTP replay miss: {request.Method} {new Uri(request.Url).PathAndQuery} was never recorded."));
    }

    /// <summary>The calls a replay could not answer, as <c>METHOD /path?query</c>, oldest first.</summary>
    public IReadOnlyList<string> ReplayMisses()
        => [.. _server.LogEntries
            .Where(entry => entry.ResponseMessage is not null && Convert.ToInt32(entry.ResponseMessage.StatusCode, CultureInfo.InvariantCulture) == ReplayMissStatus)
            .OrderBy(entry => entry.RequestMessage!.DateTime)
            .Select(entry => entry.RequestMessage!.Method + " " + new Uri(entry.RequestMessage.Url).PathAndQuery)];

    private static IRequestBuilder RequestFor(HttpExchange exchange, IReadOnlyCollection<string> ignored)
    {
        var request = Request.Create().UsingMethod(exchange.Method.ToUpperInvariant()).WithPath(HttpMatching.Path(exchange.Url));
        // The query and body as the handler compares them: the live call is the recorded one with its own query, then body.
        var withQuery = request.WithUrl(url => HttpMatching.Matches(
            new HttpExchange { Method = exchange.Method, Url = new Uri(url).PathAndQuery, RequestBody = exchange.RequestBody, RequestBodyEncoding = exchange.RequestBodyEncoding },
            exchange,
            ignored));
        return string.IsNullOrEmpty(exchange.RequestBody)
            ? withQuery
            : withQuery.WithBody((string? body) => HttpMatching.Matches(
                new HttpExchange { Method = exchange.Method, Url = exchange.Url, RequestBody = body },
                exchange,
                ignored));
    }

    private static ResponseMessage ResponseFor(HttpExchange exchange)
    {
        var response = new ResponseMessage { StatusCode = exchange.Status };
        foreach (var (name, value) in exchange.ResponseHeaders)
        {
            response.AddHeader(name, value);
        }

        var text = exchange.Chunks is { } chunks ? string.Concat(chunks.Select(chunk => chunk.Data)) : exchange.ResponseBody;
        if (text is not null)
        {
            response.BodyData = exchange.ResponseBodyEncoding == "base64"
                ? new BodyData { BodyAsBytes = Convert.FromBase64String(text), DetectedBodyType = BodyType.Bytes }
                : new BodyData { BodyAsString = text, Encoding = Encoding.UTF8, DetectedBodyType = BodyType.String };
        }

        return response;
    }
}
