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

    /// <summary>Why a request is blocked, or null when it goes through; a blocked one is kept in <see cref="Blocked"/>.</summary>
    /// <param name="method">The request's method.</param>
    /// <param name="url">The request's address.</param>
    /// <param name="navigatesPage">True for a navigation of the page itself (not a frame, not a fetch).</param>
    public string? Check(string method, string url, bool navigatesPage)
    {
        var reason = Why(method, url, navigatesPage);
        if (reason is not null)
            _blocked.Enqueue(new BlockedRequest(method.ToUpperInvariant(), url, reason));
        return reason;
    }

    /// <summary>Notes a websocket the page opened.</summary>
    public void WebSocketOpened(string url) => _webSockets.Enqueue(url);

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
