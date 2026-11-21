using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bruinet.Api.Contracts;
using Bruinet.Core.Api;

namespace Bruinet.Api.OpenApi;

/// <summary>What two descriptions of one API differ in: the sign that a back end changed, before any test fails.</summary>
public static class DescriptionDiff
{
    /// <summary>
    /// The differences, a line each: an operation added or gone, a status added or gone, and per body a field added,
    /// gone or of another type. Empty when the two say the same.
    /// </summary>
    /// <param name="before">The older description.</param>
    /// <param name="now">The newer one.</param>
    public static IReadOnlyList<string> Compare(ApiDescription before, ApiDescription now)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(now);
        var lines = new List<string>();
        var old = before.Operations.ToDictionary(operation => operation.Name, StringComparer.Ordinal);
        var current = now.Operations.ToDictionary(operation => operation.Name, StringComparer.Ordinal);
        foreach (var name in old.Keys.Union(current.Keys).Order(StringComparer.Ordinal))
        {
            if (!current.TryGetValue(name, out var after))
            {
                lines.Add($"{name}: gone");
                continue;
            }

            if (!old.TryGetValue(name, out var earlier))
            {
                lines.Add($"{name}: new");
                continue;
            }

            Fields(lines, $"{name} sends", earlier.Request, after.Request);
            foreach (var status in earlier.Responses.Keys.Union(after.Responses.Keys).Order(StringComparer.Ordinal))
            {
                if (!after.Responses.ContainsKey(status))
                {
                    lines.Add($"{name}: no longer answers {status}");
                }
                else if (!earlier.Responses.ContainsKey(status))
                {
                    lines.Add($"{name}: answers {status} now");
                }
                else
                {
                    Fields(lines, $"{name} {status}", earlier.Responses[status], after.Responses[status]);
                }
            }
        }

        return lines;
    }

    private static void Fields(List<string> lines, string where, JsonObject? before, JsonObject? now)
    {
        var (old, current) = (Shapes.Flatten(before), Shapes.Flatten(now));
        foreach (var path in old.Keys.Union(current.Keys).Order(StringComparer.Ordinal))
        {
            if (!current.TryGetValue(path, out var type))
            {
                lines.Add($"{where}: {path} gone (was {old[path]})");
            }
            else if (!old.TryGetValue(path, out var was))
            {
                lines.Add($"{where}: {path} added ({type})");
            }
            else if (was != type)
            {
                lines.Add($"{where}: {path} is {type}, was {was}");
            }
        }
    }
}

