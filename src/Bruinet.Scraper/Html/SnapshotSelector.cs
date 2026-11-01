using System.Diagnostics.CodeAnalysis;
using System.Text;
using Bruinet.Core.Scraping;

namespace Bruinet.Scraper.Html;

/// <summary>
/// A CSS selector matched against a recorded snapshot, so a control's parts can be checked against the
/// recordings without a browser. The part of CSS page objects use: type, <c>*</c>, <c>#id</c>,
/// <c>.class</c>, attributes (<c>[a]</c>, <c>[a=v]</c>, <c>[a^=v]</c>, <c>[a$=v]</c>, <c>[a*=v]</c>),
/// <c>:nth-of-type(n)</c>, <c>:first-child</c>, <c>:has-text("t")</c> (Playwright's: a part of the text, any
/// case), descendant and child combinators, and lists (<c>a, b</c>). Anything else is not read.
/// </summary>
public sealed class SnapshotSelector
{
    private readonly IReadOnlyList<Compound[]> _alternatives;

    private SnapshotSelector(IReadOnlyList<Compound[]> alternatives) => _alternatives = alternatives;

    /// <summary>The selector, or null when it uses what this does not read.</summary>
    public static SnapshotSelector? Parse(string css)
    {
        var alternatives = new List<Compound[]>();
        foreach (var part in SplitTop(css, ','))
        {
            if (ParseComplex(part.Trim()) is not { } complex)
                return null;
            alternatives.Add(complex);
        }
        return alternatives.Count == 0 ? null : new SnapshotSelector(alternatives);
    }

    /// <summary>The nodes under <paramref name="root"/> (not the root itself) the selector matches, in document order.</summary>
    public IReadOnlyList<ScrapeNode> Under(ScrapeSnapshot snapshot, ScrapeNode root)
    {
        var below = new HashSet<int>();
        var found = new List<ScrapeNode>();
        foreach (var node in snapshot.Nodes)
        {
            if (node.Parent == 0 || !(node.Parent == root.Number || below.Contains(node.Parent)))
                continue;
            below.Add(node.Number);
            if (node.Kind == ScrapeNodeKind.Element && _alternatives.Any(a => Matches(snapshot, node, a, a.Length - 1, root.Number)))
                found.Add(node);
        }
        return found;
    }

    // Right to left: the last compound on the node, the ones before on its ancestors (stopping at the root).
    private static bool Matches(ScrapeSnapshot snapshot, ScrapeNode node, Compound[] complex, int index, int root)
    {
        if (!complex[index].Matches(snapshot, node))
            return false;
        if (index == 0)
            return true;
        var combinator = complex[index].Combinator;
        for (var parent = node.Parent; parent != 0 && parent != root; parent = snapshot[parent].Parent)
        {
            if (Matches(snapshot, snapshot[parent], complex, index - 1, root))
                return true;
            if (combinator == '>')
                return false;
        }
        return false;
    }

