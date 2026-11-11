using System.Diagnostics;
using System.Text.Json;
using WireMock.RequestBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace Bruinet.Mocking;

/// <summary>
/// A WireMock server standing in for an app's backend during a test.
/// </summary>
/// <remarks>
/// <para>
/// <b>Start it before the app.</b> The app has to be told where its backend is, and that is only
/// known once the server has a port. A fixture starts the server, hands <see cref="BaseUrl"/> to
/// the app (an environment variable the app inherits, on Windows), then launches the app.
/// </para>
/// <para>
/// <b>Port 0 by default.</b> The operating system picks a free port, so a dev server or a leftover
/// run on a fixed port cannot make the tests talk to the wrong backend.
/// </para>
/// <para>
/// <b>Owned or shared.</b> <see cref="Start"/> gives a server this caller owns and disposes.
/// <see cref="AcquireShared"/> gives one server shared by every fixture that asks, counted, and
/// stopped when the last one disposes its lease - the lifecycle
/// <c>Exact.Construction.UITests</c>' <c>MockBackendHostManager</c> uses.
/// </para>
/// <para>
/// <b>Wait for requests; do not count them straight after an action.</b> The app sends requests
/// asynchronously, so a count read right after a click races it. <see cref="WaitForRequest"/>
/// waits for the request to arrive (AD-004); the count queries are for asserting afterwards that
/// something did <i>not</i> happen, or happened exactly once.
/// </para>
/// </remarks>
public sealed partial class MockApiServer : IDisposable
{
    /// <summary>How often <see cref="WaitForRequest"/> looks at the request log.</summary>
    public static readonly TimeSpan RequestPollInterval = TimeSpan.FromMilliseconds(25);

    /// <summary>The timeout <see cref="WaitForRequest"/> uses when none is given.</summary>
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(10);

    /// <summary>JSON options for stubbed bodies and <see cref="RecordedRequest.BodyAs{T}"/>: the web defaults.</summary>
    public static readonly JsonSerializerOptions DefaultJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Lock SharedGate = new();
    private static MockApiServer? _shared;
    private static int _sharedLeases;

    private readonly WireMockServer _server;
    private readonly bool _isShared;
    private bool _disposed;

    private MockApiServer(WireMockServer server, MockApiServerOptions options, bool isShared)
    {
        _server = server;
        _isShared = isShared;
        Options = options;
    }

    /// <summary>The options the server was started with.</summary>
    public MockApiServerOptions Options { get; }

    /// <summary>The port the server listens on.</summary>
    public int Port => _server.Ports[0];

    /// <summary>The address to give the app, e.g. <c>http://127.0.0.1:51234</c>, without a trailing slash.</summary>
    public string BaseUrl => _server.Urls[0].TrimEnd('/');

    /// <summary>
    /// The underlying WireMock server, for what the fluent API does not cover.
    /// </summary>
    public WireMockServer Server => _server;

    /// <summary>Whether the server is running.</summary>
    public bool IsStarted => !_disposed && _server.IsStarted;

    /// <summary>
    /// Starts a server this caller owns. Dispose it to stop it.
    /// </summary>
    public static MockApiServer Start(MockApiServerOptions? options = null)
    {
        options ??= new MockApiServerOptions();
        return new MockApiServer(StartWireMock(options), options, isShared: false);
    }

    /// <summary>
    /// Takes a lease on the process-wide shared server, starting it on the first lease.
    /// </summary>
    /// <remarks>
    /// Every lease is released by disposing the returned instance once; the server stops when the
    /// last lease is released. The options only matter to the lease that starts the server.
    /// </remarks>
    public static MockApiServer AcquireShared(MockApiServerOptions? options = null)
    {
        lock (SharedGate)
        {
            if (_shared is null)
            {
                options ??= new MockApiServerOptions();
                _shared = new MockApiServer(StartWireMock(options), options, isShared: true);
            }

            _sharedLeases++;
            return _shared;
        }
    }

    /// <summary>The process-wide shared server while a lease on it is held (<see cref="AcquireShared"/>); null otherwise.</summary>
    public static MockApiServer? Shared
    {
        get
        {
            lock (SharedGate)
            {
                return _shared;
            }
        }
    }

    /// <summary>
    /// Starts describing a stub for <paramref name="method"/> requests to <paramref name="path"/>.
    /// </summary>
    /// <param name="method">The HTTP method, e.g. <c>GET</c>.</param>
    /// <param name="path">The path; <c>*</c> is a wildcard, as in <c>/api/todos/*</c>.</param>
    public ApiStubBuilder Stub(string method, string path) => new(this, method, path);

    /// <summary>Stubs a <c>GET</c>.</summary>
    public ApiStubBuilder Get(string path) => Stub("GET", path);

    /// <summary>Stubs a <c>POST</c>.</summary>
    public ApiStubBuilder Post(string path) => Stub("POST", path);

