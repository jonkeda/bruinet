namespace Bruinet.Uat;

public abstract class UatSpecFormatTestBase
{
    protected virtual IReadOnlyList<string> RequiredMetadata { get; } =
    [
        BddMetadataFields.App,
        BddMetadataFields.Area,
        BddMetadataFields.Target,
        BddMetadataFields.Tags,
        BddMetadataFields.Mode,
        BddMetadataFields.Requires,
        BddMetadataFields.Priority,
        BddMetadataFields.Evidence
    ];

    protected virtual string? ExpectedApp => null;

    protected virtual string? ExpectedTarget => null;

    protected virtual string ConfigFilePath => BddScenarioSource.GetConfigFilePath();

    protected virtual Type? RuntimeRootType => null;

    /// <summary>
    /// The tag in a file's <c>Tags</c> metadata that leaves it out of <see cref="AssertUatFileFormat"/>, such as a draft
    /// whose steps do not bind yet. The file must then say why in its <see cref="OptOutReasonField"/> metadata.
    /// </summary>
    public const string OptOutTag = "no-format-check";

    /// <summary>The metadata field that says why a file carries <see cref="OptOutTag"/>.</summary>
    public const string OptOutReasonField = "FormatCheck";

    /// <summary>
    /// Every feature file under each folder, one case per file, so a new file is checked without a list. A folder with
    /// no file gives no case, which xUnit reports as a failure.
    /// </summary>
    /// <param name="folderNames">The folders, relative to the output.</param>
    protected static IEnumerable<object[]> GetFolderFiles(params string[] folderNames)
    {
        ArgumentNullException.ThrowIfNull(folderNames);
        return folderNames.SelectMany(folder => BddScenarioSource.GetScenarioFileTheoryData(folder));
    }

    /// <summary>
    /// Checks one file of <see cref="GetFolderFiles"/>: it parses, has the required metadata and binds through the catalog,
    /// unless it opts out with <see cref="OptOutTag"/> and a reason.
    /// </summary>
    /// <param name="filePath">The feature file.</param>
    protected void AssertUatFileFormat(string filePath)
    {
        if (OptOutReason(filePath) is not null)
        {
            return;
        }

        AssertUatFileParsesAndContainsRequiredMetadata(filePath);
        AssertUatFileBindsThroughCatalog(filePath);
    }

    /// <summary>
    /// Why a file is left out of the format check; null when it is checked. A file that carries <see cref="OptOutTag"/>
    /// without a reason fails.
    /// </summary>
    /// <param name="filePath">The feature file.</param>
    public static string? OptOutReason(string filePath)
    {
        if (BddMarkdownParser.ParseFile(filePath).Document is not { } document
            || !document.Metadata.TryGetValue(BddMetadataFields.Tags, out var tags)
            || !tags.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(BddTagConventions.NormalizeTag)
                .Contains(OptOutTag, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return document.Metadata.TryGetValue(OptOutReasonField, out var reason) && !string.IsNullOrWhiteSpace(reason)
            ? reason.Trim()
            : throw new InvalidOperationException(
                $"{Path.GetFileName(filePath)} carries the tag '{OptOutTag}' but no reason: add the metadata field '{OptOutReasonField}' saying why.");
    }

    protected static IEnumerable<object[]> GetScenarioFiles(
        string folderName = BddFileFormat.FeatureFolder,
        string? configurationFilter = null)
    {
        return BddScenarioSource.GetScenarioFileTheoryData(
            folderName,
            configurationFilter: configurationFilter);
    }

    protected void AssertUatFileParsesAndContainsRequiredMetadata(string filePath)
    {
        var parse = BddMarkdownParser.ParseFile(filePath);

        if (!parse.Success || parse.Document is null)
        {
            throw new InvalidOperationException(
                BddDiagnosticsFormatter.FormatDiagnostics(parse.Diagnostics));
        }

        foreach (var field in RequiredMetadata)
        {
            if (!parse.Document.Metadata.TryGetValue(field, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"{Path.GetFileName(filePath)} must set metadata field '{field}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(ExpectedApp))
        {
            AssertMetadataEquals(parse.Document, filePath, BddMetadataFields.App, ExpectedApp);
        }

        if (!string.IsNullOrWhiteSpace(ExpectedTarget))
        {
            AssertMetadataEquals(parse.Document, filePath, BddMetadataFields.Target, ExpectedTarget);
        }

        if (parse.Document.Scenarios.Count == 0)
        {
            throw new InvalidOperationException($"{Path.GetFileName(filePath)} must contain at least one scenario.");
        }

        var untagged = parse.Document.Scenarios.FirstOrDefault(scenario => scenario.Tags.Count == 0);
        if (untagged is not null)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(filePath)} scenario '{untagged.Name}' must set at least one tag.");
        }
    }

    protected void AssertUatFileBindsThroughCatalog(string filePath)
    {
        var file = BddScenarioFiles.Load(filePath);
        if (!file.Success)
        {
            throw new InvalidOperationException(
                BddDiagnosticsFormatter.FormatDiagnostics(file.Diagnostics));
        }

        var bind = file.Bind(new BddCatalogSet(CreateCatalog()));
        if (!bind.Success || bind.Document is null)
        {
            throw new InvalidOperationException(
                BddDiagnosticsFormatter.FormatDiagnostics(bind.Diagnostics));
        }
    }

    protected void AssertUatConfigParses()
    {
        AssertConfig(BddConfigParser.ParseFile(ConfigFilePath, GetReportingSuiteName()));
    }

    protected virtual BddCommandCatalog CreateCatalog()
    {
        var catalog = UatSpecCommandCatalog.CreateDefault();
        if (RuntimeRootType is { } runtimeRootType)
        {
            UatReflectionRuntime.RegisterCompositionPhrases(catalog, runtimeRootType);
            UatReflectionRuntime.RegisterRootPhrases(catalog, runtimeRootType);
            catalog.IsGeneric = false;
        }

        return catalog;
    }

    protected virtual void AssertConfig(BddConfig config)
    {
    }

    protected virtual string? GetReportingSuiteName()
    {
        return RuntimeRootType?.Assembly.GetName().Name ?? GetType().Assembly.GetName().Name;
    }

    private static void AssertMetadataEquals(
        BddDocument document,
        string filePath,
        string field,
        string expected)
    {
        if (!document.Metadata.TryGetValue(field, out var actual) ||
            !actual.Equals(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(filePath)} must set metadata field '{field}' to '{expected}'. Actual value: '{actual ?? "(missing)"}'.");
        }
    }
}
