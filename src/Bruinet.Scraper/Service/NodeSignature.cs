using Bruinet.Core.Scraping;

namespace Bruinet.Scraper.Service;

/// <summary>
/// What makes two nodes "the same kind of thing" across pages: the type, the role, the class names that
/// are not generated ones, and the types of the direct children, a run of one type counted once. Text
/// and ids are left out.
/// </summary>
public static class NodeSignature
{
    /// <summary>The signature of a node in its snapshot.</summary>
    public static string Of(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        var classes = node.Attribute("class") is { } c
            // A modifier (v-input--dense, card--active) is how one occurrence looks, not what it is.
            ? string.Join('.', c.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(n => IsStable(n) && !n.Contains("--", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
            : "";
        var children = string.Join(',', Runs(snapshot.ChildrenOf(node.Number).Select(n => n.Kind == ScrapeNodeKind.ShadowRoot ? "#shadow" : n.Type)));
        return $"{node.Type}|{node.Role}|{classes}|{children}";
    }

    // A run of the same type is one entry (li+): a list of three and a list of five are alike.
    private static IEnumerable<string> Runs(IEnumerable<string> types)
    {
        string? last = null;
        var repeated = false;
        foreach (var type in types)
        {
            if (type == last)
            {
                repeated = true;
                continue;
            }
            if (last is not null)
                yield return repeated ? last + "+" : last;
            last = type;
            repeated = false;
        }
        if (last is not null)
            yield return repeated ? last + "+" : last;
    }

    // Generated class names (Emotion's css-1x2y3z, JSS's jss123, styled-components' sc-bdVaJa, CSS
    // modules' _abc123) differ per build, so they cannot say two nodes are alike; Angular's ng-* are state.
    /// <summary>Whether a class name is the app's own, not one a CSS-in-JS tool generates (<c>css-1x2y</c>).</summary>
    internal static bool IsStable(string name) =>
        !(name.StartsWith("css-", StringComparison.Ordinal)
          || name.StartsWith("jss", StringComparison.Ordinal) && name.Skip(3).All(char.IsAsciiDigit)
          || name.StartsWith("sc-", StringComparison.Ordinal)
          || name.StartsWith('_') && name.Any(char.IsAsciiDigit)
          || name.StartsWith("ng-", StringComparison.Ordinal));
}
