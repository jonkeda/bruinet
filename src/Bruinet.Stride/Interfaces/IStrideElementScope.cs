using Bruinet.Core.Scopes;

namespace Bruinet.Stride.Interfaces;

// The element-free half is Core's IScope; Stride adds its typed lookups. See AD-010.

/// <summary>
/// Where controls are found: the game's UI root, a page, a panel or a list row. A scope
/// says whether it is ready, asking its parent first, and finds elements inside itself in one attempt.
/// </summary>
public interface IStrideElementScope : IElementScope<IStrideElement>
{
    /// <summary>The test context the scope belongs to.</summary>
    IStrideTestContext Context { get; }

    /// <summary>The page the scope is on; null for the app root.</summary>
    IStridePage? Page { get; }



}