    private static Compound[]? ParseComplex(string text)
    {
        var compounds = new List<Compound>();
        var combinator = ' ';
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '>')
            { combinator = '>'; i++; continue; }
            if (char.IsWhiteSpace(text[i]))
            { i++; continue; }
            var start = i;
            var depth = 0;
            char? quote = null;
            for (; i < text.Length; i++)
            {
                var c = text[i];
                if (quote is not null)
                { if (c == quote) quote = null; continue; }
                if (c is '"' or '\'')
                    quote = c;
                else if (c is '(' or '[')
                    depth++;
                else if (c is ')' or ']')
                    depth--;
                else if (depth == 0 && (char.IsWhiteSpace(c) || c == '>'))
                    break;
            }
            if (Compound.Parse(text[start..i], compounds.Count == 0 ? ' ' : combinator) is not { } compound)
                return null;
            compounds.Add(compound);
            combinator = ' ';
        }
        return compounds.Count == 0 ? null : compounds.ToArray();
    }

    private static IEnumerable<string> SplitTop(string text, char separator)
    {
        var depth = 0;
        char? quote = null;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quote is not null)
            { if (c == quote) quote = null; continue; }
            if (c is '"' or '\'')
                quote = c;
            else if (c is '(' or '[')
                depth++;
            else if (c is ')' or ']')
                depth--;
            else if (c == separator && depth == 0)
            { yield return text[start..i]; start = i + 1; }
        }
        yield return text[start..];
    }

    private static string TextOf(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        var text = new StringBuilder(node.Text);
        var below = new HashSet<int> { node.Number };
        foreach (var n in snapshot.Nodes)
        {
            if (n.Parent == 0 || !below.Contains(n.Parent))
                continue;
            below.Add(n.Number);
            if (n.Text is { Length: > 0 } t)
                text.Append(' ').Append(t);
        }
        return text.ToString();
    }

    private sealed record Compound(char Combinator, string? Type, IReadOnlyList<Func<ScrapeSnapshot, ScrapeNode, bool>> Tests)
    {
        public bool Matches(ScrapeSnapshot snapshot, ScrapeNode node) =>
            (Type is null || string.Equals(node.Type, Type, StringComparison.OrdinalIgnoreCase)) && Tests.All(t => t(snapshot, node));

        [SuppressMessage("Maintainability", "CA1502", Justification = "Known debt (AD-033): split when it next changes.")]
        public static Compound? Parse(string text, char combinator)
        {
            var i = 0;
            string? type = null;
            var tests = new List<Func<ScrapeSnapshot, ScrapeNode, bool>>();
            if (i < text.Length && text[i] == '*')
                i++;
            else
            {
                var start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] is '-' or '_'))
                    i++;
                if (i > start)
                    type = text[start..i];
            }
            while (i < text.Length)
            {
                var c = text[i];
                if (c is '#' or '.')
                {
                    var start = ++i;
                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] is '-' or '_'))
                        i++;
                    var name = text[start..i];
                    if (name.Length == 0)
                        return null;
                    tests.Add(c == '#'
                        ? (_, n) => n.Attribute("id") == name
                        : (_, n) => n.Attribute("class")?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(name, StringComparer.Ordinal) == true);
                }
                else if (c == '[')
                {
                    var end = text.IndexOf(']', i);
                    if (end < 0 || Attribute(text[(i + 1)..end]) is not { } test)
                        return null;
                    tests.Add(test);
                    i = end + 1;
                }
                else if (c == ':')
                {
                    var start = ++i;
                    while (i < text.Length && (char.IsLetter(text[i]) || text[i] == '-'))
                        i++;
                    var name = text[start..i];
                    string? argument = null;
                    if (i < text.Length && text[i] == '(')
                    {
                        var close = Close(text, i);
                        if (close < 0)
                            return null;
                        argument = text[(i + 1)..close].Trim();
                        i = close + 1;
                    }
                    if (Pseudo(name, argument) is not { } test)
                        return null;
                    tests.Add(test);
                }
                else
                    return null;
            }
            return new Compound(combinator, type, tests);
        }

        private static int Close(string text, int open)
        {
            var depth = 0;
            char? quote = null;
            for (var i = open; i < text.Length; i++)
            {
                var c = text[i];
                if (quote is not null)
                { if (c == quote) quote = null; continue; }
                if (c is '"' or '\'')
                    quote = c;
                else if (c == '(')
                    depth++;
                else if (c == ')' && --depth == 0)
                    return i;
            }
            return -1;
        }

        private static Func<ScrapeSnapshot, ScrapeNode, bool>? Attribute(string inside)
        {
            var op = new[] { "^=", "$=", "*=", "=" }.FirstOrDefault(o => inside.Contains(o, StringComparison.Ordinal));
            if (op is null)
            {
                var only = inside.Trim();
                return (_, n) => n.Attributes.ContainsKey(only);
            }
            var at = inside.IndexOf(op, StringComparison.Ordinal);
            var name = inside[..at].Trim();
            var value = Unquote(inside[(at + op.Length)..].Trim());
            return op switch
            {
                "^=" => (_, n) => n.Attribute(name)?.StartsWith(value, StringComparison.Ordinal) == true,
                "$=" => (_, n) => n.Attribute(name)?.EndsWith(value, StringComparison.Ordinal) == true,
                "*=" => (_, n) => n.Attribute(name)?.Contains(value, StringComparison.Ordinal) == true,
                _ => (_, n) => n.Attribute(name) == value,
            };
        }

        private static Func<ScrapeSnapshot, ScrapeNode, bool>? Pseudo(string name, string? argument) => name switch
        {
            "has-text" when argument is not null => (s, n) => TextOf(s, n).Contains(Unquote(argument), StringComparison.OrdinalIgnoreCase),
            "nth-of-type" when int.TryParse(argument, out var nth) => (s, n) =>
                n.Parent != 0 && s.ChildrenOf(n.Parent).Where(x => x.Type == n.Type).Select((x, index) => (x, index)).FirstOrDefault(p => p.x.Number == n.Number).index == nth - 1,
            "first-child" => (s, n) => n.Parent != 0 && s.ChildrenOf(n.Parent).FirstOrDefault(x => x.Kind == ScrapeNodeKind.Element)?.Number == n.Number,
            _ => null,
        };

        private static string Unquote(string value) =>
            value.Length >= 2 && (value[0] is '"' or '\'') && value[^1] == value[0] ? value[1..^1] : value;
    }
}
