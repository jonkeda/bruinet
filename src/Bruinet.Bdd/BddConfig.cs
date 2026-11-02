using Bruinet.Core.Artifacts;
using Bruinet.Core.Testing;
using Bruinet.Core.Utilities;

namespace Bruinet.Bdd;

public sealed record BddConfig(
    IReadOnlyDictionary<string, string> Runtime,
    IReadOnlyList<BddAssemblyRegistration> Assemblies,
    BddDiscoverySettings Discovery,
    BddReportingSettings Reporting,
    BddSettingsConfig Settings,
    IReadOnlyList<BddSkipRule> SkipRules)
{
    public BddConfig(
        IReadOnlyDictionary<string, string> Runtime,
        IReadOnlyList<BddAssemblyRegistration> Assemblies,
        BddDiscoverySettings Discovery)
        : this(Runtime, Assemblies, Discovery, BddReportingSettings.CreateDefault(), BddSettingsConfig.Default, [])
    {
    }

    /// <summary>
    /// The apps a scenario can switch between (<c>{app} uses the app</c>), from the <c>Apps</c>
    /// table. Empty for a single-app suite.
    /// </summary>
    public IReadOnlyList<BddAppConfig> Apps { get; init; } = [];

    /// <summary>The namespaces every scenario of the suite uses, from the <c>## Using</c> table.</summary>
    public IReadOnlyList<string> Usings { get; init; } = [];

    /// <summary>
    /// How the suite's tests get what is outside the app, from the <c>## Dependencies</c> table as written; a run's
    /// overrides are applied by <see cref="BddDependencies.Effective"/>. Empty for a suite with no table.
    /// </summary>
    public IReadOnlyList<BddDependency> Dependencies { get; init; } = [];

    /// <summary>The suite's <c>Head</c>, trimmed; null when it names none.</summary>
    public string? SuiteHead => Runtime.TryGetValue(BddConfigFields.Head, out var head) && head.Trim() is { Length: > 0 } trimmed ? trimmed : null;

    /// <summary>Whether a scenario with these tags runs on this suite's head; a suite without a head runs every scenario.</summary>
    /// <param name="tags">The scenario's tags, with or without <c>@</c>.</param>
    public bool RunsOnHead(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        return SuiteHead is not { } suiteHead ||
            BddTagConventions.WhyNotOnHead(tags.Select(BddTagConventions.NormalizeTag).ToHashSet(StringComparer.OrdinalIgnoreCase), suiteHead) is null;
    }

    public BddSkipDecision EvaluateSkip(
        IEnumerable<string> tags,
        Func<string, string?>? getEnvironmentVariable = null)
    {
        ArgumentNullException.ThrowIfNull(tags);

        HashSet<string> normalizedTags = new(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            var normalized = BddTagConventions.NormalizeTag(tag);
            if (normalized.Length > 0)
            {
                normalizedTags.Add(normalized);
            }
        }

        if (SuiteHead is { } suiteHead && BddTagConventions.WhyNotOnHead(normalizedTags, suiteHead) is { } reason)
        {
            return new BddSkipDecision(ShouldSkip: true, Reason: reason, Rule: null);
        }

        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        if (normalizedTags.Contains(BddTagConventions.Browser) && WebHeads.Parse(getEnvironmentVariable(WebHeads.Variable)) == WebHead.Bunit)
        {
            return new BddSkipDecision(
                ShouldSkip: true,
                Reason: $"Scenario is tagged @{BddTagConventions.Browser}; this run uses the bUnit head ({WebHeads.Variable}=bunit).",
                Rule: null);
        }

        if (normalizedTags.Contains(BddTagConventions.Device) && MauiHeads.Parse(getEnvironmentVariable(MauiHeads.Variable)) == MauiHead.Headless)
        {
            return new BddSkipDecision(
                ShouldSkip: true,
                Reason: $"Scenario is tagged @{BddTagConventions.Device}; this run uses the MauiHeadless head ({MauiHeads.Variable}=headless).",
                Rule: null);
        }

        if (normalizedTags.Contains(BddTagConventions.NeedsStub)
            && getEnvironmentVariable(BddReplayFields.HttpReplayVariable)?.Trim() is { Length: > 0 } replay
            && !string.Equals(replay, "Off", StringComparison.OrdinalIgnoreCase))
        {
            return new BddSkipDecision(
                ShouldSkip: true,
                Reason: $"Scenario is tagged @{BddTagConventions.NeedsStub}: it reads its backend stub back, and this run records or replays the backend ({BddReplayFields.HttpReplayVariable}={replay}).",
                Rule: null);
        }

        if (SkipRules.Count == 0)
        {
            return BddSkipDecision.Run;
        }

        foreach (var rule in SkipRules)
        {
            if (!normalizedTags.Contains(rule.Tag))
            {
                continue;
            }

            var value = getEnvironmentVariable(rule.EnvironmentVariable);
            if (!BddConfigParser.IsEnabled(value))
            {
                return new BddSkipDecision(
                    ShouldSkip: true,
                    Reason: $"Scenario tag '@{rule.Tag}' requires environment variable '{rule.EnvironmentVariable}' to be enabled.",
                    Rule: rule);
            }
        }

        return BddSkipDecision.Run;
    }
}

