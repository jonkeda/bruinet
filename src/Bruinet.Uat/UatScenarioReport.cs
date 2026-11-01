using System.Text.Json;
using System.Text.Json.Nodes;
using Bruinet.Core.Testing;

namespace Bruinet.Uat;

/// <summary>
/// A scenario's <c>__result.json</c>: its file and line, its status and duration, and each step's
/// line, status and message, whatever the trace setting (P5 of the IDE plan), so a tool can show a
/// run at the lines it ran. The trace adds each step's binding and the catalog.
/// </summary>
internal static class UatScenarioReport
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>
    /// The report's file name: the feature file's name, then the scenario's, so two files with a
    /// scenario of the same name do not share a report.
    /// </summary>
    /// <param name="featureFile">The feature file.</param>
    /// <param name="scenarioName">The scenario's name, sanitized for a file name.</param>
    public static string FileName(string? featureFile, string scenarioName)
    {
        var stem = featureFile is null ? null : Path.GetFileName(featureFile);
        stem = stem?.EndsWith(BddFileFormat.FeatureExtension, StringComparison.OrdinalIgnoreCase) == true
            ? stem[..^BddFileFormat.FeatureExtension.Length]
            : stem is null ? null : Path.GetFileNameWithoutExtension(stem);
        return stem is null ? $"{scenarioName}__result.json" : $"{stem}__{scenarioName}__result.json";
    }

    /// <summary>The report as JSON.</summary>
    /// <param name="scenario">The scenario that ran.</param>
    /// <param name="execution">How it ran.</param>
    /// <param name="status"><c>passed</c>, <c>failed</c>, <c>skipped</c> or <c>canceled</c>.</param>
    /// <param name="duration">How long it took.</param>
    /// <param name="configDirectory">The config's folder, which file paths are relative to.</param>
    /// <param name="trace">The binding and catalog detail, when the config's <c>IncludeRuntimeTrace</c> asks for it; null otherwise.</param>
    public static string Build(
        BddBoundScenario scenario,
        BddScenarioExecutionResult execution,
        string status,
        TimeSpan duration,
        string configDirectory,
        UatReportTrace? trace)
    {
        var source = scenario.Source.Source;
        var report = new JsonObject
        {
            ["scenario"] = scenario.Source.Name,
            ["file"] = Relative(configDirectory, source.FilePath),
            ["line"] = source.LineNumber,
            ["status"] = status,
            ["durationMs"] = (long)duration.TotalMilliseconds,
            ["tags"] = new JsonArray([.. scenario.Source.Tags.Select(tag => (JsonNode?)tag)]),
            ["evidencePath"] = execution.EvidencePath,
            ["skipReason"] = execution.Result.SkipDecision?.Reason,
            ["head"] = WebHeads.Current.ToString().ToLowerInvariant(),
            ["steps"] = new JsonArray([.. execution.Result.Steps.Select(step => (JsonNode?)Step(step, configDirectory, trace is not null))]),
        };

        if (trace is not null)
        {
            report["diagnostics"] = new JsonArray([.. execution.Context.Diagnostics.Select(line => (JsonNode?)line)]);
            report["discovery"] = JsonSerializer.SerializeToNode(trace.Discovery);
            report["commandCatalog"] = new JsonArray([.. trace.Catalog.Patterns.Select(pattern => (JsonNode?)new JsonObject
            {
                ["keyword"] = pattern.Keyword.ToString(),
                ["phrase"] = pattern.Phrase,
                ["commandId"] = pattern.CommandId,
            })]);
        }

        return report.ToJsonString(Json);
    }

    // Always the lines and the outcome; with the trace, what the step bound to and its call chain.
    private static JsonObject Step(BddStepResult step, string configDirectory, bool trace)
    {
        var invocation = step.Invocation;
        var outermost = invocation.Call?.Chain.FirstOrDefault();
        var node = new JsonObject
        {
            ["line"] = invocation.Step.Source.LineNumber,
            ["file"] = Relative(configDirectory, invocation.Step.Source.FilePath),
            ["callLine"] = outermost?.Call.Source.LineNumber,
            ["status"] = step.Status.ToString(),
            ["message"] = step.Message,
            ["durationMs"] = step.Duration is { } duration ? (long)duration.TotalMilliseconds : null,
        };

        if (UatStepFailure.Of(step) is { } failure)
        {
            node["failure"] = failure;
        }

        if (trace)
        {
            node["source"] = invocation.Step.Source.ToString();
            node["keyword"] = invocation.Step.EffectiveKeyword.ToString();
            node["app"] = invocation.App;
            node["commandId"] = invocation.CommandId;
            node["text"] = invocation.Step.Text;
            node["call"] = invocation.Call is null
                ? null
                : new JsonArray([.. invocation.Call.Chain.Select(frame => (JsonNode?)new JsonObject
                {
                    ["phrase"] = frame.Callable.Phrase,
                    ["source"] = frame.Location.ToString(),
                    ["row"] = frame.Row,
                })]);
        }

        return node;
    }

    private static string? Relative(string configDirectory, string? path)
        => path is null ? null : Path.GetRelativePath(configDirectory, Path.GetFullPath(path)).Replace('\\', '/');
}

/// <summary>What <c>IncludeRuntimeTrace</c> adds to a report.</summary>
/// <param name="Catalog">The catalog the scenario bound against.</param>
/// <param name="Discovery">The runtime's discovery report.</param>
internal sealed record UatReportTrace(BddCommandCatalog Catalog, object? Discovery);
