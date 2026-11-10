namespace Bruinet.Bdd;

[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public static class BddScenarioSource
{
    /// <summary>
    /// Set by a host that starts a suite's runner (the IDEs' language server, the Presenter) to the
    /// suite's source folder: the runner reads the scenario files from there, so an edit runs without a build.
    /// </summary>
    public const string SourceDirectoryEnvironmentVariable = "BRUINET_SUITE_SOURCE";

    /// <summary>The suite's source folder when a host runs it from there; null under <c>dotnet test</c>, which reads its output.</summary>
    public static string? SourceDirectory
        => Environment.GetEnvironmentVariable(SourceDirectoryEnvironmentVariable) is { Length: > 0 } source && Directory.Exists(source) ? source : null;

    /// <summary>The suite's folder its scenario files are read from: <see cref="SourceDirectory"/>, else <paramref name="output"/>.</summary>
    /// <param name="output">The suite's output folder, as the gate reads it.</param>
    public static string SuiteDirectory(string output) => SourceDirectory ?? output;

    /// <summary>The suite's config: <paramref name="configFileName"/>, or else <c>uat.config.md</c> or <c>bdd.config.md</c>.</summary>
    /// <param name="baseDirectory">The folder it is in; <see cref="SourceDirectory"/>, else the output, by default.</param>
    /// <param name="configFileName">Its name, when not one of the usual two.</param>
    public static string GetConfigFilePath(
        string? baseDirectory = null,
        string? configFileName = null)
    {
        var directory = baseDirectory ?? SuiteDirectory(AppContext.BaseDirectory);
        return configFileName is null ? BddFileFormat.FindConfig(directory) : Path.Combine(directory, configFileName);
    }

    /// <summary>
    /// Enumerates scenario files, optionally filtered by configuration.
    /// </summary>
    public static IEnumerable<string> EnumerateScenarioFiles(
        string folderName = BddFileFormat.FeatureFolder,
        string? baseDirectory = null,
        string? configurationFilter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

        var scenarioDirectory = Path.Combine(baseDirectory ?? AppContext.BaseDirectory, folderName);
        IEnumerable<string> files = Directory.Exists(scenarioDirectory)
            ? Directory
                .EnumerateFiles(scenarioDirectory, BddFileFormat.FeatureFilePattern, SearchOption.AllDirectories)
                .Order(StringComparer.OrdinalIgnoreCase)
            : Array.Empty<string>();

        return ApplyFilter(files, configurationFilter);
    }

    /// <summary>
    /// Gets scenario files as theory data, optionally filtered by configuration.
    /// </summary>
    public static IEnumerable<object[]> GetScenarioFileTheoryData(
        string folderName = BddFileFormat.FeatureFolder,
        string? baseDirectory = null,
        string? configurationFilter = null)
    {
        foreach (var filePath in EnumerateScenarioFiles(folderName, baseDirectory, configurationFilter))
        {
            yield return [filePath];
        }
    }

    /// <summary>
    /// Gets one theory case per scenario - <c>[filePath, scenarioName]</c> - so each scenario (and
    /// each outline row) passes or fails on its own.
    /// </summary>
    /// <remarks>
    /// Cases come grouped by the files' <c>TestData</c> metadata, then by file and position, which
    /// is <see cref="GetOrderKey"/>. A runner that keeps that order (an xUnit test-case orderer)
    /// switches test data once per test set. A file that does not parse still yields one case,
    /// with an empty scenario name, so its diagnostics are reported instead of the file vanishing.
    /// A suite whose head is opt-in (<see cref="BddTagConventions.OptInHeads"/>) lists only the
    /// scenarios tagged for it, read from the config next to the scenarios; on any other head a
    /// scenario for another head still shows, as skipped.
    /// </remarks>
    public static IEnumerable<object[]> GetScenarioTheoryData(
        string folderName = BddFileFormat.FeatureFolder,
        string? baseDirectory = null,
        string? configurationFilter = null)
    {
        var configPath = GetConfigFilePath(baseDirectory);
        var config = File.Exists(configPath) ? BddConfigParser.ParseFile(configPath) : null;
        var optIn = config?.SuiteHead is { } head && BddTagConventions.OptInHeads.Contains(head, StringComparer.OrdinalIgnoreCase);

        return EnumerateScenarios(folderName, baseDirectory, configurationFilter)
            .Where(scenario => !optIn || scenario.ScenarioName.Length == 0 || config!.RunsOnHead(scenario.Tags))
            .Select(scenario => new object[] { scenario.FilePath, scenario.ScenarioName });
    }

    /// <summary>Every scenario in the scenario files, in <see cref="GetOrderKey"/> order.</summary>
    public static IReadOnlyList<BddScenarioReference> EnumerateScenarios(
        string folderName = BddFileFormat.FeatureFolder,
        string? baseDirectory = null,
        string? configurationFilter = null)
    {
        return EnumerateScenarioFiles(folderName, baseDirectory, configurationFilter)
            .SelectMany(ScenariosOf)
            .OrderBy(scenario => scenario.OrderKey, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The position of a scenario in a run: its file's test set, then the file, then its place in
    /// the file.
    /// </summary>
    public static string GetOrderKey(string filePath, string scenarioName)
    {
        return ScenariosOf(filePath).FirstOrDefault(scenario => scenario.ScenarioName == scenarioName)?.OrderKey
            ?? new BddScenarioReference(filePath, scenarioName, null, int.MaxValue).OrderKey;
    }

    private static IEnumerable<BddScenarioReference> ScenariosOf(string filePath)
    {
        var document = BddMarkdownParser.ParseFile(filePath).Document;
        if (document is null || document.Scenarios.Count == 0)
        {
            return [new BddScenarioReference(filePath, string.Empty, null, 0)];
        }

        var testData = document.Metadata.TryGetValue(BddMetadataFields.TestData, out var name) && name.Length > 0 ? name : null;
        return document.Scenarios.Select((scenario, index) => new BddScenarioReference(filePath, scenario.Name, testData, index) { Tags = scenario.Tags });
    }

    public static IEnumerable<string> ApplyFilter(IEnumerable<string> filePaths, string? filter)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        if (string.IsNullOrWhiteSpace(filter))
            return filePaths;

        var terms = filter.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return terms.Length == 0
            ? filePaths
            : filePaths.Where(path => terms.Any(term =>
                path.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).Contains(term, StringComparison.OrdinalIgnoreCase)));
    }
}

/// <summary>One scenario of a scenario file.</summary>
/// <param name="FilePath">The file.</param>
/// <param name="ScenarioName">The scenario's name; outline rows carry their <c>[n]</c> suffix.</param>
/// <param name="TestData">The file's <c>TestData</c> metadata, or <c>null</c>.</param>
/// <param name="Index">The scenario's position in the file.</param>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddScenarioReference(string FilePath, string ScenarioName, string? TestData, int Index)
{
    /// <summary>The scenario's own tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Sorts by test set, then file, then position.</summary>
    public string OrderKey => $"{TestData}\u0001{FilePath}\u0001{Index:D6}";
}
