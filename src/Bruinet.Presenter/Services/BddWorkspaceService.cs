using Bruinet.Bdd.Preview;
using Bruinet.Bdd.Workspace;
using Bruinet.Presenter.Models;
using Bruinet.Uat;

namespace Bruinet.Presenter.Services;

public sealed class BddWorkspaceService : IBddWorkspaceService, IDisposable
{
    private readonly IProjectEvaluator? _projectEvaluator;
    private BddSuite? _suite;

    /// <summary>Creates the service with the shared MSBuild evaluator.</summary>
    public BddWorkspaceService()
        : this(null)
    {
    }

    /// <summary>Creates the service with a specific project evaluator.</summary>
    /// <param name="projectEvaluator">The evaluator to resolve <c>Runtime Project</c> with, or null for the default.</param>
    public BddWorkspaceService(IProjectEvaluator? projectEvaluator)
    {
        _projectEvaluator = projectEvaluator;
    }

    public string? FindDefaultWorkspace()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "Bruinet.Maui.UITests");
            if (File.Exists(BddFileFormat.FindConfig(candidate)))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// Loads a workspace folder. Each file is checked as the gate and the IDEs check it
    /// (<see cref="BddFileAnalysis"/>), against the suite's own catalogs from
    /// <see cref="BddSuite"/>; without a build there are no catalogs, and the files are only parsed.
    /// </summary>
    /// <param name="folderPath">The workspace folder.</param>
    public BddWorkspaceLoadResult LoadFolder(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        List<BddFileLoadResult> files = [];
        List<BddScenarioLoadResult> scenarios = [];
        List<string> diagnostics = [];

        var configInfo = BddWorkspaceConfigInspector.Inspect(folderPath, _projectEvaluator);
        diagnostics.AddRange(configInfo.Diagnostics);

        var configPath = BddFileFormat.FindConfig(folderPath);
        var config = File.Exists(configPath) ? BddConfigParser.ParseFile(configPath) : null;
        var discoveryReport = config is not null
            ? FormatDiscoveryReport(config)
            : "Discovery report:\n- No uat.config.md or bdd.config.md found.";

        var catalogs = OpenSuite(configPath, diagnostics);
        foreach (var filePath in EnumerateUatFiles(folderPath))
        {
            var loaded = LoadFile(filePath, catalogs, IsExpectedFailurePath(filePath));
            files.Add(loaded.File);
            diagnostics.AddRange(loaded.Lines);
            scenarios.AddRange(loaded.Scenarios);
        }

        foreach (var stepFile in EnumerateFiles(folderPath, BddFileFormat.StepFilePattern))
        {
            var analysis = BddFileAnalysis.Analyze(File.ReadAllText(stepFile), stepFile, catalogs);
            diagnostics.AddRange(FormatDiagnostics(analysis.Diagnostics.Where(analysis.IsHere), expected: false));
        }

        if (files.Count == 0)
        {
            diagnostics.Add("No .feature.md files were found.");
        }

        var workspaceName = new DirectoryInfo(folderPath).Name;
        return new BddWorkspaceLoadResult(
            folderPath,
            workspaceName,
            configInfo,
            files,
            scenarios,
            diagnostics,
            discoveryReport,
            FormatCatalog(catalogs?.Default ?? UatGenericCatalog.CreateDefault()),
            catalogs);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _suite?.Dispose();
        _suite = null;
    }

    // The suite's catalogs, loaded from a shadow copy so a build never meets a locked file; the
    // previous workspace's load is released first.
    private BddCatalogSet? OpenSuite(string configPath, List<string> diagnostics)
    {
        Dispose();
        if (!File.Exists(configPath))
        {
            return null;
        }

        _suite = BddSuite.Open(configPath);
        if (_suite.TryGetCatalogs(out var reason) is { } catalogs)
        {
            return catalogs;
        }

        diagnostics.Add($"Error: the steps cannot be checked: {reason}");
        return null;
    }

    private static (BddFileLoadResult File, IReadOnlyList<string> Lines, IReadOnlyList<BddScenarioLoadResult> Scenarios) LoadFile(
        string filePath,
        BddCatalogSet? catalogs,
        bool expectedFailure)
    {
        var text = File.ReadAllText(filePath);
        var name = Path.GetFileName(filePath);
        var parse = BddScenarioFiles.Load(filePath, text);
        var analysis = BddFileAnalysis.Analyze(text, filePath, catalogs);
        var problems = FormatDiagnostics(analysis.Diagnostics.Where(analysis.IsHere), expectedFailure);
        var parsed = parse.Success;
        var bound = catalogs is not null && analysis.Diagnostics.All(diagnostic => diagnostic.Severity != BddDiagnosticSeverity.Error);

        // A file under ExpectedFailures fails on purpose; it is reported, not counted as a problem.
        var bindText = catalogs is null ? "not checked" : bound ? "ok" : expectedFailure ? "expected failure" : "error";
        List<string> lines =
        [
            $"{name}: Parse: {(parsed ? "ok" : expectedFailure ? "expected failure" : "error")}",
            $"{name}: Bind: {bindText}",
            .. problems,
        ];

        IReadOnlyList<BddScenarioLoadResult> scenarios = analysis.Scenarios.Count > 0
            ? [.. analysis.Scenarios.Select(scenario => Bound(scenario, parse.Document?.Title ?? name, filePath))]
            : parse.Document is { } document
                ? [.. document.Scenarios.Select(scenario => Unbound(scenario, document.Title, filePath))]
                : [];
        return (new BddFileLoadResult(filePath, name, parsed, bound, problems), lines, scenarios);
    }

    private static BddScenarioLoadResult Bound(BddBoundScenario scenario, string suiteName, string filePath)
        => new(
            scenario.Source.Name,
            suiteName,
            filePath,
            scenario.Source.Tags,
            [.. scenario.Invocations.Select(invocation => new BddStepLoadResult(
                "wait",
                invocation.Call is { } call ? $"{new string(' ', 2 * call.Depth)}{Written(invocation.Step)}" : Written(invocation.Step),
                invocation.CommandId,
                // One row per step keeps the run pointer's index; a callable's step opens the call.
                invocation.Call is { } frame ? frame.Chain[0].Location.LineNumber : invocation.Step.Source.LineNumber,
                invocation.Call is { } root ? $"{root.Chain[0].Call.Keyword} {root.Chain[0].Call.Text}" : null))],
            scenario.Source.Source.LineNumber);

    private static BddScenarioLoadResult Unbound(BddScenario scenario, string suiteName, string filePath)
        => new(
            scenario.Name,
            suiteName,
            filePath,
            scenario.Tags,
            [.. scenario.Steps.Select(step => new BddStepLoadResult("wait", Written(step), "(unbound)", step.Source.LineNumber))],
            scenario.Source.LineNumber);

    // A step as it is written: its keyword, then its text.
    private static string Written(BddStep step) => $"{step.Keyword} {step.Text}";

    private static IEnumerable<string> EnumerateFiles(string folderPath, string pattern)
    {
        return Directory
            .EnumerateFiles(folderPath, pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase);
    }
    private static IEnumerable<string> EnumerateUatFiles(string folderPath)
    {
        return EnumerateFiles(folderPath, BddFileFormat.FeatureFilePattern)
            .OrderBy(path => IsExpectedFailurePath(path) ? 1 : 0)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase);
    }
    private static bool IsExpectedFailurePath(string path)
    {
        return path.Contains(
            $"{Path.DirectorySeparatorChar}ExpectedFailures{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase);
    }

    // "error" in a line is what makes the workspace need attention, so an expected failure says "expected".
    private static IReadOnlyList<string> FormatDiagnostics(IEnumerable<BddDiagnostic> diagnostics, bool expected)
    {
        return diagnostics
            .Select(diagnostic => $"{diagnostic.Location}: {(expected ? "expected" : Severity(diagnostic.Severity))} {diagnostic.Code} {diagnostic.Message}")
            .ToArray();
    }

    private static string Severity(BddDiagnosticSeverity severity) => severity == BddDiagnosticSeverity.Error ? "error" : "warning";
    private static string FormatDiscoveryReport(BddConfig config)
    {
        var assemblies = config.Assemblies.Count == 0
            ? "- No assemblies registered."
            : string.Join(Environment.NewLine, config.Assemblies.Select(assembly => $"- {assembly.Kind}: {assembly.Assembly}"));

        return string.Join(
            Environment.NewLine,
            "Discovery report:",
            $"Target: {config.Runtime.GetValueOrDefault("Target", "(not set)")}",
            $"Fixture: {config.Runtime.GetValueOrDefault("Fixture", "(not set)")}",
            assemblies);
    }

    private static string FormatCatalog(BddCommandCatalog catalog)
    {
        return "Command catalog:" + Environment.NewLine + string.Join(
            Environment.NewLine,
            catalog.Patterns
                .OrderBy(pattern => pattern.Keyword)
                .ThenBy(pattern => pattern.Phrase, StringComparer.Ordinal)
                .Select(pattern => $"- {pattern.Keyword}: {pattern.Phrase} -> {pattern.CommandId}"));
    }
}
