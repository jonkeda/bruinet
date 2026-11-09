using Bruinet.Core.Scopes;

namespace Bruinet.WinForms.Interfaces;

// The page's members are Core's ITestPage, by which UAT and test composition find WinForms pages.
// See AD-010.

/// <summary>
/// One form, or one view inside a form: loaded, busy and titled as the other stacks' pages are. The
/// form is the lookup root, so a page is known by a control it shows.
/// </summary>
public interface IWinFormsPage : IWinFormsElementScope, ITestPage
{
}

/// <summary>A page that returns itself, so its controls' actions chain back to it.</summary>
/// <typeparam name="TSelf">The page's own type.</typeparam>
public interface IWinFormsPage<TSelf> : IWinFormsPage, IWinFormsScope<TSelf>
    where TSelf : IWinFormsPage<TSelf>
{
}
