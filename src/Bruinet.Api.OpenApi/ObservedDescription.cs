using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bruinet.Core.Api;

namespace Bruinet.Api.OpenApi;

/// <summary>
/// A description of an API merged from the exchanges Bruinet saw (a walk, a test run, the API objects' own calls):
/// each address with its parameters found by comparing calls, the shape of each body, and how many exchanges stand
/// behind each operation. It keeps shapes only, never a value, and it says what was observed, never what is promised.
/// </summary>
public static partial class ObservedDescription
{
    /// <summary>Merges exchanges into a description.</summary>
    /// <param name="exchanges">The exchanges; one with no answer is skipped.</param>
    /// <param name="title">The API's name.</param>
    /// <param name="hosts">Only the calls to these hosts, when given: a page also calls maps, fonts and trackers.</param>
    public static ApiDescription Merge(IEnumerable<ApiExchange> exchanges, string title = "API", IReadOnlyCollection<string>? hosts = null)
    {
        ArgumentNullException.ThrowIfNull(exchanges);
        var operations = new Dictionary<(string Method, string Path), Seen>();
        var servers = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var exchange in exchanges)
        {
            if (exchange.StatusCode is not { } status || !Uri.TryCreate(exchange.Url, UriKind.Absolute, out var address)
                || (hosts is { Count: > 0 } && !hosts.Contains(address.Host, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            servers.Add(address.GetLeftPart(UriPartial.Authority));
            var (path, values) = Template(address.AbsolutePath);
            var key = (exchange.Method.ToUpperInvariant(), path);
            if (!operations.TryGetValue(key, out var seen))
            {
                operations[key] = seen = new Seen(values);
            }

            seen.Count++;
            foreach (var (name, value) in Query(address.Query))
            {
                seen.Query[name] = seen.Query.TryGetValue(name, out var before)
                    ? (before.Count + 1, Wider(before.Type, TypeOf(value)))
                    : (1, TypeOf(value));
            }

            if (ApiMasks.Parse(exchange.RequestBody) is { } sent)
            {
                seen.Request = Shapes.Merge(seen.Request, Shapes.Of(sent));
            }

            var answered = status.ToString(CultureInfo.InvariantCulture);
            var answer = ApiMasks.Parse(exchange.ResponseBody);
            seen.Responses[answered] = answer is null
                ? seen.Responses.GetValueOrDefault(answered)
                : Shapes.Merge(seen.Responses.GetValueOrDefault(answered), Shapes.Of(answer));
        }

        return new ApiDescription
        {
            Title = title,
            Version = "observed",
            IsObserved = true,
            Servers = [.. servers],
            Operations = [.. operations.OrderBy(each => each.Key.Path, StringComparer.Ordinal).ThenBy(each => each.Key.Method, StringComparer.Ordinal).Select(each => new ApiOperation(each.Key.Method, each.Key.Path)
            {
                Observed = each.Value.Count,
                Parameters =
                [
                    .. each.Value.PathValues.Select(name => new ApiParameter(name.Name, "path", true, name.Type)),
                    .. each.Value.Query.OrderBy(query => query.Key, StringComparer.Ordinal).Select(query => new ApiParameter(query.Key, "query", query.Value.Count == each.Value.Count, query.Value.Type)),
                ],
                Request = each.Value.Request,
                Responses = each.Value.Responses,
            })],
        };
    }

    /// <summary>A path with <c>{id}</c> where a value stands: a number, a GUID or a long hexadecimal text.</summary>
    /// <param name="path">A real path.</param>
    /// <returns>The path as a template, and the name and type of each value in it.</returns>
    public static (string Path, IReadOnlyList<(string Name, string Type)> Values) Template(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var values = new List<(string, string)>();
        var segments = path.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var type = Digits().IsMatch(segments[i]) ? "integer" : Guid.TryParse(segments[i], out _) || LongHex().IsMatch(segments[i]) ? "string" : null;
            if (type is not null)
            {
                var name = values.Count == 0 ? "id" : $"id{values.Count + 1}";
                values.Add((name, type));
                segments[i] = "{" + name + "}";
            }
        }

        return (string.Join('/', segments), values);
    }

    private static IEnumerable<(string Name, string Value)> Query(string query)
        => query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Select(pair => (Uri.UnescapeDataString(pair[0]), pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty));

    private static string TypeOf(string value)
        => Digits().IsMatch(value) ? "integer"
            : decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? "number"
            : value is "true" or "false" ? "boolean"
            : "string";

    private static string Wider(string? before, string now)
        => before is null || before == now ? now
            : (before, now) is ("integer", "number") or ("number", "integer") ? "number"
            : "string";

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex Digits();

    [GeneratedRegex("^[0-9a-fA-F]{16,}$")]
    private static partial Regex LongHex();

    private sealed class Seen(IReadOnlyList<(string Name, string Type)> pathValues)
    {
        public IReadOnlyList<(string Name, string Type)> PathValues { get; } = pathValues;

        public int Count { get; set; }

        public Dictionary<string, (int Count, string Type)> Query { get; } = new(StringComparer.Ordinal);

        public JsonObject? Request { get; set; }

        public Dictionary<string, JsonObject?> Responses { get; } = new(StringComparer.Ordinal);
    }
}

/// <summary>The shape of a JSON value as a schema, and two shapes merged into one that takes both.</summary>
public static partial class Shapes
{
    /// <summary>The shape of a value: its type, an object's fields (all required), a list's items merged. No value is kept.</summary>
    /// <param name="value">The value; null for JSON's null.</param>
    public static JsonObject Of(JsonNode? value)
    {
        switch (value)
        {
            case null:
                return new JsonObject { ["nullable"] = true };
            case JsonObject record:
                var properties = new JsonObject();
                foreach (var (name, field) in record)
                {
                    properties[name] = Of(field);
                }

                return new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = new JsonArray([.. record.Select(field => (JsonNode)field.Key)]),
                };
            case JsonArray list:
                JsonObject? items = null;
                foreach (var item in list)
                {
                    items = Merge(items, Of(item));
                }

                return new JsonObject { ["type"] = "array", ["items"] = items ?? [] };
            default:
                return value.GetValueKind() switch
                {
                    JsonValueKind.String => IsoMoment().IsMatch(value.GetValue<string>())
                        ? new JsonObject { ["type"] = "string", ["format"] = value.GetValue<string>().Length == 10 ? "date" : "date-time" }
                        : new JsonObject { ["type"] = "string" },
                    JsonValueKind.Number => new JsonObject { ["type"] = value.ToJsonString().AsSpan().IndexOfAny('.', 'e', 'E') < 0 ? "integer" : "number" },
                    _ => new JsonObject { ["type"] = "boolean" },
                };
        }
    }

