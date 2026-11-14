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
}
