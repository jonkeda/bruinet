using System.Globalization;
using System.Text.Json;

namespace Bruinet.Mocking.Http;

/// <summary>
/// How a live call finds its recorded exchange: the same method, path and query, and the same body: a JSON body field by
/// field. Volatile values and a preset's fields are left out of the comparison on both sides.
/// </summary>
internal static class HttpMatching
{
    /// <summary>The path of <paramref name="url"/>, absolute or not, without its query.</summary>
    public static string Path(string url)
    {
        var pathAndQuery = PathAndQuery(url);
        var question = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
        return question < 0 ? pathAndQuery : pathAndQuery[..question];
    }

    /// <summary>The path and query of <paramref name="url"/>, absolute or not.</summary>
    public static string PathAndQuery(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var absolute) ? absolute.PathAndQuery : (url.StartsWith('/') ? url : "/" + url);

    /// <summary>
    /// A request's compared values by name: <c>?name</c> for each query parameter (repeated ones joined), <c>$.path</c>
    /// for each JSON body leaf, <c>$</c> for a body that is not JSON.
    /// </summary>
    public static Dictionary<string, string> RequestValues(HttpExchange exchange)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var pathAndQuery = PathAndQuery(exchange.Url);
        var question = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
        if (question >= 0)
        {
            foreach (var group in pathAndQuery[(question + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('=', 2))
                .GroupBy(pair => Uri.UnescapeDataString(pair[0]), StringComparer.Ordinal))
            {
                values["?" + group.Key] = string.Join(",", group.Select(pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : string.Empty));
            }
        }

        if (string.IsNullOrEmpty(exchange.RequestBody))
        {
            return values;
        }

        if (exchange.RequestBodyEncoding is null && TryParse(exchange.RequestBody, out var document))
        {
            using (document)
            {
                Flatten(document.RootElement, "$", values);
            }
        }
        else
        {
            values["$"] = exchange.RequestBody;
        }

        return values;
    }

    /// <summary>Whether <paramref name="live"/> is the call <paramref name="recorded"/> answers.</summary>
    public static bool Matches(HttpExchange live, HttpExchange recorded, IReadOnlyCollection<string> ignoredFields)
        => Difference(live, recorded, ignoredFields) is null;

    /// <summary>The first way <paramref name="live"/> differs from <paramref name="recorded"/>, or null when it does not.</summary>
    public static string? Difference(HttpExchange live, HttpExchange recorded, IReadOnlyCollection<string> ignoredFields)
    {
        if (!string.Equals(live.Method, recorded.Method, StringComparison.OrdinalIgnoreCase))
        {
            return $"method {live.Method} where {recorded.Method} was recorded";
        }

        if (!string.Equals(Path(live.Url), Path(recorded.Url), StringComparison.Ordinal))
        {
            return $"path {Path(live.Url)} where {Path(recorded.Url)} was recorded";
        }

        var left = RequestValues(live);
        var right = RequestValues(recorded);
        foreach (var name in left.Keys.Union(right.Keys, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal))
        {
            if (Ignored(name, recorded.Volatile, ignoredFields))
            {
                continue;
            }

            var now = left.TryGetValue(name, out var value) ? value : null;
            var then = right.TryGetValue(name, out var old) ? old : null;
            // A redacted value was never written, so it matches what the live call sends.
            if (now != then && then is not (HttpRedaction.Redacted or HttpRedaction.RedactedJson))
            {
                return $"{name} is {Show(now)} where {Show(then)} was recorded";
            }
        }

        return null;
    }

    /// <summary>Whether a value is left out: volatile in the recording, or a preset's field (by its last name).</summary>
    public static bool Ignored(string name, IReadOnlyCollection<string> volatileValues, IReadOnlyCollection<string> ignoredFields)
    {
        if (volatileValues.Contains(name))
        {
            return true;
        }

        var last = name.StartsWith('?') ? name[1..] : LastName(name);
        return last.Length > 0 && ignoredFields.Contains(last);
    }

    public static bool TryParse(string text, out JsonDocument document)
    {
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            try
            {
                document = JsonDocument.Parse(text);
                return true;
            }
            catch (JsonException)
            {
                // Not JSON after all: compared as text.
            }
        }

        document = null!;
        return false;
    }

    private static void Flatten(JsonElement element, string path, Dictionary<string, string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Flatten(property.Value, path + "." + property.Name, values);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Flatten(item, path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]", values);
                    index++;
                }

                break;
            default:
                values[path] = element.GetRawText();
                break;
        }
    }

    // "$.messages[0].content" -> "content"; "$.ids[3]" -> "ids".
    private static string LastName(string path)
    {
        var end = path.Length;
        while (end > 0 && path[end - 1] == ']')
        {
            end = path.LastIndexOf('[', end - 1);
        }

        var dot = path.LastIndexOf('.', Math.Max(0, end - 1));
        return dot < 0 ? string.Empty : path[(dot + 1)..end];
    }

    private static string Show(string? value) => value is null ? "absent" : value.Length > 80 ? "'" + value[..80] + "…'" : "'" + value + "'";
}
