using System.Collections.Concurrent;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Explore;

/// <summary>
/// Guards 2 and 4 while exploring: a request whose method is in <see cref="ExploreSettings.BlockMethods"/>
/// is aborted unless <see cref="ExploreSettings.AllowRequests"/> lets it through, and the page is never
/// taken to a host that is not in <see cref="ExploreSettings.Hosts"/>. What it stopped is kept for the report.
/// </summary>
public sealed class RequestGuard(ExploreSettings settings)
{
    private readonly ConcurrentQueue<BlockedRequest> _blocked = new();
    private readonly ConcurrentQueue<string> _webSockets = new();

    /// <summary>The settings it guards with.</summary>
    public ExploreSettings Settings { get; } = settings;

    /// <summary>The requests it aborted, in order.</summary>
    public IReadOnlyList<BlockedRequest> Blocked => _blocked.ToArray();

    /// <summary>The websockets the page opened (guard 3: what this guard cannot see), in order.</summary>
    public IReadOnlyList<string> WebSockets => _webSockets.ToArray();

    /// <summary>
    /// While true, a request that would write is held back (see <see cref="HeldReason"/>): the walk presses a button on
    /// a record that is not its own, sees what the button would do, and changes nothing.
    /// </summary>
    public bool HoldWrites { get; set; }

    /// <summary>
    /// While true, a POST is taken for a read and its address remembered: a page that opens and nothing pressed yet.
    /// Such a POST goes through later while writes are held.
    /// </summary>
    public bool Learning { get; set; }

    private readonly ConcurrentDictionary<string, bool> _readPosts = new(StringComparer.Ordinal);

    /// <summary>What a held request's reason starts with.</summary>
    public const string HeldReason = "held";

    /// <summary>Why a request is blocked, or null when it goes through; a blocked one is kept in <see cref="Blocked"/>.</summary>
    /// <param name="method">The request's method.</param>
    /// <param name="url">The request's address.</param>
    /// <param name="navigatesPage">True for a navigation of the page itself (not a frame, not a fetch).</param>
    /// <param name="body">What the request sends, when it sends something: read to tell a query from a change, never kept.</param>
    public string? Check(string method, string url, bool navigatesPage, string? body = null)
    {
        var reason = Why(method, url, navigatesPage) ?? Held(method, url, body);
        if (reason is not null)
            _blocked.Enqueue(new BlockedRequest(method.ToUpperInvariant(), WithoutSecrets(url), reason));
        return reason;
    }

    /// <summary>Notes a websocket the page opened: its scheme and host only.</summary>
    public void WebSocketOpened(string url)
        => _webSockets.Enqueue(Uri.TryCreate(url, UriKind.Absolute, out var uri) ? $"{uri.Scheme}://{uri.Authority}" : "a websocket");

