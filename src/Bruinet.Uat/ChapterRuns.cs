using Bruinet.Bdd;

namespace Bruinet.Uat;

/// <summary>
/// A spec's chapter, run once for the examples that only check: when the example before was of the same chapter, only
/// checked, and passed, the page is still as the chapter's steps left it, so the next such example runs its own Then
/// steps and leaves the chapter's out. A suite asks for it (<c>ShareChapters</c> in its config), because it holds only
/// where a Then step never changes the page. Any other example, a failure, or another file starts over.
/// </summary>
internal sealed class ChapterRuns
{
    private string? _file;
    private string? _chapter;

    /// <summary>The page the example before ended on, by its name: a shared run starts there, with no step that names it.</summary>
    public string? Page { get; private set; }

    /// <summary>The chapter an example may share: its <c>chapter:</c> tag when it only checks, otherwise null.</summary>
    /// <param name="tags">The scenario's tags.</param>
    public static string? SharedChapterOf(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        return tags.Contains(BddSpecReader.ThenOnlyTag, StringComparer.Ordinal)
            ? tags.FirstOrDefault(tag => tag.StartsWith("chapter:", StringComparison.Ordinal))
            : null;
    }

    /// <summary>How many of a scenario's last steps are its own checks: the Then steps after the last Given or When.</summary>
    /// <param name="keywords">The steps' keywords, in order.</param>
    public static int OwnChecks(IReadOnlyList<BddEffectiveStepKeyword> keywords)
    {
        ArgumentNullException.ThrowIfNull(keywords);
        var own = 0;
        for (var i = keywords.Count - 1; i >= 0 && keywords[i] == BddEffectiveStepKeyword.Then; i--)
        {
            own++;
        }

        return own;
    }

    /// <summary>
    /// Whether this example runs on the page the example before left: the same file and chapter, and both only check.
    /// </summary>
    /// <param name="file">The spec's path.</param>
    /// <param name="tags">The scenario's tags.</param>
    public bool Shares(string? file, IReadOnlyList<string> tags)
        => SharedChapterOf(tags) is { } chapter && file is not null
            && string.Equals(_file, file, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_chapter, chapter, StringComparison.Ordinal);

    /// <summary>Remembers how the example ended: a passed example that only checked leaves its chapter's page to share.</summary>
    /// <param name="file">The spec's path.</param>
    /// <param name="tags">The scenario's tags.</param>
    /// <param name="passed">Whether it passed.</param>
    /// <param name="page">The page it ended on, by its name.</param>
    public void Ran(string? file, IReadOnlyList<string> tags, bool passed, string? page = null)
    {
        var chapter = passed ? SharedChapterOf(tags) : null;
        _file = chapter is null ? null : file;
        _chapter = chapter;
        Page = chapter is null ? null : page;
    }

    /// <summary>The scenario with only its own checks, for a run that shares its chapter's page.</summary>
    /// <param name="scenario">The bound scenario, chapter steps first.</param>
    public static BddBoundScenario OwnChecksOf(BddBoundScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var own = OwnChecks([.. scenario.Invocations.Select(invocation => invocation.Step.EffectiveKeyword)]);
        return own == 0 || own == scenario.Invocations.Count
            ? scenario
            : scenario with { Invocations = [.. scenario.Invocations.Skip(scenario.Invocations.Count - own)] };
    }
}
