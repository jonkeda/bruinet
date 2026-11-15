namespace Bruinet.Uat;

// The page each app is on as binding walks a scenario (AD-019 D5, D7, D8).
internal sealed class UatPageTracker(BddCatalogSet catalogs, HashSet<string>? used) : IStepScope
{
    private const string PageParameter = "page";

    private readonly Dictionary<string, PageState> _pages = new(StringComparer.OrdinalIgnoreCase);

    // A control verb or page phrase only on the current page, never one of those together with a
    // phrase of wider scope. A catalog without pages binds without page scope.
    public IReadOnlyList<BddCommandMatch> Visible(BddCommandCatalog catalog, BddStep step, BddAppConfig? app, IReadOnlyList<BddCommandMatch> candidates, List<BddDiagnostic> problems)
    {
        if (UatPageSet.Of(catalog).Count == 0)
        {
            return BddCommandCatalog.Select(candidates);
        }

        var wide = candidates.Where(candidate => !candidate.Pattern.IsPageScoped).ToList();
        var scoped = candidates.Where(candidate => candidate.Pattern.IsPageScoped).ToList();
        if (scoped.Count == 0)
        {
            return BddCommandCatalog.Select(wide);
        }

        var state = PageOf(app);
        if (state.Page is null || !UatPageSet.Of(catalog).TryGetValue(state.Page, out var page))
        {
            if (wide.Count > 0)
            {
                return BddCommandCatalog.Select(wide);
            }

            problems.Add(BddBinder.Error(
                "BDDB008",
                $"Step '{step.Text}' needs to know the page, but no step has said it yet. Start with 'Given I am on the ... page'.",
                step.Source));
            return [];
        }

        List<string> reasons = [];
        var fitting = scoped.Where(candidate => Fits(page, candidate, reasons)).ToList();
        if (fitting.Count == 0 && wide.Count == 0 && state.LostAt is { } lost)
        {
            problems.Add(BddBinder.Error(
                "BDDB008",
                $"Step '{step.Text}' does not fit the {page.Name} page, and the page may have changed at '{lost.Text}' ({lost.Source}). Say where that step ends: 'Then I should be on the ... page'.",
                step.Source));
            return [];
        }

        if (fitting.Count > 0 && wide.Count > 0)
        {
            problems.Add(BddBinder.Error(
                "BDDB002",
                $"Step '{step.Text}' is ambiguous: on the {page.Name} page it matches {string.Join(", ", fitting.Select(BddBinder.Describe))}, and also {string.Join(", ", wide.Select(BddBinder.Describe))}. Rename one of them.",
                step.Source));
            return [];
        }

        if (fitting.Count == 0 && wide.Count == 0)
        {
            problems.Add(BddBinder.Error("BDDB009", $"Step '{step.Text}' does not fit the {page.Name} page: {string.Join(" ", reasons.Distinct())}", step.Source));
            return [];
        }

        return BddCommandCatalog.Select(fitting.Count > 0 ? fitting : wide);
    }

    // The page after the step (D7): the page a step names; a page phrase's or control verb's
    // returned page. After a verb that can leave the page and does not say where, the page is
    // only a guess until a step fits it. Other phrases keep it.
    public bool Bound(BddStep step, BddCommandMatch match, ref IReadOnlyDictionary<string, string> arguments, BddAppConfig? app, BddScope where, List<BddDiagnostic> diagnostics)
    {
        var catalog = catalogs.CatalogFor(app);
        if (UatPageSet.Of(catalog).Count == 0)
        {
            return true;
        }

        var key = app?.Name ?? string.Empty;
        var pattern = match.Pattern;
        if (pattern.NamesPage && arguments.TryGetValue(PageParameter, out var named))
        {
            if (ResolvePage(catalog, step, named, where, diagnostics) is not { } namedPage)
            {
                return false;
            }

            // The step runs on the page it resolved to: its key, when two namespaces share the name.
            if (!string.Equals(named, namedPage.Key, StringComparison.Ordinal))
            {
                arguments = new Dictionary<string, string>(arguments, StringComparer.Ordinal) { [PageParameter] = namedPage.Key };
            }

            _pages[key] = new PageState(namedPage.Key, null);
            return true;
        }

        var state = PageOf(app);
        if (state.Page is null || !UatPageSet.Of(catalog).TryGetValue(state.Page, out var page))
        {
            return true;
        }

        if (pattern.Page is not null)
        {
            _pages[key] = new PageState(pattern.PageAfter ?? page.Key, null);
            return true;
        }

        if (pattern.ControlVerb is { } verb && UatPageScope.ControlFor(page, match, out _) is { } control)
        {
            var after = UatPageScope.PageAfter(page, control, verb, UatPageSet.Of(catalog));
            _pages[key] = new PageState(after ?? page.Key, after is null ? step : null);
        }

        return true;
    }

    public string? After(BddCommandPattern pattern, BddAppConfig? app)
        => pattern.IsPageScoped && PageOf(app) is { LostAt: null, Page: { } known } ? known : null;

    private static bool Fits(UatPageModel page, BddCommandMatch candidate, List<string> reasons)
    {
        if (candidate.Pattern.Page is { } owner)
        {
            if (string.Equals(owner, page.Key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            reasons.Add($"'{candidate.Pattern.Phrase}' is a phrase of the {owner} page.");
            return false;
        }

        if (UatPageScope.ControlFor(page, candidate, out var problem) is not null)
        {
            return true;
        }

        reasons.Add(char.ToUpperInvariant(problem![0]) + problem[1..]);
        return false;
    }

    private PageState PageOf(BddAppConfig? app)
        => _pages.TryGetValue(app?.Name ?? string.Empty, out var state) ? state : default;

    // A page named in a step (D8): by name among the pages the step sees, or by its key. One name
    // in two seen namespaces is ambiguous, as a phrase is.
    private UatPageModel? ResolvePage(BddCommandCatalog catalog, BddStep step, string named, BddScope where, List<BddDiagnostic> diagnostics)
    {
        var all = UatPageSet.Of(catalog).Values
            .Where(page => page.Key.Equals(named, StringComparison.OrdinalIgnoreCase) || page.Name.Equals(named, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var seen = all.Where(page => where.Sees(page.Namespace)).ToList();
        if (seen.Count == 1)
        {
            used?.UnionWith(where.UsingsFor(seen[0].Namespace));
            return seen[0];
        }

        diagnostics.Add(seen.Count > 1
            ? BddBinder.Error("BDDB002", $"Step '{step.Text}' names the page '{named}', which here is {string.Join(" and ", seen.Select(page => page.Key))}. Name it with its namespace: '{seen[0].Key}'.", step.Source)
            : all.Count > 0
                ? BddBinder.Error("BDDB010", $"Step '{step.Text}' names the page '{named}' of {string.Join(", ", all.Select(page => BddNamespaces.Describe(page.Namespace)))}, which {BddNamespaces.Describe(where.Namespace)} does not see. Add 'Using' for it, or move the file there.", step.Source)
                : BddBinder.Error("BDDB009", $"Step '{step.Text}' names the page '{named}', which does not exist. The pages are {string.Join(", ", UatPageSet.Of(catalog).Values.Where(page => where.Sees(page.Namespace)).Select(page => page.Key).Order(StringComparer.OrdinalIgnoreCase))}.", step.Source));
        return null;
    }

    // The page an app is on; null before any step has said it. With LostAt, only a guess: the
    // step there may have left it.
    private readonly record struct PageState(string? Page, BddStep? LostAt);
}
