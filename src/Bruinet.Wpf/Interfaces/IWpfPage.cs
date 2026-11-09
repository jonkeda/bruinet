using Bruinet.Core.Scopes;

namespace Bruinet.Wpf.Interfaces;

// The page's members are Core's ITestPage, by which UAT and test composition find WPF pages.
// See AD-010.

/// <summary>
/// One view of the app: loaded, busy and titled as the other stacks' pages are. A WPF app often has
/// one window whose content changes, so a page is known by an element it shows.
/// </summary>
public interface IWpfPage : IWpfElementScope, ITestPage
{
}

/// <summary>A page that returns itself, so its controls' actions chain back to it.</summary>
/// <typeparam name="TSelf">The page's own type.</typeparam>
public interface IWpfPage<TSelf> : IWpfPage, IWpfScope<TSelf>
    where TSelf : IWpfPage<TSelf>
{
}