/// <summary>Exchanges held against a description: what the service answered against what the description says.</summary>
public static class DescriptionCheck
{
    /// <summary>The operations of a description that none of the exchanges called: what no test and no walk reaches.</summary>
    /// <param name="description">The description.</param>
    /// <param name="exchanges">The exchanges of a run.</param>
    public static IReadOnlyList<ApiOperation> Uncalled(ApiDescription description, IEnumerable<ApiExchange> exchanges)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(exchanges);
        var called = exchanges.Select(exchange => description.Find(exchange.Method, exchange.Path)).OfType<ApiOperation>().ToHashSet();
        return [.. description.Operations.Where(operation => !called.Contains(operation))];
    }

    /// <summary>
    /// Checks one exchange's answer against the schema the description has for its operation and status. A call or a
    /// status the description does not have is a warning; a value that does not fit its schema a failure.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <param name="exchange">The exchange.</param>
    public static ContractResult Check(ApiDescription description, ApiExchange exchange)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(exchange);
        var result = new ContractResult($"{exchange.Method.ToUpperInvariant()} {exchange.Path}");
        if (description.Find(exchange.Method, exchange.Path) is not { } operation)
        {
            result.Add(ContractSeverity.Warning, "$", "The description has no such operation.");
            return result;
        }

        if (exchange.StatusCode is not { } status)
        {
            result.Add(ContractSeverity.Blocked, "$", $"The call had no answer ({exchange.Error}).");
            return result;
        }

        var schema = operation.Response(status, out var described);
        if (!described)
        {
            result.Add(ContractSeverity.Warning, "$", $"The description of {operation.Name} has no answer {status}.");
        }
        else if (schema is not null)
        {
            if (ApiMasks.Parse(exchange.ResponseBody) is { } body)
            {
                result.ItemCount = 1;
                Value(result, schema, body, "$", 0);
            }
            else if (!string.IsNullOrWhiteSpace(exchange.ResponseBody) || status is not (204 or 304))
            {
                result.Add(string.IsNullOrWhiteSpace(exchange.ResponseBody) ? ContractSeverity.Warning : ContractSeverity.Failure, "$", "The description says the answer is JSON; the body is empty or not JSON.");
            }
        }

        return result;
    }

    /// <summary>Checks a JSON value against a schema.</summary>
    /// <param name="name">What the check is of.</param>
    /// <param name="schema">The schema, with no references left in it.</param>
    /// <param name="value">The value; null for JSON's null.</param>
    public static ContractResult Check(string name, JsonObject schema, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var result = new ContractResult(name) { ItemCount = 1 };
        Value(result, schema, value, "$", 0);
        return result;
    }

    private static void Value(ContractResult result, JsonObject schema, JsonNode? value, string at, int depth)
    {
        if (depth > 32)
        {
            return;
        }

        Composed(result, schema, value, at, depth);
        var types = Types(schema);
        if (value is null)
        {
            if (!((bool?)schema["nullable"] == true || types.Count == 0 || types.Contains("null")))
            {
                result.Add(ContractSeverity.Failure, at, $"null, where the description says {string.Join(" or ", types)}.");
            }

            return;
        }

        var kind = Kind(value);
        if (types.Count > 0 && !types.Contains(kind) && !(kind == "integer" && types.Contains("number")))
        {
            result.Add(ContractSeverity.Failure, at, $"{Article(kind)} {kind}, where the description says {string.Join(" or ", types)}.");
            return;
        }

        if (schema["enum"] is JsonArray allowed && !allowed.Any(item => item?.ToJsonString() == value.ToJsonString()))
        {
            result.Add(ContractSeverity.Failure, at, $"{Cut(value.ToJsonString())} is not one of the {allowed.Count} values the description names.");
        }

        if (value is JsonObject record)
        {
            Record(result, schema, record, at, depth);
        }
        else if (value is JsonArray list && schema["items"] is JsonObject items)
        {
            for (var i = 0; i < list.Count; i++)
            {
                Value(result, items, list[i], $"{at}[{i.ToString(CultureInfo.InvariantCulture)}]", depth + 1);
            }
        }
    }

    private static void Composed(ContractResult result, JsonObject schema, JsonNode? value, string at, int depth)
    {
        foreach (var part in schema["allOf"] as JsonArray ?? [])
        {
            if (part is JsonObject each)
            {
                Value(result, each, value, at, depth + 1);
            }
        }

        foreach (var key in new[] { "oneOf", "anyOf" })
        {
            if (schema[key] is JsonArray options && options.Count > 0
                && !options.OfType<JsonObject>().Any(option =>
                {
                    var tried = new ContractResult(at);
                    Value(tried, option, value, at, depth + 1);
                    return !tried.Failures.Any();
                }))
            {
                result.Add(ContractSeverity.Failure, at, $"The value fits none of the {options.Count} schemas of {key}.");
            }
        }
    }

    private static void Record(ContractResult result, JsonObject schema, JsonObject record, string at, int depth)
    {
        var properties = schema["properties"] as JsonObject;
        foreach (var name in (schema["required"] as JsonArray ?? []).Select(item => (string?)item).OfType<string>())
        {
            if (!record.ContainsKey(name))
            {
                result.Add(ContractSeverity.Failure, $"{at}.{name}", "Missing, where the description says every record has it.");
            }
        }

        foreach (var (name, field) in record)
        {
            if (properties?[name] is JsonObject child)
            {
                Value(result, child, field, $"{at}.{name}", depth + 1);
            }
            else if (properties is not null && schema["additionalProperties"] is not (JsonObject or JsonValue))
            {
                result.Add(ContractSeverity.Warning, $"{at}.{name}", "A field the description does not have.");
            }
            else if (schema["additionalProperties"] is JsonObject extra)
            {
                Value(result, extra, field, $"{at}.{name}", depth + 1);
            }
        }
    }

    private static List<string> Types(JsonObject schema)
        => schema["type"] switch
        {
            JsonArray list => [.. list.Select(item => (string?)item).OfType<string>()],
            JsonValue one => [one.GetValue<string>()],
            _ => [],
        };

    private static string Kind(JsonNode value)
        => value.GetValueKind() switch
        {
            JsonValueKind.Object => "object",
            JsonValueKind.Array => "array",
            JsonValueKind.String => "string",
            JsonValueKind.Number => value.ToJsonString().AsSpan().IndexOfAny('.', 'e', 'E') < 0 ? "integer" : "number",
            _ => "boolean",
        };

    private static string Article(string kind) => kind[0] is 'a' or 'i' or 'o' ? "An" : "A";

    private static string Cut(string text) => text.Length <= 60 ? text : text[..60] + "…";
}

