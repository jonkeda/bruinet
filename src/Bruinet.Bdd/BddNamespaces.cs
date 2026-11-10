namespace Bruinet.Bdd;

/// <summary>
/// Phrase namespaces: a namespace is a folder under the scenario root, written with <c>/</c>
/// (<c>Invoicing/Credit notes</c>); the root is <c>""</c>. A phrase of a namespace is visible from
/// that folder and every folder below it; a phrase of the root, from everywhere.
/// </summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public static class BddNamespaces
{
    /// <summary>A namespace as it is compared: <c>/</c> between folders, no empty parts, <c>""</c> for the root.</summary>
    /// <param name="name">A namespace or a relative folder, with <c>/</c> or <c>\</c>; null for the root.</param>
    public static string Normalize(string? name)
        => string.Join('/', (name ?? string.Empty)
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part != "."));

    /// <summary>Whether a phrase of <paramref name="owner"/> is visible from <paramref name="from"/>.</summary>
    /// <param name="owner">The phrase's namespace; null or <c>""</c> for the root.</param>
    /// <param name="from">The namespace of the file that uses it.</param>
    public static bool IsVisible(string? owner, string? from)
    {
        var ownerName = Normalize(owner);
        if (ownerName.Length == 0)
        {
            return true;
        }

        var fromName = Normalize(from);
        return fromName.Equals(ownerName, StringComparison.OrdinalIgnoreCase) ||
               fromName.StartsWith(ownerName + "/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The namespace of a scenario or phrase file: its folder, relative to the scenario root.</summary>
    /// <param name="filePath">The file; null or a path without a folder gives the root.</param>
    public static string Of(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.Empty;
        }

        var folder = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        return Normalize(Path.GetRelativePath(BddScenarioFiles.ScenarioRoot(filePath), folder));
    }

    /// <summary>A page as steps and the runtime know it: <c>Invoicing/Detail</c>, or its name without a namespace.</summary>
    public static string PageKey(string? name, string page)
        => Normalize(name) is { Length: > 0 } normalized ? $"{normalized}/{page}" : page;

    /// <summary>The namespaces a <c>Using</c> value names: <c>Accounts, Backend</c>.</summary>
    public static IReadOnlyList<string> ParseList(string? value)
        => (value ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>A namespace as a message names it.</summary>
    public static string Describe(string? name)
        => Normalize(name) is { Length: > 0 } normalized ? $"the folder '{normalized}'" : "the scenario root";
}

/// <summary>
/// Where a step is written, for what it can see: a namespace, the file for its own callables, and
/// the namespaces it uses (the file's <c>Using</c> row and the config's <c>## Using</c>).
/// </summary>
/// <param name="Namespace">The folder under the scenario root.</param>
/// <param name="File">The full path of the scenario file; null inside a phrase file's callable.</param>
/// <param name="Usings">The namespaces it uses; seeing one is as if the file were also in it.</param>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddScope(string Namespace, string? File, IReadOnlyList<string> Usings)
{
    public static BddScope Of(BddDocument document, BddCatalogSet catalogs)
        => new(
            document.Source.FilePath is { } path ? BddNamespaces.Of(path) : string.Empty,
            document.Source.FilePath is { } file ? Path.GetFullPath(file) : null,
            [.. catalogs.Usings, .. BddNamespaces.ParseList(document.Metadata.GetValueOrDefault(BddMetadataFields.Using))]);

    // A callable's body binds where it is written (D9): its file's own callables and usings, or
    // its phrase file's namespace and the config's usings.
    public static BddScope Of(BddCallable callable, BddCatalogSet catalogs)
        => new(callable.Namespace, callable.LocalTo, [.. catalogs.Usings, .. callable.Usings]);

    public bool Sees(string? owner)
        => BddNamespaces.IsVisible(owner, Namespace) || Usings.Any(used => BddNamespaces.IsVisible(owner, used));

    // The usings a phrase of owner is seen through, when the folder alone does not see it.
    public IEnumerable<string> UsingsFor(string? owner)
        => BddNamespaces.IsVisible(owner, Namespace) ? [] : Usings.Where(used => BddNamespaces.IsVisible(owner, used));

    public bool Sees(BddCallable callable)
        => callable.LocalTo is { } local
            ? File is not null && string.Equals(local, File, StringComparison.OrdinalIgnoreCase)
            : Sees(callable.Namespace);

    public bool Sees(BddCommandPattern pattern)
        => pattern.Callable is { } callable ? Sees(callable) : Sees(pattern.Namespace);

    // The namespace a bound phrase belongs to; null for a file's own callable, which is always seen.
    public static string? OwnerOf(BddCommandPattern pattern)
        => pattern.Callable is { } callable ? callable.LocalTo is null ? callable.Namespace : null : pattern.Namespace;
}