public sealed record BddAssemblyRegistration(string Kind, string Assembly);

/// <summary>
/// One app of a multi-app suite: a row of the <c>Apps</c> table. <see cref="Name"/> is what a
/// scenario calls it; the other columns are the fixture's to read (a user, a head, a test set).
/// </summary>
/// <param name="Name">The app's name in scenarios, such as <c>Alice</c>.</param>
/// <param name="Values">Every column of the row, by column name.</param>
public sealed record BddAppConfig(string Name, IReadOnlyDictionary<string, string> Values)
{
    /// <summary>Who uses the app: the <c>User</c> column, or the app's name when it has none.</summary>
    public string User => Values.TryGetValue("User", out var user) && user.Trim() is { Length: > 0 } trimmed ? trimmed : Name;

    /// <summary>The head whose pages and phrases the app uses: the <c>Head</c> column, or null for the suite's.</summary>
    public string? Head => Values.TryGetValue("Head", out var head) && head.Trim() is { Length: > 0 } trimmed ? trimmed : null;
}

public sealed record BddDiscoverySettings(
    bool RequireExplicitUatAttributes = false,
    bool AllowNameInference = true);

public sealed record BddReportingSettings(
    string OutputDirectory,
    bool ScreenshotOnFailure = false,
    bool IncludeRuntimeTrace = false)
{
    public static BddReportingSettings CreateDefault(string? suiteName = null)
    {
        return new BddReportingSettings(
            DefaultTestArtifactPathProvider.Create(suiteName).UatDirectory);
    }
}

public sealed record BddSettingsConfig(
    string Root = "TestSettings",
    string DefaultFile = "testsettings.json",
    string? LocalFile = "testsettings.local.json",
    string? ScenarioConvention = "scenarios/{ScenarioId}.json")
{
    public static BddSettingsConfig Default { get; } = new();
}

public sealed record BddSkipRule
{
    public BddSkipRule(string tag, string environmentVariable)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(environmentVariable);

        Tag = BddTagConventions.NormalizeTag(tag);
        EnvironmentVariable = environmentVariable.Trim();
    }

    public string Tag { get; init; }

    public string EnvironmentVariable { get; init; }
}

public sealed record BddSkipDecision(bool ShouldSkip, string? Reason, BddSkipRule? Rule)
{
    public static BddSkipDecision Run { get; } = new(false, null, null);
}

public static class BddMetadataFields
{
    public const string App = "App";
    public const string Area = "Area";
    public const string Target = "Target";
    public const string Tags = "Tags";
    public const string Mode = "Mode";
    public const string Requires = "Requires";
    public const string Owner = "Owner";
    public const string Priority = "Priority";
    public const string Evidence = "Evidence";

