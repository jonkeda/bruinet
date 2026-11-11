using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Bruinet.Bdd;

[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public static class BddBinder
{
    private const string UserParameter = "user";
    private const string PageParameter = "page";

    // A callable that calls a callable that calls a callable is already hard to follow in a
    // failure report; a fourth level is refused.
    private const int MaxCallDepth = 3;

    public static BddBindResult Bind(BddDocument document, BddCommandCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Bind(document, new BddCatalogSet(catalog));
    }

    /// <summary>
    /// Binds every step against the catalog of the app it runs on. The current app follows the
    /// steps: a step that starts with an app or user (<c>Daan taps Save</c>) makes that app
    /// current, and so does <c>{app} uses the app</c>. See AD-015.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="catalogs">The catalogs and the apps.</param>
    public static BddBindResult Bind(BddDocument document, BddCatalogSet catalogs)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Bind(document, catalogs, new BddCallableSet(document.Callables));
    }

    /// <summary>
    /// Binds every step, and expands every call to a callable phrase into its steps, bound where
    /// the call is. A problem inside a call is reported at the call.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="catalogs">The catalogs and the apps.</param>
    /// <param name="callables">The callable phrases the document can see (<see cref="BddScenarioFiles.Load(string)"/>).</param>
    /// <param name="only">The one scenario to bind, by name, for a run of it alone; null binds them all, as a check does.</param>
    public static BddBindResult Bind(BddDocument document, BddCatalogSet catalogs, BddCallableSet callables, string? only = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(catalogs);
        ArgumentNullException.ThrowIfNull(callables);

        List<BddDiagnostic> diagnostics;
        using (Bruinet.Core.Logging.Timing.Span("bind-part", "CheckNames"))
        {
            diagnostics = [.. catalogs.CheckNames(document.Source)];
        }

        List<BddBoundScenario> scenarios = [];
        BddPreparedCallables prepared;
        using (Bruinet.Core.Logging.Timing.Span("bind-part", "PrepareCallables"))
        {
            prepared = callables.Prepare(catalogs);
        }

        BddScope scope;
        using (Bruinet.Core.Logging.Timing.Span("bind-part", "Scope"))
        {
            scope = BddScope.Of(document, catalogs);
        }

        HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
        var hooks = catalogs.All
            .SelectMany(catalog => catalog.Hooks)
            .DistinctBy(hook => (hook.Kind, hook.Name))
            .OrderBy(hook => hook.Name, StringComparer.Ordinal)
            .ToList();

        foreach (var scenario in document.Scenarios)
        {
            // A run of one scenario binds only that one: each scenario's walk costs, and the others do not run (speed plan,
            // iteration 1). Their problems are a check's to report, which binds them all.
            if (only is not null && !string.Equals(scenario.Name, only, StringComparison.Ordinal))
            {
                scenarios.Add(new BddBoundScenario(scenario, []));
                continue;
            }

            // A scenario for another head is skipped when it runs, so its steps are not this head's to bind (AD-014).
            if (catalogs.SuiteHead is { } suiteHead &&
                BddTagConventions.WhyNotOnHead(scenario.Tags.Select(BddTagConventions.NormalizeTag).ToHashSet(StringComparer.OrdinalIgnoreCase), suiteHead) is not null)
            {
                scenarios.Add(new BddBoundScenario(scenario, []));
                continue;
            }

            List<BddStepInvocation> invocations = [];
            using var walking = Bruinet.Core.Logging.Timing.Span("bind-part", "Walk");
            var walk = new Walk(catalogs, prepared, scenario.Name, invocations, scope, used, inference: null);

            foreach (var step in document.Background)
            {
                walk.Step(step, fromBackground: true, frame: null, diagnostics);
            }

            foreach (var step in scenario.Steps)
            {
                walk.Step(step, fromBackground: false, frame: null, diagnostics);
            }

            scenarios.Add(new BddBoundScenario(scenario, invocations) { Hooks = hooks });
        }

        using (Bruinet.Core.Logging.Timing.Span("bind-part", "CheckUsings"))
        {
            diagnostics.AddRange(CheckUsings(document, catalogs, callables, used));
        }

        var boundDocument = diagnostics.Any(x => x.Severity == BddDiagnosticSeverity.Error)
            ? null
            : new BddBoundDocument(document, scenarios);

        return new BddBindResult(boundDocument, diagnostics) { Scenarios = scenarios };
    }

    // Binds a callable's body with its placeholders in, against the suite's own catalog, to learn
    // the types of its values. Only a problem that no call could avoid makes it broken.
    internal static void Infer(BddCallable callable, BddCatalogSet catalogs, BddPreparedCallables callables, BddInference inference)
    {
        var walk = new Walk(catalogs, callables, callable.Phrase, [], BddScope.Of(callable, catalogs), used: null, inference);
        List<BddDiagnostic> ignored = [];
        foreach (var step in callable.Steps)
        {
            walk.Step(step, fromBackground: false, frame: null, ignored);
        }
    }

    // A file's Using names what something provides (else it is a typo that hides phrases), and is
    // needed by a step (else it only widens what could collide). The config's usings are the suite's
    // choice, checked once by the tool rather than in every file. The generic catalog knows no
    // namespaces.
    private static IEnumerable<BddDiagnostic> CheckUsings(BddDocument document, BddCatalogSet catalogs, BddCallableSet callables, IReadOnlySet<string> used)
    {
        var usings = BddNamespaces.ParseList(document.Metadata.GetValueOrDefault(BddMetadataFields.Using));
        if (usings.Count == 0 || catalogs.All.All(catalog => catalog.IsGeneric))
        {
            yield break;
        }

        var provided = catalogs.ProvidedNamespaces(callables).ToList();
        foreach (var name in usings)
        {
            if (!provided.Any(owner => BddNamespaces.IsVisible(owner, name)))
            {
                yield return Error(
                    "UATB011",
                    $"Using '{name}' names no namespace. The namespaces are {(provided.Count == 0 ? "(none)" : string.Join(", ", provided.Order(StringComparer.OrdinalIgnoreCase)))}.",
                    document.Source);
            }
            else if (!used.Contains(name))
            {
                yield return new BddDiagnostic(
                    BddDiagnosticSeverity.Warning,
                    "UATB012",
                    $"Using '{name}' is not needed: no step binds to a phrase or page it adds. Remove it.",
                    document.Source);
            }
        }
    }

    // {user} is the acting user: "I" is the current app's user, and a name is checked against the
    // Apps table. In first place it also makes that user's app current, as a subject does.
    private static bool TryBindUser(
        BddCatalogSet catalogs,
        AppTracker apps,
        BddCommandMatch match,
        string user,
        ref BddAppConfig? app,
        out string boundUser,
        out string error)
    {
        boundUser = user;
        if (user == "I")
        {
            if (app is null)
            {
                error = $"it says 'I' for {{{UserParameter}}}, but no app is current yet, so there is no user. Start with a step that names one: {catalogs.DescribeApps()}.";
                return false;
            }

            boundUser = app.User;
            error = string.Empty;
            return true;
        }

        if (!apps.TryResolve(user, out var userApp, out error))
        {
            return false;
        }

        boundUser = userApp.User;
        if (FirstParameterName(match.Pattern) == UserParameter)
        {
            app = userApp;
        }

        return true;
    }

    // " The nearest are 'a', 'b'.": up to three phrases of the step's keyword, closest by edit distance over words, and only
    // ones near enough to be a typo or a forgotten word (review 01, finding 4). Empty when none is.
    private static string Nearest(BddCommandCatalog catalog, BddStep step)
    {
        var text = Words(step.Text);
        var nearest = catalog.Patterns
            .Where(pattern => pattern.Keyword == step.EffectiveKeyword)
            .Select(pattern => pattern.Phrase)
            .Distinct(StringComparer.Ordinal)
            .Select(phrase => (Phrase: phrase, Distance: Distance(text, Words(phrase))))
            .Where(candidate => candidate.Distance <= Math.Max(2, text.Length / 2))
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Phrase, StringComparer.Ordinal)
            .Take(3)
            .Select(candidate => $"'{candidate.Phrase}'")
            .ToList();
        return nearest.Count == 0 ? string.Empty : $" The nearest are {string.Join(", ", nearest)}.";
    }

    // A phrase's words, its {parameters} and a step's quoted values read alike as one placeholder word.
    private static string[] Words(string text)
        => System.Text.RegularExpressions.Regex.Replace(text, @"\{[^}]*\}|""[^""]*""|'[^']*'", "#")
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static int Distance(string[] a, string[] b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var previous = row[0];
            row[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var kept = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), previous + (a[i - 1] == b[j - 1] ? 0 : 1));
                previous = kept;
            }
        }

        return row[b.Length];
    }

    private static IReadOnlyList<string> NamedPhrases(BddCommandCatalog catalog, BddEffectiveStepKeyword keyword)
        => catalog.Patterns
            .Where(pattern => pattern.Keyword == keyword && FirstParameterName(pattern) == UserParameter)
            .Select(pattern => pattern.Phrase)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string? FirstArgument(BddCommandMatch match)
        => FirstParameterName(match.Pattern) is { } name && match.Arguments.TryGetValue(name, out var value) ? value : null;

    private static string? FirstParameterName(BddCommandPattern pattern)
        => Regex.Match(pattern.Phrase, @"^\{([A-Za-z][A-Za-z0-9_-]*)\}") is { Success: true } found ? found.Groups[1].Value : null;

    // The engine's page steps, control verbs and page phrases.
    private static bool IsScreenStep(BddCommandPattern pattern)
        => pattern.NamesPage || pattern.IsPageScoped || pattern.CommandId.StartsWith("Builtin.Page.", StringComparison.Ordinal);

    internal static BddDiagnostic Error(string code, string message, BddSourceLocation location)
        => new(BddDiagnosticSeverity.Error, code, message, location);

    internal static string Describe(BddCommandMatch match)
        => match.Pattern.Callable is { } callable ? $"'{callable.Phrase}' ({callable.Source})" : $"'{match.Pattern.Phrase}'";

    // One scenario's steps in order, with the current app. Inference walks a callable's body
    // instead: it learns types and drops what only a call can decide. The scope is where the
    // scenario is written; a step a call put there sees what its callable's own scope sees (D9).
    private sealed class Walk(
        BddCatalogSet catalogs,
        BddPreparedCallables callables,
        string scenarioName,
        List<BddStepInvocation> invocations,
        BddScope scope,
        HashSet<string>? used,
        BddInference? inference)
    {
        private readonly AppTracker _apps = new(catalogs);

        // The head's scope (Bruinet.Uat's pages); none for a callable's body read on its own.
        private readonly IStepScope? _scope = inference is null
            ? catalogs.All.Select(catalog => catalog.Scope).FirstOrDefault(model => model is not null)?.StartWalk(catalogs, used)
            : null;

        [SuppressMessage("Maintainability", "CA1502", Justification = "Known debt (AD-033): split when it next changes.")]
        public void Step(BddStep step, bool fromBackground, BddCallFrame? frame, List<BddDiagnostic> diagnostics)
        {
            var app = _apps.Current;
            BddCommandMatch? match = null;
            IReadOnlyList<string> named = [];
            List<BddDiagnostic> pageProblems = [];
            var where = frame is null ? scope : BddScope.Of(frame.Callable, catalogs);

            if (catalogs.TryFindSubject(step.Text, out var subject, out _))
            {
                if (!_apps.TryResolve(subject, out var subjectApp, out var error))
                {
                    diagnostics.Add(Error("UATB006", error, step.Source));
                    return;
                }

                // Only a phrase declared with the subject in its first parameter binds it: {user} taps
                // {control}, {app} uses the app. The engine infers no verb forms (AD-015).
                var catalog = catalogs.CatalogFor(subjectApp);
                var declared = Match(catalog, step, subjectApp, where, pageProblems)
                    .Where(candidate => FirstArgument(candidate) == subject)
                    .ToArray();
                if (declared.Length > 1)
                {
                    diagnostics.Add(Error(
                        "UATB002",
                        $"Step '{step.Text}' is ambiguous: as {subject}, it matches {string.Join(", ", declared.Select(candidate => $"'{candidate.Pattern.Phrase}'"))}.",
                        step.Source));
                    return;
                }

                if (declared.Length == 1)
                {
                    match = declared[0];
                    app = subjectApp;
                }
                else if (pageProblems.Count > 0)
                {
                    diagnostics.AddRange(pageProblems);
                    return;
                }
                else
                {
                    named = NamedPhrases(catalog, step.EffectiveKeyword);
                }
            }

            if (match is null)
            {
                pageProblems.Clear();
                var matches = Match(catalogs.CatalogFor(app), step, app, where, pageProblems);
                if (matches.Count == 0 && pageProblems.Count > 0)
                {
                    diagnostics.AddRange(pageProblems);
                    return;
                }

                if (matches.Count == 0)
                {
                    diagnostics.Add(Error(
                        "UATB001",
                        subject.Length == 0
                            ? $"No command binding matches step '{step.Text}'." + Nearest(catalogs.CatalogFor(app), step)
                            : $"No command binding matches step '{step.Text}'. A step that names {subject} binds only to a phrase declared with {{{UserParameter}}} first; "
                              + (named.Count == 0
                                  ? $"no {step.EffectiveKeyword} phrase is."
                                  : $"the {step.EffectiveKeyword} ones are {string.Join(", ", named.Select(phrase => $"'{phrase}'"))}."),
                        step.Source));
                    return;
                }

                if (matches.Count > 1)
                {
                    diagnostics.Add(Error(
                        "UATB002",
                        $"Step '{step.Text}' is ambiguous and matches {matches.Count} command bindings: {string.Join(", ", matches.Select(Describe))}.",
                        step.Source));
                    return;
                }

                match = matches[0];
            }

            // The service head has no screen: a step that opens, drives or reads one cannot run there.
            if (BddTagConventions.IsService(app?.Head ?? catalogs.SuiteHead) && IsScreenStep(match.Pattern))
            {
                diagnostics.Add(Error(
                    "UATB013",
                    $"'{step.Text}' is a UI step ('{match.Pattern.Phrase}'); the service head has no screen. Use a step the service head declares, or tag the scenario for a UI head.",
                    step.Source));
                return;
            }

            used?.UnionWith(where.UsingsFor(BddScope.OwnerOf(match.Pattern)));

            // A body step that passes the call's table has none yet: only a call can check it.
            if (match.Pattern.Callable is null && !(inference is not null && step.Passes != BddPassedBlock.None))
            {
                if (match.Pattern.RequiresTable && step.Table is null)
                {
                    diagnostics.Add(Error("UATB003", $"Step '{step.Text}' requires a table.", step.Source));
                    return;
                }

                if (!match.Pattern.AllowsTable && step.Table is not null)
                {
                    diagnostics.Add(Error("UATB004", $"Step '{step.Text}' does not accept a table.", step.Table.Source));
                    return;
                }
            }

            var arguments = match.Arguments;
            var rawArguments = match.RawArguments;
            if (arguments.TryGetValue(UserParameter, out var user) && inference is not null && BddOutline.WholePlaceholder(user) is { } userPlaceholder)
            {
                inference.Record(userPlaceholder, typeof(BddUserRole));
            }
            else if (catalogs.Apps.Count == 0 && user is not null && user != "I")
            {
                diagnostics.Add(Error(
                    "UATB007",
                    $"Step '{step.Text}' names the user '{user}', but the config has no Apps table to say whose app that is.",
                    step.Source));
                return;
            }
            else if (catalogs.Apps.Count > 0 && user is not null)
            {
                if (!TryBindUser(catalogs, _apps, match, user, ref app, out var boundUser, out var error))
                {
                    diagnostics.Add(Error("UATB007", $"Step '{step.Text}': {error}", step.Source));
                    return;
                }

                arguments = new Dictionary<string, string>(arguments, StringComparer.Ordinal) { [UserParameter] = boundUser };
                rawArguments = new Dictionary<string, string>(rawArguments, StringComparer.Ordinal) { [UserParameter] = boundUser };
            }

            if (match.Pattern.Callable is { } callable)
            {
                if (inference is not null)
                {
                    InferThroughCall(step, callable, rawArguments);
                }
                else
                {
                    Call(step, callables.Get(callable), rawArguments, app, fromBackground, frame, diagnostics);
                }

                return;
            }

            // A broken phrase and a value that does not fit fail here, before any app starts (AD-016).
            var argumentProblems = BddArguments.Check(
                match.Pattern,
                step,
                rawArguments,
                new BddValueContext(DateTimeOffset.UtcNow, catalogs.Apps, catalogs.CatalogFor(app).ArgumentTypes)
                {
                    Placeholders = inference is null ? null : inference.Record,
                });
            if (inference is not null)
            {
                // What the body writes itself cannot be fixed by a call: the callable is broken.
                var avoidable = step.Passes == BddPassedBlock.None ? [] : new[] { "UATA007" };
                if (argumentProblems.FirstOrDefault(problem => !avoidable.Contains(problem.Code)) is { } problem)
                {
                    inference.Fail("UATC008", $"has a step that cannot run: {problem.Code} {problem.Message}");
                }

                if (step.Passes == BddPassedBlock.Table)
                {
                    inference.BlockTypes.Add(match.Pattern.Parameters.FirstOrDefault(p => p.Source == BddParameterSource.Table)?.Type ?? typeof(BddTable));
                }

                _apps.Use(app);
                return;
            }

            if (argumentProblems.Count > 0)
            {
                diagnostics.AddRange(argumentProblems);
                return;
            }

            if (_scope is not null && !_scope.Bound(step, match, ref arguments, app, where, diagnostics))
            {
                return;
            }

            _apps.Use(app);
            var pageAfter = _scope?.After(match.Pattern, app);
            invocations.Add(new BddStepInvocation(
                scenarioName,
                step,
                match.Pattern,
                arguments,
                step.Table,
                fromBackground)
            {
                App = app?.Name,
                RawArguments = rawArguments,
                Call = frame,
                PageAfter = pageAfter,
            });
        }

        // The phrases a step can bind to where it stands (D5): those of its folder and the folders
        // above it, then what the head's scope lets through at this point of the walk.
        private IReadOnlyList<BddCommandMatch> Match(BddCommandCatalog catalog, BddStep step, BddAppConfig? app, BddScope where, List<BddDiagnostic> problems)
        {
            IReadOnlyList<BddCommandMatch> all = [.. catalog.Candidates(step), .. callables.Candidates(step)];
            var candidates = all.Where(candidate => where.Sees(candidate.Pattern)).ToList();
            if (candidates.Count == 0 && all.Count > 0)
            {
                problems.Add(Error("UATB010", NotSeen(step, all, where), step.Source));
                return [];
            }

            return _scope is null
                ? BddCommandCatalog.Select(candidates)
                : _scope.Visible(catalog, step, app, candidates, problems);
        }

        private static string NotSeen(BddStep step, IReadOnlyList<BddCommandMatch> hidden, BddScope where)
        {
            var owners = hidden
                .Select(match => match.Pattern.Callable is { } callable
                    ? callable.LocalTo is { } local
                        ? $"'{callable.Phrase}', written in {Path.GetFileName(local)} for that file only"
                        : $"'{callable.Phrase}' of {BddNamespaces.Describe(callable.Namespace)} ({callable.Source})"
                    : $"'{match.Pattern.Phrase}' of {BddNamespaces.Describe(match.Pattern.Namespace)}")
                .Distinct(StringComparer.Ordinal);
            return $"Step '{step.Text}' matches {string.Join(", ", owners)}, which {BddNamespaces.Describe(where.Namespace)} does not see. "
                + "A scenario sees the phrases of its own folder, the folders above it, and the namespaces it uses: add a 'Using' row for it, move the file there, or move the phrase up.";
        }

        // A body step that calls another callable: its placeholders take that callable's types.
        private void InferThroughCall(BddStep step, BddCallable callable, IReadOnlyDictionary<string, string> rawArguments)
        {
            var called = callables.Get(callable);
            if (called.Problem is { } problem)
            {
                inference!.Fail(problem.Code == "UATC004" ? "UATC004" : "UATC008", $"calls '{callable.Phrase}' ({callable.Source}), which {problem.Message.TrimEnd('.')}.");
                return;
            }

            foreach (var (name, value) in rawArguments)
            {
                if (BddOutline.WholePlaceholder(value) is { } placeholder && called.Types.TryGetValue(name, out var type))
                {
                    inference!.Record(placeholder, type);
                }
            }

            if (step.Passes == BddPassedBlock.Table && called.Passes == BddPassedBlock.Table && called.BlockType is { } blockType)
            {
                inference!.BlockTypes.Add(blockType);
            }

            if (callable.Kind == BddCallableKind.EachRow && step.Table is { } table)
            {
                foreach (var column in table.Columns)
                {
                    var parameter = called.Columns.FirstOrDefault(name => BddTableBinding.Key(name) == BddTableBinding.Key(column));
                    foreach (var row in table.Rows)
                    {
                        if (parameter is not null && BddOutline.WholePlaceholder(row.Cells[column]) is { } placeholder)
                        {
                            inference!.Record(placeholder, called.Types[parameter]);
                        }
                    }
                }
            }
        }

        private void Call(
            BddStep step,
            BddPreparedCallable prepared,
            IReadOnlyDictionary<string, string> rawArguments,
            BddAppConfig? app,
            bool fromBackground,
            BddCallFrame? frame,
            List<BddDiagnostic> diagnostics)
        {
            var callable = prepared.Callable;
            if (prepared.Problem is { } broken)
            {
                diagnostics.Add(Error(broken.Code, $"Step '{step.Text}': the phrase '{callable.Phrase}' from {callable.Source} {broken.Message}", step.Source));
                return;
            }

            if (frame?.Chain.FirstOrDefault(outer => outer.Callable == callable) is not null)
            {
                var chain = frame.Chain.Select(outer => $"'{outer.Callable.Phrase}'").Append($"'{callable.Phrase}'");
                diagnostics.Add(Error("UATC004", $"Step '{step.Text}' calls a phrase that is already running: {string.Join(" -> ", chain)}.", step.Source));
                return;
            }

            if ((frame?.Depth ?? 0) >= MaxCallDepth)
            {
                diagnostics.Add(Error("UATC005", $"Step '{step.Text}' calls '{callable.Phrase}' at depth {frame!.Depth + 1}; callable phrases nest at most {MaxCallDepth} deep. Call it from the scenario, or make one of them a C# phrase.", step.Source));
                return;
            }

            if (!CheckBlocks(step, prepared, diagnostics) || !CheckValues(step, prepared, rawArguments, app, diagnostics, out var rows))
            {
                return;
            }

            _apps.Use(app);
            foreach (var (row, rowValues) in rows)
            {
                var values = new Dictionary<string, string>(rawArguments, StringComparer.Ordinal);
                foreach (var (name, value) in rowValues)
                {
                    values[name] = value;
                }

                var callFrame = new BddCallFrame(callable, step, row, values, frame);
                var first = invocations.Count;
                List<BddDiagnostic> inner = [];
                foreach (var body in callable.Steps)
                {
                    var expanded = BddOutline.Substitute(body, values, quoteAware: true) with { Passes = BddPassedBlock.None };
                    expanded = body.Passes switch
                    {
                        BddPassedBlock.Table => expanded with { Table = step.Table },
                        BddPassedBlock.DocString => expanded with { DocString = step.DocString },
                        _ => expanded,
                    };
                    Step(expanded, fromBackground, callFrame, inner);
                }

                diagnostics.AddRange(inner.Select(diagnostic => Relocate(diagnostic, step, callFrame)));
                if (inner.Count == 0 && PageProblem(callable, invocations.Skip(first).ToList()) is { } pageProblem)
                {
                    diagnostics.Add(Error("UATC006", $"Step '{step.Text}': the phrase '{callable.Phrase}' from {callable.Source} {pageProblem}", callFrame.Location));
                }
            }
        }

        private static bool CheckBlocks(BddStep step, BddPreparedCallable prepared, List<BddDiagnostic> diagnostics)
        {
            var phrase = prepared.Callable.Phrase;
            string? problem = prepared.Callable.Kind == BddCallableKind.EachRow
                ? step.Table is null || step.Table.Rows.Count == 0 ? "UATA007" : step.DocString is not null ? "UATB004" : null
                : prepared.Passes switch
                {
                    BddPassedBlock.Table when step.Table is null => "UATA007",
                    BddPassedBlock.DocString when step.DocString is null => "UATA007",
                    not BddPassedBlock.Table when step.Table is not null => "UATB004",
                    not BddPassedBlock.DocString when step.DocString is not null => "UATB004",
                    _ => null,
                };

            if (problem is null)
            {
                return true;
            }

            var message = (problem, prepared.Callable.Kind, prepared.Passes) switch
            {
                ("UATA007", BddCallableKind.EachRow, _) => $"Step '{step.Text}' needs a table under it: '{phrase}' runs once per row, with the columns {string.Join(", ", prepared.Columns)}.",
                ("UATA007", _, BddPassedBlock.Table) => $"Step '{step.Text}' needs a table under it, which '{phrase}' passes on.",
                ("UATA007", _, _) => $"Step '{step.Text}' needs a doc string (\"\"\") under it, which '{phrase}' passes on.",
                _ => $"Step '{step.Text}' does not accept a {(step.Table is not null ? "table" : "doc string")}: '{phrase}' does not pass one on.",
            };
            diagnostics.Add(Error(problem, message, problem == "UATB004" && step.Table is not null ? step.Table.Source : step.Source));
            return false;
        }

        // Every value of the call is read as its type now, so a wrong one is reported once, at
        // the call's line or its row, and nothing is expanded.
        private bool CheckValues(
            BddStep step,
            BddPreparedCallable prepared,
            IReadOnlyDictionary<string, string> rawArguments,
            BddAppConfig? app,
            List<BddDiagnostic> diagnostics,
            out IReadOnlyList<(int? Row, IReadOnlyDictionary<string, string> Values)> rows)
        {
            var callable = prepared.Callable;
            var context = new BddValueContext(DateTimeOffset.UtcNow, catalogs.Apps, catalogs.CatalogFor(app).ArgumentTypes);
            var count = diagnostics.Count;
            void Check(string name, string text, BddSourceLocation location)
            {
                var type = prepared.Types[name];
                if (!BddValues.TryRead(text, type, context, out _, out var problem, allowNull: true))
                {
                    diagnostics.Add(Error(problem!.Code, $"Step '{step.Text}': '{name}' of '{callable.Phrase}' is a {BddValues.Describe(type)}; {problem.Message}", location) with
                    {
                        Fix = problem is { Fix: { } fix, Original: { } original } ? new BddTextFix(original, fix) : null,
                    });
                }
            }

            foreach (var name in callable.HeadingParameters)
            {
                if (rawArguments.TryGetValue(name, out var text))
                {
                    Check(name, text, step.Source);
                }
            }

            rows = [(null, new Dictionary<string, string>())];
            if (callable.Kind == BddCallableKind.EachRow && step.Table is { } table)
            {
                Dictionary<string, string> byColumn = new(StringComparer.Ordinal);
                foreach (var column in table.Columns)
                {
                    if (prepared.Columns.FirstOrDefault(name => BddTableBinding.Key(name) == BddTableBinding.Key(column)) is { } name)
                    {
                        byColumn[column] = name;
                    }
                    else
                    {
                        diagnostics.Add(Error("UATA006", $"Step '{step.Text}': '{column}' is not a column of '{callable.Phrase}'. Its columns are {string.Join(", ", prepared.Columns)}.", table.Source));
                    }
                }

                foreach (var name in prepared.Columns.Where(name => !byColumn.ContainsValue(name)))
                {
                    diagnostics.Add(Error("UATA006", $"Step '{step.Text}': the table needs a column '{name}' for '{callable.Phrase}'.", table.Source));
                }

                if (diagnostics.Count > count)
                {
                    return false;
                }

                List<(int?, IReadOnlyDictionary<string, string>)> perRow = [];
                for (var i = 0; i < table.Rows.Count; i++)
                {
                    var row = table.Rows[i];
                    Dictionary<string, string> values = new(StringComparer.Ordinal);
                    foreach (var (column, name) in byColumn)
                    {
                        Check(name, row.Cells[column], row.Source);
                        values[name] = row.Cells[column];
                    }

                    perRow.Add((i + 1, values));
                }

                rows = perRow;
            }

            if (prepared.Passes == BddPassedBlock.Table && step.Table is { } passed && prepared.BlockType is { } blockType && blockType != typeof(BddTable))
            {
                diagnostics.AddRange(BddTableBinding.Check(passed, blockType, context).Select(found =>
                    Error(found.Problem.Code, $"Step '{step.Text}': the table '{callable.Phrase}' passes on: {found.Problem.Message}", found.Location) with
                    {
                        Fix = found.Problem is { Fix: { } fix, Original: { } original } ? new BddTextFix(original, fix) : null,
                    }));
            }

            return diagnostics.Count == count;
        }

        // A problem inside the call is reported at the call (or its row), and the steps it came
        // through are kept. A cell of the table the call passed on already has the call's line.
        // Only what the steps say is checked: a callable that names a page ends on one, and a row
        // ends on the page it started on.
        private static string? PageProblem(BddCallable callable, IReadOnlyList<BddStepInvocation> bound)
        {
            var pages = bound.Where(invocation => invocation.Command.NamesPage).ToList();
            if (pages.Count == 0 || bound.Count == 0)
            {
                return null;
            }

            if (!bound[^1].Command.NamesPage)
            {
                return $"names the {Page(pages[^1])} page but does not end on one. End it with a step that says where it ends: 'Then I should be on the ... page'.";
            }

            return callable.Kind == BddCallableKind.EachRow && !string.Equals(Page(pages[0]), Page(bound[^1]), StringComparison.OrdinalIgnoreCase)
                ? $"runs once per row, so each row ends on the page it started on; this one starts on the {Page(pages[0])} page and ends on the {Page(bound[^1])} page."
                : null;
        }

        private static string Page(BddStepInvocation invocation)
            => invocation.Arguments.TryGetValue(PageParameter, out var page) ? page : "?";

        private static BddDiagnostic Relocate(BddDiagnostic diagnostic, BddStep call, BddCallFrame frame)
        {
            if (call.Table is { } table && (diagnostic.Location == table.Source || table.Rows.Any(row => row.Source == diagnostic.Location)))
            {
                return diagnostic;
            }

            return diagnostic with
            {
                Location = frame.Location,
                Within = [diagnostic.Location, .. diagnostic.Within],
                Message = $"{diagnostic.Message} (in '{frame.Callable.Phrase}' at {diagnostic.Location})",
            };
        }
    }

    // The current app as binding walks a scenario, and which app of each user was used last.
    private sealed class AppTracker(BddCatalogSet catalogs)
    {
        private readonly Dictionary<string, BddAppConfig> _lastUsed = new(StringComparer.OrdinalIgnoreCase);

        public BddAppConfig? Current { get; private set; }

        public void Use(BddAppConfig? app)
        {
            Current = app;
            if (app is not null)
            {
                _lastUsed[app.User] = app;
            }
        }

        // An app name; a user with one app; a user with several: the one used last (AD-015).
        public bool TryResolve(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BddAppConfig? app, out string error)
        {
            error = string.Empty;
            app = catalogs.FindApp(name);
            if (app is not null)
            {
                return true;
            }

            var owned = catalogs.AppsOf(name);
            if (owned.Count == 1)
            {
                app = owned[0];
                return true;
            }

            if (owned.Count > 1 && _lastUsed.TryGetValue(name, out app))
            {
                return true;
            }

            error = owned.Count > 1
                ? $"{name} has several apps ({string.Join(", ", owned.Select(candidate => candidate.Name))}) and none has been used yet in this scenario. Name the app, or start with '{owned[0].Name} uses the app'."
                : $"no app or user '{name}' in the Apps table. It has: {catalogs.DescribeApps()}.";
            return false;
        }
    }
}

