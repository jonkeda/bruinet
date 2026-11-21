using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Bruinet.Core.Logging;
using Bruinet.Core.Testing;

namespace Bruinet.Core.Api;

/// <summary>What an <see cref="ApiSession"/> is made with.</summary>
public sealed class ApiSessionOptions
{
    /// <summary>The service's address on the environment under test.</summary>
    public required Uri BaseAddress { get; init; }

    /// <summary>The hosts the suite may call (<see cref="ApiEnvironmentGuard"/>); at least one.</summary>
    public required IReadOnlyList<string> AllowedHosts { get; init; }

    /// <summary>How long one call may take.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>The handler the calls go through, for a test or a replay; the system's when null.</summary>
    public HttpMessageHandler? Handler { get; init; }

    /// <summary>The JSON fields whose values are never written down.</summary>
    public IReadOnlyCollection<string> MaskedFields { get; init; } = ApiMasks.DefaultFields;

    /// <summary>
    /// The paths a call that is not a GET only reads at (a search by POST): a call there is not a write. A path, or the
    /// start of one ending in a star.
    /// </summary>
    public IReadOnlyList<string> ReadPaths { get; init; } = [];

    /// <summary>The file every exchange is appended to; <c>BRUINET_API_EXCHANGES</c> names one when this is null.</summary>
    public string? ExchangeLog { get; init; }
}

/// <summary>
/// A service on a running environment, called as an account, for what a test does beside the screen: making the data
/// it needs, removing what it made, reading what was stored. It holds what every suite needs: the hosts it may call,
/// the account's sign-in, the rule that an account that never writes sends no write, every call in the step log and
/// kept as an exchange, and the undo of what it made. A suite's API objects are written over it; tests call those.
/// </summary>
public sealed class ApiSession : IDisposable
{
    private readonly HttpClient _http;
    private readonly ApiSessionOptions _options;
    private readonly ApiEnvironmentGuard _guard;
    private readonly IApiSignIn? _signIn;
    private readonly List<ApiExchange> _exchanges = [];
    private readonly List<string> _kept = [];
    private readonly string? _log;

    /// <summary>A session as an account.</summary>
    /// <param name="options">The service and the suite's rules.</param>
    /// <param name="account">The account's name, as <c>TestAccounts.md</c> has it.</param>
    /// <param name="writes">Whether the account may create, change and delete.</param>
    /// <param name="signIn">How the calls are signed; null for a service that asks nothing.</param>
    public ApiSession(ApiSessionOptions options, string account, bool writes, IApiSignIn? signIn = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(account);
        _options = options;
        _guard = new ApiEnvironmentGuard(options.AllowedHosts);
        _guard.Check(options.BaseAddress);
        _signIn = signIn;
        Account = account;
        Writes = writes;
        _http = options.Handler is null ? new HttpClient() : new HttpClient(options.Handler, disposeHandler: false);
        _http.BaseAddress = options.BaseAddress;
        _http.Timeout = options.Timeout;
        _log = options.ExchangeLog ?? ApiExchangeLog.Current;
    }

    /// <summary>A session as an account of <c>TestAccounts.md</c>.</summary>
    /// <param name="options">The service and the suite's rules.</param>
    /// <param name="account">The account.</param>
    /// <param name="signIn">How the calls are signed.</param>
    public ApiSession(ApiSessionOptions options, TestAccount account, IApiSignIn? signIn = null)
        : this(options, (account ?? throw new ArgumentNullException(nameof(account))).Name, account.Writes, signIn)
    {
    }

    /// <summary>The account the calls are made as.</summary>
    public string Account { get; }

    /// <summary>Whether the account may write.</summary>
    public bool Writes { get; }

    /// <summary>The service's address.</summary>
    public Uri BaseAddress => _options.BaseAddress;

    /// <summary>The undo of what was made through this session, run in reverse when the session is disposed.</summary>
    public CleanUpRegistry CleanUp { get; } = new();

    /// <summary>Every call this session made, oldest first.</summary>
    public IReadOnlyList<ApiExchange> Exchanges
    {
        get
        {
            lock (_exchanges)
            {
                return [.. _exchanges];
            }
        }
    }

    /// <summary>The marked records a delete left because the service still needs them, each as "name (#id): why".</summary>
    public IReadOnlyList<string> Kept => _kept;

