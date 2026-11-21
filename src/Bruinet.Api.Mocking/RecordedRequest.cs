using System.Text;
using System.Text.Json;
using WireMock;

namespace Bruinet.Api.Mocking;

/// <summary>
/// A request the mock server received, as a test needs to read it.
/// </summary>
/// <remarks>
/// A copy rather than WireMock's own <c>IRequestMessage</c>, so a test that asserts on requests
/// does not take a WireMock dependency, and so a request read once does not change when the
/// server's log does.
/// </remarks>
public sealed class RecordedRequest
{
    private RecordedRequest(
        string method,
        string path,
        IReadOnlyDictionary<string, string> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        DateTime receivedAtUtc)
    {
        Method = method;
        Path = path;
        Query = query;
        Headers = headers;
        Body = body;
        ReceivedAtUtc = receivedAtUtc;
    }

    /// <summary>The HTTP method, upper case.</summary>
    public string Method { get; }

    /// <summary>The path, without the query string.</summary>
    public string Path { get; }

    /// <summary>Query parameters. A repeated parameter keeps its values comma-joined.</summary>
    public IReadOnlyDictionary<string, string> Query { get; }

    /// <summary>Request headers, case-insensitive. A repeated header keeps its values comma-joined.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>The body as text, or <c>null</c> when there was none.</summary>
    public string? Body { get; }

    /// <summary>When the server received the request.</summary>
    public DateTime ReceivedAtUtc { get; }

    /// <summary>
    /// Deserializes the body, or returns <c>default</c> when there is none.
    /// </summary>
    /// <param name="options">Serializer options; the web defaults when omitted.</param>
    public T? BodyAs<T>(JsonSerializerOptions? options = null)
        => string.IsNullOrWhiteSpace(Body)
            ? default
            : JsonSerializer.Deserialize<T>(Body, options ?? MockApiServer.DefaultJsonOptions);

    /// <summary>
    /// Whether the body is JSON that satisfies <paramref name="predicate"/>. Not JSON is not a match.
    /// </summary>
    public bool BodyMatchesJson(Func<JsonElement, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (string.IsNullOrWhiteSpace(Body))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(Body);
            return predicate(document.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the body as <c>application/x-www-form-urlencoded</c> fields.
    /// </summary>
    public IReadOnlyDictionary<string, string> BodyAsForm()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(Body))
        {
            return values;
        }

        foreach (var pair in Body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = DecodeFormValue(parts[0]);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            values[key] = parts.Length > 1 ? DecodeFormValue(parts[1]) : string.Empty;
        }

        return values;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Method} {Path}";

    internal static RecordedRequest From(IRequestMessage message) => new(
        message.Method.ToUpperInvariant(),
        message.Path,
        Flatten(message.Query),
        Flatten(message.Headers),
        BodyText(message),
        message.DateTime.ToUniversalTime());

    private static IReadOnlyDictionary<string, string> Flatten<TValues>(IDictionary<string, TValues>? source)
        where TValues : IEnumerable<string>
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
        {
            return result;
        }

        foreach (var (key, values) in source)
        {
            result[key] = string.Join(",", values);
        }

        return result;
    }

    private static string? BodyText(IRequestMessage message)
    {
        if (!string.IsNullOrEmpty(message.Body))
        {
            return message.Body;
        }

        if (!string.IsNullOrEmpty(message.BodyData?.BodyAsString))
        {
            return message.BodyData.BodyAsString;
        }

        return message.BodyAsBytes is { Length: > 0 } bytes
            ? Encoding.UTF8.GetString(bytes)
            : null;
    }

    private static string DecodeFormValue(string value)
        => Uri.UnescapeDataString(value.Replace("+", " ", StringComparison.Ordinal));
}
