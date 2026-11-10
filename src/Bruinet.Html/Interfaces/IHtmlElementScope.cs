using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using Bruinet.Core.Scopes;

namespace Bruinet.Html.Interfaces;

// The element-free half is Core's IScope; Html adds its typed lookups. See AD-010.

/// <summary>
/// Anything controls can be declared in: the test context, a page, a container, a frame.
/// </summary>
public interface IHtmlElementScope : IElementScope<IHtmlElement>
{
    /// <summary>The test context the scope belongs to.</summary>
    IHtmlTestContext Context { get; }

    /// <summary>The page the scope is on, for naming; <c>null</c> for the context itself.</summary>
    IHtmlPage? Page { get; }



}
