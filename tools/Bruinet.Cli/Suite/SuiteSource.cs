using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bruinet.Cli.Suite;

/// <summary>A member of a suite's class, as its source says it: a property, or a method with a phrase of its own.</summary>
/// <param name="Name">The member's name.</param>
/// <param name="Type">Its type's own name, without namespace and type arguments.</param>
/// <param name="TypeArguments">Every type named in its type arguments, at any depth.</param>
/// <param name="Phrase">Its <c>[BddControl]</c> phrase; null when it has none.</param>
/// <param name="IsMethod">A method, not a property.</param>
internal sealed record SourceMember(string Name, string Type, IReadOnlyList<string> TypeArguments, string? Phrase, bool IsMethod);

/// <summary>A class or interface of a suite, as its source says it.</summary>
/// <param name="Name">The type's name, without type parameters.</param>
/// <param name="File">The file it is declared in.</param>
/// <param name="Bases">Its base types' own names, in the order written.</param>
/// <param name="Kind">Its <c>[BddKind]</c>; null when it has none.</param>
/// <param name="PageName">Its <c>[BddPage]</c> name; null when it has none.</param>
/// <param name="Address">The first address (a text starting with <c>/</c>) it hands its base constructor; null when none.</param>
/// <param name="Members">Its public instance members.</param>
/// <param name="IsBase">Abstract or with type parameters: a base for other types, not a thing of its own.</param>
internal sealed record SourceType(string Name, string File, IReadOnlyList<string> Bases, string? Kind, string? PageName, string? Address, IReadOnlyList<SourceMember> Members, bool IsBase);

/// <summary>
/// A suite's types read from its source with Roslyn's syntax only: no build, no semantic model. A type is known by its
/// name, so two types of one name are one here (the first read wins; a partial class's parts are joined).
/// </summary>
internal sealed class SuiteSource
{
    private readonly Dictionary<string, SourceType> _types = new(StringComparer.Ordinal);

    /// <summary>Every type read, by name.</summary>
    public IReadOnlyDictionary<string, SourceType> Types => _types;

    /// <summary>Reads every <c>.cs</c> file under a folder, but for <c>bin</c> and <c>obj</c>.</summary>
    public static SuiteSource Read(string folder)
    {
        var source = new SuiteSource();
        foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(folder, file).Replace('\\', '/');
            if (relative.Split('/').Any(part => part is "bin" or "obj" or "artifacts" or "node_modules"))
                continue;
            source.Add(relative, File.ReadAllText(file));
        }