    /// <summary>
    /// One shape that takes both: an object's fields joined (required only when both have it), a list's items merged,
    /// an integer and a number a number, null beside a type that type made nullable; two types that do not go together
    /// are any value, with the types seen under <c>x-observed-types</c>.
    /// </summary>
    /// <param name="one">A shape, or null for none yet.</param>
    /// <param name="other">Another shape.</param>
    public static JsonObject Merge(JsonObject? one, JsonObject other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (one is null)
        {
            return (JsonObject)other.DeepClone();
        }

        var nullable = (bool?)one["nullable"] == true || (bool?)other["nullable"] == true;
        var (oneType, otherType) = ((string?)one["type"], (string?)other["type"]);
        JsonObject merged;
        if (SaysNoType(one))
        {
            merged = (JsonObject)other.DeepClone();
        }
        else if (SaysNoType(other))
        {
            merged = (JsonObject)one.DeepClone();
        }
        else if (oneType == otherType && oneType == "object")
        {
            merged = MergeRecords(one, other);
        }
        else if (oneType == otherType && oneType == "array")
        {
            merged = new JsonObject { ["type"] = "array", ["items"] = Merge(one["items"] as JsonObject, other["items"] as JsonObject ?? []) };
        }
        else if (oneType == otherType && oneType is not null)
        {
            merged = new JsonObject { ["type"] = oneType };
            if ((string?)one["format"] is { } format && format == (string?)other["format"])
            {
                merged["format"] = format;
            }
        }
        else if ((oneType, otherType) is ("integer", "number") or ("number", "integer"))
        {
            merged = new JsonObject { ["type"] = "number" };
        }
        else
        {
            var types = Names(one["x-observed-types"]).Append(oneType).Concat(Names(other["x-observed-types"])).Append(otherType)
                .OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
            merged = new JsonObject { ["x-observed-types"] = new JsonArray([.. types.Select(type => (JsonNode)type)]) };
        }

        if (nullable)
        {
            merged["nullable"] = true;
        }

        return merged;
    }

