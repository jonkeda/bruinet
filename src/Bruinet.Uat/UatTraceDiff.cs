using System.Text.Json.Nodes;

namespace Bruinet.Uat;

/// <summary>Where a failing run first went another way than a passing one (AI9).</summary>
/// <param name="Kind">
/// <c>scenario-changed</c> (the steps are not the same), <c>page-changed</c> (another page, or a control the green run never
/// drove), <c>timing-only</c> (the same calls, and the call that failed already took most of its time in the green run),
/// <c>data-changed</c> (the same path, other values or app events), or <c>none</c> (no step differs).
/// </param>
/// <param name="Index">The step's place in the scenario, from 0; -1 for none.</param>
/// <param name="Line">The failing run's line of the step (its call's line for a callable's step).</param>
/// <param name="Text">The step's text.</param>
/// <param name="Differences">What differs at that step, green first, then failing; ten at most.</param>
/// <param name="Slower">Steps before it that took much longer (three times, half a second more): a hint, not a cause.</param>
internal sealed record UatTraceDifference(string Kind, int Index, int? Line, string? Text, IReadOnlyList<string> Differences, IReadOnlyList<string> Slower)
{
    /// <summary>One line for a summary: <c>line 27 (page-changed): page: Todo Detail then Todo Edit</c>.</summary>
    public string Summary => Kind == UatTraceDiff.None
        ? "no step differs from the last green run"
        : $"first differs from the last green run at line {Line} ({Kind}): {UatTraceDiff.Cut(Differences.FirstOrDefault() ?? string.Empty, 160)}";
}

/// <summary>
/// Compares two step traces of one scenario (<see cref="UatScenarioTrace"/>), the newest failing one against the newest
/// passing one: steps in order, each pair by its page, the controls and actions it drove, what the app said, what the page
/// showed after it, and its status. The first pair that differs is where the runs parted.
/// </summary>
internal static class UatTraceDiff
{
    public const string None = "none";
    public const string ScenarioChanged = "scenario-changed";
    public const string PageChanged = "page-changed";
    public const string TimingOnly = "timing-only";
    public const string DataChanged = "data-changed";

    private const int DifferencesKept = 10;

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
                return Found(ScenarioChanged, index, step, [$"step: {Describe(good, index)} then {Describe(bad, index)}"], slower);
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

