using System.Globalization;
using Bruinet.Core.Utilities;

namespace Bruinet.Scraper.Projects;

/// <summary>
/// How Explore may go through an app: its budget and its four guards, from <c>scraper.config.md</c>
/// (<c>## Explore</c>, <c>## Danger words</c>, <c>## Explore hosts</c>, <c>## Never click</c>,
/// <c>## Always click</c>, <c>## Never visit</c>, <c>## Allow requests</c>). The defaults are the
/// careful ones; a project loosens what is harmless for its app.
/// </summary>
public sealed record ExploreSettings
{
    /// <summary>The danger-word categories, all on by default.</summary>
    public static readonly IReadOnlyList<string> Categories = ["sign-out", "delete", "update", "send", "pay", "approve", "bulk"];

    /// <summary>The most captures a run makes.</summary>
    public int MaxPages { get; init; } = 50;

    /// <summary>The most clicks from the start page.</summary>
    public int MaxDepth { get; init; } = 4;

    /// <summary>The longest a run takes.</summary>
    public int MaxMinutes { get; init; } = 10;

    /// <summary>The pause between clicks, so the app is not hammered.</summary>
    public int DelayMs { get; init; } = 500;

    /// <summary>The longest the agent stage of the loop may take.</summary>
    public int AgentMinutes { get; init; } = 10;

    /// <summary>The app's sign-in page (a path or URL), where Explore stops for the person; empty: found by its password field.</summary>
    public string LoginUrl { get; init; } = "";

    /// <summary>A path (or <c>/#/route</c>) the links Explore visits stay under; empty: the whole app.</summary>
    public string Within { get; init; } = "";

    /// <summary>What Explore clicks (guard 1).</summary>
    public ClickPolicy ClickPolicy { get; init; } = ClickPolicy.Safe;

    /// <summary>The request methods aborted while exploring (guard 2); GET, HEAD and OPTIONS always go.</summary>
    public IReadOnlyList<string> BlockMethods { get; init; } = ["POST", "PUT", "PATCH", "DELETE"];

    /// <summary>What Explore does in an app that talks over a websocket (guard 3).</summary>
    public WebsocketApps WebsocketApps { get; init; } = WebsocketApps.Links;

    /// <summary>Which danger-word categories are on (guard 1); a category not listed is on.</summary>
    public IReadOnlyDictionary<string, bool> DangerWords { get; init; } = new Dictionary<string, bool>(StringComparer.Ordinal);

    /// <summary>The hosts Explore may run against (guard 4).</summary>
    public IReadOnlyList<string> Hosts { get; init; } = ["localhost", "127.0.0.1"];

    /// <summary>Elements never clicked, besides the danger words.</summary>
    public IReadOnlyList<ElementMatch> NeverClick { get; init; } = [];

    /// <summary>Elements clicked whatever the danger words say.</summary>
    public IReadOnlyList<ElementMatch> AlwaysClick { get; init; } = [];

    /// <summary>URLs (a path, or a full URL) never visited; a path matches itself and below.</summary>
    public IReadOnlyList<string> NeverVisit { get; init; } = [];

    /// <summary>Requests let through although their method is blocked.</summary>
    public IReadOnlyList<AllowedRequest> AllowRequests { get; init; } = [];

    /// <summary>Whether a danger-word category is on.</summary>
    public bool IsOn(string category) => !DangerWords.TryGetValue(category, out var on) || on;