    // A shape that only says "nothing yet" or "null": an empty list's items, a null value.
    private static bool SaysNoType(JsonObject shape)
        => shape["type"] is null && shape["x-observed-types"] is null && (shape.Count == 0 || shape["nullable"] is not null);

    private static JsonObject MergeRecords(JsonObject one, JsonObject other)
    {
        var properties = new JsonObject();
        var (ours, theirs) = (one["properties"] as JsonObject ?? [], other["properties"] as JsonObject ?? []);
        foreach (var name in ours.Select(field => field.Key).Union(theirs.Select(field => field.Key)))
        {
            properties[name] = ours[name] is JsonObject mine
                ? theirs[name] is JsonObject yours ? Merge(mine, yours) : mine.DeepClone()
                : theirs[name]!.DeepClone();
        }

        var required = Names(one["required"]).Intersect(Names(other["required"]), StringComparer.Ordinal).ToList();
        var merged = new JsonObject { ["type"] = "object", ["properties"] = properties };
        if (required.Count > 0)
        {
            merged["required"] = new JsonArray([.. required.Select(name => (JsonNode)name)]);
        }

        return merged;
    }

    /// <summary>
    /// A schema as lines a diff can compare: each field's path with its type (<c>items[].amount: number</c>), a field
    /// every record has marked with <c>!</c>, a nullable one with <c>?</c>.
    /// </summary>
    /// <param name="schema">The schema; null for none.</param>
    public static IReadOnlyDictionary<string, string> Flatten(JsonObject? schema)
    {
        var lines = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (schema is not null)
        {
            Walk(schema, string.Empty, false, lines, 0);
        }

        return lines;
    }

    private static void Walk(JsonObject schema, string path, bool required, SortedDictionary<string, string> lines, int depth)
    {
        var type = (string?)schema["type"]
            ?? (schema["x-observed-types"] is JsonArray seen ? $"any({string.Join('|', Names(seen))})"
                : schema["properties"] is not null ? "object"
                : schema["allOf"] is not null || schema["oneOf"] is not null || schema["anyOf"] is not null ? "composed"
                : "any");
        if (path.Length > 0)
        {
            lines[path] = type + ((bool?)schema["nullable"] == true ? "?" : string.Empty) + (required ? "!" : string.Empty);
        }

        if (depth > 16)
        {
            return;
        }

        var needed = Names(schema["required"]).ToHashSet(StringComparer.Ordinal);
        foreach (var (name, field) in schema["properties"] as JsonObject ?? [])
        {
            if (field is JsonObject child)
            {
                Walk(child, path.Length == 0 ? name : $"{path}.{name}", needed.Contains(name), lines, depth + 1);
            }
        }

        if (schema["items"] is JsonObject items)
        {
            Walk(items, path + "[]", false, lines, depth + 1);
        }

        foreach (var part in schema["allOf"] as JsonArray ?? [])
        {
            if (part is JsonObject child)
            {
                Walk(child, path, required, lines, depth + 1);
            }
        }
    }

    private static IEnumerable<string> Names(JsonNode? list) => (list as JsonArray ?? []).Select(item => (string?)item).OfType<string>();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}([T ]\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:?\d{2})?)?$")]
    private static partial Regex IsoMoment();
}
