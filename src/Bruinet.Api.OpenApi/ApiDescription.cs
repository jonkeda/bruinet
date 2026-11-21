using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bruinet.Api.OpenApi;

/// <summary>A parameter of an operation: in its path or its query.</summary>
/// <param name="Name">The parameter's name.</param>
/// <param name="In"><c>path</c> or <c>query</c> (or <c>header</c>, <c>cookie</c> in a description someone wrote).</param>
/// <param name="Required">Whether every call has it.</param>
/// <param name="Type">The JSON type of its value, such as <c>string</c> or <c>integer</c>.</param>
public sealed record ApiParameter(string Name, string In, bool Required, string Type);

/// <summary>One call of an API: a method on a path, with what is sent and what each status answers.</summary>
public sealed class ApiOperation
{
    private readonly string[] _segments;

    /// <summary>An operation.</summary>
    /// <param name="method">The HTTP method, in capitals.</param>
    /// <param name="path">The path, with <c>{name}</c> where a value goes.</param>
    public ApiOperation(string method, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Method = method.ToUpperInvariant();
        Path = path;
        _segments = path.Trim('/').Split('/');
    }

    /// <summary>The HTTP method, in capitals.</summary>
    public string Method { get; }

    /// <summary>The path, with <c>{name}</c> where a value goes.</summary>
    public string Path { get; }

    /// <summary>How many exchanges stand behind it in an observed description; 0 in one someone wrote.</summary>
    public int Observed { get; init; }

    /// <summary>Its parameters.</summary>
    public IReadOnlyList<ApiParameter> Parameters { get; init; } = [];

    /// <summary>The schema of the JSON body sent; null when it sends none.</summary>
    public JsonObject? Request { get; init; }

    /// <summary>The schema of the JSON answer per status (<c>200</c>, <c>default</c>); a null schema is an answer without a JSON body.</summary>
    public IReadOnlyDictionary<string, JsonObject?> Responses { get; init; } = new Dictionary<string, JsonObject?>();

    /// <summary>The method and the path, as one name: <c>GET /contact/{id}</c>.</summary>
    public string Name => $"{Method} {Path}";

