using Bruinet.Core.Locators;

namespace Bruinet.Core.Interfaces;

/// <summary>
/// Generic driver interface for UI automation.
/// The generic parameter enables type-safe element returns without runtime casting.
/// </summary>
/// <typeparam name="TElement">The element type returned by this driver.</typeparam>
public interface IDriver<TElement> : IDisposable
    where TElement : IElement<TElement>
{
    #region Element Finding

    /// <summary>
    /// Finds an element using the specified locator.
    /// </summary>
    /// <param name="locator">The locator strategy and value.</param>
    /// <param name="timeoutMs">Maximum time to wait for the element. Default is 5000ms.</param>
    /// <returns>The found element.</returns>
    /// <exception cref="Exceptions.ElementNotFoundException">When no element matches within timeout.</exception>
    TElement FindElement(Locator locator, int timeoutMs = 5000);

    /// <summary>
    /// Finds all elements matching the specified locator.
    /// </summary>
    /// <param name="locator">The locator strategy and value.</param>
    /// <param name="timeoutMs">Maximum time to wait for at least one element. Default is 0ms (immediate).</param>
    /// <returns>List of matching elements (empty if none found).</returns>
    IReadOnlyList<TElement> FindElements(Locator locator, int timeoutMs = 0);

    /// <summary>
    /// Tries to find an element without throwing.
    /// </summary>
    /// <param name="locator">The locator strategy and value.</param>
    /// <param name="element">The found element, or null.</param>
    /// <param name="timeoutMs">Maximum time to wait for the element. Default is 0ms (immediate).</param>
    /// <returns>True if element was found.</returns>
    bool TryFindElement(Locator locator, out TElement? element, int timeoutMs = 0);

    #endregion

    #region Session Management

    /// <summary>
    /// Closes the current window/context.
    /// </summary>
    void Close();

    /// <summary>
    /// Terminates the driver session and cleans up resources.
    /// </summary>
    void Quit();

    #endregion

    #region Screenshots

    /// <summary>
    /// Captures a screenshot of the current state.
    /// </summary>
    /// <returns>Screenshot as PNG byte array.</returns>
    byte[] GetScreenshot();

    #endregion
}
