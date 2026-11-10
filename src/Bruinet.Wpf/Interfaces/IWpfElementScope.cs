using Bruinet.Core.Scopes;

namespace Bruinet.Wpf.Interfaces;

// The element-free half is Core's IScope; WPF adds its typed lookups. See AD-010.

/// <summary>
/// Where controls are found: the app's window, a page, a container, a dialog or a list row. A scope
/// says whether it is ready, asking its parent first, and finds elements inside itself in one attempt.
/// </summary>
public interface IWpfElementScope : IElementScope<IWpfElement>
{
    /// <summary>The test context the scope belongs to.</summary>
    IWpfTestContext Context { get; }

    /// <summary>The page the scope is on; null for the app root.</summary>
    IWpfPage? Page { get; }



}
