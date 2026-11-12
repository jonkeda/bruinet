using System.Reflection;
using Bruinet.Core.Artifacts;
using Bruinet.Core.Interfaces;

namespace Bruinet.Uat;

/// <summary>
/// Writes a scenario's result where the IDEs and the BDD Explorer read it: its report
/// (<c>__result.json</c>) and its record in the run's manifest (P5 of AD-022, AD-007). The gate
/// (<see cref="UatScenarioTestBase{TFixture}"/>) and the runner process write through it alike, so a
/// scenario run from either shows the same way.
/// </summary>
public static class UatScenarioResults
{
    /// <summary>A run by the xUnit gate.</summary>
    public const string TestMode = "test";

    /// <summary>A run in the suite's runner process, driven by a host (the Presenter, an IDE).</summary>
    public const string RunnerMode = "runner";

    /// <summary>
    /// Writes the report and the manifest record. A disk or folder that will not take them leaves the outcome as it is
    /// and says so on stderr; a fault in building them is thrown.
    /// </summary>
    /// <param name="runtime">The suite's runtime: its config, discovery and output folder.</param>
    /// <param name="catalog">The command catalog, for a report with the runtime trace.</param>
    /// <param name="scenario">The scenario that ran.</param>
    /// <param name="execution">How it ran.</param>
    /// <param name="duration">How long it took.</param>
    /// <param name="suite">The suite's name: its project's assembly.</param>
    /// <param name="evidenceName">The scenario's name as files are named after it.</param>
    /// <param name="mode"><see cref="TestMode"/> or <see cref="RunnerMode"/>.</param>
    /// <returns>The report's path; null when the disk or folder would not take it.</returns>
    public static string? Record(
        UatRuntime runtime,
        BddCommandCatalog catalog,
        BddBoundScenario scenario,
        BddScenarioExecutionResult execution,
        TimeSpan duration,
        string? suite,
        string evidenceName,
        string mode)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(execution);
        try
        {
            var status = Status(execution.Result);
            var directory = runtime.Config.Reporting.OutputDirectory;
            Directory.CreateDirectory(directory);
            var file = scenario.Source.Source.FilePath;
            var reportPath = Path.Combine(directory, UatScenarioReport.FileName(file, evidenceName));
            var trace = runtime.Config.Reporting.IncludeRuntimeTrace
                ? new UatReportTrace(catalog, runtime.DiscoveryReport)
                : null;

            var tracePath = WriteTrace(Path.Combine(directory, "step-traces", UatScenarioTrace.FileName(file, evidenceName)), scenario, execution, status, runtime.ConfigDirectory);

            File.WriteAllText(reportPath, UatScenarioReport.Build(scenario, execution, status, duration, runtime.ConfigDirectory, trace, tracePath));

            // Since last green (AI9): where a failure first went another way than the newest earlier pass, for summary.md.
            var why = status == "failed" && tracePath is not null ? Why(tracePath) : null;
            var failureKind = status == "failed"
                ? (string?)execution.Result.Steps.Select(UatStepFailure.Of).FirstOrDefault(failure => failure is not null)?["kind"]
                : null;

            // The suite, file and line let a tool find the newest run of a file's scenarios (P5, AD-007).
            TestArtifactManifestWriter.RecordArtifact(
                reportPath,
                "uat-scenario",
                scenario.Source.Name,
                status,
                new Dictionary<string, string?>
                {
                    ["target"] = runtime.Config.Runtime.TryGetValue("Target", out var target) ? target : null,
                    ["fixture"] = runtime.Config.Runtime.TryGetValue("Fixture", out var fixture) ? fixture : null,
                    ["evidencePath"] = execution.EvidencePath,
                    ["suite"] = suite,
                    ["file"] = file is null ? null : Path.GetRelativePath(runtime.ConfigDirectory, Path.GetFullPath(file)).Replace('\\', '/'),
                    ["line"] = scenario.Source.Source.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["mode"] = mode,
                    ["detail"] = why,
                    ["failureKind"] = failureKind,
                });
            return reportPath;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A disk or folder that will not take the report does not change the scenario's outcome, but it is said:
            // without this file the tools read the previous run's, and could take it for this one. Any other
            // exception is a fault in building the report and fails the test like any other (AD-009).
            Console.Error.WriteLine($"bruinet: the result of '{scenario.Source.Name}' was not written: {exception.Message}");
            return null;
        }
    }

    // The step trace (AI8): every scenario that passed or failed; a pass's file is its green one (AI9), which a later failure
    // in the same run folder does not overwrite, and a failure's is __trace.json. None with BRUINET_TRACE=off, or for a
    // skipped one. Whatever stops it leaves the report to be written: null then.
    private static string? WriteTrace(string path, BddBoundScenario scenario, BddScenarioExecutionResult execution, string status, string configDirectory)
    {
        try
        {
            if (!Bruinet.Core.Logging.StepTrace.Enabled || status is not ("failed" or "passed"))
            {
                File.Delete(path);
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var written = status == "passed" ? UatTraceDiff.GreenOf(path) : path;
            File.WriteAllText(written, UatScenarioTrace.Build(scenario, execution, status, configDirectory));
            if (written != path)
            {
                File.Delete(path);
            }

            return written;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    // The failure against the newest green run's trace: one line; null when no earlier run passed, or it cannot be read.
    private static string? Why(string tracePath)
    {
        try
        {
            return UatTraceDiff.LastGreen(tracePath) is { } green && UatTraceDiff.Read(green) is { } passed && UatTraceDiff.Read(tracePath) is { } failed
                ? UatTraceDiff.Compare(failed, passed).Summary
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary><c>passed</c>, <c>skipped</c>, <c>canceled</c> or <c>failed</c>.</summary>
    /// <param name="result">The scenario's result.</param>
    public static string Status(BddScenarioRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Passed)
        {
            return "passed";
        }

        if (result.Skipped)
        {
            return "skipped";
        }

        return result.Steps.Any(step => step.Status == BddStepResultStatus.Canceled)
            ? "canceled"
            : "failed";
    }

    /// <summary>
    /// Writes the result of a scenario that did not bind, so the tools that read a run's results (list_results,
    /// get_trace) show it as failed with kind <c>bind</c>, not as never run (review 01, finding 4). One failed step at
    /// the first error's line, its message the diagnostics; the disk refusing it changes nothing, as for a run.
    /// </summary>
    /// <param name="runtime">The suite's runtime: its config and output folder.</param>
    /// <param name="scenario">The scenario, as the file was parsed.</param>
    /// <param name="diagnostics">Why it did not bind.</param>
    /// <param name="suite">The suite's name: its project's assembly.</param>
    /// <param name="mode"><see cref="TestMode"/> or <see cref="RunnerMode"/>.</param>
    /// <returns>The report's path; null when the disk or folder would not take it.</returns>
    public static string? RecordBindFailure(UatRuntime runtime, BddScenario scenario, IReadOnlyList<BddDiagnostic> diagnostics, string? suite, string mode)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(diagnostics);
        try
        {
            var directory = runtime.Config.Reporting.OutputDirectory;
            Directory.CreateDirectory(directory);
            var file = scenario.Source.FilePath;
            var invalid = Path.GetInvalidFileNameChars();
            var name = string.Concat(scenario.Name.Select(ch => invalid.Contains(ch) ? '_' : ch));
            name = name.Length <= 80 ? name : name[..80];
            var reportPath = Path.Combine(directory, UatScenarioReport.FileName(file, name));
            var errors = diagnostics.Where(diagnostic => diagnostic.Severity == BddDiagnosticSeverity.Error).ToList();
            var first = errors.FirstOrDefault()?.Location.LineNumber ?? scenario.Source.LineNumber;
            var message = string.Join("\n", errors.Select(diagnostic => $"{diagnostic.Code} line {diagnostic.Location.LineNumber}: {diagnostic.Message}"));
            var relative = file is null ? null : Path.GetRelativePath(runtime.ConfigDirectory, Path.GetFullPath(file)).Replace('\\', '/');
            var report = new System.Text.Json.Nodes.JsonObject
            {
                ["scenario"] = scenario.Name,
                ["status"] = "failed",
                ["durationMs"] = 0,
                ["steps"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
                {
                    ["line"] = first,
                    ["file"] = relative,
                    ["status"] = "Failed",
                    ["message"] = message,
                    ["failure"] = new System.Text.Json.Nodes.JsonObject { ["kind"] = "bind", ["message"] = message },
                }),
            };
            File.WriteAllText(reportPath, report.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            TestArtifactManifestWriter.RecordArtifact(
                reportPath,
                "uat-scenario",
                scenario.Name,
                "failed",
                new Dictionary<string, string?>
                {
                    ["suite"] = suite,
                    ["file"] = relative,
                    ["line"] = scenario.Source.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["mode"] = mode,
                    ["failureKind"] = "bind",
                });
            return reportPath;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"bruinet: the result of '{scenario.Name}' was not written: {exception.Message}");
            return null;
        }
    }

    /// <summary>A scenario's name as files are named after it: no characters a path refuses, and not too long.</summary>
    /// <param name="scenario">The scenario.</param>
    /// <param name="maxLength">The longest name.</param>
    public static string EvidenceName(BddBoundScenario scenario, int maxLength = 80)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(scenario.Source.Name.Select(ch => invalidChars.Contains(ch) ? '_' : ch));
        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength];
    }

    /// <summary>
    /// The fixture's screenshots, for a failure's evidence: the fixture itself, its public
    /// <c>ScreenshotService</c>, or the <see cref="IScreenshotSource"/> it is or holds in a public
    /// property (its test context, as Html's Playwright context is).
    /// </summary>
    /// <param name="fixture">The suite's fixture.</param>
    public static IScreenshotService? ScreenshotServiceOf(object fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        if (fixture is IScreenshotService screenshotService)
        {
            return screenshotService;
        }

        var property = fixture.GetType().GetProperty("ScreenshotService", BindingFlags.Instance | BindingFlags.Public);
        if (property is not null && typeof(IScreenshotService).IsAssignableFrom(property.PropertyType))
        {
            return property.GetValue(fixture) as IScreenshotService;
        }

        if (fixture is IScreenshotSource source)
        {
            return source.Screenshots;
        }

        return fixture.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(candidate => candidate.GetIndexParameters().Length == 0)
            .Select(candidate => ValueOrNull(candidate, fixture))
            .OfType<IScreenshotSource>()
            .Select(holder => holder.Screenshots)
            .FirstOrDefault();
    }

    // A property that throws (a fixture not started) holds no screenshots.
    private static object? ValueOrNull(PropertyInfo property, object owner)
    {
        try
        {
            return property.GetValue(owner);
        }
        catch (TargetInvocationException)
        {
            return null;
        }
    }
}