        return new UatTraceDifference(None, -1, null, null, [], slower);
    }

    /// <summary>Whether a trace read the page after its passing steps (<c>BRUINET_TRACE=all</c>): only then are values compared.</summary>
    /// <param name="trace">A trace.</param>
    public static bool ReadPages(JsonObject trace)
        => Steps(trace).Any(step => (string?)step["status"] == "Passed" && step["pageAfter"] is not null);

    private static UatTraceDifference Found(string kind, int index, JsonObject step, List<string> differences, List<string> slower)
    {
        if (differences.Count > DifferencesKept)
        {
            differences = [.. differences.Take(DifferencesKept - 1), $"+{differences.Count - DifferencesKept + 1} more"];
        }

        return new(kind, index, Line(step), (string?)step["text"], differences, slower);
    }

    // What differs between a step's two runs, in the order a reader looks: where it was, what it did, what was said, what showed, how it ended.
    private static List<string> Differences(JsonObject then, JsonObject now)
    {
        List<string> differences = [];
        if (!SamePage(then, now))
        {
            differences.Add($"page: {Page(then, now) ?? "none"} then {Page(now, then) ?? "none"}");
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

        differences.AddRange(Shown(then, now).Select(shown => $"{shown.Name}: {shown.Before} then {shown.After}"));
        if ((string?)then["status"] != (string?)now["status"])
        {
            differences.Add($"status: {(string?)then["status"]} then {(string?)now["status"]}" + ((string?)now["message"] is { } message ? $" ({Cut(message, 120)})" : string.Empty));
        }

        return differences;
    }

    private static string KindOf(JsonObject then, JsonObject now)
    {
        // Another page, or a control the green run never drove: the way through the app changed. A failing step that
        // stopped early drove less, which is not a change of way.
        if (!SamePage(then, now) || Driven(now).Except(Driven(then)).Any())
        {
            return PageChanged;
        }

        // The same calls, nothing else said or shown otherwise, and the call that failed already took at least half as long
        // in the green run: it was near its budget then, and over it now. A wait for a value that never came took little
        // time in the green run, and is data.
        var sameElse = !Said(now).Except(Said(then)).Any() && !Shown(then, now).Any();
        var nearBudget = Calls(now).Where(call => (string?)call["result"] == "error").Any(failed =>
            Calls(then).Where(call => Name(call) == Name(failed)).Sum(call => (int?)call["ms"] ?? 0) * 2 >= ((int?)failed["ms"] ?? 0));
        return sameElse && nearBudget ? TimingOnly : DataChanged;
    }

    // A control's value after the step, where both runs read the page: the ones that differ.
    private static IEnumerable<(string Name, string Before, string After)> Shown(JsonObject then, JsonObject now)
    {
        if (then["pageAfter"]?["controls"] is not JsonArray before || now["pageAfter"]?["controls"] is not JsonArray after)
        {
            yield break;
        }

        var was = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var control in before.OfType<JsonObject>())
        {
            was.TryAdd((string?)control["name"] ?? string.Empty, State(control));
        }

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
        => (bool?)control["shown"] == false ? "hidden" : (string?)control["value"] is { } value ? $"'{Cut(value, 80)}'" : "shown";

    private static List<JsonObject> Steps(JsonObject trace) => trace["steps"]?.AsArray().OfType<JsonObject>().ToList() ?? [];

    private static IEnumerable<JsonObject> Calls(JsonObject step) => step["calls"]?.AsArray().OfType<JsonObject>() ?? [];

    private static string Name(JsonObject call) => $"{(string?)call["control"]}.{(string?)call["action"]}";

    // The controls and actions driven, each once, in the order first driven: a wait polls the same few again and again.
    private static List<string> Driven(JsonObject step) => [.. Calls(step).Select(Name).Distinct(StringComparer.Ordinal)];

    // The same page, read the same way on both sides: the page read after the step when both have one, else the page object
    // of the last call ("TodoListPage" and "Todo List" are one page).
    // A step that drove nothing and read no page has none known: not another page.
    private static bool SamePage(JsonObject then, JsonObject now)
        => Page(then, now) is not { } before || Page(now, then) is not { } after || Normal(before) == Normal(after);

    private static string? Page(JsonObject step, JsonObject other)
        => step["pageAfter"] is not null && other["pageAfter"] is not null
            ? (string?)step["pageAfter"]!["page"]
            : Calls(step).LastOrDefault() is { } last ? (string?)last["page"] : null;

    private static string? Normal(string? page)
    {
        var compact = page?.Replace(" ", string.Empty, StringComparison.Ordinal);
        return compact is not null && compact.EndsWith("Page", StringComparison.Ordinal) && compact.Length > 4 ? compact[..^4] : compact;
    }

    private static List<string> Said(JsonObject step)
        => [.. step["appEvents"]?.AsArray().OfType<JsonObject>().Select(said => $"{(string?)said["level"]} {Cut((string?)said["message"] ?? string.Empty, 80)}") ?? []];

    // A step by what it says and where it comes from, not by its line: an edit above it moves every line after.
    private static string Key(JsonObject step)
        => $"{Path.GetFileName((string?)step["file"])}|{step["callLine"] is not null}|{(string?)step["text"]}";

    private static int? Line(JsonObject step) => (int?)step["callLine"] ?? (int?)step["line"];

    private static long? Ms(JsonObject step) => (long?)step["durationMs"];

    private static string Describe(List<JsonObject> steps, int index)
        => index < steps.Count ? $"line {Line(steps[index])} '{(string?)steps[index]["text"]}'" : "none";

    private static string Join(IEnumerable<string> items) => items.Any() ? string.Join(", ", items.Take(6)) : "nothing";

    // On one line, at most so long: a message's line breaks would break a summary's list.
    internal static string Cut(string text, int most)
    {
        var line = text.ReplaceLineEndings(" ");
        return line.Length > most ? line[..most] + "…" : line;
    }

    /// <summary>Reads a trace file; null when it is missing or not one.</summary>
    /// <param name="path">The trace's path.</param>
    public static JsonObject? Read(string path)
    {
        try
        {
            return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>A passing trace's file: a later failure in the same run folder, which writes <c>__trace.json</c>, leaves it.</summary>
    /// <param name="tracePath">The trace's <c>__trace.json</c> path.</param>
    public static string GreenOf(string tracePath)
    {
        ArgumentNullException.ThrowIfNull(tracePath);
        return tracePath.EndsWith("__trace.json", StringComparison.Ordinal) ? tracePath[..^"__trace.json".Length] + "__trace.green.json" : tracePath + ".green";
    }

    /// <summary>
    /// The newest passing trace of the same scenario written before <paramref name="tracePath"/>: its green file in this run's
    /// folder (a warm runner keeps one) or in one of the twenty runs before it. Null when none passed before.
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

        // Runs by their manifest's time, as the results are read: a folder's own time changes only with its direct children.
        var relative = Path.GetRelativePath(run, GreenOf(full));
        var failedAt = File.Exists(full) ? File.GetLastWriteTimeUtc(full) : DateTime.MaxValue;
        var others = Directory.EnumerateDirectories(root)
            .Where(other => !string.Equals(other, run, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(RunTime)
            .Take(20);
        return others.Prepend(run)
            .Select(other => Path.Combine(other, relative))
            .Where(candidate => File.Exists(candidate) && File.GetLastWriteTimeUtc(candidate) <= failedAt)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault(candidate => Read(candidate) is { } trace && (string?)trace["status"] == "passed");

        static DateTime RunTime(string folder)
            => File.Exists(Path.Combine(folder, "manifest.json")) ? File.GetLastWriteTimeUtc(Path.Combine(folder, "manifest.json")) : Directory.GetLastWriteTimeUtc(folder);
    }
}
