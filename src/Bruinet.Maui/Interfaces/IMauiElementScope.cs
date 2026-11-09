namespace Bruinet.Maui.Interfaces;

// The element-free half is Core's IScope; MAUI adds its typed lookups, which make one attempt and
// never wait. See .docs/decisions/ad-010-maui-ahead-of-core.md.
/// <summary>
/// Anything controls can be declared in: a page, a container, a collection row, or the app root.
/// </summary>
public interface IMauiElementScope : IScope
{
    /// <summary>
    /// Gets the MAUI test context for this scope.
    /// </summary>
    IMauiTestContext Context { get; }

    /// <summary>
    /// The page this scope belongs to, or null when it belongs to none (the app root, chrome).
    /// </summary>
    IMauiPage? Page { get; }

    /// <summary>
    /// Finds the first element in this scope matching <paramref name="locator"/>, in one attempt.
    /// </summary>
    /// <returns>The element, or null when none matches now.</returns>
    IMauiElement? TryFindElement(Locator locator);

    /// <summary>
    /// Finds the first element in this scope matching <paramref name="locator"/>, or throws.
    /// </summary>
    /// <exception cref="ElementNotFoundException">No element matches.</exception>
    IMauiElement FindElement(Locator locator);

    /// <summary>
    /// The error for <paramref name="locator"/> not found in this scope, in the scope's own words
    /// ("not found within ..."). Builds the message only: it does not look again.
    /// </summary>
    /// <param name="locator">The locator that found nothing.</param>
    ElementNotFoundException DescribeMiss(Locator locator) => new(locator);

    /// <summary>
    /// Finds every element in this scope matching <paramref name="locator"/>, in one attempt.
    /// </summary>
    /// <returns>The matches; empty when none match now.</returns>
    IReadOnlyList<IMauiElement> FindElements(Locator locator);

    /// <summary>
    /// The element that scrolls for this scope, or null to let the driver pick the one on screen.
    /// </summary>
    /// <remarks>
    /// Android publishes nodes only for what is inside the viewport, so a control that finds
    /// nothing asks this element to scroll looking for it
    /// (<see cref="IMauiElement.TryFindByScrolling"/>), or the app element when this is null.
    /// Windows keeps off-screen elements in the tree and scrolls nothing.
    /// </remarks>
    IMauiElement? ScrollingRoot => null;

    /// <summary>
    /// Whether a control in this scope may scroll to look for itself when a plain lookup finds nothing.
    /// </summary>
    /// <remarks>
    /// A scroll lookup searches the whole scroller, not this scope, which is sound only where the
    /// locator is unique on the page. Rows of a collection repeat their children's ids, so inside a
    /// row a sweep finds another row's element: a row without a due date answered with the next
    /// row's (the Todo sample on Android). A realized row already holds its children, so there is
    /// nothing to scroll to.
    /// </remarks>
    bool AllowsScrollLookup => true;
}
