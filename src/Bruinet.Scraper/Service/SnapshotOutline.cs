using System.Text;
using Bruinet.Core.Scraping;

namespace Bruinet.Scraper.Service;

/// <summary>
/// A snapshot as short text for an agent or a tree view: one line per node that matters, indented by
/// depth, with its number. A wrapper with nothing of its own (a plain <c>div</c> around one child) is
/// left out and its child takes its place, so a framework's nesting does not fill the budget.
/// </summary>
public static class SnapshotOutline
{
    private static readonly HashSet<string> Interactive = new(StringComparer.Ordinal)
    {
        "a", "button", "input", "select", "textarea", "option", "label", "form", "table", "thead", "tbody", "tr", "th", "td",
        "ul", "ol", "li", "nav", "main", "header", "footer", "aside", "section", "dialog", "details", "summary",
        "h1", "h2", "h3", "h4", "h5", "h6", "iframe", "img", "svg",
    };

    /// <summary>The outline of the subtree at <paramref name="root"/> (the whole page when 0), at most <paramref name="maxLines"/> lines.</summary>
    /// <param name="snapshot">The capture.</param>
    /// <param name="root">The subtree's node, or 0.</param>
    /// <param name="maxLines">The most lines.</param>
    /// <param name="focus">A node shown even when it is a wrapper: the one the reader asked for.</param>
    public static string Render(ScrapeSnapshot snapshot, int root = 0, int maxLines = 400, int focus = 0)
    {
        var children = snapshot.Nodes.ToLookup(n => n.Parent);
        var builder = new StringBuilder();
        var lines = 0;
        var cut = false;

        void Visit(ScrapeNode node, int depth)
        {
            if (cut)
                return;
            var shown = node.Number == focus || Matters(node, children[node.Number].Count());
            if (shown)
            {
                if (lines == maxLines)
                {
                    cut = true;
                    return;
                }
                builder.Append(' ', depth * 2).Append(Line(node)).Append('\n');
                lines++;
            }
            foreach (var child in children[node.Number])
                Visit(child, shown ? depth + 1 : depth);
        }

        var start = root == 0 ? children[0] : [snapshot[root]];
        foreach (var node in start)
            Visit(node, 0);
        if (cut)
            builder.Append($"… more nodes: ask for a node's subtree (node = its number)\n");
        return builder.ToString();
    }

    /// <summary>One node as a line: <c>#12 button "Pay now" name="Pay" role=… [data-testid=pay] #id .class</c>.</summary>
    public static string Line(ScrapeNode node)
    {
        var line = new StringBuilder().Append('#').Append(node.Number).Append(' ');
        line.Append(node.Kind == ScrapeNodeKind.ShadowRoot ? "(shadow root)" : node.Type);
        if (node.Text is { Length: > 0 } text)
            line.Append(" \"").Append(text).Append('"');
        if (node.Name is { Length: > 0 } name)
            line.Append(" name=\"").Append(name).Append('"');
        if (node.Role is { Length: > 0 } role)
            line.Append(" role=").Append(role);
        if (node.Frame is { Length: > 0 } frame)
            line.Append(" frame=").Append(frame);
        foreach (var (key, value) in node.Attributes.OrderBy(a => a.Key, StringComparer.Ordinal))
        {
            switch (key)
            {
                case "id":
                    line.Append(" #").Append(value);
                    break;
                case "class":
                    line.Append(' ').Append(string.Concat(value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(4).Select(c => "." + c)));
                    break;
                default:
                    line.Append(" [").Append(key).Append(value.Length > 0 ? "=" + value : "").Append(']');
                    break;
            }
        }
        return line.ToString();
    }

    private static bool Matters(ScrapeNode node, int childCount) =>
        node.Kind == ScrapeNodeKind.ShadowRoot
        || Interactive.Contains(node.Type)
        || node.Text is { Length: > 0 }
        || node.Name is { Length: > 0 }
        || node.Role is { Length: > 0 }
        || node.Attributes.Keys.Any(k => k is "id" || k.StartsWith("data-", StringComparison.Ordinal))
        || node.Type.Contains('-', StringComparison.Ordinal) // a custom element is a component
        || childCount > 1 && node.Attributes.ContainsKey("class");
}
