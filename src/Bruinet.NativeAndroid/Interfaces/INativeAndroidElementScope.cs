using Bruinet.Core.Scopes;

namespace Bruinet.NativeAndroid.Interfaces;

// The element-free half is Core's IScope; NativeAndroid adds its typed lookups. See AD-010.

/// <summary>
/// Where controls are found: the app, a page, a container, a dialog or a list row. A scope says
/// whether it is ready, asking its parent first, and finds views inside itself in one attempt.
/// </summary>
public interface INativeAndroidElementScope : IElementScope<INativeAndroidElement>
{
    /// <summary>The test context the scope belongs to.</summary>
    INativeAndroidTestContext Context { get; }

    /// <summary>The page the scope is on; null for the app root.</summary>
    INativeAndroidPage? Page { get; }



}
