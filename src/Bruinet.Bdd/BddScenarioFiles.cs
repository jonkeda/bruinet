namespace Bruinet.Bdd;

/// <summary>A scenario file with the callable phrases it can see, ready to bind.</summary>
/// <param name="Document">The parsed file; null when it has no <c># Feature:</c> heading.</param>
/// <param name="Callables">
/// The file's own callables and those of every phrase file under its scenario root. The binder
/// binds a step only to those the file sees (<see cref="BddScenarioFiles.PhraseFilesFor"/>), and
/// names where the others are.
/// </param>
/// <param name="Diagnostics">The parse problems of the file and of the phrase files it sees.</param>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddLoadedScenarioFile(
    BddDocument? Document,
    BddCallableSet Callables,
    IReadOnlyList<BddDiagnostic> Diagnostics)
{
    /// <summary>Whether the file and its phrase files parsed without errors.</summary>
    public bool Success => Document is not null && Diagnostics.All(x => x.Severity != BddDiagnosticSeverity.Error);

    /// <summary>Binds the file against <paramref name="catalogs"/>, with its callables.</summary>
    /// <param name="catalogs">The suite's catalogs.</param>
    /// <param name="only">The one scenario to bind, by name; null binds them all.</param>
    public BddBindResult Bind(BddCatalogSet catalogs, string? only = null)
        => BddBinder.Bind(Document ?? throw new InvalidOperationException("The file did not parse."), catalogs, Callables, only);

    /// <summary>
    /// Every problem of the file, as the gate reports it: parse (<c>UAT0xx</c>), bind
    /// (<c>UATB0xx</c>) and values (<c>UATA0xx</c>), and each of its own broken callables as a
    /// warning at its heading (a call to one is the error).
    /// </summary>
    /// <param name="catalogs">The suite's catalogs.</param>
    public BddFileCheck Check(BddCatalogSet catalogs)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        if (Document is null)
        {
            return new BddFileCheck(Diagnostics, null);
        }

        var prepared = Callables.Prepare(catalogs);
        var own = Callables.Callables.Where(callable => callable.LocalTo is not null).Select(prepared.Get);
        var bind = Bind(catalogs);
        return new BddFileCheck([.. Diagnostics, .. bind.Diagnostics, .. BddScenarioFiles.BrokenCallables(own)], bind);
    }
}

/// <summary>A checked file: every diagnostic, and the binding when the file parsed.</summary>
/// <param name="Diagnostics">Parse, bind and value problems, and broken callables as warnings.</param>
/// <param name="Bind">The binding; null when the file has no <c># Feature:</c> heading.</param>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddFileCheck(IReadOnlyList<BddDiagnostic> Diagnostics, BddBindResult? Bind);

