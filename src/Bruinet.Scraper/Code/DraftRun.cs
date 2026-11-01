using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Code;

/// <summary>
/// Drafts page objects for a project's recorded pages (<see cref="PageDrafter"/>) and passes them through
/// the gate: every draft is written, the project is built, and a draft the build fails on is put back as
/// it was, with its errors. Nothing is left half-written.
/// </summary>
public static class DraftRun
{
    /// <summary>
    /// The patterns a draft asks a control for: those no control stands for yet that repeat, on two pages
    /// or more, or three times on one (a site of one page has its sidebar groups and table rows too).
    /// </summary>
    public static IReadOnlyList<Pattern> ControlsToMake(IEnumerable<Pattern> patterns) =>
        patterns.Where(p => p.Control is null && p.Hint is null && (p.Pages >= 2 || p.Kind is PatternKind.Repeated or PatternKind.Collection)).ToArray();

    /// <summary>Drafts every recorded page, or the one with key <paramref name="page"/>.</summary>
    public static Task<DraftResult> RunAsync(ScrapeProject project, string? page = null, CancellationToken cancellationToken = default) =>
        RunAsync(project, page is null ? null : [page], null, cancellationToken);

    /// <summary>Drafts the recorded pages with these keys, or every one when <paramref name="keys"/> is null.</summary>
    /// <param name="project">The scraped project.</param>
    /// <param name="keys">The pages' keys; every page when null.</param>
    /// <param name="templates">Generates the project's control templates before each build; null to build only.</param>
    /// <param name="cancellationToken">Ends the builds.</param>
    public static async Task<DraftResult> RunAsync(ScrapeProject project, IReadOnlyCollection<string>? keys, ITemplateGenerator? templates, CancellationToken cancellationToken = default)
    {
        var index = project.Index();
        var pages = keys is null
            ? index.Pages
            : keys.Select(k => index.Page(k) ?? throw new ArgumentException($"No recorded page '{k}'. The pages are {string.Join(", ", index.Pages.Select(p => p.Key))}.")).ToArray();
        var code = ProjectCode.Read(project).Where(c => c.Project == project.Name).ToArray();
        var names = code.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

        var outcomes = new List<DraftedPage>();
        var written = new List<(DraftedPage Page, string Full, string? Before)>();
        foreach (var recorded in pages)
        {
            // A page object a person made for the page (not a draft) is theirs: it is not drafted over.
            var own = code.FirstOrDefault(c => c.Header?.Page == recorded.Key && !c.File.EndsWith(PageDrafter.Extension, StringComparison.OrdinalIgnoreCase));
            if (own is not null)
            {
                outcomes.Add(new DraftedPage(recorded.Key, own.Name, Path.GetRelativePath(project.Folder, own.File), DraftOutcome.Skipped, 0, [], [], $"{own.Name} is made by hand for this page."));
                continue;
            }
            var previous = code.FirstOrDefault(c => c.Header?.Page == recorded.Key);
            var className = previous?.Name ?? Free(PageDrafter.ClassName(recorded.Key, PageDrafter.Basis(recorded).Event.Title), names);
            names.Add(className);
            var draft = PageDrafter.Draft(project, index, recorded, className);
            var path = previous is null ? draft.Path : Path.GetRelativePath(project.Folder, previous.File);
            var full = Path.Combine(project.Folder, path);
            var before = File.Exists(full) ? await File.ReadAllTextAsync(full, cancellationToken).ConfigureAwait(false) : null;
            // What the last draft had (its own file, not a person's part) and this one has not.
            var drafted = before is null ? null : ProjectCode.ReadText(before, full, project.Name).FirstOrDefault();
            var removed = drafted is null ? [] : drafted.Members.Select(m => m.Name).Except(draft.Members.Select(m => m.Name), StringComparer.Ordinal)
                .Select(m => new RemovedMember(m, drafted.Header?.From)).ToArray();
            var unstable = draft.Unstable.Select(u => $"{u.Control} {u.Name}: {u.Locator}").ToArray();

            if (before is not null && Same(before, draft.Code))
            {
                outcomes.Add(new DraftedPage(recorded.Key, className, path, DraftOutcome.Unchanged, draft.Members.Count, unstable, removed, null));
                continue;
            }
            var result = CodeGate.Write(project, path, draft.Code);
            var outcome = new DraftedPage(recorded.Key, className, path, result.Written ? DraftOutcome.Written : DraftOutcome.Reverted, draft.Members.Count, unstable, removed, result.Reason)
            {
                Problems = result.Problems,
            };
            outcomes.Add(outcome);
            if (result.Written)
                written.Add((outcome, full, before));
        }

        if (written.Count == 0)
            return new DraftResult(outcomes, null);

        var build = await CodeGate.BuildAsync(project, templates, cancellationToken).ConfigureAwait(false);
        if (build.Succeeded)
            return new DraftResult(outcomes, build);

        // Put back each draft the build failed on; when the failures are elsewhere, every draft.
        var failing = written.Where(w => build.Problems.Any(p => p.Severity == "error" && SameFile(p.File, w.Full))).ToList();
        var revert = failing.Count > 0 ? failing : written;
        foreach (var (drafted, full, before) in revert)
        {
            if (before is null)
                File.Delete(full);
            else
                await File.WriteAllTextAsync(full, before, cancellationToken).ConfigureAwait(false);
            var i = outcomes.IndexOf(drafted);
            outcomes[i] = drafted with
            {
                Outcome = DraftOutcome.Reverted,
                Reason = failing.Count > 0 ? "The project did not build with it." : "The project did not build; the errors are not in the drafts.",
                Problems = build.Problems.Where(p => failing.Count == 0 || SameFile(p.File, full)).ToArray(),
            };
        }
        var after = await CodeGate.BuildAsync(project, templates, cancellationToken).ConfigureAwait(false);
        return new DraftResult(outcomes, after);
    }

