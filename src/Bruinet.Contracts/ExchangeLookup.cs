using System.Text.Json;

namespace Bruinet.Contracts;

/// <summary>Finds the exchanges and items that belong to one entity in the recorded exchange log.</summary>
public static class ExchangeLookup
{
    public static ContractExchange? NewestForId(IEnumerable<ContractExchange> exchanges, DtoContract contract, DateTimeOffset since, int id)
        => exchanges
            .Where(exchange => exchange.TimestampUtc >= since
                && exchange.Method.Equals(contract.Method, StringComparison.OrdinalIgnoreCase)
                && contract.MatchesPath(ContractExchanges.AbsolutePath(exchange.Path))
                && ContractExchanges.AbsolutePath(exchange.Path).TrimEnd('/').EndsWith($"/{id}", StringComparison.Ordinal))
            .OrderByDescending(exchange => exchange.TimestampUtc)
            .FirstOrDefault();

    public static (ContractExchange? Exchange, string? Item) ListPageWith(IEnumerable<ContractExchange> exchanges, DtoContract listContract, DateTimeOffset since, int id)
    {
        var pages = exchanges
            .Where(exchange => exchange.TimestampUtc >= since
                && exchange.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)
                && listContract.MatchesPath(ContractExchanges.AbsolutePath(exchange.Path)))
            .OrderByDescending(exchange => exchange.TimestampUtc);

        foreach (var page in pages)
        {
            if (FindItem(page.ResponseBody, id) is { } item)
                return (page, item);
        }

        return (null, null);
    }

    /// <summary>
    /// The list item with <paramref name="id"/>: from a root array, from the array properties of a page
    /// object such as <c>{"items": [...]}</c>, or from the complete leading items of a truncated root array.
    /// </summary>
    public static string? FindItem(string? body, int id)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            using var document = JsonDocument.Parse(body);
            return FindItem(document.RootElement, id);
        }
        catch (JsonException)
        {
        }

        var complete = TruncatedJson.CompleteArrayItems(body)?.Json;
        if (complete is null)
            return null;
        using var leading = JsonDocument.Parse(complete);
        return FindItem(leading.RootElement, id);
    }

    private static string? FindItem(JsonElement root, int id)
    {
        List<JsonElement> arrays = root.ValueKind switch
        {
            JsonValueKind.Array => [root],
            JsonValueKind.Object => root.EnumerateObject()
                .Where(property => property.Value.ValueKind == JsonValueKind.Array)
                .Select(property => property.Value)
                .ToList(),
            _ => []
        };
        return arrays
            .SelectMany(array => array.EnumerateArray())
            .Where(item => item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("id", out var itemId)
                && itemId.ValueKind == JsonValueKind.Number
                && itemId.TryGetInt32(out var value)
                && value == id)
            .Select(item => item.GetRawText())
            .FirstOrDefault();
    }

    public static int? ReadId(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("id", out var id)
                && id.ValueKind == JsonValueKind.Number
                && id.TryGetInt32(out var value)
                    ? value
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static T? Deserialize<T>(string? json, JsonSerializerOptions options) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json, options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static ContractResult Warning(string endpoint, string message)
    {
        var result = new ContractResult(endpoint);
        result.Add(ContractSeverity.Warning, "$", message);
        return result;
    }

    /// <summary>The unescaped value of one query parameter of a recorded request path, or null.</summary>
    public static string? QueryParameter(string? path, string name)
    {
        var start = path?.IndexOf('?', StringComparison.Ordinal) ?? -1;
        if (start < 0)
            return null;
        foreach (var pair in path![(start + 1)..].Split('&'))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0 && Uri.UnescapeDataString(pair[..separator]) == name)
                return Uri.UnescapeDataString(pair[(separator + 1)..].Replace('+', ' '));
        }

        return null;
    }
}
