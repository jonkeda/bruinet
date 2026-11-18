using Bruinet.Scraper.Service;

namespace Bruinet.Scraper.Explore;

/// <summary>A source that says what its page asked of the network: the calls its scripts made, and the scripts it loaded.</summary>
public interface IPageCalls
{
    /// <summary>The calls the page's scripts made since the last time this was asked (fetch and XHR), each with its answer's status; never a header.</summary>
    IReadOnlyList<PageCall> TakeCalls();

    /// <summary>The addresses of the scripts the page loaded since the last time this was asked.</summary>
    IReadOnlyList<string> TakeScripts();

    /// <summary>A text the app serves (a script), read with the browser's own session; null when it could not be read.</summary>
    Task<string?> ReadTextAsync(string url, CancellationToken cancellationToken = default);
}
