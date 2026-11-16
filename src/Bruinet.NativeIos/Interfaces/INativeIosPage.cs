using Bruinet.Core.Scopes;

namespace Bruinet.NativeIos.Interfaces;

// The page's members are Core's ITestPage, by which UAT and test composition find NativeIos
// pages. iOS has no activity: a page is known by its ready element and titled by its navigation
// bar. See AD-010.

/// <summary>
/// One screen of the app: loaded, busy and titled as the other stacks' pages are. Its title is its
/// navigation bar's title.
/// </summary>
public interface INativeIosPage : INativeIosElementScope, ITestPage
{
}

/// <summary>A page that returns itself, so its controls' actions chain back to it.</summary>
/// <typeparam name="TSelf">The page's own type.</typeparam>
public interface INativeIosPage<TSelf> : INativeIosPage, INativeIosScope<TSelf>
    where TSelf : INativeIosPage<TSelf>
{
}
