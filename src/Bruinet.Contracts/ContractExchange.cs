using System.Text.Json;

namespace Bruinet.Contracts;

/// <summary>
/// One HTTP call the app made and its answer, as the app's own exchange log recorded it. An app maps its
/// log entries into this record, so the checks do not depend on the app's log format.
/// </summary>
/// <param name="Id">Unique per call, so a log read repeatedly keeps each call once.</param>
/// <param name="TimestampUtc">When the call started.</param>
/// <param name="Method">The HTTP method, for example <c>GET</c>.</param>
/// <param name="Path">The absolute URL, or the path with its query.</param>
/// <param name="StatusCode">The answer's status; null when there was no answer.</param>
/// <param name="ContentType">The answer's content type.</param>
/// <param name="ResponseBody">The answer's body as recorded.</param>
public sealed record ContractExchange(
    string Id,
    DateTimeOffset TimestampUtc,
    string Method,
    string Path,
    int? StatusCode,
    string? ContentType,
    string? ResponseBody)
{
    /// <summary>The app capped the recorded body, so a list keeps only its leading items.</summary>
    public bool IsTruncated { get; init; }

    /// <summary>The transport error or exception type when the call had no answer.</summary>
    public string? Error { get; init; }

    public string? RequestContentType { get; init; }

    public string? RequestBody { get; init; }

    /// <summary>The app environment that made the call, for example <c>Acceptance</c>.</summary>
    public string? Environment { get; init; }
}

/// <summary>Finds exchanges by contract and reads their paths.</summary>
public static class ContractExchanges
{
    /// <summary>
    /// The newest exchange for the contract's endpoint recorded at or after <paramref name="since"/>.
    /// Paged calls end with an empty page, so an exchange whose body holds data wins over a newer empty one.
    /// </summary>
    public static ContractExchange? Newest(IEnumerable<ContractExchange> exchanges, DtoContract contract, DateTimeOffset since)
        => Matching(exchanges, contract, since)
            .OrderByDescending(exchange => HasData(exchange.ResponseBody))
            .ThenByDescending(exchange => exchange.TimestampUtc)
            .FirstOrDefault();

    /// <summary>The exchanges at or after <paramref name="since"/> that match the contract's method and path.</summary>
    public static IEnumerable<ContractExchange> Matching(IEnumerable<ContractExchange> exchanges, DtoContract contract, DateTimeOffset since)
        => exchanges.Where(exchange => exchange.TimestampUtc >= since
            && exchange.Method.Equals(contract.Method, StringComparison.OrdinalIgnoreCase)
            && contract.MatchesPath(AbsolutePath(exchange.Path)));

    /// <summary>True when a body holds at least one item or field; an unparseable body counts as data.</summary>
    public static bool HasData(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("items", out var items)
                && items.ValueKind == JsonValueKind.Array)
                root = items;
            return root.ValueKind switch
            {
                JsonValueKind.Array => root.GetArrayLength() > 0,
                JsonValueKind.Object => root.EnumerateObject().Any(),
                _ => false
            };
        }
        catch (JsonException)
        {
            // An unparseable body is still evidence; the validator reports it.
            return true;
        }
    }

    public static string AbsolutePath(string path)
        => Uri.TryCreate(path, UriKind.Absolute, out var uri)
            ? uri.AbsolutePath
            : path.Split('?', 2)[0];

    public static string? Host(string path)
        => Uri.TryCreate(path, UriKind.Absolute, out var uri) ? uri.Host : null;
}
