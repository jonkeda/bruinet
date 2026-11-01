using Bruinet.Core.Scraping;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Explore;

/// <summary>
/// Guard 1: before Explore acts on an element, it is judged safe (it only goes somewhere or shows
/// more), unsafe (it submits, signs out, deletes, or a setting says never) or unknown; the project's
/// <see cref="ClickPolicy"/> says which of them are acted on.
/// </summary>
public static class ClickJudge
{
    private static readonly HashSet<string> ClickableRoles = new(StringComparer.Ordinal)
    {
        "button", "tab", "menuitem", "menuitemcheckbox", "menuitemradio", "link", "checkbox", "switch", "option", "treeitem",
    };

    /// <summary>The elements of a capture Explore could act on, each judged, in document order.</summary>
    /// <param name="snapshot">The capture.</param>
    /// <param name="context">The settings, the app's hosts, and the elements the settings' selectors matched.</param>
    public static IReadOnlyList<Judgement> Judge(ScrapeSnapshot snapshot, JudgeContext context) =>
        snapshot.Nodes.Where(n => IsCandidate(snapshot, n)).Select(n => Judge(snapshot, n, context)).ToArray();

    /// <summary>One element's judgement.</summary>
    public static Judgement Judge(ScrapeSnapshot snapshot, ScrapeNode node, JudgeContext context)
    {
        var settings = context.Settings;
        var text = TextOf(snapshot, node);
        var label = node.Name ?? text ?? node.Attribute("title") ?? node.Attribute("data-testid") ?? node.Type;

        Judgement Of(Verdict verdict, JudgedAction action, string reason, string? url = null) => new(node.Number, verdict, action, reason, label, url);

        // The settings name elements: never clicked wins over always clicked.
        if (context.NeverNodes.Contains(node.Number) || settings.NeverClick.Any(m => m.Kind == "text" && Same(m.Value, text, node)))
            return Of(Verdict.Unsafe, JudgedAction.Click, "never click (the config)");
        var always = context.AlwaysNodes.Contains(node.Number) || settings.AlwaysClick.Any(m => m.Kind == "text" && Same(m.Value, text, node));

        if (node.Type == "a" && node.Attribute("href") is { } href && Link(snapshot, node, href, context) is { } link)
            return always && link.Verdict == Verdict.Unsafe && link.Reason.StartsWith("danger", StringComparison.Ordinal)
                ? link with { Verdict = Verdict.Safe, Reason = "always click (the config)" }
                : link with { Label = label };

        if (always)
            return Of(Verdict.Safe, JudgedAction.Click, "always click (the config)");

        if (Submits(snapshot, node))
            return Of(Verdict.Unsafe, JudgedAction.Click, "submits a form");

        if (Danger(snapshot, node, text, settings) is { } danger)
            return Of(Verdict.Unsafe, JudgedAction.Click, $"danger word '{danger.Word}' ({danger.Category})");

        if (ShowsMore(snapshot, node) is { } more)
            return Of(Verdict.Safe, JudgedAction.Click, more);

        return Of(Verdict.Unknown, JudgedAction.Click, "not known to only show more");
    }

    /// <summary>Whether the policy lets Explore act on a judgement.</summary>
    public static bool Allows(ClickPolicy policy, Judgement judgement) => policy switch
    {
        ClickPolicy.Links => judgement.Action == JudgedAction.Visit && judgement.Verdict == Verdict.Safe,
        ClickPolicy.Safe => judgement.Verdict == Verdict.Safe,
        ClickPolicy.NotUnsafe => judgement.Verdict != Verdict.Unsafe,
        // Everything, except what the settings name, other hosts and what cannot be followed.
        ClickPolicy.All => judgement.Verdict != Verdict.Unsafe || judgement.Reason.StartsWith("danger", StringComparison.Ordinal) || judgement.Reason == "submits a form",
        _ => false,
    };

