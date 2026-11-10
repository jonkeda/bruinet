using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Recordings;

/// <summary>
/// A snapshot as a file (<c>*.nodes.jsonl</c>): a header line, then one line per node in document order,
/// fields in a fixed order and no empty ones, so a page recorded again diffs line by line.
/// </summary>
public static class SnapshotFile
{
    /// <summary>The file name's ending.</summary>
    public const string Extension = ".nodes.jsonl";

    private static readonly JsonWriterOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Writes a snapshot.</summary>
    public static void Write(ScrapeSnapshot snapshot, TextWriter writer)
    {
        var header = new JsonObject
        {
            ["platform"] = snapshot.Platform,
            ["location"] = snapshot.Location,
            ["title"] = snapshot.Title,
            ["capturedAt"] = snapshot.CapturedAt.ToString("O", CultureInfo.InvariantCulture),
            ["nodes"] = snapshot.Nodes.Count,
        };
        writer.Write(ToLine(header));
        foreach (var node in snapshot.Nodes)
        {
            writer.Write('\n');
            writer.Write(ToLine(ToJson(node)));
        }
        writer.Write('\n');
    }

    /// <summary>Writes a snapshot to a file.</summary>
    public static void Write(ScrapeSnapshot snapshot, string path)
    {
        using var writer = new StreamWriter(path, append: false, new System.Text.UTF8Encoding(false));
        Write(snapshot, writer);
    }

    /// <summary>Reads a snapshot.</summary>
    /// <exception cref="FormatException">The text is not a snapshot file.</exception>
    public static ScrapeSnapshot Read(TextReader reader)
    {
        var first = reader.ReadLine() ?? throw new FormatException("A snapshot file starts with a header line; this one is empty.");
        var header = Parse(first, 1);
        var nodes = new List<ScrapeNode>();
        var lineNumber = 1;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Length == 0)
                continue;
            var node = ToNode(Parse(line, lineNumber));
            if (node.Number != nodes.Count + 1)
                throw new FormatException($"Line {lineNumber}: node {node.Number} where node {nodes.Count + 1} was expected.");
            nodes.Add(node);
        }
        return new ScrapeSnapshot
        {
            Platform = (string?)header["platform"] ?? "",
            Location = (string?)header["location"] ?? "",
            Title = (string?)header["title"] ?? "",
            CapturedAt = header["capturedAt"] is { } at ? DateTimeOffset.Parse((string)at!, CultureInfo.InvariantCulture) : default,
            Nodes = nodes,
        };
    }

    /// <summary>Reads a snapshot from a file.</summary>
    public static ScrapeSnapshot Read(string path)
    {
        using var reader = new StreamReader(path);
        return Read(reader);
    }

    /// <summary>
    /// A short hash of what a snapshot shows: its nodes without their boxes, so a page seen in another
    /// window size, or captured again unchanged, has the same hash.
    /// </summary>
    public static string ContentHash(ScrapeSnapshot snapshot)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (var node in snapshot.Nodes)
        {
            var json = ToJson(node);
            json.Remove("box");
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(ToLine(json) + '\n'));
        }
        return Convert.ToHexString(hash.GetHashAndReset())[..8].ToLowerInvariant();
    }

    private static JsonObject ToJson(ScrapeNode node)
    {
        var o = new JsonObject { ["n"] = node.Number, ["p"] = node.Parent };
        if (node.Kind != ScrapeNodeKind.Element)
            o["kind"] = node.Kind == ScrapeNodeKind.ShadowRoot ? "shadow-root" : node.Kind.ToString();
        Add(o, "type", node.Type);
        Add(o, "role", node.Role);
        Add(o, "name", node.Name);
        Add(o, "text", node.Text);
        Add(o, "frame", node.Frame);
        if (node.Bounds is { } b)
            o["box"] = new JsonArray(b.X, b.Y, b.Width, b.Height);
        if (node.Attributes.Count > 0)
        {
            var attributes = new JsonObject();
            foreach (var (name, value) in node.Attributes.OrderBy(a => a.Key, StringComparer.Ordinal))
                attributes[name] = value;
            o["attr"] = attributes;
        }
        return o;
    }

    private static ScrapeNode ToNode(JsonObject o)
    {
        var box = o["box"]?.AsArray();
        return new ScrapeNode
        {
            Number = (int)o["n"]!,
            Parent = (int)o["p"]!,
            Kind = (string?)o["kind"] switch
            {
                null => ScrapeNodeKind.Element,
                "shadow-root" => ScrapeNodeKind.ShadowRoot,
                var other => throw new FormatException($"Node {(int)o["n"]!}: unknown kind '{other}'."),
            },
            Type = (string?)o["type"] ?? "",
            Role = (string?)o["role"],
            Name = (string?)o["name"],
            Text = (string?)o["text"],
            Frame = (string?)o["frame"],
            Bounds = box is { Count: 4 } ? new ScrapeBounds((int)box[0]!, (int)box[1]!, (int)box[2]!, (int)box[3]!) : null,
            Attributes = o["attr"]?.AsObject().ToDictionary(a => a.Key, a => (string?)a.Value ?? "", StringComparer.Ordinal)
                ?? new Dictionary<string, string>(),
        };
    }

    private static void Add(JsonObject o, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            o[name] = value;
    }

    private static string ToLine(JsonObject o)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Compact))
            o.WriteTo(writer);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static JsonObject Parse(string line, int lineNumber)
    {
        try
        {
            return JsonNode.Parse(line)?.AsObject() ?? throw new FormatException($"Line {lineNumber} is not a JSON object.");
        }
        catch (JsonException e)
        {
            throw new FormatException($"Line {lineNumber} is not JSON: {e.Message}", e);
        }
    }
}