/// <summary>An example of what a schema describes: the body a fake service answers with.</summary>
public static class SchemaSample
{
    /// <summary>
    /// A value that fits a schema: its <c>example</c> or first <c>enum</c> value when it has one, else every field of
    /// an object, one item in a list, and a plain value per type.
    /// </summary>
    /// <param name="schema">The schema, with no references left in it; null gives null.</param>
    public static JsonNode? Of(JsonObject? schema) => Of(schema, 0);

    private static JsonNode? Of(JsonObject? schema, int depth)
    {
        if (schema is null || depth > 12)
        {
            return null;
        }

        if (schema["example"] is { } example)
        {
            return example.DeepClone();
        }

        if (schema["enum"] is JsonArray { Count: > 0 } allowed)
        {
            return allowed[0]?.DeepClone();
        }

        if ((schema["allOf"] ?? schema["oneOf"] ?? schema["anyOf"]) is JsonArray { Count: > 0 } parts && schema["properties"] is null)
        {
            return Composed(schema, parts, depth);
        }

        switch (TypeOf(schema))
        {
            case "object":
                var record = new JsonObject();
                foreach (var (name, field) in schema["properties"] as JsonObject ?? [])
                {
                    record[name] = Of(field as JsonObject, depth + 1);
                }

                return record;
            case "array":
                var item = Of(schema["items"] as JsonObject, depth + 1);
                return item is null ? [] : new JsonArray(item);
            case "integer":
                return 0;
            case "number":
                return 0.0;
            case "boolean":
                return false;
            case "string":
                return (string?)schema["format"] switch
                {
                    "date-time" => "2026-01-01T00:00:00Z",
                    "date" => "2026-01-01",
                    "uuid" => "00000000-0000-0000-0000-000000000000",
                    _ => "text",
                };
            default:
                return null;
        }
    }

    // The first of oneOf or anyOf; every part of allOf joined.
    private static JsonNode? Composed(JsonObject schema, JsonArray parts, int depth)
    {
        if (schema["allOf"] is null)
        {
            return Of(parts[0] as JsonObject, depth + 1);
        }

        var joined = new JsonObject();
        foreach (var part in parts.OfType<JsonObject>())
        {
            foreach (var (name, field) in Of(part, depth + 1) as JsonObject ?? [])
            {
                joined[name] = field?.DeepClone();
            }
        }

        return joined;
    }

    private static string? TypeOf(JsonObject schema)
        => schema["type"] switch
        {
            JsonArray list => list.Select(item => (string?)item).FirstOrDefault(item => item is not (null or "null")),
            JsonValue one => one.GetValue<string>(),
            _ => schema["properties"] is not null ? "object" : schema["x-observed-types"] is JsonArray seen ? (string?)seen.FirstOrDefault() : null,
        };
}