    /// <summary>The test set the file runs on, e.g. <c>one-todo</c>.</summary>
    public const string TestData = "TestData";

    /// <summary>The namespaces whose phrases and pages the file sees besides its folder's: <c>Accounts, Backend</c>.</summary>
    public const string Using = "Using";
}

public static class BddTagConventions
{
    public const string Smoke = "smoke";
    public const string Regression = "regression";
    public const string Automated = "automated";
    public const string SemiAutomated = "semi-automated";
    public const string Manual = "manual";
    public const string Hardware = "hardware";
    public const string LiveApi = "live-api";
    public const string Maui = "maui";
    public const string Web = "web";

    /// <summary>A scenario that uses apps of several heads; it runs only on a suite whose head is <c>mixed</c>.</summary>
    public const string Mixed = "mixed";

    /// <summary>A scenario that runs without a screen, against the app's services; it runs only on a suite whose head is <c>service</c>.</summary>
    public const string Service = "service";
    public const string Windows = "windows";
    public const string Android = "android";
    public const string Ios = "ios";
    public const string Deterministic = "deterministic";

    /// <summary>A scenario that needs a browser: skipped on the bUnit head (<see cref="WebHeads"/>).</summary>
    public const string Browser = "browser";

    /// <summary>A MAUI scenario that needs a device: skipped on the MauiHeadless head (<see cref="MauiHeads"/>).</summary>
    public const string Device = "device";

    /// <summary>
    /// A scenario that reads its backend stub back (what the server holds): skipped in a run that records or replays the
    /// backend (<see cref="BddReplayFields.HttpReplayVariable"/> set), where the stub receives nothing, recording too, so a
    /// recording run and its replay run the same scenarios in the same order.
    /// </summary>
    public const string NeedsStub = "needs-stub";
    public const string OpenAiLive = "openai-live";

    /// <summary>
    /// The head tags. A scenario tagged with one or more runs only on a suite whose
    /// <see cref="BddConfigFields.Head"/> is among them; one with none runs on every head.
    /// </summary>
    public static IReadOnlyList<string> Heads { get; } = [Maui, Web, Mixed, Service];

    /// <summary>
    /// The heads a scenario must name to run there: a suite with one of these runs only the
    /// scenarios tagged with it, so an untagged scenario stays a UI scenario.
    /// </summary>
    public static IReadOnlyList<string> OptInHeads { get; } = [Mixed, Service];

    /// <summary>Whether <paramref name="head"/> is the head without a screen.</summary>
    /// <param name="head">A suite's head, or null.</param>
    public static bool IsService(string? head) => string.Equals(head, Service, StringComparison.OrdinalIgnoreCase);

    /// <summary>Why a scenario with these tags does not run on <paramref name="suiteHead"/>; null when it does.</summary>
    /// <param name="normalizedTags">The scenario's tags without <c>@</c>, compared ignoring case.</param>
    /// <param name="suiteHead">The suite's head.</param>
    public static string? WhyNotOnHead(IReadOnlySet<string> normalizedTags, string suiteHead)
    {
        ArgumentNullException.ThrowIfNull(normalizedTags);
        ArgumentNullException.ThrowIfNull(suiteHead);

        var heads = Heads.Where(normalizedTags.Contains).ToList();
        if (heads.Count == 0 && OptInHeads.FirstOrDefault(optIn => optIn.Equals(suiteHead, StringComparison.OrdinalIgnoreCase)) is { } optInHead)
        {
            return $"Scenario is not tagged @{optInHead}; this suite runs only @{optInHead} scenarios.";
        }

        return heads.Count > 0 && !heads.Contains(suiteHead, StringComparer.OrdinalIgnoreCase)
            ? $"Scenario is for {string.Join(", ", heads.Select(tag => $"@{tag}"))}; this suite runs the '{suiteHead}' head."
            : null;
    }

