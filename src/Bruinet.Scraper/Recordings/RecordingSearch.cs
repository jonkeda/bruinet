using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Recordings;

/// <summary>A node of a recorded page whose text, name or an attribute holds what was looked for.</summary>
/// <param name="File">The snapshot file, relative to the recordings folder (<c>2026-11-14-explore-4/002-hours.nodes.jsonl</c>).</param>
/// <param name="Location">The page's address when it was captured.</param>
/// <param name="Node">The node's number in the file.</param>
/// <param name="Field"><c>text</c>, <c>name</c>, or the attribute's name (<c>placeholder</c>).</param>
/// <param name="Value">The field's whole value.</param>
/// <param name="Path">The node and its ancestors, root first, a shadow root as <c>#shadow</c>: where it is, for a locator.</param>
public sealed record RecordingHit(string File, string Location, int Node, string Field, string Value, string Path);

/// <summary>
/// Finds text in a project's recorded pages (<c>Recordings/**/*.nodes.jsonl</c>), shadow roots included: the facts a probe
/// would otherwise dig out of the running app (a placeholder, a filter's options, a card's count). Reads files only.
/// </summary>
public static class RecordingSearch
{
    private const int MaxPath = 8;

    /// <summary>Every node whose text, name or attribute value holds <paramref name="text"/> (any case), file by file.</summary>
    /// <param name="recordingsFolder">The project's <c>Recordings</c> folder.</param>
    /// <param name="text">What to look for.</param>
    /// <param name="run">Only this run's folder (<c>2026-11-14-explore-4</c>); every run when null.</param>
    public static IReadOnlyList<RecordingHit> Find(string recordingsFolder, string text, string? run = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var folder = run is null ? recordingsFolder : Path.Combine(recordingsFolder, run);
        if (!Directory.Exists(folder))
            return [];

        return Directory.EnumerateFiles(folder, "*" + SnapshotFile.Extension, SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .SelectMany(path => Find(SnapshotFile.Read(path), text, Path.GetRelativePath(recordingsFolder, path).Replace('\\', '/')))
            .ToList();
    }

    /// <summary>The nodes of one snapshot that hold <paramref name="text"/>.</summary>
    public static IEnumerable<RecordingHit> Find(ScrapeSnapshot snapshot, string text, string file)
    {
        foreach (var node in snapshot.Nodes)
        {
            foreach (var (field, value) in Fields(node))
            {
                if (value.Contains(text, StringComparison.OrdinalIgnoreCase))
                    yield return new(file, snapshot.Location, node.Number, field, value, PathOf(snapshot, node));
            }
        }
    }

    private static IEnumerable<(string Field, string Value)> Fields(ScrapeNode node)
    {
        if (node.Text is { Length: > 0 } text)
            yield return ("text", text);
        if (node.Name is { Length: > 0 } name && name != node.Text)
            yield return ("name", name);
        foreach (var (attribute, value) in node.Attributes)
        {
            if (value.Length > 0)
                yield return (attribute, value);
        }
    }

    // The node and its ancestors up to MaxPath of them, as "tag.class#id", a shadow root as "#shadow".
    private static string PathOf(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        var parts = new List<string>();
        for (var current = node; parts.Count < MaxPath; current = snapshot[current.Parent])
        {
            parts.Add(current.Kind == ScrapeNodeKind.ShadowRoot ? "#shadow" : Step(current));
            if (current.Parent <= 0)
                break;
        }

        parts.Reverse();
        return string.Join(" > ", parts);
    }

    private static string Step(ScrapeNode node)
    {
        var classes = node.Attribute("class") is { Length: > 0 } c
            ? "." + string.Join('.', c.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(3))
            : "";
        var id = node.Attribute("data-cy") is { } cy ? $"[data-cy={cy}]" : "";
        return node.Type + classes + id;
    }
}