        return source;
    }

    /// <summary>Reads one file's types.</summary>
    /// <param name="file">The file's path, as the checklist shows it.</param>
    /// <param name="text">The file's text.</param>
    public void Add(string file, string text)
    {
        var root = CSharpSyntaxTree.ParseText(text).GetRoot();
        foreach (var declared in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            // A private class inside another is a test's own stand-in, not a type of the suite.
            if (declared is not (ClassDeclarationSyntax or InterfaceDeclarationSyntax or RecordDeclarationSyntax) || declared.Modifiers.Any(SyntaxKind.PrivateKeyword))
                continue;
            var read = new SourceType(
                declared.Identifier.ValueText,
                file,
                declared.BaseList?.Types.Select(type => NameOf(type.Type)).ToList() ?? [],
                AttributeText(declared.AttributeLists, "BddKind"),
                AttributeText(declared.AttributeLists, "BddPage"),
                AddressOf(declared),
                [.. Members(declared)],
                declared.TypeParameterList is not null || declared.Modifiers.Any(SyntaxKind.AbstractKeyword) || declared is InterfaceDeclarationSyntax);

            // A partial class is one type: its parts' base types and members together.
            _types[read.Name] = _types.TryGetValue(read.Name, out var part) && declared.Modifiers.Any(SyntaxKind.PartialKeyword)
                ? part with
                {
                    Bases = [.. part.Bases.Union(read.Bases, StringComparer.Ordinal)],
                    Kind = part.Kind ?? read.Kind,
                    PageName = part.PageName ?? read.PageName,
                    Address = part.Address ?? read.Address,
                    Members = [.. part.Members.Concat(read.Members).DistinctBy(member => member.Name, StringComparer.Ordinal)],
                }
                : part ?? read;
        }
    }

    /// <summary>A type and every type its base types lead to, by name; a base outside the suite ends its branch.</summary>
    public IEnumerable<string> Chain(string type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var todo = new Queue<string>([type]);
        while (todo.TryDequeue(out var name))
        {
            if (!seen.Add(name))
                continue;
            yield return name;
            if (_types.TryGetValue(name, out var known))
            {
                foreach (var parent in known.Bases)
                    todo.Enqueue(parent);
            }
        }
    }

    /// <summary>A type's members with those of its base types in the suite; a member of the type itself hides one of the same name.</summary>
    public IEnumerable<SourceMember> AllMembers(string type)
        => Chain(type).Where(_types.ContainsKey).SelectMany(name => _types[name].Members).DistinctBy(member => member.Name, StringComparer.Ordinal);

    private static IEnumerable<SourceMember> Members(TypeDeclarationSyntax declared)
    {
        foreach (var member in declared.Members)
        {
            var modifiers = member.Modifiers;
            var isPublic = modifiers.Any(SyntaxKind.PublicKeyword) || declared is InterfaceDeclarationSyntax;
            if (!isPublic || modifiers.Any(SyntaxKind.StaticKeyword) || AttributeText(member.AttributeLists, "BddIgnore") is not null)
                continue;
            switch (member)
            {
                case PropertyDeclarationSyntax property:
                    yield return new SourceMember(property.Identifier.ValueText, NameOf(property.Type), [.. Arguments(property.Type)], AttributeText(property.AttributeLists, "BddControl"), false);
                    break;
                case MethodDeclarationSyntax method when AttributeText(method.AttributeLists, "BddControl") is { } phrase:
                    yield return new SourceMember(method.Identifier.ValueText, NameOf(method.ReturnType), [.. Arguments(method.ReturnType)], phrase, true);
                    break;
            }
        }
    }

    // The first argument of an attribute, when it is a text; an empty text for an attribute without one; null when the attribute is absent.
    private static string? AttributeText(SyntaxList<AttributeListSyntax> lists, string attribute)
    {
        foreach (var candidate in lists.SelectMany(list => list.Attributes))
        {
            var name = NameOf(candidate.Name);
            if (name != attribute && name != attribute + "Attribute")
                continue;
            return candidate.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
                ? literal.Token.ValueText
                : string.Empty;
        }

        return null;
    }

    // The address a page hands its base: the first text starting with / among the base's arguments.
    private static string? AddressOf(TypeDeclarationSyntax declared)
    {
        var arguments = declared.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().SelectMany(type => type.ArgumentList.Arguments)
            .Concat(declared.Members.OfType<ConstructorDeclarationSyntax>().SelectMany(constructor => constructor.Initializer?.ArgumentList.Arguments ?? []))
            ?? [];
        return arguments.Select(argument => argument.Expression).OfType<LiteralExpressionSyntax>()
            .Where(literal => literal.IsKind(SyntaxKind.StringLiteralExpression))
            .Select(literal => literal.Token.ValueText)
            .FirstOrDefault(text => text.StartsWith('/'));
    }

    private static string NameOf(SyntaxNode type) => type switch
    {
        NullableTypeSyntax nullable => NameOf(nullable.ElementType),
        ArrayTypeSyntax array => NameOf(array.ElementType),
        QualifiedNameSyntax qualified => NameOf(qualified.Right),
        AliasQualifiedNameSyntax alias => NameOf(alias.Name),
        GenericNameSyntax generic => generic.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        _ => type.ToString(),
    };

    private static IEnumerable<string> Arguments(TypeSyntax type)
        => type.DescendantNodes().OfType<TypeArgumentListSyntax>().SelectMany(list => list.Arguments).Select(NameOf).Distinct(StringComparer.Ordinal);
}