    /// <summary>
    /// An address as a report may show it: the query's names with their values masked (<c>?token=*</c>), no fragment,
    /// and a websocket with no path, because apps put sign-in tokens there (Bouw7's notification socket has one in its path).
    /// </summary>
    public static string WithoutSecrets(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;
        if (uri.Scheme is "ws" or "wss")
            return $"{uri.Scheme}://{uri.Authority}";
        var query = uri.Query.TrimStart('?');
        var masked = query.Length == 0
            ? ""
            : "?" + string.Join('&', query.Split('&').Select(pair => pair.Split('=', 2)[0] + (pair.Contains('=', StringComparison.Ordinal) ? "=*" : "")));
        return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}{masked}";
    }

    /// <summary>Why Explore may not run against <paramref name="location"/>, or null when its host is in <see cref="ExploreSettings.Hosts"/>.</summary>
    public static string? RefuseHost(Uri location, ExploreSettings settings) =>
        IsExploreHost(location, settings)
            ? null
            : $"Explore may not run against {location.Authority}: it is not in the Explore hosts ({string.Join(", ", settings.Hosts)}). Add it by name to ## Explore hosts in scraper.config.md when this is a test environment.";

    /// <summary>True when the host (or host and port) is in <see cref="ExploreSettings.Hosts"/>.</summary>
    public static bool IsExploreHost(Uri location, ExploreSettings settings) =>
        settings.Hosts.Any(h => string.Equals(h, location.Host, StringComparison.OrdinalIgnoreCase) || string.Equals(h, location.Authority, StringComparison.OrdinalIgnoreCase));

    private string? Why(string method, string url, bool navigatesPage)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return null; // data:, blob: and the like never leave the browser
        if (navigatesPage && RefuseHost(uri, Settings) is not null)
            return $"the page would go to {uri.Authority}, not an Explore host";
        var upper = method.ToUpperInvariant();
        if (!Settings.BlockMethods.Contains(upper, StringComparer.Ordinal))
            return null;
        // SignalR's negotiate (Blazor Server's circuit) changes nothing: it opens the websocket, which
        // guard 3 (WebsocketApps) then decides on. Blocked, the app would be dead and guard 3 blind.
        if (upper == "POST" && IsNegotiate(uri))
            return null;
        if (Settings.AllowRequests.Any(a => Allows(a, upper, uri)))
            return null;
        return $"{upper} is blocked (BlockMethods)";
    }

    // More words that say so in an address, and are no danger on a button (a button's call is held by its method).
    private static readonly string[] WritingGetWords = ["duplicate", "copy", "clone", "toggle", "activate", "restore", "cancel", "finish", "complete", "lock", "unlock", "assign", "book", "process", "execute", "generate", "mark"];

    // The words in an address that say a GET changes something: an old app deletes and sends by a link.
    private static readonly string[] WritingGet = ["delete", "send"];

    // While writes are held: a PUT, PATCH or DELETE; a POST that is not known as a read; a GET whose path says it changes something.
    private string? Held(string method, string url, string? body)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return null;
        var upper = method.ToUpperInvariant();
        if (upper == "POST" && Learning)
        {
            _readPosts[PostKey(uri)] = true;
            return null;
        }
        if (!HoldWrites)
            return null;
        switch (upper)
        {
            case "PUT" or "PATCH" or "DELETE":
                return $"{HeldReason}: {upper} would write to a record that is not the walk's";
            case "POST":
                if (IsNegotiate(uri))
                    return null;
                // GraphQL sends reads and writes to one address: what it sends says which.
                if (body is not null && GraphQl().Match(body) is { Success: true } graph)
                    return graph.Groups[1].Value == "mutation" ? $"{HeldReason}: a GraphQL mutation would write to a record that is not the walk's" : null;
                // An address that says it reads (search, get-calendar-data, match-criteria) is one, as a POST too.
                if (_readPosts.ContainsKey(PostKey(uri)) || ReadingPost().IsMatch(uri.AbsolutePath))
                    return null;
                return $"{HeldReason}: POST would write to a record that is not the walk's";
            case "GET":
                // What an address says after its first part: /purchase-invoice is a page, /invoice/send-reminder an act.
                var path = Uri.UnescapeDataString(uri.AbsolutePath);
                var slash = path.IndexOf('/', 1);
                var act = slash < 0 ? "" : path[slash..];
                var word = DangerWords.Find([path], category => category == "sign-out")?.Word
                    ?? DangerWords.Find([act], category => WritingGet.Contains(category, StringComparer.Ordinal))?.Word
                    ?? WritingGetWords.FirstOrDefault(writing => DangerWords.Contains(act, writing));
                return word is null ? null : $"{HeldReason}: a GET whose address says '{word}'";
            default:
                return null;
        }
    }

    private static string PostKey(Uri uri) => uri.Authority + Projects.PageKey.Of(uri.GetLeftPart(UriPartial.Path));

    private static System.Text.RegularExpressions.Regex GraphQl() => GraphQlPattern;

    private static System.Text.RegularExpressions.Regex ReadingPost() => ReadingPostPattern;

    private static readonly System.Text.RegularExpressions.Regex ReadingPostPattern = new(@"/(search|query|filter|list|find|lookup|count|match|get|fetch|load|validate|check|preview|calculate|export)(-[a-z-]+)?/?$", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex GraphQlPattern = new(@"""query""\s*:\s*""(?:\s|\\[nrt])*(query|mutation|subscription|\{)", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static bool IsNegotiate(Uri uri) =>
        uri.AbsolutePath.EndsWith("/negotiate", StringComparison.OrdinalIgnoreCase)
        && uri.Query.Contains("negotiateVersion=", StringComparison.OrdinalIgnoreCase);

    // A method (empty for any) and a URL: a full one covers what starts with it, a path the paths below it.
    private static bool Allows(AllowedRequest allowed, string method, Uri uri) =>
        (allowed.Method.Length == 0 || allowed.Method == method)
        && (allowed.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? uri.AbsoluteUri.StartsWith(allowed.Url, StringComparison.OrdinalIgnoreCase)
            : uri.PathAndQuery.StartsWith(allowed.Url, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A request the guard aborted: its method, address, and why.</summary>
public sealed record BlockedRequest(string Method, string Url, string Reason);