    /// <summary>What a path reads; null when the service has nothing there (a 404).</summary>
    /// <param name="path">The path, with its query.</param>
    /// <exception cref="ApiCallException">The service answered with another error.</exception>
    public JsonNode? Get(string path)
    {
        var exchange = Call(HttpMethod.Get, path);
        return exchange.StatusCode == (int)HttpStatusCode.NotFound ? null : Answer(exchange);
    }

    /// <summary>Sends a POST with a JSON body.</summary>
    /// <param name="path">The path.</param>
    /// <param name="body">The body.</param>
    /// <returns>The answer's JSON; null when it has none.</returns>
    public JsonNode? Post(string path, JsonNode? body) => Send(HttpMethod.Post, path, body);

    /// <summary>Sends a PUT with a JSON body.</summary>
    /// <param name="path">The path.</param>
    /// <param name="body">The body.</param>
    /// <returns>The answer's JSON; null when it has none.</returns>
    public JsonNode? Put(string path, JsonNode? body) => Send(HttpMethod.Put, path, body);

    /// <summary>Sends a DELETE, with a JSON body when the service wants one.</summary>
    /// <param name="path">The path.</param>
    /// <param name="body">The body, or null.</param>
    /// <returns>The answer's JSON; null when it has none.</returns>
    public JsonNode? Delete(string path, JsonNode? body = null) => Send(HttpMethod.Delete, path, body);

    /// <summary>Sends a call with a JSON body and reads its JSON answer.</summary>
    /// <param name="method">The method.</param>
    /// <param name="path">The path, with its query.</param>
    /// <param name="body">The body, or null.</param>
    /// <returns>The answer's JSON; null when it has none.</returns>
    /// <exception cref="ApiCallException">The call was refused, or the service answered with an error.</exception>
    public JsonNode? Send(HttpMethod method, string path, JsonNode? body = null)
        => Answer(Call(method, path, body?.ToJsonString()));

    /// <summary>
    /// Makes a call and gives its exchange whatever the status: for a check on what the service answers. The guards
    /// hold as for every call.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="path">The path, with its query.</param>
    /// <param name="body">The body as text, or null.</param>
    /// <param name="contentType">The body's content type.</param>
    /// <exception cref="ApiCallException">The call was refused before it was sent, or had no answer.</exception>
    public ApiExchange Call(HttpMethod method, string path, string? body = null, string contentType = "application/json")
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var address = new Uri(_options.BaseAddress, path);
        _guard.Check(address);
        if (!Writes && IsWrite(method, address.AbsolutePath))
        {
            throw new ApiCallException($"{Account} never writes ({TestAccounts.FileName}): {method} {address.AbsolutePath} is refused.");
        }

        var exchange = Exchange(method, address, body, contentType);
        if (exchange.StatusCode == (int)HttpStatusCode.Unauthorized && _signIn?.Renew() == true)
        {
            exchange = Exchange(method, address, body, contentType);
        }

