using Bruinet.Core.Locators;

namespace Bruinet.Core.Scopes;

/// <summary>
/// A scope that finds its stack's elements: a page, a container, a row, the context itself
/// (public API review, proposal 5). Each stack's element scope extends it with its element type,
/// so Core's shared bases can look elements up without a hook per stack.
/// </summary>
/// <typeparam name="TElement">The stack's element.</typeparam>
public interface IElementScope<TElement> : IScope
    where TElement : class
{
    /// <summary>The element <paramref name="locator"/> finds in this scope now, or null. One attempt.</summary>
    TElement? TryFindElement(Locator locator);

    /// <summary>The element <paramref name="locator"/> finds in this scope now; throws when there is none.</summary>
    TElement FindElement(Locator locator);

    /// <summary>Every element <paramref name="locator"/> finds in this scope now. One attempt.</summary>
    IReadOnlyList<TElement> FindElements(Locator locator);
}
