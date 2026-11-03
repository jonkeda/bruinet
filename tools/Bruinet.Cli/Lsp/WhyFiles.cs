namespace Bruinet.Bdd.LanguageServer;

/// <summary>
/// The files changed since a green run, sorted for <c>why</c> (AI9): those that touch the step where the runs part, those
/// that touch an earlier step of the scenario, and the others. A file touches a step when its name has every word of a page
/// the step drove (<c>TodoDetailViewModel.cs</c> for <c>TodoDetailPage</c>), or its text names an id-like control the step
/// drove, quoted (<c>"TodoDetail_Delete"</c>).
/// </summary>
/// <param name="Touching">The ones that touch the step.</param>
/// <param name="Earlier">The ones that touch an earlier step.</param>
/// <param name="Other">The rest.</param>
/// <param name="Note">Why there are none, when there are none to sort.</param>
internal sealed record WhyFiles(IReadOnlyList<string> Touching, IReadOnlyList<string> Earlier, IReadOnlyList<string> Other, string? Note)
{
    private const int Named = 20;

    private static readonly HashSet<string> Binary = new(
        [".png", ".jpg", ".jpeg", ".gif", ".ico", ".webp", ".pdf", ".zip", ".dll", ".exe", ".pdb", ".nupkg", ".woff", ".woff2", ".ttf", ".mp4", ".webm", ".db", ".sqlite"],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>The answer's part: twenty named in each list at most, with how many more.</summary>
    public object Answer => new
    {
        Touching = Touching.Take(Named),
        TouchingMore = Touching.Count > Named ? Touching.Count - Named : (int?)null,
        Earlier = Earlier.Take(Named),
        EarlierMore = Earlier.Count > Named ? Earlier.Count - Named : (int?)null,
        Other = Other.Count,
        First = Other.Take(10),
    };

    /// <summary>Sorts <paramref name="changed"/>, reading each file's text once, and only for an id-like control.</summary>
    /// <param name="changed">The changed files, as git names them.</param>
    /// <param name="pathOf">Where a file is on disk.</param>
    /// <param name="step">The pages and controls the step where the runs part drove.</param>
    /// <param name="before">The pages and controls the steps before it drove.</param>
    public static (List<string> Touching, List<string> Earlier, List<string> Other) Sort(
        IEnumerable<string> changed, Func<string, string> pathOf, IReadOnlyList<(string Name, bool IsPage)> step, IReadOnlyList<(string Name, bool IsPage)> before)
    {
        var (stepPages, stepIds) = Split(step);
        var (beforePages, beforeIds) = Split(before);
        List<string> touching = [], earlier = [], other = [];
        foreach (var file in changed)
        {
            var words = BddMcpServer.Words(Path.GetFileNameWithoutExtension(file));
            var text = stepIds.Count + beforeIds.Count > 0 ? Text(pathOf(file)) : null;
            if (stepPages.Any(page => page.IsSubsetOf(words)) || Names(text, stepIds))
            {
                touching.Add(file);
            }
            else if (beforePages.Any(page => page.IsSubsetOf(words)) || Names(text, beforeIds))
            {
                earlier.Add(file);
            }
            else
            {
                other.Add(file);
            }
        }

        return (touching, earlier, other);
    }

    /// <summary>A control's test id, not a word any code may quote ("Delete", "Back"): <c>TodoDetail_Delete</c>, <c>counter-title</c>, <c>SaveButton</c>.</summary>
    /// <param name="name">The control's name.</param>
    public static bool IdLike(string name)
        => name.Contains('_', StringComparison.Ordinal) || name.Contains('-', StringComparison.Ordinal) || BddMcpServer.Words(name).Count > 1;

    private static (List<HashSet<string>> Pages, List<string> Ids) Split(IReadOnlyList<(string Name, bool IsPage)> names)
        => ([.. names.Where(name => name.IsPage).Select(name => BddMcpServer.Words(name.Name)).Where(words => words.Count > 0)],
            [.. names.Where(name => !name.IsPage && IdLike(name.Name)).Select(name => name.Name).Distinct(StringComparer.Ordinal)]);

    private static bool Names(string? text, List<string> ids)
        => text is not null && ids.Any(id => text.Contains($"\"{id}\"", StringComparison.Ordinal) || text.Contains($"'{id}'", StringComparison.Ordinal));

    // A text file's text, under a megabyte; null for a binary, a large or a gone one.
    private static string? Text(string path)
    {
        try
        {
            return !Binary.Contains(Path.GetExtension(path)) && File.Exists(path) && new FileInfo(path).Length < 1_000_000 ? File.ReadAllText(path) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
