using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bruinet.Mocking.Data.Replay;

/// <summary>
/// The gate on re-records: <c>&lt;folder&gt;/reviewed.json</c> holds each recording's content hash as a person accepted it
/// (<c>bruinet sql diff --accept</c>, after reading the summary). A recording whose hash differs went in unread. A
/// re-record that comes out the same keeps its hash, so only real changes ask for a reading.
/// </summary>
public static class RecordingReview
{
    public const string FileName = "reviewed.json";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>The recordings under <paramref name="folder"/>, by path relative to it with <c>/</c>: files with <c>Exchanges</c>.</summary>
    public static IReadOnlyList<string> Recordings(string folder)
        => [.. Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(path), FileName, StringComparison.OrdinalIgnoreCase) && IsRecording(File.ReadAllText(path)))
            .Select(path => Relative(folder, path))
            .OrderBy(path => path, StringComparer.Ordinal)];

    /// <summary>Whether <paramref name="json"/> is a recording rather than a snapshot or another file.</summary>
    public static bool IsRecording(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            // An HTTP recording (Bruinet.Mocking.Http) has Exchanges too, beside it in Recordings/http: its exchanges have no Text.
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("Exchanges", out var exchanges)
                   && (exchanges.ValueKind != JsonValueKind.Array || exchanges.GetArrayLength() == 0 || exchanges[0].TryGetProperty("Text", out _));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// A recording's hash by what replay answers, not its bytes: the stamp, each write in its order and the reads between
    /// two writes as a set (a browser repeats reads a varying number of times), volatile values blanked (each recording
    /// makes its own ids). Two recordings of an unchanged test hash the same.
    /// </summary>
    /// <param name="json">The recording.</param>
    /// <param name="shared">Values volatile in the suite's other recordings (<see cref="VolatileValues(string)"/>): tests that share an
    /// app read rows earlier tests made, with those tests' new ids and titles.</param>
    public static string Hash(string json, IReadOnlyCollection<string>? shared = null)
    {
        var recording = Recording.Parse(json) ?? new Recording();
        var volatileValues = new HashSet<string>(VolatileValues([recording]), StringComparer.Ordinal);
        volatileValues.UnionWith(shared ?? []);

        string Show(System.Text.Json.Nodes.JsonNode? value) => Values.Canonical(value) is { } canonical ? (volatileValues.Contains(canonical) ? "~" : canonical) : "null";

        var lines = new List<string>();
        if (recording.Stamp is { } stamp)
        {
            lines.AddRange(stamp.Versions.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"version {pair.Key} {pair.Value}"));
            lines.AddRange(stamp.Objects.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"object {pair.Key} {pair.Value}"));
        }

        var reads = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var exchange in recording.Exchanges)
        {
            var line = new StringBuilder(exchange.Kind).Append('|').Append(Statements.Key(exchange.Text));
            foreach (var parameter in exchange.Parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                line.Append('|').Append(parameter.Key).Append('=').Append(Show(parameter.Value));
            }

            foreach (var result in exchange.Results ?? [])
            {
                line.Append("|columns ").Append(string.Join(",", result.Columns.Select(column => column.Name)));
                foreach (var row in result.Rows)
                {
                    line.Append("|row ").Append(string.Join(",", row.Select(Show)));
                }
            }

            line.Append("|affected ").Append(exchange.RecordsAffected).Append("|scalar ").Append(Show(exchange.Scalar));
            foreach (var output in (exchange.Outputs ?? []).OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                line.Append('|').Append(output.Key).Append("=>").Append(Show(output.Value));
            }

            line.Append("|error ").Append(exchange.Error?.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none");
            if (exchange.IsWrite)
            {
                lines.AddRange(reads);
                reads.Clear();
                lines.Add("write " + line);
            }
            else
            {
                reads.Add("read " + line);
            }
        }

        lines.AddRange(reads);
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
        return BitConverter.ToString(bytes, 0, 8).Replace("-", string.Empty);
    }

    /// <summary>The canonical values of every volatile parameter in <paramref name="recordings"/>.</summary>
    public static IReadOnlyCollection<string> VolatileValues(IEnumerable<Recording> recordings)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var exchange in recordings.SelectMany(recording => recording.Exchanges))
        {
            foreach (var name in exchange.Volatile)
            {
                if (exchange.Parameters.TryGetValue(name, out var value) && Values.Canonical(value) is { } canonical)
                {
                    values.Add(canonical);
                }
            }
        }

        return values;
    }

    /// <summary>The volatile values of every recording under <paramref name="folder"/>.</summary>
    public static IReadOnlyCollection<string> VolatileValues(string folder)
        => VolatileValues(Recordings(folder).Select(path => Recording.Load(Path.Combine(folder, path))!));

    /// <summary>What went in unread: <c>new</c>, <c>changed</c> or <c>gone</c>, and the recording's path.</summary>
    public static IReadOnlyList<string> Unreviewed(string folder)
    {
        var reviewed = Load(folder);
        var shared = VolatileValues(folder);
        var lines = new List<string>();
        foreach (var path in Recordings(folder))
        {
            var hash = Hash(File.ReadAllText(Path.Combine(folder, path)), shared);
            if (!reviewed.TryGetValue(path, out var accepted))
            {
                lines.Add("new " + path);
            }
            else if (accepted != hash)
            {
                lines.Add("changed " + path);
            }
        }

        lines.AddRange(reviewed.Keys.Except(Recordings(folder), StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).Select(path => "gone " + path));
        return lines;
    }

    /// <summary>Accepts every recording under <paramref name="folder"/> as it is now.</summary>
    public static void Accept(string folder)
    {
        var reviewed = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var shared = VolatileValues(folder);
        foreach (var path in Recordings(folder))
        {
            reviewed[path] = Hash(File.ReadAllText(Path.Combine(folder, path)), shared);
        }

        File.WriteAllText(Path.Combine(folder, FileName), JsonSerializer.Serialize(reviewed, Json));
    }

    private static Dictionary<string, string> Load(string folder)
    {
        var path = Path.Combine(folder, FileName);
        return File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [] : [];
    }

    private static string Relative(string folder, string path)
    {
        var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).Substring(root.Length).Replace('\\', '/');
    }
}