    internal static ExploreSettings Read(IReadOnlyDictionary<string, MarkdownTable> sections)
    {
        var settings = new ExploreSettings();
        if (sections.TryGetValue("Explore", out var explore))
        {
            foreach (var row in explore.Rows)
            {
                var value = row.Cells.GetValueOrDefault("Value", "");
                var line = row.LineNumber;
                settings = row.Cells.GetValueOrDefault("Field", "") switch
                {
                    "MaxPages" => settings with { MaxPages = Count(value, line) },
                    "MaxDepth" => settings with { MaxDepth = Count(value, line) },
                    "MaxMinutes" => settings with { MaxMinutes = Count(value, line) },
                    "DelayMs" => settings with { DelayMs = Count(value, line) },
                    "AgentMinutes" => settings with { AgentMinutes = Count(value, line) },
                    "LoginUrl" => settings with { LoginUrl = value },
                    "Within" => settings with { Within = value },
                    "ClickPolicy" => settings with { ClickPolicy = Policy(value, line) },
                    "BlockMethods" => settings with { BlockMethods = Methods(value, line) },
                    "WebsocketApps" => settings with { WebsocketApps = Websockets(value, line) },
                    var other => throw new FormatException($"Line {line}: '{other}' is not a field of ## Explore (MaxPages, MaxDepth, MaxMinutes, DelayMs, AgentMinutes, LoginUrl, Within, ClickPolicy, BlockMethods, WebsocketApps)."),
                };
            }
        }
        if (sections.TryGetValue("Danger words", out var danger))
        {
            var words = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var row in danger.Rows)
            {
                var category = row.Cells.GetValueOrDefault("Category", "");
                if (!Categories.Contains(category))
                    throw new FormatException($"Line {row.LineNumber}: '{category}' is not a danger-word category ({string.Join(", ", Categories)}).");
                var on = row.Cells.GetValueOrDefault("On", "true");
                words[category] = bool.TryParse(on, out var b) ? b : throw new FormatException($"Line {row.LineNumber}: '{on}' is not true or false.");
            }
            settings = settings with { DangerWords = words };
        }
        if (sections.TryGetValue("Explore hosts", out var hosts))
            settings = settings with { Hosts = Column(hosts, "Host") };
        if (sections.TryGetValue("Never click", out var never))
            settings = settings with { NeverClick = Column(never, "Match").Select((m, i) => ElementMatch.Parse(m, never.Rows[i].LineNumber)).ToArray() };
        if (sections.TryGetValue("Always click", out var always))
            settings = settings with { AlwaysClick = Column(always, "Match").Select((m, i) => ElementMatch.Parse(m, always.Rows[i].LineNumber)).ToArray() };
        if (sections.TryGetValue("Never visit", out var visit))
            settings = settings with { NeverVisit = Column(visit, "Url") };
        if (sections.TryGetValue("Allow requests", out var allow))
            settings = settings with { AllowRequests = allow.Rows.Select(r => new AllowedRequest(r.Cells.GetValueOrDefault("Method", "").ToUpperInvariant(), r.Cells.GetValueOrDefault("Url", ""))).Where(r => r.Url.Length > 0).ToArray() };
        return settings;
    }

    private static string[] Column(MarkdownTable table, string column) =>
        table.Rows.Select(r => r.Cells.GetValueOrDefault(column, "")).Where(v => v.Length > 0).ToArray();

    private static int Count(string value, int line) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : throw new FormatException($"Line {line}: '{value}' is not a whole number above 0.");

    private static ClickPolicy Policy(string value, int line) => value switch
    {
        "links" => ClickPolicy.Links,
        "safe" => ClickPolicy.Safe,
        "not-unsafe" => ClickPolicy.NotUnsafe,
        "all" => ClickPolicy.All,
        _ => throw new FormatException($"Line {line}: ClickPolicy is links, safe, not-unsafe or all, not '{value}'."),
    };

    private static WebsocketApps Websockets(string value, int line) => value switch
    {
        "links" => WebsocketApps.Links,
        "warn" => WebsocketApps.Warn,
        "ignore" => WebsocketApps.Ignore,
        _ => throw new FormatException($"Line {line}: WebsocketApps is links, warn or ignore, not '{value}'."),
    };

    private static string[] Methods(string value, int line)
    {
        var methods = value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries).Select(m => m.ToUpperInvariant()).ToArray();
        var wrong = methods.FirstOrDefault(m => m is not ("POST" or "PUT" or "PATCH" or "DELETE"));
        return wrong is null ? methods : throw new FormatException($"Line {line}: BlockMethods takes POST, PUT, PATCH and DELETE, not '{wrong}'.");
    }
}

/// <summary>What Explore clicks.</summary>
public enum ClickPolicy
{
    /// <summary>Links only.</summary>
    Links,

    /// <summary>Links, and what is known only to show more (tabs, menus, expanders, pagers, dialogs).</summary>
    Safe,

    /// <summary>Safe, and what is not known either way.</summary>
    NotUnsafe,

    /// <summary>Everything, submits too: a throwaway environment only.</summary>
    All,
}

/// <summary>What Explore does in an app that talks over a websocket, whose clicks the request guard cannot see.</summary>
public enum WebsocketApps
{
    /// <summary>Links only.</summary>
    Links,

    /// <summary>The click policy stands; the person is warned first.</summary>
    Warn,

    /// <summary>The click policy stands.</summary>
    Ignore,
}

/// <summary>An element named in the config: by its text (<c>text: Empty cache</c>) or a CSS selector (<c>selector: [data-testid=x] button</c>).</summary>
public sealed record ElementMatch(string Kind, string Value)
{
    /// <summary>Reads <c>text: …</c> or <c>selector: …</c>; a bare value is a text.</summary>
    public static ElementMatch Parse(string cell, int line)
    {
        var colon = cell.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
            return new("text", cell.Trim());
        var kind = cell[..colon].Trim().ToLowerInvariant();
        var value = cell[(colon + 1)..].Trim();
        return kind is "text" or "selector" ? new(kind, value) : throw new FormatException($"Line {line}: a match is 'text: …' or 'selector: …', not '{cell}'.");
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Kind}: {Value}";
}

/// <summary>A request let through although its method is blocked: a method and a URL or path it starts with.</summary>
public sealed record AllowedRequest(string Method, string Url);
