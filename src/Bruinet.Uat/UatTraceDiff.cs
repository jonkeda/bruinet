using System.Text.Json.Nodes;

namespace Bruinet.Uat;

/// <summary>Where a failing run first went another way than a passing one (AI9).</summary>
/// <param name="Kind">
/// <c>scenario-changed</c> (the steps are not the same), <c>page-changed</c> (the page or the controls driven differ),
/// <c>timing-only</c> (the same calls, the failing step waited and errored where it passed), <c>data-changed</c> (the
/// same path, other values or app events), or <c>none</c> (no step differs).
/// </param>
/// <param name="Index">The step's place in the scenario, from 0; -1 for none.</param>
/// <param name="Line">The step's line in the feature file (its call's line for a callable's step).</param>
/// <param name="Text">The step's text.</param>
/// <param name="Differences">What differs at that step, green first, then failing.</param>
/// <param name="Slower">Steps before it that took much longer (three times, half a second more): a hint, not a cause.</param>
public sealed record UatTraceDifference(string Kind, int Index, int? Line, string? Text, IReadOnlyList<string> Differences, IReadOnlyList<string> Slower)
{
    /// <summary>One line for a summary: <c>line 27 (page-changed): Todo Detail then Todo Edit</c>.</summary>
    public string Summary => Kind == "none"
        ? "no step differs from the last green run"
        : $"first differs from the last green run at line {Line} ({Kind}): {(Differences.FirstOrDefault() is { Length: > 160 } first ? first[..160] + "…" : Differences.FirstOrDefault())}";
}

/// <summary>
/// Compares two step traces of one scenario (<see cref="UatScenarioTrace"/>), the newest failing one against the newest
/// passing one: steps in order, each pair by its page, the controls and actions it drove, what the app said, what the page
/// showed after it, and its status. The first pair that differs is where the runs parted.
/// </summary>
public static partial class UatTraceDiff
{
    /// <summary>Where <paramref name="failing"/> first differs from <paramref name="green"/>.</summary>
    /// <param name="failing">The failing run's trace.</param>
    /// <param name="green">The passing run's trace.</param>
    public static UatTraceDifference Compare(JsonObject failing, JsonObject green)
    {
        ArgumentNullException.ThrowIfNull(failing);
        ArgumentNullException.ThrowIfNull(green);
        var bad = Steps(failing);
        var good = Steps(green);
        List<string> slower = [];
        for (var index = 0; index < Math.Max(bad.Count, good.Count); index++)
        {
            if (index >= bad.Count || index >= good.Count || Key(bad[index]) != Key(good[index]))
            {
                var step = index < bad.Count ? bad[index] : good[index];
                return Found("scenario-changed", index, step, [$"step: {Describe(good, index)} then {Describe(bad, index)}"], slower);
            }

            var (then, now) = (good[index], bad[index]);
            if (Differences(then, now) is { Count: > 0 } differences)
            {
                return Found(KindOf(then, now), index, now, differences, slower);
            }

            if (Ms(now) is { } nowMs && Ms(then) is { } thenMs && nowMs > 3 * thenMs && nowMs - thenMs > 500)
            {
                slower.Add($"line {Line(now)}: {thenMs} ms then {nowMs} ms");
            }
        }

        return new UatTraceDifference("none", -1, null, null, [], slower);
    }

    private static UatTraceDifference Found(string kind, int index, JsonObject step, IReadOnlyList<string> differences, List<string> slower)
        => new(kind, index, Line(step), (string?)step["text"], differences, slower);

    // What differs between a step's two runs, in the order a reader looks: where it was, what it did, what was said, what showed, how it ended.
    private static List<string> Differences(JsonObject then, JsonObject now)
    {
        List<string> differences = [];
        if (!SamePage(then, now))
        {
            differences.Add($"page: {(string?)then["page"] ?? "none"} then {(string?)now["page"] ?? "none"}");
        }

        var (calledThen, calledNow) = (Driven(then), Driven(now));
        if (calledNow.Except(calledThen).ToList() is { Count: > 0 } added)
        {
            differences.Add($"drove: {Join(added)}, which the green run did not");
        }
        else if (calledThen.Except(calledNow).ToList() is { Count: > 0 } left && (string?)then["status"] == (string?)now["status"])
        {
            differences.Add($"did not drive: {Join(left)}");
        }

        var (saidThen, saidNow) = (Said(then), Said(now));
        if (saidNow.Except(saidThen).ToList() is { Count: > 0 } newly)
        {
            differences.Add($"app said: {Join(newly)}");
        }

        foreach (var (name, before, after) in Shown(then, now))
        {
            differences.Add($"{name}: {before} then {after}");
        }

        if ((string?)then["status"] != (string?)now["status"])
        {
            differences.Add($"status: {(string?)then["status"]} then {(string?)now["status"]}" + ((string?)now["message"] is { } message ? $" ({Cut(message)})" : string.Empty));
        }

        return differences;
    }

    private static string KindOf(JsonObject then, JsonObject now)
    {
        // Another page, or a control the green run never drove: the way through the app changed. A failing step that
        // stopped early drove less, which is not a change of way.
        if (!SamePage(then, now) || Driven(now).Except(Driven(then)).Any())
        {
            return "page-changed";
        }

        // The same calls, and the failing one ran out of time (a wait that errored, a message that says so) where the green
        // one answered: only time differs. A wait for a value that never came is data.
        var waited = Calls(now).Any(call => (string?)call["result"] == "error" && ((int?)call["attempts"] ?? 1) > 1);
        var timedOut = (string?)now["message"] is { } message && TimedOut().IsMatch(message);
        var sameElse = !Said(now).Except(Said(then)).Any() && !Shown(then, now).Any();
        return waited && timedOut && sameElse ? "timing-only" : "data-changed";
    }

