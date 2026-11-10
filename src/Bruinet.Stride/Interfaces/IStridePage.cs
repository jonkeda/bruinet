using Bruinet.Core.Scopes;

namespace Bruinet.Stride.Interfaces;

// The page's members are Core's ITestPage, by which UAT and test composition find Stride pages.
// See AD-010.

/// <summary>
/// One view of the game's UI (a HUD, a menu, an overlay): loaded, busy and titled as the other
/// stacks' pages are. It is known by an element it shows.
/// </summary>
public interface IStridePage : IStrideElementScope, ITestPage
{
}

/// <summary>A page that returns itself, so its controls' actions chain back to it.</summary>
/// <typeparam name="TSelf">The page's own type.</typeparam>
public interface IStridePage<TSelf> : IStridePage, IStrideScope<TSelf>
    where TSelf : IStridePage<TSelf>
{
}
