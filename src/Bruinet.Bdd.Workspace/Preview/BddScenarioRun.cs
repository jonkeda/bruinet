using System.Text.Json;

namespace Bruinet.Bdd.Preview;

/// <summary>A scenario's last run, as its <c>__result.json</c> reports it (P5).</summary>
/// <param name="Scenario">The scenario's name; an outline row's has its <c>[n]</c>.</param>
/// <param name="Status"><c>passed</c>, <c>failed</c>, <c>skipped</c> or <c>canceled</c>.</param>
/// <param name="DurationMs">How long it took.</param>
/// <param name="EvidencePath">The screenshot taken at a failure; null when none.</param>
/// <param name="Steps">Each step that ran.</param>
/// <param name="At">When the report was written.</param>
public sealed record BddScenarioRun(string Scenario, string Status, long DurationMs, string? EvidencePath, IReadOnlyList<BddStepRun> Steps, DateTimeOffset At)
{
    /// <summary>Reads a report; null when it is not one this version can read (no step lines).</summary>
    /// <param name="json">The report's text.</param>
    /// <param name="at">When it was written.</param>
    public static BddScenarioRun? Parse(string json, DateTimeOffset at)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("scenario", out var scenario) || !root.TryGetProperty("steps", out var steps))
            {
                return null;
            }

            var runs = steps.EnumerateArray()
                .Where(step => step.TryGetProperty("line", out var line) && line.ValueKind == JsonValueKind.Number)
                .Select(step => new BddStepRun(
                    step.GetProperty("line").GetInt32(),
                    Text(step, "file"),
                    step.TryGetProperty("callLine", out var call) && call.ValueKind == JsonValueKind.Number ? call.GetInt32() : null,
                    Text(step, "status") ?? "Passed",
                    Text(step, "message"))
                {
                    DurationMs = step.TryGetProperty("durationMs", out var ms) && ms.ValueKind == JsonValueKind.Number ? ms.GetInt64() : null,
                    Failure = step.TryGetProperty("failure", out var failure) && failure.ValueKind == JsonValueKind.Object ? failure.GetRawText() : null,
                })
                .ToList();
            return new BddScenarioRun(
                scenario.GetString() ?? string.Empty,
                Text(root, "status") ?? "failed",
                root.TryGetProperty("durationMs", out var duration) && duration.ValueKind == JsonValueKind.Number ? duration.GetInt64() : 0,
                Text(root, "evidencePath"),
                runs,
                at);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>A step that ran.</summary>
/// <param name="Line">The step's own line, in <paramref name="File"/>.</param>
/// <param name="File">Where the step is written, relative to the config: the feature file, or a step file for a callable's step.</param>
/// <param name="CallLine">For a callable's step, the line of the call in the feature file; null otherwise.</param>
/// <param name="Status"><c>Passed</c>, <c>Failed</c>, <c>Skipped</c> or <c>Canceled</c>.</param>
/// <param name="Message">Why it failed, or what it said.</param>
public sealed record BddStepRun(int Line, string? File, int? CallLine, string Status, string? Message)
{
    /// <summary>The line the step is written on in the feature file: the call's, for a callable's step.</summary>
    public int WrittenLine => CallLine ?? Line;

    /// <summary>How long it ran; null in a report written before steps were timed.</summary>
    public long? DurationMs { get; init; }

    /// <summary>A failed step's failure as the report gives it (kind, expected, actual, locator, binding, frames), as JSON; null otherwise.</summary>
    public string? Failure { get; init; }
}
