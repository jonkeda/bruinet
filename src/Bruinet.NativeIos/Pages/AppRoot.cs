using Bruinet.Core.Scopes;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Pages;

/// <summary>
/// The whole screen as a scope, for controls that do not live inside a page object: Core's
/// <see cref="AppRootBase{TContext, TElement}"/> on the NativeIos context.
/// </summary>
public sealed class AppRoot : AppRootBase<INativeIosTestContext, INativeIosElement>, INativeIosScope<AppRoot>
{
    /// <summary>The root scope of <paramref name="context"/>.</summary>
    public AppRoot(INativeIosTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public AppRoot Self => this;

    /// <summary>None: this scope is not a page and is not in one.</summary>
    public INativeIosPage? Page => null;
}
