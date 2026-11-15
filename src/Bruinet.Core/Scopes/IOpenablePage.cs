namespace Bruinet.Core.Scopes;

/// <summary>
/// A page that knows how to open itself, such as a web page at its path, so <c>Given I am on the … page</c>
/// needs no fixture method for it.
/// </summary>
public interface IOpenablePage : ITestPage
{
    /// <summary>Whether the page knows where it is, and so can open itself.</summary>
    bool CanOpen { get; }

    /// <summary>Opens the page where it is, in the current app.</summary>
    void Open();

    /// <summary>
    /// Whether the app is at the page's address already, loaded or still loading. A page that is there is not opened
    /// again: opening it would throw away what the steps before set up on it (a tab chosen, a filter typed).
    /// </summary>
    bool IsAtItsAddress() => IsLoaded();
}
