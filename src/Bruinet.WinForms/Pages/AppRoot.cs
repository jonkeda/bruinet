using Bruinet.Core.Scopes;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Pages;

/// <summary>
/// The whole screen as a scope, for controls that do not live inside a page object: Core's
/// <see cref="AppRootBase{TContext, TElement}"/> on the WinForms context.
/// </summary>
public sealed class AppRoot : AppRootBase<IWinFormsTestContext, IWinFormsElement>, IWinFormsScope<AppRoot>
{
    /// <summary>The root scope of <paramref name="context"/>.</summary>
    public AppRoot(IWinFormsTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public AppRoot Self => this;

    /// <summary>None: this scope is not a page and is not in one.</summary>
    public IWinFormsPage? Page => null;
}
