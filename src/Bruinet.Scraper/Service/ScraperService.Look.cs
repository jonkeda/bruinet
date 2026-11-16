using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Service;

/// <summary>What to look at: an address, and what to click on the way, in order.</summary>
public sealed record LookOptions
{
    /// <summary>The address, full or a path on the start URL's site; the config's StartUrl when null.</summary>
    public string? Url { get; init; }

    /// <summary>What to click, in order: a text as the page shows it, <c>field:</c> and a field's label, or <c>css:</c> and a selector.</summary>
    public IReadOnlyList<string> Clicks { get; init; } = [];

    /// <summary>Whether the browser shows.</summary>
    public bool Visible { get; init; }

    /// <summary>How long the page gets to settle after it opens and after each click.</summary>
    public int WaitMs { get; init; } = 2000;
}

/// <summary>One click of a look: what was asked, what was clicked, or why it was not.</summary>
/// <param name="Asked">The text or selector asked for.</param>
/// <param name="Clicked">What was clicked, as the page names it; null when nothing was.</param>
/// <param name="Refused">Why nothing was clicked, or why the click did not land; null when it did.</param>
public sealed record LookStep(string Asked, string? Clicked, string? Refused);

/// <summary>What a look found: the clicks it made, the page's facts after them, and what stopped it.</summary>
/// <param name="Url">The address asked for.</param>
/// <param name="Steps">The clicks, in order, up to the first that was refused.</param>
/// <param name="Facts">The page's facts at the end; null when the page did not open.</param>
/// <param name="Problem">What stopped the look (the page did not open, a sign-in page, a refused click); null when nothing did.</param>
public sealed record Look(string Url, IReadOnlyList<LookStep> Steps, PageFacts? Facts, string? Problem);

public sealed partial class ScraperService
{
    /// <summary>
    /// Opens an address with the project's profile and saved sign-in, clicks what is asked, and says what the page shows
    /// then: a fact about a page without a recording or a test. Under the project's guards: its hosts, its request
    /// guard, and for each click what Explore itself may click (never-click, danger words, the click policy). Nothing is
    /// typed and nothing is saved.
    /// </summary>
    /// <param name="projectName">The project.</param>
    /// <param name="options">Where to look and what to click.</param>
    /// <param name="cancellationToken">Stops the look.</param>
    /// <exception cref="ScraperProblem">A recording holds the profile, the platform cannot be looked at, or there is no address.</exception>
    public async Task<Look> LookAsync(string? projectName, LookOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var project = Project(projectName);
        if (_recording is not null)
            throw new ScraperProblem("A recording is in progress, and it holds the project's browser profile: stop it first.");
        if (_sourceFor(project.Config.Platform) is not PlaywrightScrapeSource source)
            throw new ScraperProblem($"{project.Config.Platform} apps cannot be looked at yet: html and blazor can.");
        var url = LookAddress(project.Config.StartUrl, options.Url)
            ?? throw new ScraperProblem($"'{options.Url ?? project.Config.StartUrl}' is not an address: give a full address, or a path with a StartUrl in {ScraperConfig.FileName}.");
        var settings = project.Config.Explore;
        if (RequestGuard.RefuseHost(new Uri(url), settings) is { } refused)
            return new Look(url, [], null, refused);

        var steps = new List<LookStep>();
        await using (source.ConfigureAwait(false))
        {
            await source.StartAsync(new ScrapeStart("about:blank", project.ProfileFolder, options.Visible) { SignInState = project.SignInStatePath }, cancellationToken).ConfigureAwait(false);
            await source.GuardAsync(new RequestGuard(settings)).ConfigureAwait(false);
            if (!await source.GoToAsync(url, cancellationToken).ConfigureAwait(false))
                return new Look(url, steps, null, "The page did not open: it did not answer, or a guard refused it.");

            async Task<ScrapeSnapshot?> SettledAsync()
            {
                await Task.Delay(options.WaitMs, cancellationToken).ConfigureAwait(false);
                return await MovingPage.ReadAsync(source.CaptureAsync, token => Task.Delay(options.WaitMs, token), cancellationToken).ConfigureAwait(false);
            }

            var snapshot = await SettledAsync().ConfigureAwait(false);
            if (snapshot is null)
                return new Look(url, steps, null, "The page kept moving while it was read.");
            if (SignInPage(project, snapshot) is { } signIn)
                return new Look(url, steps, PageFacts.Of(snapshot), signIn);

            foreach (var asked in options.Clicks)
            {
                var matched = asked.StartsWith(LookTarget.SelectorPrefix, StringComparison.OrdinalIgnoreCase)
                    ? await source.MatchAsync(asked[LookTarget.SelectorPrefix.Length..].Trim(), cancellationToken).ConfigureAwait(false)
                    : null;
                if (LookTarget.Find(snapshot, asked, matched) is not { } node)
                {
                    steps.Add(new LookStep(asked, null, "Nothing on the page says or matches that."));
                    return new Look(url, steps, PageFacts.Of(snapshot), $"'{asked}' was not found.");
                }

                var context = new JudgeContext(settings, uri => project.IsOwnPage(uri.AbsoluteUri))
                {
                    NeverNodes = await MatchedAsync(source, settings.NeverClick, cancellationToken).ConfigureAwait(false),
                    AlwaysNodes = await MatchedAsync(source, settings.AlwaysClick, cancellationToken).ConfigureAwait(false),
                };
                var judgement = ClickJudge.Judge(snapshot, node, context);
                if (!LookTarget.MayClick(settings.ClickPolicy, judgement))
                {
                    steps.Add(new LookStep(asked, null, $"Not clicked: {judgement.Reason}."));
                    return new Look(url, steps, PageFacts.Of(snapshot), $"'{asked}' may not be clicked: {judgement.Reason}.");
                }

                if (await source.ClickAsync(node.Number, cancellationToken).ConfigureAwait(false) is { } failed)
                {
                    steps.Add(new LookStep(asked, null, failed));
                    return new Look(url, steps, PageFacts.Of(snapshot), $"'{asked}' could not be clicked: {failed}");
                }

                steps.Add(new LookStep(asked, judgement.Label.Trim().Trim((char)0x200B).Length > 0 ? judgement.Label.Trim() : asked, null));
                snapshot = await SettledAsync().ConfigureAwait(false);
                if (snapshot is null)
                    return new Look(url, steps, null, "The page kept moving while it was read.");
                if (SignInPage(project, snapshot) is { } wentToSignIn)
                    return new Look(url, steps, PageFacts.Of(snapshot), wentToSignIn);
            }

            return new Look(url, steps, PageFacts.Of(snapshot), null);
        }
    }