    /// <summary>Stubs a <c>PUT</c>.</summary>
    public ApiStubBuilder Put(string path) => Stub("PUT", path);

    /// <summary>Stubs a <c>PATCH</c>.</summary>
    public ApiStubBuilder Patch(string path) => Stub("PATCH", path);

    /// <summary>Stubs a <c>DELETE</c>.</summary>
    public ApiStubBuilder Delete(string path) => Stub("DELETE", path);

    /// <summary>
    /// Removes every stub, forgets every request and restarts every scenario.
    /// </summary>
    /// <remarks>
    /// Between tests, so each one starts from a server state it declared itself. Stubs that pile
    /// up over a run make a test's outcome depend on which tests ran before it.
    /// </remarks>
    public void Reset()
    {
        _server.ResetMappings();
        _server.ResetLogEntries();
        _server.ResetScenarios();
    }

    /// <summary>Forgets every received request, keeping the stubs.</summary>
    public void ResetRequests() => _server.ResetLogEntries();

    /// <summary>Every request received so far, oldest first.</summary>
    public IReadOnlyList<RecordedRequest> Requests()
        => _server.LogEntries
            .Where(entry => entry.RequestMessage is not null)
            .Select(entry => RecordedRequest.From(entry.RequestMessage!))
            .OrderBy(request => request.ReceivedAtUtc)
            .ToList();

    /// <summary>
    /// Every write (any method but <c>GET</c>) received since <paramref name="sinceUtc"/>, oldest first, in the
    /// shape <see cref="Bruinet.Contracts.ExpectedRequestMockCheck"/> compares with each contract's expected
    /// request: a multipart body is described part by part.
    /// </summary>
    /// <param name="sinceUtc">The moment the test started, in UTC.</param>
    public IReadOnlyList<Bruinet.Contracts.RecordedWrite> WritesSince(DateTime sinceUtc)
        => _server.LogEntries
            .Select(entry => entry.RequestMessage)
            .Where(message => message is not null && message.DateTime >= sinceUtc
                && !string.Equals(message.Method, "GET", StringComparison.OrdinalIgnoreCase))
            .OrderBy(message => message!.DateTime)
            .Select(message => Write(message!))
            .ToList();

    private static Bruinet.Contracts.RecordedWrite Write(WireMock.IRequestMessage message)
    {
        var contentType = message.Headers?
            .FirstOrDefault(header => string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            .Value?.FirstOrDefault();
        var mediaType = contentType?.Split(';', 2)[0].Trim();
        var text = !string.IsNullOrEmpty(message.Body) ? message.Body
            : !string.IsNullOrEmpty(message.BodyData?.BodyAsString) ? message.BodyData.BodyAsString
            : message.BodyAsBytes is { Length: > 0 } bytes ? System.Text.Encoding.UTF8.GetString(bytes)
            : null;
        var body = string.Equals(mediaType, "multipart/form-data", StringComparison.OrdinalIgnoreCase)
            ? Bruinet.Contracts.ExpectedRequestCheck.DescribeMultipart(contentType!,
                message.BodyAsBytes ?? message.BodyData?.BodyAsBytes ?? System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty))
            : text;
        return new Bruinet.Contracts.RecordedWrite(message.Method, message.Path, mediaType, body);
    }

    /// <summary>
    /// The requests received for <paramref name="method"/> and <paramref name="path"/>, oldest first.
    /// </summary>
    /// <param name="method">The HTTP method; <c>null</c> for any.</param>
    /// <param name="path">The path; <c>*</c> is a wildcard.</param>
    public IReadOnlyList<RecordedRequest> Requests(string? method, string path)
        => _server.FindLogEntries(CreateMatcher(method, path))
            .Where(entry => entry.RequestMessage is not null)
            .Select(entry => RecordedRequest.From(entry.RequestMessage!))
            .OrderBy(request => request.ReceivedAtUtc)
            .ToList();

    /// <summary>How many requests arrived for <paramref name="method"/> and <paramref name="path"/>.</summary>
    public int RequestCount(string? method, string path) => Requests(method, path).Count;

    /// <summary>How many of them satisfy <paramref name="predicate"/>.</summary>
    public int RequestCount(string? method, string path, Func<RecordedRequest, bool> predicate)
        => Requests(method, path).Count(predicate);

    /// <summary>How many of them have a JSON body satisfying <paramref name="predicate"/>.</summary>
    public int JsonRequestCount(string? method, string path, Func<JsonElement, bool> predicate)
        => Requests(method, path).Count(request => request.BodyMatchesJson(predicate));

    /// <summary>How many of them have form fields satisfying <paramref name="predicate"/>.</summary>
    public int FormRequestCount(string? method, string path, Func<IReadOnlyDictionary<string, string>, bool> predicate)
        => Requests(method, path).Count(request => request.Body is not null && predicate(request.BodyAsForm()));

    /// <summary>The bodies of the requests for <paramref name="method"/> and <paramref name="path"/>, oldest first.</summary>
    public IReadOnlyList<string> RequestBodies(string? method, string path)
        => Requests(method, path).Select(request => request.Body ?? string.Empty).ToList();