        return exchange;
    }

    /// <summary>The records of a kind that carry a run's marker: this run's, and what an earlier run left.</summary>
    /// <param name="resource">The kind of record.</param>
    public IReadOnlyList<JsonObject> Marked(MarkedResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return [.. resource.List(this).Where(record => resource.MarkedText(record) is not null)];
    }

    /// <summary>
    /// Deletes one record a test made. The record is read from the service first, and only one whose text, as the
    /// service gives it, carries a run's marker is deleted: any other is refused.
    /// </summary>
    /// <param name="resource">The kind of record.</param>
    /// <param name="id">The record's id.</param>
    /// <returns>Whether a record was deleted: false when there was none, or when it is kept (<see cref="Kept"/>).</returns>
    /// <exception cref="ApiCallException">The record is not one a test made.</exception>
    public bool DeleteMarked(MarkedResource resource, string id)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (resource.Delete is null)
        {
            throw new ApiCallException($"A {resource.Name} has no delete declared: it is removed another way.");
        }

        if (resource.Read(this, id) is not { } record)
        {
            return false;
        }

        if (resource.MarkedText(record) is not { } text)
        {
            throw new ApiCallException($"The {resource.Name} {id} is '{(string?)record[resource.MarkerField]}', not one a test made: it is not deleted.");
        }

        if (resource.Keep?.Invoke(this, record) is { } why)
        {
            _kept.Add($"{text} (#{id}): {why}");
            return false;
        }

        resource.Delete(this, record);
        return true;
    }

    /// <summary>Deletes every record of a kind that carries a run's marker: this run's, and what an earlier run left.</summary>
    /// <param name="resource">The kind of record.</param>
    /// <returns>The marked texts of the records deleted.</returns>
    public IReadOnlyList<string> Sweep(MarkedResource resource)
    {
        var deleted = new List<string>();
        foreach (var record in Marked(resource))
        {
            if (resource.Id(record) is { } id && DeleteMarked(resource, id))
            {
                deleted.Add(resource.MarkedText(record)!);
            }
        }

        return deleted;
    }

    /// <summary>Says a record was just made through this session, so it is deleted when the session is disposed.</summary>
    /// <param name="resource">The kind of record.</param>
    /// <param name="id">The new record's id.</param>
    public void Made(MarkedResource resource, string id)
    {
        ArgumentNullException.ThrowIfNull(resource);
        CleanUp.Add($"delete {resource.Name} {id}", () => DeleteMarked(resource, id));
    }

    /// <summary>Runs the undos, then closes the connection. An undo that failed is in <see cref="CleanUp"/>.</summary>
    public void Dispose()
    {
        CleanUp.Dispose();
        _http.Dispose();
    }

    /// <summary>An answer's body as text.</summary>
    /// <param name="response">The answer.</param>
    public static string ReadText(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        // Not HttpClient.Send: a handler that only overrides SendAsync (a replay, a test's fake) would be passed by.
        return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
    }

    private static string Short(string? text) => text is null ? string.Empty : text.Length <= 300 ? text : text[..300] + "…";

    private bool IsWrite(HttpMethod method, string path)
    {
        if (method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Options)
        {
            return false;
        }

        return !_options.ReadPaths.Any(read => read.EndsWith('*')
            ? path.StartsWith(read[..^1], StringComparison.OrdinalIgnoreCase)
            : string.Equals(path, read, StringComparison.OrdinalIgnoreCase));
    }

    private JsonNode? Answer(ApiExchange exchange)
        => exchange.Succeeded
            ? exchange.Json()
            : throw new ApiCallException($"{exchange.Describe()}: {Short(exchange.ResponseBody)}") { Exchange = exchange };

    private ApiExchange Exchange(HttpMethod method, Uri address, string? body, string contentType)
    {
        using var request = new HttpRequestMessage(method, address);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, contentType);
        }

        _signIn?.Apply(request);
        var started = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();
        var sent = ApiMasks.Mask(body, _options.MaskedFields);
        ApiExchange exchange;
        try
        {
            using var response = _http.SendAsync(request).GetAwaiter().GetResult();
            exchange = new ApiExchange(Guid.NewGuid().ToString("N"), started, method.Method, address.ToString(), (int)response.StatusCode, response.Content.Headers.ContentType?.MediaType, ApiMasks.Mask(ReadText(response), _options.MaskedFields))
            {
                RequestContentType = body is null ? null : contentType,
                RequestBody = sent,
                Account = Account,
                DurationMs = clock.ElapsedMilliseconds,
            };
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            exchange = new ApiExchange(Guid.NewGuid().ToString("N"), started, method.Method, address.ToString(), null, null, null)
            {
                RequestContentType = body is null ? null : contentType,
                RequestBody = sent,
                Account = Account,
                DurationMs = clock.ElapsedMilliseconds,
                Error = $"{e.GetType().Name}: {e.Message}",
            };
            Keep(exchange);
            throw new ApiCallException($"{exchange.Describe()}.", e) { Exchange = exchange };
        }

        Keep(exchange);
        return exchange;
    }

    private void Keep(ApiExchange exchange)
    {
        lock (_exchanges)
        {
            _exchanges.Add(exchange);
        }

        // A 404 is an answer a read expects ("is it there?"); another error is worth a warning beside the step.
        var level = exchange.StatusCode is null or >= 500 ? StepLogLevel.Error
            : exchange.StatusCode >= 400 && exchange.StatusCode != (int)HttpStatusCode.NotFound ? StepLogLevel.Warning
            : StepLogLevel.Information;
        var sent = exchange.RequestBody is { Length: > 0 } body ? $" sent {Short(body)}" : string.Empty;
        var got = level == StepLogLevel.Information ? string.Empty : $" got {Short(exchange.ResponseBody)}";
        StepLog.Add(level, "api", $"{exchange.Describe()} in {exchange.DurationMs} ms{sent}{got}");
        if (_log is not null)
        {
            ApiExchangeLog.Append(_log, exchange);
        }
    }
}