    private static Judgement? Link(ScrapeSnapshot snapshot, ScrapeNode node, string href, JudgeContext context)
    {
        var settings = context.Settings;
        Judgement Of(Verdict verdict, string reason, string? url = null) => new(node.Number, verdict, JudgedAction.Visit, reason, "", url);

        if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) || href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
            return Of(Verdict.Unsafe, "a mail or phone link");
        if (href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            return null; // a script behind a link: judged as the button it is
        if (node.Attributes.ContainsKey("download"))
            return Of(Verdict.Unsafe, "a download");
        if (!Uri.TryCreate(snapshot.Location, UriKind.Absolute, out var page) || !Uri.TryCreate(page, href, out var target))
            return Of(Verdict.Unsafe, "not a link that can be followed");
        if (target.Scheme is not ("http" or "https"))
            return Of(Verdict.Unsafe, $"a {target.Scheme}: link");
        if (!context.IsOwnHost(target))
            return Of(Verdict.Unsafe, $"another host ({target.Authority})", target.AbsoluteUri);
        // A plain anchor on the same page (#top) goes nowhere new; a hash route (#/orders) does.
        if (target.GetLeftPart(UriPartial.Query) == page.GetLeftPart(UriPartial.Query) && target.Fragment.Length > 0 && !target.Fragment.StartsWith("#/", StringComparison.Ordinal))
            return Of(Verdict.Unsafe, "an anchor on the same page", target.AbsoluteUri);
        if (settings.Within.Length > 0 && !Covers(settings.Within, target))
            return Of(Verdict.Unsafe, $"outside Within {settings.Within} (the config)", target.AbsoluteUri);
        if (settings.NeverVisit.FirstOrDefault(v => Covers(v, target)) is { } never)
            return Of(Verdict.Unsafe, $"never visit {never} (the config)", target.AbsoluteUri);
        var path = Uri.UnescapeDataString(target.PathAndQuery + target.Fragment);
        if (DangerWords.Find([path, TextOf(snapshot, node), node.Name, node.Attribute("title")], settings.IsOn) is { } danger)
            return Of(Verdict.Unsafe, $"danger word '{danger.Word}' ({danger.Category})", target.AbsoluteUri);
        return Of(Verdict.Safe, "a link to a page of the app", target.AbsoluteUri);
    }

    private static bool Submits(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        var type = node.Attribute("type")?.ToLowerInvariant();
        if (node.Type == "input")
            return type is "submit" or "image";
        if (node.Type != "button")
            return false;
        if (type is "submit")
            return true;
        if (type is "button" or "reset")
            return false;
        // A button without a type submits the form it is in.
        for (var p = node.Parent; p != 0; p = snapshot[p].Parent)
        {
            if (snapshot[p].Type == "form")
                return true;
            if (snapshot[p].Frame is not null)
                break;
        }
        return false;
    }

    private static (string Category, string Word)? Danger(ScrapeSnapshot snapshot, ScrapeNode node, string? text, ExploreSettings settings) =>
        DangerWords.Find(Says(node).Prepend(text).Append(node.Attribute("href")).Concat(Icons(snapshot, node)), settings.IsOn);

    private static IEnumerable<string?> Says(ScrapeNode node) =>
        [node.Name, node.Attribute("title"), node.Attribute("data-testid"), node.Attribute("data-test"), node.Attribute("data-automation-id"), node.Attribute("data-action"), node.Attribute("id"), node.Attribute("class")];

    // An icon button says what it does in its icon: <i class="fa fa-trash">, <svg data-icon="trash">, <img alt="Delete">.
    private static IEnumerable<string?> Icons(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        foreach (var child in snapshot.ChildrenOf(node.Number))
        {
            yield return child.Attribute("alt");
            foreach (var said in Says(child))
                yield return said;
            yield return child.Attribute("data-icon");
            foreach (var grandchild in snapshot.ChildrenOf(child.Number))
            {
                yield return grandchild.Attribute("alt");
                yield return grandchild.Attribute("class");
                yield return grandchild.Attribute("data-icon");
                yield return grandchild.Name;
            }
        }
    }

    private static string? ShowsMore(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        if (node.Role is "tab")
            return "a tab";
        if (node.Type == "summary")
            return "an expander";
        if (node.Attribute("aria-haspopup") is { } popup && popup != "false")
            return popup == "dialog" ? "opens a dialog" : "opens a menu";
        if (node.Attributes.ContainsKey("aria-expanded"))
            return "shows or hides a part";
        if (node.Type == "th" || node.Attributes.ContainsKey("aria-sort"))
            return "sorts a table";
        if (node.Attribute("aria-controls") is not null && node.Role is "button" or "tab")
            return "shows a part";
        if (IsPager(snapshot, node))
            return "a pager";
        if (IsClose(node) || node.Attribute("data-dismiss") is not null || node.Attribute("data-bs-dismiss") is not null)
            return "closes";
        return null;
    }

