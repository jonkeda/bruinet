using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Bruinet.Mocking.Http;

/// <summary>A call replay could not answer from the recording, or one a hermetic run refused.</summary>
public sealed class HttpReplayMissException(string message) : HttpRequestException(message);

/// <summary>One call a test made, as its newest run saw it.</summary>
/// <param name="Method">The method.</param>
/// <param name="Url">The URL, redacted as recorded.</param>
/// <param name="Status">The answer's status; 0 for a miss.</param>
/// <param name="Outcome"><c>recorded</c>, <c>matched</c> or <c>missed</c>.</param>
/// <param name="Exchange">The recorded exchange that answered it; -1 for none.</param>
public sealed record HttpCall(string Method, string Url, int Status, string Outcome, int Exchange);

/// <summary>A test's calls in its newest run, beside its recording (<c>&lt;test&gt;.calls.json</c>).</summary>
public sealed class HttpCallLog
{
    public string Mode { get; set; } = string.Empty;

    public List<HttpCall> Calls { get; set; } = [];

    /// <summary>Replay: the recorded exchanges nothing asked for, by index (a call the app no longer makes).</summary>
    public List<int> Unused { get; set; } = [];

    public List<string> Misses { get; set; } = [];

    /// <summary>The call log beside the recording at <paramref name="recordingPath"/>.</summary>
    public static string PathFor(string recordingPath)
        => System.IO.Path.ChangeExtension(recordingPath, null) + ".calls.json";

    public static HttpCallLog? Load(string path)
        => File.Exists(path) ? JsonSerializer.Deserialize<HttpCallLog>(File.ReadAllText(path), HttpRecording.Json) : null;
}

/// <summary>
/// One test's HTTP record or replay, as SQL replay's <c>ReplaySession</c>: nothing in it is shared, so tests in parallel
/// each have their own, handed to the app through <see cref="HttpReplayHandler"/> or <c>AddBruinetHttpReplay</c>. In replay
/// a call is answered by the first unused recorded exchange it matches, so the same call answers in recorded order (a poll
/// that says "pending", then "done"); one asked for more often than recorded gets the last answer again.
/// </summary>
public sealed class HttpReplaySession
{
    /// <summary>The environment variable that sets the mode: <c>Record</c>, <c>Replay</c> or <c>Refresh</c>.</summary>
    public const string ModeVariable = "BRUINET_HTTP_REPLAY";

    private static readonly string[] StreamTypes = ["text/event-stream", "application/x-ndjson", "application/stream+json"];
    private static readonly HashSet<string> DroppedHeaders = new(
        ["Date", "Transfer-Encoding", "Connection", "Keep-Alive", "Content-Length", "Server", "Via", "Alt-Svc", "Strict-Transport-Security"],
        StringComparer.OrdinalIgnoreCase);

    private readonly object _gate = new();
    private readonly HttpRecording _recording;
    private readonly HttpRecording? _previous;
    private readonly HashSet<string> _hosts;
    private readonly bool[] _used;
    private readonly List<HttpCall> _calls = [];
    private readonly List<string> _misses = [];
    private readonly List<string> _changes = [];

    /// <param name="mode">Record, replay or refresh.</param>
    /// <param name="path">The test's recording (<see cref="PathFor"/>).</param>
    /// <param name="options">Redaction, presets, pace and the hermetic guard; the defaults when null.</param>
    public HttpReplaySession(HttpReplayMode mode, string path, HttpReplayOptions? options = null)
    {
        if (mode == HttpReplayMode.Off)
        {
            throw new ArgumentException("A session records or replays; with replay off, give the app no session.", nameof(mode));
        }

        Mode = mode;
        Path = path;
        Options = options ?? new HttpReplayOptions();
        if (mode == HttpReplayMode.Replay)
        {
            _recording = HttpRecording.Load(path) ?? throw new HttpReplayMissException($"No HTTP recording at {path}: record the test first ({ModeVariable}=Record), twice.");
        }
        else
        {
            _recording = new HttpRecording();
            _previous = HttpRecording.Load(path);
        }

        _hosts = new HashSet<string>(
            _recording.Exchanges.Select(exchange => Uri.TryCreate(exchange.Url, UriKind.Absolute, out var url) ? url.Authority : string.Empty),
            StringComparer.OrdinalIgnoreCase);
        _used = new bool[_recording.Exchanges.Count];
    }

    public HttpReplayMode Mode { get; }

    public string Path { get; }

    public HttpReplayOptions Options { get; }