    public static string NormalizeTag(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return tag.Trim().TrimStart('@').Trim();
    }
}

public static class BddConfigParser
{
    public static BddConfig Parse(string markdown, string? suiteName = null)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        Dictionary<string, string> runtime = new(StringComparer.OrdinalIgnoreCase);
        List<BddAssemblyRegistration> assemblies = [];
        Dictionary<string, string> discovery = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> reporting = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> settings = new(StringComparer.OrdinalIgnoreCase);
        List<BddSkipRule> skipRules = [];
        List<BddAppConfig> apps = [];
        List<string> usings = [];

        var sections = ParseSections(markdown);
        if (sections.TryGetValue("Runtime", out var runtimeTable))
        {
            runtime = ToFieldValueDictionary(runtimeTable);
        }

        if (sections.TryGetValue("Assemblies", out var assembliesTable))
        {
            foreach (var row in assembliesTable.Rows)
            {
                if (row.Cells.TryGetValue("Kind", out var kind) &&
                    row.Cells.TryGetValue("Assembly", out var assembly) &&
                    kind.Length > 0 &&
                    assembly.Length > 0)
                {
                    assemblies.Add(new BddAssemblyRegistration(kind, assembly));
                }
            }
        }

        if (sections.TryGetValue("Discovery", out var discoveryTable))
        {
            discovery = ToFieldValueDictionary(discoveryTable);
        }

        if (sections.TryGetValue("Reporting", out var reportingTable))
        {
            reporting = ToFieldValueDictionary(reportingTable);
        }

        if (sections.TryGetValue("Settings", out var settingsTable))
        {
            settings = ToFieldValueDictionary(settingsTable);
        }

        if (sections.TryGetValue("Skip Rules", out var skipRulesTable))
        {
            foreach (var row in skipRulesTable.Rows)
            {
                if (row.Cells.TryGetValue("Tag", out var tag) &&
                    row.Cells.TryGetValue("EnvironmentVariable", out var environmentVariable) &&
                    tag.Length > 0 &&
                    environmentVariable.Length > 0)
                {
                    skipRules.Add(new BddSkipRule(tag, environmentVariable));
                }
            }
        }

        if (sections.TryGetValue("Apps", out var appsTable))
        {
            foreach (var row in appsTable.Rows)
            {
                if (row.Cells.TryGetValue("Name", out var name) && name.Length > 0)
                {
                    apps.Add(new BddAppConfig(name, new Dictionary<string, string>(row.Cells, StringComparer.OrdinalIgnoreCase)));
                }
            }
        }

        if (sections.TryGetValue("Using", out var usingTable))
        {
            usings.AddRange(usingTable.Rows
                .Select(row => row.Cells.TryGetValue("Namespace", out var name) ? BddNamespaces.Normalize(name) : string.Empty)
                .Where(name => name.Length > 0));
        }

