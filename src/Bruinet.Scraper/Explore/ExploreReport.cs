using System.Globalization;
using System.Text;

namespace Bruinet.Scraper.Explore;

/// <summary><c>explore.md</c>: what a run of Explore captured, skipped and blocked, and why it stopped.</summary>
public static class ExploreReport
{
    /// <summary>The report's file name in the session folder.</summary>
    public const string FileName = "explore.md";

    /// <summary>The report as Markdown.</summary>
    public static string Markdown(ExploreResult result, string project, string start)
    {
        var s = result.Settings;
        var md = new StringBuilder();
        md.AppendLine(CultureInfo.InvariantCulture, $"# Explore {project}{(result.DryRun ? " (dry run)" : "")}");
        md.AppendLine();
        md.AppendLine("| | |");
        md.AppendLine("| --- | --- |");
        md.AppendLine(CultureInfo.InvariantCulture, $"| Start | {Cell(start)} |");
        md.AppendLine(CultureInfo.InvariantCulture, $"| Stopped | **{result.Stop}**: {Cell(result.StopDetail)} |");
        md.AppendLine(CultureInfo.InvariantCulture, $"| Within its guards | {(result.WithinGuards ? "yes" : "no")} |");
        md.AppendLine(CultureInfo.InvariantCulture, $"| Captures, clicks | {result.Captures.Count}, {result.Clicks} |");
        md.AppendLine(CultureInfo.InvariantCulture, $"| Skipped, blocked | {result.Skipped.Count}, {result.Blocked.Count} |");
        md.AppendLine(CultureInfo.InvariantCulture, $"| Took | {result.Took.TotalSeconds:0.0} s |");
        md.AppendLine(CultureInfo.InvariantCulture, $"| Budget | MaxPages {s.MaxPages}, MaxDepth {s.MaxDepth}, MaxMinutes {s.MaxMinutes}, DelayMs {s.DelayMs} |");
        var off = ExploreSettingsCategoriesOff(s);
        md.AppendLine(CultureInfo.InvariantCulture, $"| Guards | ClickPolicy {Explorer.Policy(s.ClickPolicy)}; danger words off: {(off.Length == 0 ? "none" : string.Join(", ", off))}; BlockMethods {(s.BlockMethods.Count == 0 ? "none" : string.Join(", ", s.BlockMethods))}; WebsocketApps {s.WebsocketApps.ToString().ToLowerInvariant()}; hosts {string.Join(", ", s.Hosts)} |");

        Section(md, "Captures", ["#", "Page", "Via", "Depth"], result.Captures.Select(c => new[] { c.Event.ToString(CultureInfo.InvariantCulture), $"`{c.Page}`", c.Via, c.Depth.ToString(CultureInfo.InvariantCulture) }));
        if (result.DryRun)
            Section(md, "Would click", ["Page", "Element", "Why"], result.WouldClick.Select(w => new[] { $"`{w.Page}`", w.Label, w.Reason }));
        Section(md, "Skipped", ["Page", "Element", "Judged", "Why"], result.Skipped.Select(k => new[] { $"`{k.Page}`", k.Label, k.Verdict.ToString().ToLowerInvariant(), k.Reason }));
        Section(md, "Blocked requests", ["Method", "Url", "Why"], result.Blocked.Select(b => new[] { b.Method, b.Url, b.Reason }));
        if (result.WebSockets.Count > 0)
            Section(md, "Websockets", ["Url"], result.WebSockets.Distinct().Select(w => new[] { w }));
        if (result.Notes.Count > 0)
        {
            md.AppendLine();
            md.AppendLine("## Notes");
            md.AppendLine();
            foreach (var note in result.Notes)
                md.AppendLine(CultureInfo.InvariantCulture, $"- {note}");
        }
        return md.ToString();
    }

    /// <summary>What a run will do, in a few lines, for a person to agree to before it starts.</summary>
    public static string Summary(string project, string start, Projects.ExploreSettings s, bool dryRun)
    {
        var host = Uri.TryCreate(start, UriKind.Absolute, out var uri) ? uri.Authority : start;
        var off = ExploreSettingsCategoriesOff(s);
        var refused = uri is null ? "the start is not an address" : RequestGuard.RefuseHost(uri, s);
        return string.Join(Environment.NewLine,
            $"Explore {project}{(dryRun ? " (dry run: links only, nothing clicked)" : "")} from {start}",
            $"  host        {host}{(refused is null ? " (an Explore host)" : $": REFUSED, {refused}")}",
            $"  clicks      ClickPolicy {Explorer.Policy(s.ClickPolicy)}; danger words off: {(off.Length == 0 ? "none" : string.Join(", ", off))}",
            $"  requests    blocked: {(s.BlockMethods.Count == 0 ? "none" : string.Join(", ", s.BlockMethods))}{(s.AllowRequests.Count > 0 ? $"; allowed anyway: {string.Join(", ", s.AllowRequests.Select(a => $"{a.Method} {a.Url}".Trim()))}" : "")}",
            $"  websockets  {s.WebsocketApps.ToString().ToLowerInvariant()}",
            $"  links       {(s.Within.Length == 0 ? "the whole app" : $"within {s.Within}")}",
            $"  budget      {s.MaxPages} captures, {s.MaxDepth} steps deep, {s.MaxMinutes} min, {s.DelayMs} ms between steps");
    }

    private static string[] ExploreSettingsCategoriesOff(Projects.ExploreSettings s) =>
        Projects.ExploreSettings.Categories.Where(c => !s.IsOn(c)).ToArray();

    private static void Section(StringBuilder md, string title, string[] columns, IEnumerable<string[]> rows)
    {
        md.AppendLine();
        md.AppendLine(CultureInfo.InvariantCulture, $"## {title}");
        md.AppendLine();
        var list = rows.ToList();
        if (list.Count == 0)
        {
            md.AppendLine("None.");
            return;
        }
        md.AppendLine("| " + string.Join(" | ", columns) + " |");
        md.AppendLine("|" + string.Concat(columns.Select(_ => " --- |")));
        foreach (var row in list)
            md.AppendLine("| " + string.Join(" | ", row.Select(Cell)) + " |");
    }

    private static string Cell(string text) => text.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
}
