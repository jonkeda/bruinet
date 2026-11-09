using Bruinet.Core.Scopes;

namespace Bruinet.Html.Interfaces;

// The page's members are Core's ITestPage, by which UAT and test composition find Html pages;
// Html adds its path. See AD-010.

/// <summary>
/// A page: a scope with a name, a load state and a title. Busy is the page's <c>data-busy</c>; the
/// title is the document title; ready is loaded and idle.
/// </summary>
public interface IHtmlPage : IHtmlElementScope, ITestPage
{
    /// <summary>The page's path, when it has one.</summary>
    string? Url { get; }
}

/// <summary>A page that returns itself from its members, for fluent chains.</summary>
public interface IHtmlPage<TSelf> : IHtmlPage, IHtmlScope<TSelf>
    where TSelf : IHtmlPage<TSelf>
{
}