    /// <summary>The misses so far: a test checks them at its end (<see cref="ThrowIfMissed"/>), as the app may have caught one.</summary>
    public IReadOnlyList<string> Misses
    {
        get
        {
            lock (_gate)
            {
                return [.. _misses];
            }
        }
    }

    /// <summary>The calls so far, in order.</summary>
    public IReadOnlyList<HttpCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>Refresh: each answer that changed against the recording replaced, after <see cref="Finish"/>.</summary>
    public IReadOnlyList<string> Changes
    {
        get
        {
            lock (_gate)
            {
                return [.. _changes];
            }
        }
    }

    /// <summary>The mode <see cref="ModeVariable"/> sets; <see cref="HttpReplayMode.Off"/> when unset.</summary>
    /// <exception cref="ArgumentException">It names no mode.</exception>
    public static HttpReplayMode ModeFromEnvironment(Func<string, string?>? environment = null)
    {
        var value = (environment ?? System.Environment.GetEnvironmentVariable)(ModeVariable);
        return string.IsNullOrWhiteSpace(value) ? HttpReplayMode.Off
            : Enum.TryParse<HttpReplayMode>(value.Trim(), ignoreCase: true, out var mode) ? mode
            : throw new ArgumentException($"{ModeVariable}={value}: Record, Replay, Refresh or Off.");
    }

    /// <summary>A test's recording: <c>&lt;folder&gt;/http/&lt;test&gt;.json</c>, beside SQL replay's.</summary>
    /// <param name="folder">The suite's <c>Recordings</c> folder.</param>
    /// <param name="test">The test or scenario name; characters a file name cannot hold become <c>_</c>.</param>
    public static string PathFor(string folder, string test)
    {
        ArgumentNullException.ThrowIfNull(test);
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var name = new string([.. test.Select(character => invalid.Contains(character) ? '_' : character)]).Trim();
        return System.IO.Path.Combine(folder, "http", name + ".json");
    }