    // A control's value after the step, where both runs read the page: the ones that differ.
    private static IEnumerable<(string Name, string Before, string After)> Shown(JsonObject then, JsonObject now)
    {
        if (then["pageAfter"]?["controls"] is not JsonArray before || now["pageAfter"]?["controls"] is not JsonArray after)
        {
            yield break;
        }

        var was = before.OfType<JsonObject>().ToDictionary(control => (string?)control["name"] ?? string.Empty, State, StringComparer.Ordinal);
        foreach (var control in after.OfType<JsonObject>())
        {
            var name = (string?)control["name"] ?? string.Empty;
            if (was.TryGetValue(name, out var old) && old != State(control))
            {
                yield return (name, old, State(control));
            }
        }
    }

    private static string State(JsonObject control)
        => (bool?)control["shown"] == false ? "hidden" : (string?)control["value"] is { } value ? $"'{Cut(value)}'" : "shown";

    private static List<JsonObject> Steps(JsonObject trace) => trace["steps"]?.AsArray().OfType<JsonObject>().ToList() ?? [];

    private static IEnumerable<JsonObject> Calls(JsonObject step) => step["calls"]?.AsArray().OfType<JsonObject>() ?? [];

    // The controls and actions driven, each once, in the order first driven: a wait polls the same few again and again.
    private static List<string> Driven(JsonObject step)
        => [.. Calls(step).Select(call => $"{(string?)call["control"]}.{(string?)call["action"]}").Distinct(StringComparer.Ordinal)];

    // The same page, however it was named: a page object's type ("TodoListPage") or its UAT name ("Todo List").
    private static bool SamePage(JsonObject then, JsonObject now) => Normal((string?)then["page"]) == Normal((string?)now["page"]);

    private static string? Normal(string? page)
    {
        var compact = page?.Replace(" ", string.Empty, StringComparison.Ordinal);
        return compact is not null && compact.EndsWith("Page", StringComparison.Ordinal) && compact.Length > 4 ? compact[..^4] : compact;
    }

    private static List<string> Said(JsonObject step)
        => [.. step["appEvents"]?.AsArray().OfType<JsonObject>().Select(said => $"{(string?)said["level"]} {Cut((string?)said["message"] ?? string.Empty)}") ?? []];

    private static string Key(JsonObject step) => $"{(string?)step["file"]}|{(int?)step["line"]}|{(int?)step["callLine"]}|{(string?)step["text"]}";

    private static int? Line(JsonObject step) => (int?)step["callLine"] ?? (int?)step["line"];

    private static long? Ms(JsonObject step) => (long?)step["durationMs"];

    private static string Describe(List<JsonObject> steps, int index)
        => index < steps.Count ? $"line {Line(steps[index])} '{(string?)steps[index]["text"]}'" : "none";

    private static string Join(IEnumerable<string> items) => items.Any() ? string.Join(", ", items.Take(6)) : "nothing";

    private static string Cut(string text) => text.Length > 80 ? text[..80] + "…" : text;

    [System.Text.RegularExpressions.GeneratedRegex(@"time(d)? ?out|did not become|within \d|not .* in time", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex TimedOut();

    /// <summary>Reads a trace file; null when it is missing or not one.</summary>
    /// <param name="path">The trace's path.</param>
    public static JsonObject? Read(string path)
    {
        try
        {
            return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or IOException)
        {
            return null;
        }
    }

    /// <summary>A passing trace's copy beside it, which a later failure in the same run folder does not overwrite.</summary>
    /// <param name="tracePath">The trace.</param>
    public static string GreenOf(string tracePath)
    {
        ArgumentNullException.ThrowIfNull(tracePath);
        return tracePath.EndsWith("__trace.json", StringComparison.Ordinal) ? tracePath[..^"__trace.json".Length] + "__trace.green.json" : tracePath + ".green";
    }

    /// <summary>
    /// The newest passing trace of the same scenario written before <paramref name="tracePath"/>: its green copy in this run's
    /// folder or in an earlier run's (the newest twenty). Null when none passed before.
    /// </summary>
    /// <param name="tracePath">The failing run's trace.</param>
    public static string? LastGreen(string tracePath)
    {
        ArgumentNullException.ThrowIfNull(tracePath);
        // The run's folder is the one with the suites in it (<root>/<run>/suites/<suite>/...); its siblings are the other runs.
        var full = Path.GetFullPath(tracePath);
        var run = Path.GetDirectoryName(full);
        while (run is not null && !Directory.Exists(Path.Combine(run, "suites")))
        {
            run = Path.GetDirectoryName(run);
        }

        if (run is null || Path.GetDirectoryName(run) is not { } root)
        {
            return null;
        }

        var relative = Path.GetRelativePath(run, GreenOf(full));
        var failedAt = File.Exists(full) ? File.GetLastWriteTimeUtc(full) : DateTime.MaxValue;
        return Directory.EnumerateDirectories(root)
            .OrderByDescending(Directory.GetLastWriteTimeUtc)
            .Take(20)
            .Select(other => Path.Combine(other, relative))
            .Where(candidate => File.Exists(candidate) && File.GetLastWriteTimeUtc(candidate) <= failedAt)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault(candidate => Read(candidate) is { } trace && (string?)trace["status"] == "passed");
    }
}