    /// <summary>
    /// Waits for a request to arrive and returns it.
    /// </summary>
    /// <param name="method">The HTTP method; <c>null</c> for any.</param>
    /// <param name="path">The path; <c>*</c> is a wildcard.</param>
    /// <param name="predicate">An extra condition on the request, e.g. on its body.</param>
    /// <param name="timeout">How long to wait; <see cref="DefaultRequestTimeout"/> when omitted.</param>
    /// <param name="skip">How many matching requests to pass over first, to wait for a second one.</param>
    /// <exception cref="TimeoutException">
    /// No such request arrived in time. The message lists what did arrive, which is usually the
    /// quickest way to see a wrong path or a missing header.
    /// </exception>
    public RecordedRequest WaitForRequest(
        string? method,
        string path,
        Func<RecordedRequest, bool>? predicate = null,
        TimeSpan? timeout = null,
        int skip = 0)
    {
        var limit = timeout ?? DefaultRequestTimeout;
        var clock = Stopwatch.StartNew();

        while (true)
        {
            var match = Requests(method, path)
                .Where(request => predicate?.Invoke(request) ?? true)
                .Skip(skip)
                .FirstOrDefault();

            if (match is not null)
            {
                return match;
            }

            if (clock.Elapsed >= limit)
            {
                throw new TimeoutException(DescribeMissingRequest(method, path, predicate is not null, limit));
            }

            Thread.Sleep(RequestPollInterval);
        }
    }

    /// <summary>
    /// Waits for a request to arrive, without blocking the calling thread.
    /// </summary>
    /// <inheritdoc cref="WaitForRequest"/>
    public async Task<RecordedRequest> WaitForRequestAsync(
        string? method,
        string path,
        Func<RecordedRequest, bool>? predicate = null,
        TimeSpan? timeout = null,
        int skip = 0,
        CancellationToken cancellationToken = default)
    {
        var limit = timeout ?? DefaultRequestTimeout;
        var clock = Stopwatch.StartNew();

        while (true)
        {
            var match = Requests(method, path)
                .Where(request => predicate?.Invoke(request) ?? true)
                .Skip(skip)
                .FirstOrDefault();

            if (match is not null)
            {
                return match;
            }

            if (clock.Elapsed >= limit)
            {
                throw new TimeoutException(DescribeMissingRequest(method, path, predicate is not null, limit));
            }

            await Task.Delay(RequestPollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Stops an owned server, or releases a lease on the shared one.
    /// </summary>
    public void Dispose()
    {
        if (!_isShared)
        {
            StopOwned();
            return;
        }

        lock (SharedGate)
        {
            if (_sharedLeases > 0)
            {
                _sharedLeases--;
            }

            if (_sharedLeases > 0 || !ReferenceEquals(_shared, this))
            {
                return;
            }

            _shared = null;
            StopOwned();
        }
    }

    internal static IRequestBuilder CreateMatcher(string? method, string path)
    {
        var request = Request.Create().WithPath(path);
        return method is null ? request : request.UsingMethod(method.ToUpperInvariant());
    }

    private static WireMockServer StartWireMock(MockApiServerOptions options)
    {
        // Loopback only: WireMock's default "*" binding triggers the Windows Firewall prompt. The Android
        // emulator (10.0.2.2) and adb reverse both reach the host loopback, see AndroidHost.
        var settings = new WireMockServerSettings
        {
            Urls = [$"http://127.0.0.1:{options.Port}"],
            StartAdminInterface = false,
            ReadStaticMappings = false,
        };

        return WireMockServer.Start(settings);
    }

    private void StopOwned()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _server.Stop();
        _server.Dispose();
    }

    private string DescribeMissingRequest(string? method, string path, bool hadPredicate, TimeSpan limit)
    {
        var received = Requests();
        var wanted = $"{method ?? "any method"} {path}{(hadPredicate ? " matching the predicate" : string.Empty)}";
        var seen = received.Count == 0
            ? "No requests arrived at all - is the app pointed at " + BaseUrl + "?"
            : "Requests that did arrive:" + Environment.NewLine
                + string.Join(Environment.NewLine, received.Select(request => "  " + request));

        return $"No request {wanted} arrived within {limit.TotalSeconds:0.#} s. {seen}";
    }
}

/// <summary>
/// How a <see cref="MockApiServer"/> starts.
/// </summary>
public sealed record MockApiServerOptions
{
    /// <summary>The port to listen on; 0, the default, lets the operating system choose a free one.</summary>
    public int Port { get; init; }

    /// <summary>
    /// The folder <see cref="ApiStubBuilder.ReturnsSample"/> reads recorded responses from, relative
    /// to the test output folder or any folder above it. Defaults to <c>ContractSamples</c>.
    /// </summary>
    public string SamplesDirectory { get; init; } = "ContractSamples";
}
