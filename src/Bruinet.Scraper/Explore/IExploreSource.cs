using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Explore;

/// <summary>A source Explore can drive: go to a page, click a node of the last capture, and be guarded.</summary>
public interface IExploreSource : IScrapeSource
{
    /// <summary>Where the page is now.</summary>
    string Location { get; }

    /// <summary>Raised when the page starts a download; the file's suggested name.</summary>
    event EventHandler<string>? Downloaded;

    /// <summary>Raised when the page shows an alert, confirm or prompt, which is dismissed; its message.</summary>
    event EventHandler<string>? DialogDismissed;

    /// <summary>Puts the request and host guards on (see <see cref="RequestGuard"/>).</summary>
    Task GuardAsync(RequestGuard guard);

    /// <summary>Takes the guards off.</summary>
    Task UnguardAsync();

    /// <summary>Goes to an address; false when the page could not be opened (blocked, or an error).</summary>
    Task<bool> GoToAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>Clicks a node of the last capture; null when it was clicked, else why not (hidden, gone, covered).</summary>
    Task<string?> ClickAsync(int node, CancellationToken cancellationToken = default);

    /// <summary>The nodes of the last capture a CSS selector matches, in any frame.</summary>
    Task<IReadOnlySet<int>> MatchAsync(string selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// The names of the server's cookies (<c>HttpOnly</c>) the browser holds for the page: a sign-in lives in
    /// one, while a script's (analytics, a one-minute <c>_gat</c>) come and go.
    /// </summary>
    Task<IReadOnlyList<string>> CookieNamesAsync(CancellationToken cancellationToken = default);
}
