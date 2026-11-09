using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using Bruinet.Core.Scopes;

namespace Bruinet.Html.Interfaces;

// The element-free half is Core's IScope; Html adds its typed lookups. See AD-010.

/// <summary>
/// Anything controls can be declared in: the test context, a page, a container, a frame.
/// </summary>
public interface IHtmlElementScope : IScope
{
    /// <summary>The test context the scope belongs to.</summary>
    IHtmlTestContext Context { get; }

    /// <summary>The page the scope is on, for naming; <c>null</c> for the context itself.</summary>
    IHtmlPage? Page { get; }

    /// <summary>The first matching element in the scope, or <c>null</c>.</summary>
    IHtmlElement? TryFindElement(Locator locator);

    /// <summary>The first matching element in the scope.</summary>
    /// <exception cref="ElementNotFoundException">Nothing matches.</exception>
    IHtmlElement FindElement(Locator locator);

    /// <summary>Every matching element in the scope.</summary>
    IReadOnlyList<IHtmlElement> FindElements(Locator locator);
}