/// <summary>
/// The one loader of scenario files, for the xUnit gate, the tool and the Presenter: the file, and
/// the <c>*.steps.md</c> files of its folder and every folder above it, up to the scenario root.
/// </summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public static class BddScenarioFiles
{
    /// <summary>The name ending of a phrase file.</summary>
    public const string PhraseFilePattern = BddFileFormat.StepFilePattern;

    private const string ScenarioFolder = BddFileFormat.FeatureFolder;

    private static readonly Dictionary<string, (DateTime Written, BddPhrasesParseResult Parse)> PhraseFiles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Parses the file and the phrase files it can see.</summary>
    /// <param name="filePath">A <c>.feature.md</c> file.</param>
    public static BddLoadedScenarioFile Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return Load(filePath, File.ReadAllText(filePath));
    }

    /// <summary>Parses <paramref name="text"/> as the file at <paramref name="filePath"/>, which may be unsaved, and the phrase files it can see.</summary>
    /// <param name="filePath">A <c>.feature.md</c> file, which says where its phrase files are.</param>
    /// <param name="text">Its text.</param>
    public static BddLoadedScenarioFile Load(string filePath, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(text);
        var parse = BddMarkdownParser.Parse(text, filePath);
        var seen = PhraseFilesFor(filePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var phraseFiles = AllPhraseFiles(filePath).Select(path => (Seen: seen.Contains(path), Parse: ParsePhraseFile(path))).ToList();

        // The file's own callables are seen by the file alone, and their bodies bind in its folder,
        // with its usings.
        var fullPath = Path.GetFullPath(filePath);
        var here = BddNamespaces.Of(fullPath);
        var usings = BddNamespaces.ParseList(parse.Document?.Metadata.GetValueOrDefault(BddMetadataFields.Using));
        List<BddCallable> callables = [.. (parse.Document?.Callables ?? []).Select(callable => callable with { Namespace = here, LocalTo = fullPath, Usings = usings })];
        callables.AddRange(phraseFiles.SelectMany(file => file.Parse.Document?.Callables ?? []));

        // Only a phrase file the scenario sees can break it; another folder's is that folder's problem.
        return new BddLoadedScenarioFile(
            parse.Document,
            new BddCallableSet(callables),
            [.. parse.Diagnostics, .. phraseFiles.Where(file => file.Seen).SelectMany(file => file.Parse.Diagnostics)]);
    }

    /// <summary>
    /// The scenario root of a file: the nearest folder named <c>Features</c> that holds it, the
    /// folder itself included; the file's own folder when there is none.
    /// </summary>
    public static string ScenarioRoot(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var folder = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        for (var candidate = new DirectoryInfo(folder); candidate is not null; candidate = candidate.Parent)
        {
            if (candidate.Name.Equals(ScenarioFolder, StringComparison.OrdinalIgnoreCase))
            {
                return candidate.FullName;
            }
        }

        return folder;
    }

    /// <summary>
    /// The phrase files a scenario file sees: the <c>*.steps.md</c> in its scenario root and in
    /// each folder down to its own, root first. A sibling folder's are not seen (D6).
    /// </summary>
    public static IReadOnlyList<string> PhraseFilesFor(string filePath)
    {
        var root = ScenarioRoot(filePath);
        List<string> folders = [];
        for (var folder = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(filePath))!); folder is not null; folder = folder.Parent)
        {
            folders.Insert(0, folder.FullName);
            if (string.Equals(folder.FullName, root, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        return folders
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, PhraseFilePattern, SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    // Every phrase file under the scenario root, so a step that calls another folder's callable
    // is told where it is rather than that it does not exist.
    private static IReadOnlyList<string> AllPhraseFiles(string filePath)
    {
        var root = ScenarioRoot(filePath);
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, PhraseFilePattern, SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
    }

    /// <summary>A phrase file, parsed, its callables in its folder's namespace; read again only when it changed.</summary>
    public static BddPhrasesParseResult ParsePhraseFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        var written = File.GetLastWriteTimeUtc(fullPath);
        lock (PhraseFiles)
        {
            if (PhraseFiles.TryGetValue(fullPath, out var cached) && cached.Written == written)
            {
                return cached.Parse;
            }
        }

        var parse = ParsePhrases(filePath, File.ReadAllText(fullPath));
        lock (PhraseFiles)
        {
            PhraseFiles[fullPath] = (written, parse);
        }

        return parse;
    }

    /// <summary>A phrase file's text, which may be unsaved, parsed with its callables in its folder's namespace.</summary>
    /// <param name="filePath">The file, which says its namespace.</param>
    /// <param name="text">Its text.</param>
    public static BddPhrasesParseResult ParsePhrases(string filePath, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(text);
        var parse = BddMarkdownParser.ParsePhrases(text, filePath);
        if (parse.Document is { } document)
        {
            var folder = BddNamespaces.Of(Path.GetFullPath(filePath));
            parse = parse with { Document = document with { Callables = [.. document.Callables.Select(callable => callable with { Namespace = folder })] } };
        }

        return parse;
    }

    /// <summary>
    /// A phrase file's problems: parse errors, and each broken callable as a warning at its heading.
    /// A call to a broken callable is the error, in the scenario that makes it.
    /// </summary>
    /// <param name="parse">The parsed phrase file.</param>
    /// <param name="catalogs">The suite's catalogs.</param>
    public static IReadOnlyList<BddDiagnostic> CheckPhrases(BddPhrasesParseResult parse, BddCatalogSet catalogs)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(catalogs);
        return parse.Document is null
            ? parse.Diagnostics
            : [.. parse.Diagnostics, .. BrokenCallables(new BddCallableSet(parse.Document.Callables).Prepare(catalogs).All)];
    }

    internal static IEnumerable<BddDiagnostic> BrokenCallables(IEnumerable<BddPreparedCallable> callables)
        => callables
            .Where(callable => callable.Problem is not null)
            .Select(callable => new BddDiagnostic(
                BddDiagnosticSeverity.Warning,
                callable.Problem!.Code,
                $"The phrase '{callable.Callable.Phrase}' {callable.Problem.Message} Steps that call it fail.",
                callable.Callable.Source));
}
