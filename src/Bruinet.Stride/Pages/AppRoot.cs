using Bruinet.Core.Scopes;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Pages;

/// <summary>
/// The whole screen as a scope, for controls that do not live inside a page object: Core's
/// <see cref="AppRootBase{TContext, TElement}"/> on the Stride context.
/// </summary>
public sealed class AppRoot : AppRootBase<IStrideTestContext, IStrideElement>, IStrideScope<AppRoot>
{
    /// <summary>The root scope of <paramref name="context"/>.</summary>
    public AppRoot(IStrideTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public AppRoot Self => this;

    /// <summary>None: this scope is not a page and is not in one.</summary>
    public IStridePage? Page => null;
}
