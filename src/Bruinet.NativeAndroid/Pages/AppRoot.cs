using Bruinet.Core.Scopes;
using Bruinet.NativeAndroid.Interfaces;

namespace Bruinet.NativeAndroid.Pages;

/// <summary>
/// The whole screen as a scope, for controls that do not live inside a page object: Core's
/// <see cref="AppRootBase{TContext, TElement}"/> on the NativeAndroid context.
/// </summary>
public sealed class AppRoot : AppRootBase<INativeAndroidTestContext, INativeAndroidElement>, INativeAndroidScope<AppRoot>
{
    /// <summary>The root scope of <paramref name="context"/>.</summary>
    public AppRoot(INativeAndroidTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public AppRoot Self => this;

    /// <summary>None: this scope is not a page and is not in one.</summary>
    public INativeAndroidPage? Page => null;
}
