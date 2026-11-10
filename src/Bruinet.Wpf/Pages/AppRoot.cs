using Bruinet.Core.Scopes;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Pages;

/// <summary>
/// The whole screen as a scope, for controls that do not live inside a page object: Core's
/// <see cref="AppRootBase{TContext, TElement}"/> on the Wpf context.
/// </summary>
public sealed class AppRoot : AppRootBase<IWpfTestContext, IWpfElement>, IWpfScope<AppRoot>
{
    /// <summary>The root scope of <paramref name="context"/>.</summary>
    public AppRoot(IWpfTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public AppRoot Self => this;

    /// <summary>None: this scope is not a page and is not in one.</summary>
    public IWpfPage? Page => null;
}
