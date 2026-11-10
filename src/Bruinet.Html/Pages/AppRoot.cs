using Bruinet.Core.Scopes;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Pages;

/// <summary>
/// The whole page as a scope, for controls that do not live inside a page object: Core's
/// <see cref="AppRootBase{TContext, TElement}"/> on the Html context.
/// </summary>
public sealed class AppRoot : AppRootBase<IHtmlTestContext, IHtmlElement>, IHtmlScope<AppRoot>
{
    /// <summary>The root scope of <paramref name="context"/>.</summary>
    public AppRoot(IHtmlTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public AppRoot Self => this;

    /// <summary>None: this scope is not a page and is not in one.</summary>
    public IHtmlPage? Page => null;
}