    /// <summary>
    /// The session a dependency's mode asks for (its <c>## Dependencies</c> row): <c>record</c>, <c>replay</c> or
    /// <c>refresh</c>; null for <c>real</c> and <c>stub</c>, which take no session.
    /// </summary>
    /// <exception cref="ArgumentException">A mode an http dependency does not take.</exception>
    public static HttpReplaySession? ForMode(string mode, string folder, string test, HttpReplayOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mode);
        return mode.Trim().ToLowerInvariant() switch
        {
            "real" or "stub" => null,
            "record" => new HttpReplaySession(HttpReplayMode.Record, PathFor(folder, test), options),
            "replay" => new HttpReplaySession(HttpReplayMode.Replay, PathFor(folder, test), options),
            "refresh" => new HttpReplaySession(HttpReplayMode.Refresh, PathFor(folder, test), options),
            _ => throw new ArgumentException($"An http dependency is real, record, replay, refresh or stub, not '{mode}'.", nameof(mode)),
        };
    }

    /// <summary>The session <see cref="ModeVariable"/> asks for, or null when it is off.</summary>
    public static HttpReplaySession? FromEnvironment(string folder, string test, HttpReplayOptions? options = null)
        => ModeFromEnvironment() is var mode && mode == HttpReplayMode.Off ? null : new HttpReplaySession(mode, PathFor(folder, test), options);

    /// <summary>Answers <paramref name="request"/>: from the recording in replay; through <paramref name="network"/>, recorded, otherwise.</summary>
    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> network,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(network);
        var live = await ToExchangeAsync(request, cancellationToken).ConfigureAwait(false);
        if (Mode == HttpReplayMode.Replay)
        {
            return Answer(live, request);
        }

        using var response = await network(request, cancellationToken).ConfigureAwait(false);
        await ReadAnswerAsync(live, response, cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            live.Index = _recording.Exchanges.Count;
            _recording.Exchanges.Add(live);
            _calls.Add(new HttpCall(live.Method, live.Url, live.Status, "recorded", live.Index));
        }

        return Respond(live, request, HttpReplayPace.None);
    }

    /// <summary>Record and refresh: keeps an exchange recorded elsewhere (a proxy, <c>MockApiServer.Record</c>), in order.</summary>
    public void Add(HttpExchange exchange)
    {
        ArgumentNullException.ThrowIfNull(exchange);
        if (Mode == HttpReplayMode.Replay)
        {
            throw new InvalidOperationException("A replay adds nothing to its recording.");
        }

        lock (_gate)
        {
            exchange.Index = _recording.Exchanges.Count;
            _recording.Exchanges.Add(exchange);
            _calls.Add(new HttpCall(exchange.Method, exchange.Url, exchange.Status, "recorded", exchange.Index));
        }
    }

    /// <summary>
    /// Saves what the run left: in record and refresh the recording, its volatile values found against the one it
    /// replaces, redacted and scanned for secrets; in every mode the call log beside it.
    /// </summary>
    /// <exception cref="HttpRecordingSecretException">The recording held what looks like a secret; nothing was saved.</exception>
    public void Finish()
    {
        lock (_gate)
        {
            if (Mode != HttpReplayMode.Replay)
            {
                foreach (var exchange in _recording.Exchanges)
                {
                    HttpRedaction.Redact(exchange, Options);
                }

                if (_previous is not null)
                {
                    _recording.MarkVolatile(_previous);
                    if (Mode == HttpReplayMode.Refresh)
                    {
                        _changes.AddRange(HttpRecordingDiff.Compare(_previous, _recording));
                    }
                }

                HttpRedaction.Scan(_recording, Path);
                _recording.Save(Path);
            }

            var log = new HttpCallLog
            {
                Mode = Mode.ToString(),
                Calls = [.. _calls],
                Misses = [.. _misses],
                Unused = Mode == HttpReplayMode.Replay ? [.. Enumerable.Range(0, _used.Length).Where(index => !_used[index])] : [],
            };
            var logPath = HttpCallLog.PathFor(Path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(logPath))!);
            File.WriteAllText(logPath, JsonSerializer.Serialize(log, HttpRecording.Json));
        }
    }

    /// <summary>Fails the test on its misses, which the app may have caught and shown as an error of its own.</summary>
    /// <exception cref="HttpReplayMissException">There was at least one.</exception>
    public void ThrowIfMissed()
    {
        var misses = Misses;
        if (misses.Count > 0)
        {
            throw new HttpReplayMissException(string.Join(System.Environment.NewLine, misses));
        }
    }

    private HttpResponseMessage Answer(HttpExchange live, HttpRequestMessage request)
    {
        lock (_gate)
        {
            if (Options.Hermetic && request.RequestUri is { IsAbsoluteUri: true } uri && !_hosts.Contains(uri.Authority))
            {
                throw Miss(live, $"Hermetic: {live.Method} {live.Url} goes to {uri.Authority}, which the recording never called.");
            }

            var matching = Enumerable.Range(0, _recording.Exchanges.Count)
                .Where(index => HttpMatching.Matches(live, _recording.Exchanges[index], (IReadOnlyCollection<string>)Options.IgnoredFields))
                .ToList();
            if (matching.Count == 0)
            {
                throw Miss(live, MissMessage(live));
            }

            var chosen = matching.FirstOrDefault(index => !_used[index], matching[^1]);
            _used[chosen] = true;
            var exchange = _recording.Exchanges[chosen];
            _calls.Add(new HttpCall(live.Method, live.Url, exchange.Status, "matched", chosen));
            return Respond(exchange, request, Options.Pace);
        }
    }

    private HttpReplayMissException Miss(HttpExchange live, string message)
    {
        _misses.Add(message);
        _calls.Add(new HttpCall(live.Method, live.Url, 0, "missed", -1));
        return new HttpReplayMissException(message);
    }

    // The miss names the call, the nearest recorded one and how they differ.
    private string MissMessage(HttpExchange live)
    {
        var path = HttpMatching.Path(live.Url);
        var nearest = _recording.Exchanges.FirstOrDefault(exchange => string.Equals(exchange.Method, live.Method, StringComparison.OrdinalIgnoreCase) && HttpMatching.Path(exchange.Url) == path)
            ?? _recording.Exchanges.FirstOrDefault(exchange => HttpMatching.Path(exchange.Url) == path)
            ?? _recording.Exchanges.FirstOrDefault(exchange => string.Equals(exchange.Method, live.Method, StringComparison.OrdinalIgnoreCase));
        var name = System.IO.Path.GetFileName(Path);
        return nearest is null
            ? $"HTTP replay miss in {name}: {live.Method} {live.PathAndQuery} was never recorded ({_recording.Exchanges.Count.ToString(CultureInfo.InvariantCulture)} calls are)."
            : $"HTTP replay miss in {name}: {live.Method} {live.PathAndQuery}; nearest recorded is exchange {nearest.Index.ToString(CultureInfo.InvariantCulture)} {nearest.Method} {nearest.PathAndQuery}: {HttpMatching.Difference(live, nearest, (IReadOnlyCollection<string>)Options.IgnoredFields) ?? "the same call, every recorded answer already given"}.";
    }

    private async Task<HttpExchange> ToExchangeAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var exchange = new HttpExchange
        {
            Method = request.Method.Method,
            Url = request.RequestUri?.ToString() ?? string.Empty,
        };
        foreach (var header in request.Headers.Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>()))
        {
            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("Accept", StringComparison.OrdinalIgnoreCase)
                || Options.RedactedHeaders.Contains(header.Key))
            {
                exchange.RequestHeaders[header.Key] = string.Join(", ", header.Value);
            }
        }

        if (request.Content is { } content)
        {
            // Buffered, so the same content still goes to the network after.
            await content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
            var bytes = await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            (exchange.RequestBody, exchange.RequestBodyEncoding) = Body(bytes, content.Headers.ContentType?.MediaType);
        }

        return exchange;
    }

    private static async Task ReadAnswerAsync(HttpExchange exchange, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        exchange.Status = (int)response.StatusCode;
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            if (!DroppedHeaders.Contains(header.Key))
            {
                exchange.ResponseHeaders[header.Key] = string.Join(", ", header.Value);
            }
        }

        var type = response.Content.Headers.ContentType?.MediaType;
        if (type is not null && StreamTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
        {
            exchange.Chunks = [];
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var buffer = new byte[8192];
            var clock = Stopwatch.StartNew();
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                exchange.Chunks.Add(new HttpChunk { DelayMs = (int)clock.ElapsedMilliseconds, Data = Encoding.UTF8.GetString(buffer, 0, read) });
                clock.Restart();
            }

            return;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        (exchange.ResponseBody, exchange.ResponseBodyEncoding) = Body(bytes, type);
    }

    private static HttpResponseMessage Respond(HttpExchange exchange, HttpRequestMessage request, HttpReplayPace pace)
    {
        HttpContent content = exchange.Chunks is { } chunks
            ? new ChunkedReplayContent(chunks, pace)
            : new ByteArrayContent(exchange.ResponseBody is null ? []
                : exchange.ResponseBodyEncoding == "base64" ? Convert.FromBase64String(exchange.ResponseBody)
                : Encoding.UTF8.GetBytes(exchange.ResponseBody));
        var response = new HttpResponseMessage((HttpStatusCode)exchange.Status) { Content = content, RequestMessage = request };
        foreach (var (name, value) in exchange.ResponseHeaders)
        {
            if (!response.Headers.TryAddWithoutValidation(name, value))
            {
                content.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return response;
    }

    private static (string? Body, string? Encoding) Body(byte[] bytes, string? mediaType)
    {
        if (bytes.Length == 0)
        {
            return (null, null);
        }

        return IsText(mediaType) && TryUtf8(bytes, out var text) ? (text, null) : (Convert.ToBase64String(bytes), "base64");
    }

    private static bool IsText(string? mediaType)
        => mediaType is null
           || mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
           || mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
           || mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase)
           || mediaType.Contains("javascript", StringComparison.OrdinalIgnoreCase)
           || mediaType.Contains("x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);

    private static bool TryUtf8(byte[] bytes, out string text)
    {
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }

    // A streamed answer given back part by part, at recorded pace or at once.
    private sealed class ChunkedReplayContent(IReadOnlyList<HttpChunk> chunks, HttpReplayPace pace) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            foreach (var chunk in chunks)
            {
                if (pace == HttpReplayPace.Recorded && chunk.DelayMs > 0)
                {
                    await Task.Delay(chunk.DelayMs).ConfigureAwait(false);
                }

                var bytes = Encoding.UTF8.GetBytes(chunk.Data);
                await stream.WriteAsync(bytes).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }
        }

        // Read as a stream, the parts come one by one, not buffered whole: a reader of a stream sees the pace.
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new ChunkStream(chunks, pace));

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class ChunkStream(IReadOnlyList<HttpChunk> chunks, HttpReplayPace pace) : Stream
    {
        private int _next;
        private byte[] _current = [];
        private int _offset;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            while (_offset >= _current.Length)
            {
                if (_next >= chunks.Count)
                {
                    return 0;
                }

                var chunk = chunks[_next++];
                if (pace == HttpReplayPace.Recorded && chunk.DelayMs > 0)
                {
                    await Task.Delay(chunk.DelayMs, cancellationToken).ConfigureAwait(false);
                }

                _current = Encoding.UTF8.GetBytes(chunk.Data);
                _offset = 0;
            }

            var count = Math.Min(buffer.Length, _current.Length - _offset);
            _current.AsMemory(_offset, count).CopyTo(buffer);
            _offset += count;
            return count;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
