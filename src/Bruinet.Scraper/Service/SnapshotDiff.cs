using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Service;

/// <summary>
/// What changed between two captures of a page: the nodes added, removed and changed. A node is the
/// same node in both when its path is: each step its type, its test id or id, and which of its
/// like siblings it is. Boxes are left out; a moved node is removed and added.
/// </summary>
public static class SnapshotDiff
{
    /// <summary>The differences, at most <paramref name="max"/> of each kind listed; the counts are all of them.</summary>
    public static PageDiff Compare(ScrapeSnapshot before, ScrapeSnapshot after, int max = 100)
    {
        var old = Keys(before);
        var now = Keys(after);
        var removed = old.Keys.Where(k => !now.ContainsKey(k)).Select(k => old[k]).OrderBy(n => n.Number).ToList();
        var added = now.Keys.Where(k => !old.ContainsKey(k)).Select(k => now[k]).OrderBy(n => n.Number).ToList();
        var changed = now.Keys.Where(old.ContainsKey)
            .Select(k => (Before: old[k], After: now[k]))
            .Where(p => !Same(p.Before, p.After))
            .OrderBy(p => p.After.Number)
            .ToList();
        return new PageDiff(
            added.Count,
            removed.Count,
            changed.Count,
            added.Take(max).Select(n => SnapshotOutline.Line(n)).ToArray(),
            removed.Take(max).Select(n => SnapshotOutline.Line(n)).ToArray(),
            changed.Take(max).Select(p => new NodeChange(SnapshotOutline.Line(p.Before), SnapshotOutline.Line(p.After))).ToArray());
    }

    private static Dictionary<string, ScrapeNode> Keys(ScrapeSnapshot snapshot)
    {
        var keys = new Dictionary<string, ScrapeNode>(StringComparer.Ordinal);
        var paths = new string[snapshot.Nodes.Count + 1];
        var seen = new Dictionary<(int Parent, string Step), int>();
        foreach (var node in snapshot.Nodes)
        {
            var id = node.Attribute("data-testid") ?? node.Attribute("id");
            var step = node.Kind == ScrapeNodeKind.ShadowRoot ? "#shadow" : node.Type + (id is { Length: > 0 } ? "#" + id : "");
            var index = seen.TryGetValue((node.Parent, step), out var count) ? count : 0;
            seen[(node.Parent, step)] = index + 1;
            paths[node.Number] = (node.Parent == 0 ? "" : paths[node.Parent] + "/") + step + (index == 0 ? "" : $"[{index}]");
            keys[paths[node.Number]] = node;
        }
        return keys;
    }

    private static bool Same(ScrapeNode a, ScrapeNode b) =>
        a.Text == b.Text && a.Name == b.Name && a.Role == b.Role && a.Frame == b.Frame
        && a.Attributes.Count == b.Attributes.Count
        && a.Attributes.All(x => b.Attributes.TryGetValue(x.Key, out var v) && v == x.Value);
}

/// <summary>What changed: counts, and the nodes as outline lines (a change as its line before and after).</summary>
public sealed record PageDiff(int AddedCount, int RemovedCount, int ChangedCount, IReadOnlyList<string> Added, IReadOnlyList<string> Removed, IReadOnlyList<NodeChange> Changed);

/// <summary>A node as it was and as it is.</summary>
public sealed record NodeChange(string Before, string After);