        return new BddConfig(
            runtime,
            assemblies,
            new BddDiscoverySettings(
                ReadBool(discovery, "RequireExplicitUatAttributes", defaultValue: false),
                ReadBool(discovery, "AllowNameInference", defaultValue: true)),
            new BddReportingSettings(
                ResolveReportingOutputDirectory(
                    ReadString(reporting, "OutputDirectory", string.Empty),
                    suiteName),
                ReadBool(reporting, "ScreenshotOnFailure", defaultValue: false),
                ReadBool(reporting, "IncludeRuntimeTrace", defaultValue: false)),
            new BddSettingsConfig(
                ReadString(settings, "Root", BddSettingsConfig.Default.Root),
                ReadString(settings, "DefaultFile", BddSettingsConfig.Default.DefaultFile),
                ReadNullableString(settings, "LocalFile", BddSettingsConfig.Default.LocalFile),
                ReadNullableString(settings, "ScenarioConvention", BddSettingsConfig.Default.ScenarioConvention)),
            skipRules)
        {
            Apps = apps,
            Usings = usings,
            Dependencies = sections.TryGetValue("Dependencies", out var dependencies) ? ReadDependencies(dependencies) : [],
        };
    }

    public static BddConfig ParseFile(string filePath, string? suiteName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var config = Parse(File.ReadAllText(filePath), suiteName);

        // AppsFile names a table shared with UI tests (apps.md); an inline Apps table wins.
        if (config.Apps.Count > 0 || !config.Runtime.TryGetValue(BddConfigFields.AppsFile, out var appsFile) || appsFile.Length == 0)
        {
            return config;
        }

        var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(filePath))!, appsFile));

        // A runner reads its suite's source config; an apps file the build copies in is in its output.
        var built = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, appsFile));
        if (!File.Exists(path) && File.Exists(built))
        {
            path = built;
        }

        return config with
        {
            Apps = TestAppDefinition.ReadMarkdownFile(path).Select(app => new BddAppConfig(app.Name, app.Values)).ToArray(),
        };
    }

    private static List<BddDependency> ReadDependencies(BddTable table)
        => [.. table.Rows
            .Where(row => row.Cells.TryGetValue("Name", out var name) && name.Length > 0)
            .Select(row => new BddDependency(
                row.Cells["Name"],
                Cell(row, "Kind").ToLowerInvariant(),
                Cell(row, "Mode").ToLowerInvariant(),
                Cell(row, "Where").Replace("`", string.Empty, StringComparison.Ordinal)))];

    private static string Cell(BddTableRow row, string column) => row.Cells.TryGetValue(column, out var value) ? value : string.Empty;

    private static Dictionary<string, BddTable> ParseSections(string markdown)
        => MarkdownTables.ReadSections(markdown).ToDictionary(
            section => section.Key,
            section => new BddTable(
                section.Value.Columns,
                section.Value.Rows.Select(row => new BddTableRow(row.Cells, new BddSourceLocation(null, row.LineNumber))).ToArray(),
                new BddSourceLocation(null, section.Value.EndLineNumber)),
            StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string> ToFieldValueDictionary(BddTable table)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows)
        {
            if (row.Cells.TryGetValue("Field", out var field) &&
                row.Cells.TryGetValue("Value", out var value) &&
                field.Length > 0)
            {
                values[field] = value;
            }
        }

        return values;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> values, string key, bool defaultValue)
    {
        return values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static string ReadString(IReadOnlyDictionary<string, string> values, string key, string defaultValue)
    {
        return values.TryGetValue(key, out var value) && value.Length > 0
            ? value
            : defaultValue;
    }

    private static string? ReadNullableString(IReadOnlyDictionary<string, string> values, string key, string? defaultValue)
    {
        return values.TryGetValue(key, out var value)
            ? string.IsNullOrWhiteSpace(value) ? null : value
            : defaultValue;
    }

    private static string ResolveReportingOutputDirectory(string configured, string? suiteName)
    {
        var provider = DefaultTestArtifactPathProvider.Create(suiteName);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return provider.UatDirectory;
        }

        var expanded = configured
            .Replace("$(BruinetTestResults)", provider.SuiteDirectory, StringComparison.OrdinalIgnoreCase)
            .Replace("${BruinetTestResults}", provider.SuiteDirectory, StringComparison.OrdinalIgnoreCase);

        return Path.GetFullPath(
            Path.IsPathRooted(expanded)
                ? expanded
                : Path.Combine(provider.SuiteDirectory, expanded));
    }

    public static bool IsEnabled(string? value)
    {
        return value is not null &&
            (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("on", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsTableRow(string line) => MarkdownTables.IsTableRow(line);

    internal static bool IsSeparatorRow(string line) => MarkdownTables.IsSeparatorRow(line);

    internal static IReadOnlyList<string> SplitTableRow(string line) => MarkdownTables.SplitRow(line);
}
