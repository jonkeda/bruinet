using System.Text.Json;
using System.Text.Json.Nodes;
using Bruinet.Core.Logging;

namespace Bruinet.Uat;

/// <summary>
/// A scenario's step trace (AI8), <c>step-traces/&lt;feature&gt;__&lt;scenario&gt;__trace.json</c> beside its report: per step,
/// the control calls it made (a wait shows as a long call or many attempts), what the app said, and the page's controls
/// after it, read after a failed step and, with <c>BRUINET_TRACE=all</c>, after each. Text for an agent; a viewer for
/// people reads the same file.
/// </summary>
internal static class UatScenarioTrace
{
    private const int CallsKept = 50;
    private const int ControlsKept = 60;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>The trace's file name: the report's, ending in <c>__trace.json</c>.</summary>
    /// <param name="featureFile">The feature file.</param>
    /// <param name="scenarioName">The scenario's name, sanitized for a file name.</param>
    public static string FileName(string? featureFile, string scenarioName)
        => UatScenarioReport.FileName(featureFile, scenarioName)[..^"__result.json".Length] + "__trace.json";

    /// <summary>The trace as JSON.</summary>
    /// <param name="scenario">The scenario that ran.</param>
    /// <param name="execution">How it ran.</param>
    /// <param name="status"><c>passed</c>, <c>failed</c>, <c>skipped</c> or <c>canceled</c>.</param>
    /// <param name="configDirectory">The config's folder, which file paths are relative to.</param>
    public static string Build(BddBoundScenario scenario, BddScenarioExecutionResult execution, string status, string configDirectory)
    {
        var trace = new JsonObject
        {
            ["scenario"] = scenario.Source.Name,
            ["file"] = UatScenarioReport.Relative(configDirectory, scenario.Source.Source.FilePath),
            ["line"] = scenario.Source.Source.LineNumber,
            ["status"] = status,
            ["commit"] = GitHead(configDirectory),
            ["steps"] = new JsonArray([.. execution.Result.Steps.Select(step => (JsonNode?)Step(step, configDirectory))]),
        };
        return Pruned(trace).ToJsonString(Json);
    }

    private static JsonObject Step(BddStepResult step, string configDirectory)
    {
        var source = step.Invocation.Step.Source;
        var calls = step.Calls;
        var node = new JsonObject
        {
            ["line"] = source.LineNumber,
            ["file"] = UatScenarioReport.Relative(configDirectory, source.FilePath),
            ["callLine"] = step.Invocation.Call?.Chain.FirstOrDefault()?.Call.Source.LineNumber,
            ["text"] = step.Invocation.Step.Text,
            ["status"] = step.Status.ToString(),
            ["durationMs"] = step.Duration is { } duration ? (long)duration.TotalMilliseconds : null,
            ["page"] = step.PageAfter?.Page ?? calls.LastOrDefault()?.Page,
            ["message"] = step.Status == BddStepResultStatus.Failed ? step.Message : null,
            ["calls"] = new JsonArray([.. Kept(calls).Select(call => (JsonNode?)Call(call))]),
        };

        if (calls.Count > CallsKept)
        {
            node["callsLeftOut"] = calls.Count - CallsKept;
        }

        if (step.AppEvents.Count > 0)
        {
            node["appEvents"] = UatScenarioReport.AppEvents(step);
        }

        if (step.PageAfter is { } page)
        {
            node["pageAfter"] = new JsonObject
            {
                ["page"] = page.Page,
                ["reason"] = page.Reason,
                ["controls"] = new JsonArray([.. page.Controls.Take(ControlsKept).Select(control => (JsonNode?)new JsonObject
                {
                    ["name"] = control.Name,
                    ["kind"] = control.Kind,
                    ["shown"] = control.Shown,
                    ["value"] = control.Value,
                    ["unreadable"] = control.Unreadable,
                })]),
            };
        }

        return node;
    }

    // The commit the checkout was on (AI9 diffs the files since a green run's): .git's HEAD, followed to its ref, read
    // without running git; null outside a repository.
    internal static string? GitHead(string folder)
    {
        try
        {
            for (var at = Path.GetFullPath(folder); at is not null; at = Path.GetDirectoryName(at))
            {
                var dotGit = Path.Combine(at, ".git");
                var gitDir = Directory.Exists(dotGit) ? dotGit
                    : File.Exists(dotGit) && File.ReadAllText(dotGit).Trim() is var link && link.StartsWith("gitdir:", StringComparison.Ordinal)
                        ? Path.GetFullPath(link["gitdir:".Length..].Trim(), at)
                        : null;
                if (gitDir is null)
                {
                    continue;
                }

                var head = File.ReadAllText(Path.Combine(gitDir, "HEAD")).Trim();
                if (!head.StartsWith("ref: ", StringComparison.Ordinal))
                {
                    return head;
                }

                var name = head[5..];
                var common = File.Exists(Path.Combine(gitDir, "commondir")) ? Path.GetFullPath(File.ReadAllText(Path.Combine(gitDir, "commondir")).Trim(), gitDir) : gitDir;
                foreach (var refs in new[] { gitDir, common })
                {
                    if (File.Exists(Path.Combine(refs, name)))
                    {
                        return File.ReadAllText(Path.Combine(refs, name)).Trim();
                    }
                }

                var packed = Path.Combine(common, "packed-refs");
                return File.Exists(packed)
                    ? File.ReadLines(packed).Select(line => line.Split(' ')).FirstOrDefault(parts => parts.Length == 2 && parts[1] == name)?[0]
                    : null;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return null;
    }

    // Without its empty fields: a smaller file for an agent to read.
    private static JsonNode Pruned(JsonNode node)
    {
        if (node is JsonObject json)
        {
            foreach (var name in json.Where(field => field.Value is null).Select(field => field.Key).ToList())
            {
                json.Remove(name);
            }

            foreach (var field in json)
            {
                Pruned(field.Value!);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array.OfType<JsonNode>())
            {
                Pruned(item);
            }
        }

        return node;
    }

    // At most the cap: the slowest when there are more, in the order they ran.
    private static IEnumerable<StepTraceCall> Kept(IReadOnlyList<StepTraceCall> calls)
        => calls.Count <= CallsKept ? calls : calls.OrderByDescending(call => call.DurationMs).Take(CallsKept).OrderBy(call => call.AtMs);

    private static JsonObject Call(StepTraceCall call) => new()
    {
        ["page"] = call.Page,
        ["control"] = call.Control,
        ["action"] = call.Action,
        ["ms"] = call.DurationMs,
        ["attempts"] = call.Attempts,
        ["result"] = call.Result,
    };
}
