using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bruinet.Scraper.Code;

/// <summary>What merging a draft into a class that is there did.</summary>
/// <param name="Code">The file's code after the merge.</param>
/// <param name="Added">Members the draft has and the class did not: added.</param>
/// <param name="Replaced">Members both have, written differently: the draft's now.</param>
/// <param name="Kept">Members the class has and the draft does not: kept as they were, whoever wrote them.</param>
public sealed record MergedClass(string Code, IReadOnlyList<string> Added, IReadOnlyList<string> Replaced, IReadOnlyList<string> Kept)
{
    /// <summary>True when the merge changed nothing but, at most, the first line.</summary>
    public bool Same => Added.Count == 0 && Replaced.Count == 0;
}

/// <summary>
/// Merges a draft into the class that is there, member by member: a member the draft has replaces the class's own of
/// that name or is added, and a member only the class has stays. Who wrote a member does not decide; what the page
/// shows does, and a member the page does not show is for its test to find. One file holds the class afterwards.
/// </summary>
public static class ClassMerge
{
    /// <summary>Merges a draft of a class into a file that declares it.</summary>
    /// <param name="existing">The file as it is; it may hold other classes, which are not touched.</param>
    /// <param name="draft">The draft: a file with the class and the line it starts with.</param>
    /// <param name="className">The class.</param>
    /// <exception cref="ArgumentException">One of the two does not declare the class.</exception>
    public static MergedClass Merge(string existing, string draft, string className)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(draft);
        var was = CSharpSyntaxTree.ParseText(existing).GetCompilationUnitRoot();
        var now = CSharpSyntaxTree.ParseText(draft).GetCompilationUnitRoot();
        var target = Declaration(was, className) ?? throw new ArgumentException($"The file does not declare {className}.", nameof(existing));
        var source = Declaration(now, className) ?? throw new ArgumentException($"The draft does not declare {className}.", nameof(draft));

        var added = new List<string>();
        var replaced = new List<string>();
        var members = target.Members.ToList();
        foreach (var member in source.Members)
        {
            if (NameOf(member) is not { } name)
                continue;
            var at = members.FindIndex(own => NameOf(own) == name);
            if (at < 0)
            {
                members.Add(member);
                added.Add(name);
            }
            else if (!Same(members[at], member))
            {
                // The class's own lines above the member stay with the draft's member when the draft has none of its own.
                members[at] = member.HasLeadingTrivia && member.GetLeadingTrivia().Any(trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)) ? member : member.WithLeadingTrivia(members[at].GetLeadingTrivia());
                replaced.Add(name);
            }
        }

        var drafted = source.Members.Select(NameOf).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var kept = target.Members.Select(NameOf).OfType<string>().Where(name => !drafted.Contains(name)).ToList();
        var merged = was.ReplaceNode(target, target.WithMembers(SyntaxFactory.List(members)));

        // The draft's usings that the file lacks; the file's own stay, in their order.
        var have = merged.Usings.Select(each => each.ToString().Trim()).ToHashSet(StringComparer.Ordinal);
        var missing = now.Usings.Where(each => !have.Contains(each.ToString().Trim())).Select(each => each.WithoutLeadingTrivia()).ToList();
        if (missing.Count > 0)
            merged = merged.WithUsings(merged.Usings.AddRange(missing));

        return new MergedClass(WithFirstLine(merged.ToFullString(), FirstLine(draft)), added, replaced, kept);
    }

    // The line that says what a class was made from is the draft's: it names the capture and the hash of now.
    private static string WithFirstLine(string code, string? header)
    {
        if (header is null)
            return code;
        var end = code.IndexOf('\n', StringComparison.Ordinal);
        var first = end < 0 ? code : code[..end].TrimEnd('\r');
        var newline = end > 0 && code[end - 1] == '\r' ? "\r\n" : "\n";
        return ScrapedHeader.Find(first) is not null ? header + code[first.Length..] : header + newline + code;
    }

    private static string? FirstLine(string draft)
    {
        var end = draft.IndexOf('\n', StringComparison.Ordinal);
        var first = (end < 0 ? draft : draft[..end]).TrimEnd('\r');
        return ScrapedHeader.Find(first) is null ? null : first;
    }

    private static TypeDeclarationSyntax? Declaration(CompilationUnitSyntax root, string className) =>
        root.DescendantNodes().OfType<TypeDeclarationSyntax>().FirstOrDefault(type => type.Identifier.ValueText == className);

    // A member's name: a property's, a method's, a field's first variable's; null for what has none.
    private static string? NameOf(MemberDeclarationSyntax member) => member switch
    {
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        FieldDeclarationSyntax field => field.Declaration.Variables.FirstOrDefault()?.Identifier.ValueText,
        _ => null,
    };

    // The same member whatever its spacing and whatever stands above it.
    private static bool Same(MemberDeclarationSyntax a, MemberDeclarationSyntax b) =>
        a.WithoutTrivia().NormalizeWhitespace().ToFullString() == b.WithoutTrivia().NormalizeWhitespace().ToFullString();
}