    private static bool IsClose(ScrapeNode node)
    {
        string[] closes = ["close", "sluiten", "schliessen", "schließen", "fermer"];
        var says = string.Join(' ', node.Name, node.Attribute("title"), node.Attribute("data-testid"));
        return closes.Any(c => DangerWords.Contains(says, c));
    }

    // A pager says so itself (Next, Previous, a pager class) or sits in a pagination list.
    private static bool IsPager(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        string[] pagers = ["pager", "pagination", "paginator", "next", "previous", "prev", "volgende", "vorige", "weiter", "zuruck", "suivant", "precedent"];
        if (pagers.Any(p => DangerWords.Contains(string.Join(' ', node.Name, TextOf(snapshot, node), node.Attribute("title"), node.Attribute("data-testid"), node.Attribute("class")), p)))
            return true;
        var parent = node.Parent;
        for (var depth = 0; depth < 3 && parent != 0; depth++, parent = snapshot[parent].Parent)
        {
            var up = snapshot[parent];
            if (DangerWords.Contains(string.Join(' ', up.Name, up.Attribute("class"), up.Attribute("data-testid")), "pagination"))
                return true;
        }
        return false;
    }

    private static bool IsCandidate(ScrapeSnapshot snapshot, ScrapeNode node) =>
        node.Kind == ScrapeNodeKind.Element
        && (node.Type is "a" or "button" or "summary"
            || node.Type == "input" && node.Attribute("type")?.ToLowerInvariant() is "button" or "submit" or "reset" or "image"
            || node.Role is { } role && ClickableRoles.Contains(role)
            || node.Attributes.ContainsKey("aria-haspopup") || node.Attributes.ContainsKey("aria-expanded")
            || node.Type == "th" && node.Attributes.ContainsKey("aria-sort"));

    // A configured text matches as whole words in what the element says.
    private static bool Same(string configured, string? text, ScrapeNode node) =>
        DangerWords.Contains(text, configured) || DangerWords.Contains(node.Name, configured) || DangerWords.Contains(node.Attribute("title"), configured);

    // A path covers itself and what is below it; a full URL covers what starts with it.
    private static bool Covers(string url, Uri target) =>
        url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? target.AbsoluteUri.StartsWith(url, StringComparison.OrdinalIgnoreCase)
            : (target.AbsolutePath + target.Fragment).StartsWith(url, StringComparison.OrdinalIgnoreCase);

    // An element's text, or a child's (a button whose label is in a span).
    private static string? TextOf(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        if (node.Text is { Length: > 0 })
            return node.Text;
        foreach (var child in snapshot.ChildrenOf(node.Number))
        {
            if (child.Text is { Length: > 0 } text)
                return text;
            foreach (var grandchild in snapshot.ChildrenOf(child.Number))
                if (grandchild.Text is { Length: > 0 } deeper)
                    return deeper;
        }
        return null;
    }
}

/// <summary>What a judgement is about: the settings, the app's hosts, and which elements the settings' selectors matched in the page.</summary>
public sealed record JudgeContext(ExploreSettings Settings, Func<Uri, bool> IsOwnHost)
{
    /// <summary>The nodes a <c>Never click</c> selector matched.</summary>
    public IReadOnlySet<int> NeverNodes { get; init; } = new HashSet<int>();

    /// <summary>The nodes an <c>Always click</c> selector matched.</summary>
    public IReadOnlySet<int> AlwaysNodes { get; init; } = new HashSet<int>();
}

/// <summary>Safe, unsafe or unknown.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<Verdict>))]
public enum Verdict
{
    /// <summary>It only goes somewhere or shows more.</summary>
    Safe,

    /// <summary>It changes something, leaves the app, or a setting says never.</summary>
    Unsafe,

    /// <summary>Neither is known.</summary>
    Unknown,
}

/// <summary>What acting on an element is.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<JudgedAction>))]
public enum JudgedAction
{
    /// <summary>Going to a link's URL.</summary>
    Visit,

    /// <summary>Clicking the element.</summary>
    Click,
}

/// <summary>An element's judgement: its node, the verdict, the action, why, what it says, and a link's URL.</summary>
public sealed record Judgement(int Node, Verdict Verdict, JudgedAction Action, string Reason, string Label, string? Url);
