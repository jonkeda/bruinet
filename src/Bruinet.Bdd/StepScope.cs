namespace Bruinet.Bdd;

/// <summary>
/// What a head adds to binding beyond the folder, file, <c>Using</c> and global scopes: the page a
/// step runs on, for Bruinet.Uat. Set on <see cref="BddCommandCatalog.Scope"/>.
/// </summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public interface IStepScopeModel
{
    /// <summary>The namespaces it provides, which a file's <c>Using</c> may name.</summary>
    IEnumerable<string> Namespaces { get; }

    /// <summary>Starts following one scenario's steps.</summary>
    /// <param name="catalogs">The catalogs the scenario binds against.</param>
    /// <param name="used">The usings a bound step needed, to add to; null when not counted.</param>
    IStepScope StartWalk(BddCatalogSet catalogs, HashSet<string>? used);
}

/// <summary>One scenario's walk: what a step can bind to where it stands, and what each bound step did.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public interface IStepScope
{
    /// <summary>The candidates the step can bind to at this point; adds a problem when none fits.</summary>
    /// <param name="catalog">The catalog of the step's app.</param>
    /// <param name="step">The step.</param>
    /// <param name="app">The step's app; null for the fixture's own.</param>
    /// <param name="candidates">The candidates the folder, file and <c>Using</c> scopes let through.</param>
    /// <param name="problems">Where a problem goes.</param>
    IReadOnlyList<BddCommandMatch> Visible(BddCommandCatalog catalog, BddStep step, BddAppConfig? app, IReadOnlyList<BddCommandMatch> candidates, List<BddDiagnostic> problems);

    /// <summary>Follows a bound step. False when it added a problem, which stops the step.</summary>
    /// <param name="step">The step.</param>
    /// <param name="match">What it bound to.</param>
    /// <param name="arguments">Its arguments, which it may rewrite.</param>
    /// <param name="app">The step's app.</param>
    /// <param name="where">Where the step is written.</param>
    /// <param name="diagnostics">Where a problem goes.</param>
    bool Bound(BddStep step, BddCommandMatch match, ref IReadOnlyDictionary<string, string> arguments, BddAppConfig? app, BddScope where, List<BddDiagnostic> diagnostics);

    /// <summary>The scope known after the step, for the runtime to follow; null when not known.</summary>
    /// <param name="pattern">What the step bound to.</param>
    /// <param name="app">The step's app.</param>
    string? After(BddCommandPattern pattern, BddAppConfig? app);
}
