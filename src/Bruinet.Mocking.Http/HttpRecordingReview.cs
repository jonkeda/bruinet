using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bruinet.Mocking.Http;

/// <summary>What changed between two recordings of a test: calls added and gone, and each answer that changed.</summary>
public static class HttpRecordingDiff
{
    /// <summary>One line per change, <c>+</c>, <c>-</c> or <c>~</c> and the call; empty when the answers are the same.</summary>
    public static IReadOnlyList<string> Compare(HttpRecording? before, HttpRecording? after)
        => Compare(before, after, []);

    /// <summary>
    /// The changes, a changed request among them (<c>~ call: request ...</c>), its volatile values and
    /// <paramref name="ignoredFields"/> left out.
    /// </summary>
    public static IReadOnlyList<string> Compare(HttpRecording? before, HttpRecording? after, IReadOnlyCollection<string> ignoredFields)
    {
        var old = HttpRecording.Occurrences(before?.Exchanges ?? []);
        var now = HttpRecording.Occurrences(after?.Exchanges ?? []);
        var lines = new List<string>();
        foreach (var key in old.Keys.Union(now.Keys, StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal))
        {
            var call = key[..key.LastIndexOf('#')];
            if (!old.TryGetValue(key, out var then))
            {
                lines.Add($"+ {call}: a new call, answered {now[key].Status.ToString(CultureInfo.InvariantCulture)}");
            }
            else if (!now.TryGetValue(key, out var answer))
            {
                lines.Add($"- {call}: no longer made");
            }
            else if (HttpMatching.Difference(answer, then, ignoredFields) is { } request)
            {
                lines.Add($"~ {call}: request {request}");
            }
            else if (then.Status != answer.Status)
            {
                lines.Add($"~ {call}: status {then.Status.ToString(CultureInfo.InvariantCulture)} is now {answer.Status.ToString(CultureInfo.InvariantCulture)}");
            }
            else if (Answer(then) != Answer(answer))
            {
                lines.Add($"~ {call}: {Changed(Answer(then), Answer(answer))}");
            }
        }

        return lines;
    }

    internal static string Answer(HttpExchange exchange)
        => exchange.Chunks is { } chunks ? string.Concat(chunks.Select(chunk => chunk.Data)) : exchange.ResponseBody ?? string.Empty;

    // The first JSON field that changed, or where the text first differs.
    private static string Changed(string before, string after)
    {
        var left = new HttpExchange { RequestBody = before };
        var right = new HttpExchange { RequestBody = after };
        var difference = HttpMatching.Difference(left, right, []);
        if (difference is not null && difference.StartsWith("$.", StringComparison.Ordinal))
        {
            return "the answer's " + difference.Replace("where", "where before", StringComparison.Ordinal);
        }

        var at = 0;
        while (at < before.Length && at < after.Length && before[at] == after[at])
        {
            at++;
        }

        return $"the answer differs from character {at.ToString(CultureInfo.InvariantCulture)}: '{Excerpt(after, at)}' where before '{Excerpt(before, at)}'";
    }

    private static string Excerpt(string text, int at) => at >= text.Length ? string.Empty : text.Substring(at, Math.Min(60, text.Length - at));
}

/// <summary>
/// The review of a suite's HTTP recordings, as SQL replay's: <c>reviewed.json</c> holds each recording's hash when it was
/// last read, so one that changed and went in unread fails the gate.
/// </summary>
public static class HttpRecordingReview
{
    /// <summary>Its own name, so SQL replay's <c>reviewed.json</c> in the same folder is not overwritten.</summary>
    public const string FileName = "reviewed.http.json";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>The recordings under <paramref name="folder"/>, by path relative to it with <c>/</c>.</summary>
    public static IReadOnlyList<string> Recordings(string folder)
        => Directory.Exists(folder)
            ? [.. Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
                .Where(path => !string.Equals(Path.GetFileName(path), FileName, StringComparison.OrdinalIgnoreCase)
                    && !path.EndsWith(".calls.json", StringComparison.OrdinalIgnoreCase)
                    && IsRecording(File.ReadAllText(path)))
                .Select(path => Path.GetRelativePath(folder, path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)]
            : [];

    /// <summary>Whether <paramref name="json"/> is an HTTP recording.</summary>
    public static bool IsRecording(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("Exchanges", out var exchanges)
                   && exchanges.ValueKind == JsonValueKind.Array
                   && (exchanges.GetArrayLength() == 0 || exchanges[0].TryGetProperty("Url", out _));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>A recording's hash by what replay answers: each call and its answer, volatile request values blanked.</summary>
    public static string Hash(string json)
    {
        var recording = HttpRecording.Parse(json) ?? new HttpRecording();
        var lines = recording.Exchanges.Select(exchange =>
        {
            var values = HttpMatching.RequestValues(exchange)
                .Select(pair => pair.Key + "=" + (exchange.Volatile.Contains(pair.Key) ? "~" : pair.Value))
                .OrderBy(value => value, StringComparer.Ordinal);
            return $"{exchange.Method.ToUpperInvariant()} {HttpMatching.Path(exchange.Url)} {string.Join("&", values)} -> {exchange.Status.ToString(CultureInfo.InvariantCulture)} {HttpRecordingDiff.Answer(exchange)}";
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines))));
    }

    /// <summary>Each recording new, changed since it was accepted, or gone: <c>new path</c>, <c>changed path</c>, <c>gone path</c>.</summary>
    public static IReadOnlyList<string> Unreviewed(string folder)
    {
        var reviewed = Load(folder);
        var lines = new List<string>();
        var recordings = Recordings(folder);
        foreach (var path in recordings)
        {
            var hash = Hash(File.ReadAllText(Path.Combine(folder, path)));
            if (!reviewed.TryGetValue(path, out var accepted))
            {
                lines.Add("new " + path);
            }
            else if (accepted != hash)
            {
                lines.Add("changed " + path);
            }
        }

        lines.AddRange(reviewed.Keys.Except(recordings, StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).Select(path => "gone " + path));
        return lines;
    }

    /// <summary>Accepts every recording under <paramref name="folder"/> as it is now.</summary>
    public static void Accept(string folder)
    {
        var reviewed = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Recordings(folder))
        {
            reviewed[path] = Hash(File.ReadAllText(Path.Combine(folder, path)));
        }

        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, FileName), JsonSerializer.Serialize(reviewed, Json));
    }

    /// <summary>
    /// Each recording its newest replay left behind: one with misses (the app calls what was not recorded) or unused
    /// exchanges (the app no longer calls what was), from the call logs beside them. Record them again.
    /// </summary>
    public static IReadOnlyList<string> Stale(string folder)
    {
        var lines = new List<string>();
        foreach (var path in Recordings(folder))
        {
            var log = HttpCallLog.Load(HttpCallLog.PathFor(Path.Combine(folder, path)));
            if (log is null || !string.Equals(log.Mode, nameof(HttpReplayMode.Replay), StringComparison.Ordinal))
            {
                continue;
            }

            if (log.Misses.Count > 0)
            {
                lines.Add($"{path}: {log.Misses.Count.ToString(CultureInfo.InvariantCulture)} calls missed, first: {log.Misses[0]}");
            }
            else if (log.Unused.Count > 0)
            {
                lines.Add($"{path}: {log.Unused.Count.ToString(CultureInfo.InvariantCulture)} recorded calls no longer made (exchanges {string.Join(", ", log.Unused.Take(10))})");
            }
        }

        return lines;
    }

    private static Dictionary<string, string> Load(string folder)
    {
        var path = Path.Combine(folder, FileName);
        return File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [] : [];
    }
}
