namespace Bruinet.Bdd;

/// <summary>
/// The catalogs a document binds against: the fixture's own, and one per head of the suite's apps.
/// It also knows the apps, so a step can name one as its subject (<c>Daan taps Save</c>).
/// </summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed class BddCatalogSet
{
    private readonly IReadOnlyDictionary<string, BddCommandCatalog> _byHead;
    private readonly IReadOnlyList<(string Name, BddAppConfig? App)> _subjects;

    /// <summary>A set with one catalog and no apps: every step binds against <paramref name="catalog"/>.</summary>
    /// <param name="catalog">The catalog.</param>
    public BddCatalogSet(BddCommandCatalog catalog)
        : this(catalog, [], suiteHead: null, new Dictionary<string, BddCommandCatalog>(), [])
    {
    }

    /// <summary>Creates the set.</summary>
    /// <param name="defaultCatalog">The fixture's own catalog: for steps before any app is current, and for apps of the suite's head.</param>
    /// <param name="apps">The <c>Apps</c> table.</param>
    /// <param name="suiteHead">The suite's head, which an app without a <c>Head</c> column has.</param>
    /// <param name="catalogsByHead">A catalog per head that is not the fixture's own.</param>
    /// <param name="reservedNames">Page and control names, which an app or user may not also be.</param>
    public BddCatalogSet(
        BddCommandCatalog defaultCatalog,
        IReadOnlyList<BddAppConfig> apps,
        string? suiteHead,
        IReadOnlyDictionary<string, BddCommandCatalog> catalogsByHead,
        IEnumerable<string> reservedNames)
    {
        ArgumentNullException.ThrowIfNull(defaultCatalog);
        ArgumentNullException.ThrowIfNull(apps);
        ArgumentNullException.ThrowIfNull(catalogsByHead);
        ArgumentNullException.ThrowIfNull(reservedNames);

        Default = defaultCatalog;
        Apps = apps;
        SuiteHead = suiteHead;
        _byHead = catalogsByHead;
        ReservedNames = new HashSet<string>(reservedNames, StringComparer.OrdinalIgnoreCase);

        // Longest first, so "Eva's phone" is found before "Eva". A user's entry has no app: it
        // resolves through the scenario's memory of which of that user's apps was used last.
        _subjects = apps.Select(app => (app.Name, (BddAppConfig?)app))
            .Concat(apps.Select(app => app.User)
                .Where(user => !apps.Any(app => string.Equals(app.Name, user, StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal)
                .Select(user => (user, (BddAppConfig?)null)))
            .OrderByDescending(subject => subject.Item1.Length)
            .ToArray();
    }

    /// <summary>The fixture's own catalog.</summary>
    public BddCommandCatalog Default { get; }

    /// <summary>The apps a step can name.</summary>
    public IReadOnlyList<BddAppConfig> Apps { get; }

    /// <summary>The suite's head.</summary>
    public string? SuiteHead { get; }

    /// <summary>The heads that have a catalog of their own.</summary>
    public IReadOnlyCollection<string> Heads => _byHead.Keys.ToArray();

    internal IEnumerable<BddCommandCatalog> All => _byHead.Values.Prepend(Default).Distinct();

    /// <summary>Page and control names of every catalog.</summary>
    public IReadOnlySet<string> ReservedNames { get; }

    /// <summary>The namespaces every scenario uses: the config's <c>## Using</c> (<see cref="BddConfig.Usings"/>).</summary>
    public IReadOnlyList<string> Usings { get; init; } = [];

    /// <summary>
    /// Every namespace something provides: C# phrases, pages and callables of a folder. A
    /// <c>Using</c> of anything else is a mistake.
    /// </summary>
    internal IEnumerable<string> ProvidedNamespaces(BddCallableSet callables)
        => All.SelectMany(catalog => catalog.Patterns.Select(pattern => pattern.Namespace).Concat(catalog.Scope?.Namespaces ?? []))
            .Concat(callables.Callables.Where(callable => callable.LocalTo is null).Select(callable => callable.Namespace))
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The catalog a step running on <paramref name="app"/> binds against: the one place that
    /// decides which phrases and pages a step can see.
    /// </summary>
    /// <param name="app">The current app, or null before any.</param>
    public BddCommandCatalog CatalogFor(BddAppConfig? app)
    {
        var head = app?.Head ?? SuiteHead;
        return head is not null && _byHead.TryGetValue(head, out var catalog) ? catalog : Default;
    }

    /// <summary>The app called <paramref name="name"/>, or null.</summary>
    public BddAppConfig? FindApp(string name)
        => Apps.FirstOrDefault(app => string.Equals(app.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The apps whose user is <paramref name="user"/>, in table order.</summary>
    public IReadOnlyList<BddAppConfig> AppsOf(string user)
        => Apps.Where(app => string.Equals(app.User, user, StringComparison.OrdinalIgnoreCase)).ToArray();

    /// <summary>The names a step can start with, for messages: <c>Daan's PC (Daan)</c>.</summary>
    public string DescribeApps()
        => string.Join(", ", Apps.Select(app => app.User == app.Name ? app.Name : $"{app.Name} ({app.User})"));

    /// <summary>
    /// The app or user <paramref name="text"/> starts with, followed by whitespace; the longest
    /// such name wins. Names are matched as written, so "todos" is not "Todos".
    /// </summary>
    /// <param name="text">A step's text.</param>
    /// <param name="subject">The name, as written.</param>
    /// <param name="rest">The text after it, without the leading whitespace.</param>
    public bool TryFindSubject(string text, out string subject, out string rest)
    {
        foreach (var (name, _) in _subjects)
        {
            if (text.Length > name.Length &&
                text.StartsWith(name, StringComparison.Ordinal) &&
                char.IsWhiteSpace(text[name.Length]))
            {
                subject = name;
                rest = text[name.Length..].TrimStart();
                return true;
            }
        }

        subject = string.Empty;
        rest = string.Empty;
        return false;
    }

    /// <summary>Errors for apps and users whose names are also page or control names.</summary>
    internal IEnumerable<BddDiagnostic> CheckNames(BddSourceLocation location)
    {
        foreach (var name in _subjects.Select(subject => subject.Name).Where(ReservedNames.Contains))
        {
            yield return new BddDiagnostic(
                BddDiagnosticSeverity.Error,
                "UATB005",
                $"'{name}' is an app or user in the Apps table and also a page or control name, so a step that starts with it is ambiguous. Rename one of them.",
                location);
        }
    }
}