    private static string Free(string name, HashSet<string> names)
    {
        var free = name;
        for (var n = 2; names.Contains(free); n++)
            free = name[..^"Page".Length] + n + "Page";
        return free;
    }

    // The line endings on disk are the writer's; the text is what counts.
    private static bool Same(string a, string b) =>
        a.Replace("\r\n", "\n", StringComparison.Ordinal) == b.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static bool SameFile(string? a, string b) =>
        a is not null && string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}

/// <summary>What happened to a page's draft.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DraftOutcome>))]
public enum DraftOutcome
{
    /// <summary>Written, and the project builds with it.</summary>
    Written,

    /// <summary>The draft is what the file holds already.</summary>
    Unchanged,

    /// <summary>Not kept: the file is as it was before, and the problems say why.</summary>
    Reverted,

    /// <summary>Not drafted: the page has a page object made by hand.</summary>
    Skipped,
}

/// <summary>A page's draft: its key, class, file, what happened, how many members, those left out, those gone since the last draft, and why.</summary>
public sealed record DraftedPage(string Key, string ClassName, string Path, DraftOutcome Outcome, int Members, IReadOnlyList<string> Unstable, IReadOnlyList<RemovedMember> Removed, string? Reason)
{
    /// <summary>The compiler's or the syntax check's problems, for a reverted draft.</summary>
    public IReadOnlyList<CodeProblem> Problems { get; init; } = [];
}

/// <summary>A member the new draft no longer has, and the recording the last draft was made from.</summary>
public sealed record RemovedMember(string Name, string? LastSeenIn);

/// <summary>A draft run: each page, and the build after it (null when nothing was written).</summary>
public sealed record DraftResult(IReadOnlyList<DraftedPage> Pages, BuildResult? Build)
{
    /// <summary>The patterns that repeat across pages and have no control yet: for the agent or a person to make controls of.</summary>
    public IReadOnlyList<Pattern> MakeControls { get; init; } = [];
}
