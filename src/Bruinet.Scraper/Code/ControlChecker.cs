using Bruinet.Core.Scraping;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bruinet.Scraper.Code;

/// <summary>
/// Checks a made control against the recordings: every occurrence of the pattern it was made from (its
/// header's <c>control</c> signature, in each page's newest capture), and in each, whether each part's
/// locator finds its element. A part of the control itself must be found once; a part of an item or
/// row class nested in it (a list's rows) at least once.
/// </summary>
public static class ControlChecker
{
    /// <summary>The project's own controls made from a recorded pattern, each checked.</summary>
    public static IReadOnlyList<ControlCheck> Check(ScrapeProject project, IReadOnlySet<string> accepted)
    {
        var index = project.Index();
        var pages = index.Pages.Select(p => (p.Key, Snapshot: index.Read(p.Latest))).ToArray();
        var classes = ProjectCode.Read(project).Where(c => c.Project == project.Name && c.Kind != CodeKind.Page).ToArray();
        var checks = new List<ControlCheck>();
        foreach (var control in classes.Where(c => c.Header?.Control is not null))
        {
            var occurrences = pages
                .SelectMany(p => p.Snapshot.Nodes.Where(n => n.Kind == ScrapeNodeKind.Element && NodeSignature.Of(p.Snapshot, n) == control.Header!.Control)
                    .Select(n => control.Header!.Lifted ? PatternDetector.RootOf(p.Snapshot, n) : n)
                    .Where(n => control.Header!.Variant is null || ControlKind.Variant(p.Snapshot, n) == control.Header.Variant)
                    .Distinct()
                    .Select(n => (p.Key, p.Snapshot, Root: n)))
                .ToArray();
            // The control's own members, then those of the classes in its file (its collection's items).
            var nested = classes.Where(c => c.File == control.File && c.Name != control.Name).ToArray();
            var parts = control.Members.Select(m => Part(m, $"{m.Name}", occurrences, once: true))
                .Concat(nested.SelectMany(n => n.Members.Select(m => Part(m, $"{Short(n.Name)}.{m.Name}", occurrences, once: false))))
                .Where(p => p is not null)
                .Select(p => p!)
                .ToArray();
            var name = Short(control.Name);
            checks.Add(new ControlCheck(
                name,
                Path.GetRelativePath(project.Folder, control.File),
                control.Header!.Control!,
                occurrences.Length,
                occurrences.Select(o => o.Key).Distinct(StringComparer.Ordinal).Count(),
                parts,
                accepted.Contains(name))
            { Variant = control.Header.Variant, Lifted = control.Header.Lifted });
        }
        return checks;
    }

    private static ControlPart? Part(CodeMember member, string name, (string Page, ScrapeSnapshot Snapshot, ScrapeNode Root)[] occurrences, bool once)
    {
        if (member.Locator is null)
            return null; // a collection or a part made in code: nothing to look up
        if (Css(member.Locator) is not { } css || SnapshotSelector.Parse(css) is not { } selector)
            return new ControlPart(name, member.Locator, Readable: false, Found: 0, Of: occurrences.Length, Misses: []);
        var misses = new List<ControlMiss>();
        foreach (var (page, snapshot, root) in occurrences)
        {
            var count = selector.Under(snapshot, root).Count;
            if (once ? count != 1 : count == 0)
                misses.Add(new ControlMiss(page, root.Number, count));
        }
        return new ControlPart(name, member.Locator, Readable: true, occurrences.Length - misses.Count, occurrences.Length, misses.Take(10).ToArray());
    }

    /// <summary>A locator as written in C# as CSS a snapshot can be matched by; null when it is not one this reads.</summary>
    public static string? Css(string locator)
    {
        var expression = SyntaxFactory.ParseExpression(locator);
        if (expression is LiteralExpressionSyntax { Token.Value: string literal })
            return literal;
        if (expression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } call
            || access.Expression.ToString() is not ("Locator" or "Bruinet.Core.Locators.Locator")
            || call.ArgumentList.Arguments.Count != 1
            || call.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax { Token.Value: string value })
            return null;
        var quoted = value.Replace("'", "\\'", StringComparison.Ordinal);
        return access.Name.Identifier.Text switch
        {
            "ByCss" or "ByTagName" => value,
            "ById" => $"[id='{quoted}']",
            "ByDataTestId" => $"[data-testid='{quoted}']",
            "ByDataAutomationId" => $"[data-automation-id='{quoted}']",
            "ByName" => $"[name='{quoted}']",
            "ByClassName" => $".{value}",
            "ByLinkText" or "ByPartialLinkText" => $"a:has-text('{quoted}')",
            _ => null,
        };
    }

    private static string Short(string name) => name.Split('<')[0];
}

/// <summary>A made control checked against the recordings.</summary>
/// <param name="Name">The control's class.</param>
/// <param name="File">Its file, relative to the project.</param>
/// <param name="Pattern">The signature of the pattern it was made from.</param>
/// <param name="Occurrences">How many times the pattern occurs in the pages' newest captures.</param>
/// <param name="Pages">On how many pages.</param>
/// <param name="Parts">Each part and where it is found.</param>
/// <param name="Accepted">The person accepted it as it is.</param>
public sealed record ControlCheck(string Name, string File, string Pattern, int Occurrences, int Pages, IReadOnlyList<ControlPart> Parts, bool Accepted)
{
    /// <summary>The variant of the pattern it was made from, when the pattern has several.</summary>
    public string? Variant { get; init; }

    /// <summary>Whether it was made from a lifted pattern (<see cref="Pattern.Lifted"/>).</summary>
    public bool Lifted { get; init; }

    /// <summary>Every readable part found in every occurrence.</summary>
    public bool Passed => Parts.All(p => !p.Readable || p.Found == p.Of);

    /// <summary>Checked and fine, or accepted.</summary>
    public bool Done => Passed || Accepted;
}

/// <summary>A part of a control: its locator, and in how many occurrences it is found (once, for the control's own parts).</summary>
public sealed record ControlPart(string Name, string Locator, bool Readable, int Found, int Of, IReadOnlyList<ControlMiss> Misses);

/// <summary>An occurrence where a part is not found as it should be: the page, the occurrence's root node, and how many it found.</summary>
public sealed record ControlMiss(string Page, int Node, int Count);