    /// <summary>Whether a real path is one of this operation's: the same segments, any value where a <c>{name}</c> stands.</summary>
    /// <param name="path">A path without host and query.</param>
    public bool Matches(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var real = path.Trim('/').Split('/');
        if (real.Length != _segments.Length)
        {
            return false;
        }

        for (var i = 0; i < real.Length; i++)
        {
            var template = _segments[i];
            if (!(template.StartsWith('{') && template.EndsWith('}')) && !string.Equals(template, real[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The schema of the answer with a status: its own, else <c>2XX</c>-style, else <c>default</c>; null when none is described.</summary>
    /// <param name="status">The status.</param>
    /// <param name="described">Whether the status is described at all.</param>
    public JsonObject? Response(int status, out bool described)
    {
        var text = status.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var key in new[] { text, text[0] + "XX", "default" })
        {
            if (Responses.TryGetValue(key, out var schema))
            {
                described = true;
                return schema;
            }
        }

        described = false;
        return null;
    }
}

/// <summary>
/// A description of an API: its operations with the shape of what each sends and answers. It is read from an OpenAPI 3
/// document in JSON (someone's promise of the API), or merged from the exchanges Bruinet saw
/// (<see cref="ObservedDescription"/>: what was observed, never what is promised), and written as OpenAPI 3 JSON.
/// </summary>
public sealed class ApiDescription
{
    private const int Depth = 24;

    private static readonly JsonSerializerOptions Written = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly string[] Methods = ["get", "put", "post", "delete", "patch", "head", "options"];

    /// <summary>The API's name.</summary>
    public string Title { get; init; } = "API";

    /// <summary>The description's version; <c>observed</c> for a merged one.</summary>
    public string Version { get; init; } = "observed";

    /// <summary>Whether it was merged from exchanges.</summary>
    public bool IsObserved { get; init; }

    /// <summary>The addresses the API is served at.</summary>
    public IReadOnlyList<string> Servers { get; init; } = [];

    /// <summary>The operations, by path and method.</summary>
    public IReadOnlyList<ApiOperation> Operations { get; init; } = [];

    /// <summary>The operation a call is of; null when the description has none for it. A path without values wins over one with.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The call's path, without host and query.</param>
    public ApiOperation? Find(string method, string path)
        => Operations
            .Where(operation => string.Equals(operation.Method, method, StringComparison.OrdinalIgnoreCase) && operation.Matches(path))
            .OrderBy(operation => operation.Path.Count(character => character == '{'))
            .FirstOrDefault();

    /// <summary>Reads an OpenAPI 3 document in JSON. References to its own schemas are followed; YAML and OpenAPI 2 are not read.</summary>
    /// <param name="json">The document.</param>
    /// <exception cref="FormatException">The text is not an OpenAPI 3 document in JSON.</exception>
    public static ApiDescription Read(string json)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject ?? throw new FormatException("An API description is a JSON object.");
        }
        catch (JsonException e)
        {
            throw new FormatException($"The description is not JSON ({e.Message}). A YAML description is converted to JSON first.", e);
        }

        if ((string?)root["openapi"] is not { } version || !version.StartsWith('3'))
        {
            throw new FormatException("The description is not OpenAPI 3 (it has no \"openapi\": \"3...\"). An OpenAPI 2 (Swagger) description is converted first.");
        }

        var operations = new List<ApiOperation>();
        foreach (var (path, item) in root["paths"] as JsonObject ?? [])
        {
            if (Resolve(item, root, 0) is not JsonObject pathItem)
            {
                continue;
            }

            foreach (var method in Methods)
            {
                if (pathItem[method] is not JsonObject operation)
                {
                    continue;
                }

                operations.Add(Operation(method, path, pathItem, operation, root));
            }
        }

        var info = root["info"] as JsonObject;
        return new ApiDescription
        {
            Title = (string?)info?["title"] ?? "API",
            Version = (string?)info?["version"] ?? string.Empty,
            IsObserved = info?["x-observed"] is not null,
            Servers = [.. (root["servers"] as JsonArray ?? []).Select(server => (string?)server?["url"]).OfType<string>()],
            Operations = operations,
        };
    }

    private static ApiOperation Operation(string method, string path, JsonObject pathItem, JsonObject operation, JsonObject root)
    {
        var parameters = (pathItem["parameters"] as JsonArray ?? []).Concat(operation["parameters"] as JsonArray ?? [])
            .Select(each => Resolve(each, root, 0)).OfType<JsonObject>()
            .Select(each => new ApiParameter((string?)each["name"] ?? string.Empty, (string?)each["in"] ?? "query", (bool?)each["required"] ?? false, (string?)(Resolve(each["schema"], root, 0) as JsonObject)?["type"] ?? "string"))
            .ToList();
        var responses = new Dictionary<string, JsonObject?>(StringComparer.Ordinal);
        foreach (var (status, response) in operation["responses"] as JsonObject ?? [])
        {
            responses[status] = JsonSchema((Resolve(response, root, 0) as JsonObject)?["content"], root);
        }

        return new ApiOperation(method, path)
        {
            Observed = (int?)operation["x-observed-count"] ?? 0,
            Parameters = parameters,
            Request = JsonSchema((Resolve(operation["requestBody"], root, 0) as JsonObject)?["content"], root),
            Responses = responses,
        };
    }

    /// <summary>The description as an OpenAPI 3 document in JSON, its operations in the order of their paths.</summary>
    public string ToJson()
    {
        var paths = new JsonObject();
        foreach (var group in Operations.GroupBy(operation => operation.Path, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var item = new JsonObject();
            foreach (var operation in group.OrderBy(operation => Array.IndexOf(Methods, operation.Method.ToLowerInvariant())))
            {
                var written = new JsonObject();
                if (operation.Observed > 0)
                {
                    written["x-observed-count"] = operation.Observed;
                }

                if (operation.Parameters.Count > 0)
                {
                    written["parameters"] = new JsonArray([.. operation.Parameters.Select(parameter => (JsonNode)new JsonObject
                    {
                        ["name"] = parameter.Name,
                        ["in"] = parameter.In,
                        ["required"] = parameter.Required,
                        ["schema"] = new JsonObject { ["type"] = parameter.Type },
                    })]);
                }

                if (operation.Request is not null)
                {
                    written["requestBody"] = new JsonObject { ["content"] = Content(operation.Request) };
                }

                var responses = new JsonObject();
                foreach (var (status, schema) in operation.Responses.OrderBy(each => each.Key, StringComparer.Ordinal))
                {
                    var response = new JsonObject { ["description"] = IsObserved ? "Observed." : string.Empty };
                    if (schema is not null)
                    {
                        response["content"] = Content(schema);
                    }

                    responses[status] = response;
                }

                written["responses"] = responses;
                item[operation.Method.ToLowerInvariant()] = written;
            }

            paths[group.Key] = item;
        }

        var info = new JsonObject { ["title"] = Title, ["version"] = Version };
        if (IsObserved)
        {
            info["description"] = "Merged from the calls Bruinet saw: what was observed, never what is promised. Shapes only, no values.";
            info["x-observed"] = new JsonObject { ["exchanges"] = Operations.Sum(operation => operation.Observed) };
        }

        var root = new JsonObject { ["openapi"] = "3.0.3", ["info"] = info };
        if (Servers.Count > 0)
        {
            root["servers"] = new JsonArray([.. Servers.Select(server => (JsonNode)new JsonObject { ["url"] = server })]);
        }

        root["paths"] = paths;
        return root.ToJsonString(Written) + "\n";
    }

    private static JsonObject Content(JsonObject schema) => new() { ["application/json"] = new JsonObject { ["schema"] = schema.DeepClone() } };

    // The schema of a content's JSON media type, with its references followed.
    private static JsonObject? JsonSchema(JsonNode? content, JsonObject root)
    {
        var media = (content as JsonObject)?.FirstOrDefault(each => each.Key.Contains("json", StringComparison.OrdinalIgnoreCase)).Value as JsonObject;
        return media?["schema"] is { } schema ? Inline(schema, root, []) as JsonObject : null;
    }

    private static JsonNode? Resolve(JsonNode? node, JsonObject root, int depth)
    {
        while (node is JsonObject reference && (string?)reference["$ref"] is { } pointer && depth++ < Depth)
        {
            node = pointer.StartsWith("#/", StringComparison.Ordinal)
                ? pointer[2..].Split('/').Aggregate((JsonNode?)root, (at, part) => (at as JsonObject)?[part.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)])
                : null;
        }

        return node;
    }

    // A schema with every reference replaced by what it points at; a schema that holds itself is followed once more,
    // then left as any value.
    private static JsonNode? Inline(JsonNode? node, JsonObject root, IReadOnlyList<string> followed)
    {
        if (node is JsonObject reference && (string?)reference["$ref"] is { } pointer)
        {
            return followed.Count(each => each == pointer) >= 2 || followed.Count > Depth
                ? new JsonObject()
                : Inline(Resolve(node, root, 0), root, [.. followed, pointer]);
        }

        switch (node)
        {
            case JsonObject schema:
                var copy = new JsonObject();
                foreach (var (key, value) in schema)
                {
                    copy[key] = Inline(value, root, followed);
                }

                return copy;
            case JsonArray list:
                return new JsonArray([.. list.Select(item => Inline(item, root, followed))]);
            case { } value:
                return value.DeepClone();
            default:
                return null;
        }
    }
}