    // A path is on the start URL's site; a full address is itself.
    internal static string? LookAddress(string startUrl, string? asked)
    {
        if (string.IsNullOrWhiteSpace(asked))
            return Uri.TryCreate(startUrl, UriKind.Absolute, out var start) && start.Scheme is "http" or "https" ? start.AbsoluteUri : null;
        if (Uri.TryCreate(asked, UriKind.Absolute, out var full) && full.Scheme is "http" or "https")
            return full.AbsoluteUri;
        return asked.StartsWith('/') && Uri.TryCreate(startUrl, UriKind.Absolute, out var site) && site.Scheme is "http" or "https"
            ? new Uri(site, asked).AbsoluteUri
            : null;
    }

    private static string? SignInPage(ScrapeProject project, ScrapeSnapshot snapshot)
        => snapshot.Nodes.Any(n => n.Type == "input" && n.Attribute("type") == "password" && n.Bounds is not null)
            && (!project.IsOwnPage(snapshot.Location) || (project.Config.Explore.LoginUrl.Length > 0 && snapshot.Location.Contains(project.Config.Explore.LoginUrl, StringComparison.OrdinalIgnoreCase)))
            ? $"A sign-in page came ({snapshot.Location}): the saved sign-in ran out. Sign in once (bruinet scrape check), or run a test that signs in."
            : null;

    private static async Task<IReadOnlySet<int>> MatchedAsync(IExploreSource source, IReadOnlyList<ElementMatch> matches, CancellationToken cancellationToken)
    {
        var nodes = new HashSet<int>();
        foreach (var match in matches.Where(m => m.Kind == "selector"))
            nodes.UnionWith(await source.MatchAsync(match.Value, cancellationToken).ConfigureAwait(false));
        return nodes;
    }
}

/// <summary>Which element a look clicks for a text or a selector, and whether the project's guards let it.</summary>
internal static class LookTarget
{
    /// <summary>What a click starts with when it is a selector, not a text.</summary>
    public const string SelectorPrefix = "css:";

    /// <summary>What a click starts with when it is a field by its label ("field:Status"): the field is clicked, which opens a list.</summary>
    public const string FieldPrefix = "field:";

    private static readonly HashSet<string> ClickableRoles = new(StringComparer.Ordinal) { "button", "link", "tab", "menuitem", "option", "combobox", "checkbox", "switch", "radio" };

    /// <summary>
    /// The element for what was asked: the first a selector matched, or for a text the element on the screen that says
    /// exactly that, a clickable one (a button, a link, a tab) before any other, the innermost first.
    /// </summary>
    /// <param name="snapshot">The capture.</param>
    /// <param name="asked">The text, or <c>css:</c> and a selector.</param>
    /// <param name="matched">The nodes the selector matched, when it is one.</param>
    public static ScrapeNode? Find(ScrapeSnapshot snapshot, string asked, IReadOnlySet<int>? matched)
    {
        var shown = snapshot.Nodes.Where(node => node.Kind == ScrapeNodeKind.Element && node.Bounds is { Width: > 0, Height: > 0 });
        if (matched is not null)
            return shown.FirstOrDefault(node => matched.Contains(node.Number));

        if (asked.StartsWith(FieldPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var label = Clean(asked[FieldPrefix.Length..]);
            var onScreen = PageFacts.Shown(snapshot);
            return onScreen.FirstOrDefault(node => PageFacts.IsField(node) && string.Equals(PageFacts.LabelOf(snapshot, node, onScreen), label, StringComparison.OrdinalIgnoreCase));
        }

        var wanted = Clean(asked);
        var saying = shown.Where(node => Same(node.Text, wanted) || Same(node.Name, wanted) || Same(snapshot.TextWithin(node), wanted)).ToList();
        return saying.LastOrDefault(IsClickable) ?? saying.LastOrDefault();
    }

    /// <summary>A look clicks what Explore may click, and also what is only unknown: a person asked for this one click.</summary>
    /// <param name="policy">The project's click policy.</param>
    /// <param name="judgement">The element's judgement.</param>
    public static bool MayClick(ClickPolicy policy, Judgement judgement)
        => judgement.Verdict != Verdict.Unsafe || ClickJudge.Allows(policy, judgement);

    private static bool IsClickable(ScrapeNode node)
        => node.Type is "button" or "a" or "input" or "summary" || (node.Role is { } role && ClickableRoles.Contains(role));

    private static bool Same(string? text, string wanted) => text is not null && string.Equals(Clean(text), wanted, StringComparison.OrdinalIgnoreCase);

    private static string Clean(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
