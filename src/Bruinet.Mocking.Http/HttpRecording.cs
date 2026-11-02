using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bruinet.Mocking.Http;

/// <summary>What a test does with its app's outgoing HTTP calls (<c>BRUINET_HTTP_REPLAY</c>).</summary>
public enum HttpReplayMode
{
    /// <summary>Neither: the calls go where the app sends them.</summary>
    Off,

    /// <summary>The calls go to the real services and are written to the test's recording.</summary>
    Record,

    /// <summary>The calls are answered from the recording; nothing leaves the process.</summary>
    Replay,

    /// <summary>Record again, and say which answers changed against the recording replaced.</summary>
    Refresh,
}

/// <summary>A streamed answer's part: its text and how long after the one before it came.</summary>
public sealed class HttpChunk
{
    public int DelayMs { get; set; }

    public string Data { get; set; } = string.Empty;
}

/// <summary>One call and its answer, as recorded.</summary>
public sealed class HttpExchange
{
    public int Index { get; set; }

    public string Method { get; set; } = "GET";

    /// <summary>The absolute URL; matched by its path and query, so a recording made in process replays through a proxy.</summary>
    public string Url { get; set; } = string.Empty;

    public Dictionary<string, string> RequestHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? RequestBody { get; set; }

    /// <summary><c>base64</c> when the request body is not text.</summary>
    public string? RequestBodyEncoding { get; set; }

    public int Status { get; set; } = 200;

    public Dictionary<string, string> ResponseHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? ResponseBody { get; set; }

    /// <summary><c>base64</c> when the response body is not text.</summary>
    public string? ResponseBodyEncoding { get; set; }

    /// <summary>How long the answer took, in milliseconds: waited out by a replay at recorded pace.</summary>
    public int DurationMs { get; set; }

    /// <summary>A call that got no answer (the connection refused): how it failed. A replay fails the same way.</summary>
    public string? Failure { get; set; }

    /// <summary>A streamed answer (server-sent events), part by part; then <see cref="ResponseBody"/> is null.</summary>
    public List<HttpChunk>? Chunks { get; set; }

    /// <summary>
    /// The request's values that differ between two recordings of the test (a request id, a time), matched as anything
    /// and bound to the live value in later answers: <c>$.path</c> for a JSON body field, <c>?name</c> for a query
    /// parameter, <c>/n</c> for the path's n-th segment (an id in the path).
    /// </summary>
    public List<string> Volatile { get; set; } = [];

    [JsonIgnore]
    public string PathAndQuery => HttpMatching.PathAndQuery(Url);
}

/// <summary>A test's recorded calls: <c>Recordings/http/&lt;test&gt;.json</c>.</summary>
public sealed class HttpRecording
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public int Version { get; set; } = 1;

    public List<HttpExchange> Exchanges { get; set; } = [];

    public static HttpRecording? Load(string path) => File.Exists(path) ? Parse(File.ReadAllText(path)) : null;

    public static HttpRecording? Parse(string json) => JsonSerializer.Deserialize<HttpRecording>(json, Json);

    public void Save(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
    }

    /// <summary>
    /// Marks as volatile each request value that differs from the same call in <paramref name="previous"/>, a recording of
    /// the same test: the same method and path, the same time it was sent. A value volatile there stays volatile.
    /// </summary>
    public void MarkVolatile(HttpRecording previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var earlier = Occurrences(previous.Exchanges);
        var now = Occurrences(Exchanges);
        foreach (var (key, exchange) in now)
        {
            if (!earlier.TryGetValue(key, out var before))
            {
                continue;
            }

            var left = HttpMatching.RequestValues(before);
            var right = HttpMatching.RequestValues(exchange);
            exchange.Volatile = [.. left.Keys.Union(right.Keys, StringComparer.Ordinal)
                .Where(name => !left.TryGetValue(name, out var old) || !right.TryGetValue(name, out var value) || old != value)
                .Union(before.Volatile, StringComparer.Ordinal)
                .Union(exchange.Volatile, StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)];
        }
    }

    /// <summary>Gives each call the volatile values the same call had in <paramref name="previous"/>, adding none.</summary>
    public void KeepVolatile(HttpRecording previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var earlier = Occurrences(previous.Exchanges);
        foreach (var (key, exchange) in Occurrences(Exchanges))
        {
            if (earlier.TryGetValue(key, out var before))
            {
                exchange.Volatile = [.. before.Volatile];
            }
        }
    }

    // Each exchange by its method, path and which time that call was made.
    internal static Dictionary<string, HttpExchange> Occurrences(IEnumerable<HttpExchange> exchanges)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var keyed = new Dictionary<string, HttpExchange>(StringComparer.Ordinal);
        foreach (var exchange in exchanges)
        {
            // By the path's shape: the same call with a new id in its path is the same call.
            var call = exchange.Method.ToUpperInvariant() + " " + HttpMatching.PathShape(exchange.Url);
            var occurrence = (seen.TryGetValue(call, out var count) ? count : 0) + 1;
            seen[call] = occurrence;
            keyed[call + "#" + occurrence.ToString(CultureInfo.InvariantCulture)] = exchange;
        }

        return keyed;
    }
}
