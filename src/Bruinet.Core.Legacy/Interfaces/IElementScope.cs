namespace Bruinet.Core.Interfaces;

/// <summary>
/// Non-generic element scope interface for polymorphic access.
/// Provides page context and ready-state checking.
/// </summary>
public interface IElementScope
{
    /// <summary>
    /// Default locator strategy for this scope.
    /// </summary>
    LocatorStrategy DefaultLocatorStrategy { get; }

    /// <summary>
    /// The page containing this scope.
    /// For pages, returns self. For containers, returns parent's page.
    /// </summary>
    IPageObject? Page { get; }

    /// <summary>
    /// Check if the scope is ready for element finding.
    /// For pages, this checks if the page is loaded.
    /// For containers, this checks if the parent is ready and the container root exists.
    /// </summary>
    bool IsReady(int? timeoutMs = null);

    /// <summary>
    /// Wait until the scope is ready for element finding.
    /// </summary>
    bool WaitReady(int? timeoutMs = null);
}

/// <summary>
/// Generic element scope providing typed element finding.
/// TElement is the platform's native element type.
/// </summary>
public interface IElementScope<TElement> : IElementScope
{
    /// <summary>
    /// Try to find a single element. Returns null if not found.
    /// </summary>
    TElement? TryFindElement(Locator locator);

    /// <summary>
    /// Find a single element. Throws if not found.
    /// </summary>
    TElement FindElement(Locator locator);

    /// <summary>
    /// Find all matching elements.
    /// </summary>
    IReadOnlyList<TElement> FindElements(Locator locator);
}
