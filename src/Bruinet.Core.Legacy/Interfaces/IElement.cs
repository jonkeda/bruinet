using System.Drawing;
using Bruinet.Core.Locators;

namespace Bruinet.Core.Interfaces;

/// <summary>
/// Core element interface providing state, location, and interaction capabilities.
/// All gestures (DoubleClick, RightClick, Hover, LongPress, ScrollIntoView) are included
/// because they are universal across all UI technologies.
/// </summary>
/// <typeparam name="TSelf">The concrete element type for self-referencing returns.</typeparam>
public interface IElement<TSelf>
    where TSelf : IElement<TSelf>
{
    #region State Properties

    /// <summary>
    /// Gets whether the element is currently visible on screen.
    /// </summary>
    bool Visible { get; }

    /// <summary>
    /// Gets whether the element is enabled for interaction.
    /// </summary>
    bool Enabled { get; }

    /// <summary>
    /// Gets whether the element is selected (for toggles, checkboxes, list items).
    /// </summary>
    bool Selected { get; }

    /// <summary>
    /// Gets the visible text content of the element, or null if not available.
    /// </summary>
    string? Text { get; }

    /// <summary>
    /// Gets the control type or tag name, or null if not available.
    /// </summary>
    string? TagName { get; }

    #endregion

    #region Location Properties

    /// <summary>
    /// Gets the top-left location of the element on screen.
    /// </summary>
    Point Location { get; }

    /// <summary>
    /// Gets the size of the element.
    /// </summary>
    Size Size { get; }

    /// <summary>
    /// Gets the bounding rectangle of the element (combines Location and Size).
    /// </summary>
    Rectangle Rect { get; }

    #endregion

    #region Basic Actions

    /// <summary>
    /// Performs a click/tap on the element.
    /// </summary>
    void Click();

    /// <summary>
    /// Sends text to the element using the specified input method.
    /// </summary>
    /// <param name="text">The text to enter.</param>
    /// <param name="method">How to enter the text (Keys, Paste, or SetValue). Default is Keys.</param>
    void SendKeys(string text, TextInputMethod method = TextInputMethod.Keys);

    /// <summary>
    /// Clears the element's value (for input fields).
    /// </summary>
    void Clear();

    #endregion

    #region Gesture Actions

    /// <summary>
    /// Performs a double-click on the element.
    /// </summary>
    void DoubleClick();

    /// <summary>
    /// Performs a right-click (context click) on the element.
    /// </summary>
    void RightClick();

    /// <summary>
    /// Hovers the pointer over the element.
    /// </summary>
    void Hover();

    /// <summary>
    /// Performs a long-press/hold on the element.
    /// </summary>
    /// <param name="durationMs">Duration in milliseconds. Default is 1000ms.</param>
    void LongPress(int durationMs = 1000);

    /// <summary>
    /// Scrolls the element into the visible viewport.
    /// </summary>
    /// <param name="timeoutMs">Maximum time to wait for scroll completion. Default is 5000ms.</param>
    void ScrollIntoView(int timeoutMs = 5000);

    /// <summary>
    /// Performs a swipe gesture from one point to another.
    /// </summary>
    /// <param name="startX">Starting X coordinate.</param>
    /// <param name="startY">Starting Y coordinate.</param>
    /// <param name="endX">Ending X coordinate.</param>
    /// <param name="endY">Ending Y coordinate.</param>
    /// <param name="durationMs">Duration of the swipe in milliseconds. Default is 500ms.</param>
    void Swipe(int startX, int startY, int endX, int endY, int durationMs = 500);

    #endregion

    #region Attributes

    /// <summary>
    /// Gets an attribute value from the element.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <returns>The attribute value, or null if not present.</returns>
    string? GetAttribute(string name);

    #endregion

    #region Child Finding

    /// <summary>
    /// Finds a child element using the specified locator.
    /// </summary>
    /// <param name="locator">The locator strategy and value.</param>
    /// <param name="timeoutMs">Maximum time to wait for the element. Default is 5000ms.</param>
    /// <returns>The found element.</returns>
    /// <exception cref="Exceptions.ElementNotFoundException">When no element matches within timeout.</exception>
    TSelf FindElement(Locator locator, int timeoutMs = 5000);

    /// <summary>
    /// Finds all child elements matching the specified locator.
    /// </summary>
    /// <param name="locator">The locator strategy and value.</param>
    /// <param name="timeoutMs">Maximum time to wait for at least one element. Default is 0ms (immediate).</param>
    /// <returns>List of matching elements (empty if none found).</returns>
    IReadOnlyList<TSelf> FindElements(Locator locator, int timeoutMs = 0);

    /// <summary>
    /// Tries to find a child element without throwing.
    /// </summary>
    /// <param name="locator">The locator strategy and value.</param>
    /// <param name="element">The found element, or null.</param>
    /// <param name="timeoutMs">Maximum time to wait for the element. Default is 0ms (immediate).</param>
    /// <returns>True if element was found.</returns>
    bool TryFindElement(Locator locator, out TSelf? element, int timeoutMs = 0);

    #endregion
}
