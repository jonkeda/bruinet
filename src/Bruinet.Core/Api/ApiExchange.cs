using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bruinet.Core.Api;

/// <summary>
/// One call an <see cref="ApiSession"/> made and its answer, with the bodies masked: what the step log shows, what a
/// contract check reads and what an observed description is merged from.
/// </summary>
/// <param name="Id">Unique per call.</param>
/// <param name="TimestampUtc">When the call started.</param>
/// <param name="Method">The HTTP method, such as <c>GET</c>.</param>
/// <param name="Url">The absolute address called, with its query.</param>
/// <param name="StatusCode">The answer's status; null when there was no answer.</param>
/// <param name="ContentType">The answer's content type.</param>
/// <param name="ResponseBody">The answer's body, masked.</param>
public sealed record ApiExchange(
    string Id,
    DateTimeOffset TimestampUtc,
    string Method,
    string Url,
    int? StatusCode,
    string? ContentType,
    string? ResponseBody)
{
    /// <summary>The content type of what was sent.</summary>
    public string? RequestContentType { get; init; }

    /// <summary>What was sent, masked.</summary>
    public string? RequestBody { get; init; }

    /// <summary>The account the call was made as.</summary>
    public string? Account { get; init; }

    /// <summary>How long the call took.</summary>
    public long DurationMs { get; init; }

    /// <summary>The transport error when the call had no answer.</summary>
    public string? Error { get; init; }

    /// <summary>Where the call was seen: <c>api</c> for a session's own call, <c>page</c> for one a page made.</summary>
    public string Source { get; init; } = "api";

    /// <summary>The address's path, without host and query.</summary>
    public string Path => Uri.TryCreate(Url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : Url.Split('?', 2)[0];

    /// <summary>Whether the answer's status says the call succeeded.</summary>
    public bool Succeeded => StatusCode is >= 200 and < 300;

    /// <summary>The answer's body as JSON; null when it is empty or not JSON.</summary>
    public JsonNode? Json() => ApiMasks.Parse(ResponseBody);

    /// <summary>What was sent as JSON; null when it is empty or not JSON.</summary>
    public JsonNode? SentJson() => ApiMasks.Parse(RequestBody);

    /// <summary>The call in one line: method, path, account and status.</summary>
    public string Describe()
        => $"{Method} {Path}{(Account is null ? string.Empty : " as " + Account)} answered {(StatusCode is { } status ? status.ToString(CultureInfo.InvariantCulture) : Error ?? "nothing")}";
}

/// <summary>A call of an <see cref="ApiSession"/> failed: refused before it was sent, or answered with an error.</summary>
public sealed class ApiCallException : Exception
{
    /// <summary>A failure with no message.</summary>
    public ApiCallException()
    {
    }

    /// <summary>A failure with its message.</summary>
    /// <param name="message">What failed: the method, path, account and status.</param>
    public ApiCallException(string message)
        : base(message)
    {
    }

    /// <summary>A failure with its message and its cause.</summary>
    /// <param name="message">What failed.</param>
    /// <param name="innerException">The cause.</param>
    public ApiCallException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The exchange that failed; null when the call was refused before it was sent.</summary>
    public ApiExchange? Exchange { get; init; }
}

/// <summary>The values a session never writes down: named JSON fields become <c>***</c>.</summary>
public static class ApiMasks
{
    /// <summary>What a masked value reads as.</summary>
    public const string Masked = "***";

    /// <summary>The field names masked when a suite names none.</summary>
    public static IReadOnlyCollection<string> DefaultFields { get; } =
        ["password", "token", "access_token", "refresh_token", "id_token", "secret", "client_secret", "apiKey", "api_key", "authorization"];

    /// <summary>A body with the named fields masked, at any depth. A body that is not JSON is returned as it is.</summary>
    /// <param name="body">The body.</param>
    /// <param name="fields">The names of the fields to mask, compared without case.</param>
    public static string? Mask(string? body, IReadOnlyCollection<string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (string.IsNullOrEmpty(body) || fields.Count == 0 || Parse(body) is not { } node)
        {
            return body;
        }

        return MaskNode(node, fields) ? node.ToJsonString() : body;
    }

    /// <summary>A text as JSON; null when it is empty or not a JSON object or list.</summary>
    /// <param name="text">The text.</param>
    public static JsonNode? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.TrimStart()[0] is not ('{' or '['))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool MaskNode(JsonNode node, IReadOnlyCollection<string> fields)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject record:
                foreach (var name in record.Select(field => field.Key).ToList())
                {
                    if (fields.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        if (record[name] is not null)
                        {
                            record[name] = Masked;
                            changed = true;
                        }
                    }
                    else if (record[name] is { } child)
                    {
                        changed |= MaskNode(child, fields);
                    }
                }

                break;
            case JsonArray list:
                foreach (var item in list)
                {
                    if (item is not null)
                    {
                        changed |= MaskNode(item, fields);
                    }
                }

                break;
        }

        return changed;
    }
}