[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed class BddBindResult
{
    public BddBindResult(BddBoundDocument? document, IReadOnlyList<BddDiagnostic> diagnostics)
    {
        Document = document;
        Diagnostics = diagnostics;
    }

    public BddBoundDocument? Document { get; }

    public IReadOnlyList<BddDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Every scenario as far as its steps bound, errors or not, for a tool that shows a file with a
    /// problem (the preview); <see cref="Document"/> is null when there is an error.
    /// </summary>
    public IReadOnlyList<BddBoundScenario> Scenarios { get; init; } = [];

    public bool Success => Document is not null && Diagnostics.All(x => x.Severity != BddDiagnosticSeverity.Error);
}

[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddBoundDocument(
    BddDocument Source,
    IReadOnlyList<BddBoundScenario> Scenarios);

public sealed record BddBoundScenario(
    BddScenario Source,
    IReadOnlyList<BddStepInvocation> Invocations)
{
    /// <summary>The hooks of the step-definition classes, in the order they run; the runner picks those for the scenario's tags.</summary>
    public IReadOnlyList<BddHook> Hooks { get; init; } = [];
}

[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddStepInvocation(
    string ScenarioName,
    BddStep Step,
    BddCommandPattern Command,
    IReadOnlyDictionary<string, string> Arguments,
    BddTable? Table,
    bool FromBackground)
{
    public string MatchedPattern => Command.Phrase;

    public string CommandId => Command.CommandId;

    /// <summary>The app the step runs on, from the <c>Apps</c> table; null for the fixture's own.</summary>
    public string? App { get; init; }

    /// <summary>The arguments as written, quotes kept, which typed parameters are read from.</summary>
    public IReadOnlyDictionary<string, string> RawArguments { get; init; } = Arguments;

    /// <summary>The call that put this step here, for a step of a callable phrase; null for a step written in the scenario.</summary>
    public BddCallFrame? Call { get; init; }

    /// <summary>
    /// Where the phrase this step bound to lives: <c>page Todo List</c> for a page phrase, the
    /// namespace (<c>Invoicing</c>) for a namespace's phrase; <c>""</c> for a phrase every scenario sees.
    /// </summary>
    public string Scope => Command.Page is { } page ? $"page {page}" : Command.Namespace;

    /// <summary>
    /// The page the binder knows is current after this control verb or page phrase; null when the
    /// step is not page-scoped or may have left the page. The runtime follows it.
    /// </summary>
    public string? PageAfter { get; init; }
}
