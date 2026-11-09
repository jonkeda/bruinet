using Bruinet.Core.Calls;
using Bruinet.Core.Interfaces;
using Bruinet.Html.Context;

namespace Bruinet.Html.Interfaces;

// Core's ITestContext (its name and near-miss settings among them) and Html's own scope, which
// derives from Core's IScope. See AD-010.

/// <summary>
/// One browser page under test: its driver, timeouts and logger, and the root scope for lookups.
/// </summary>
public interface IHtmlTestContext : ITestContext, IHtmlElementScope
{
    /// <summary>The driver that talks to the browser.</summary>
    IHtmlDriver Driver { get; }

    /// <summary>The page's URL now.</summary>
    string CurrentUrl { get; }

    /// <summary>The document title now.</summary>
    string PageTitle { get; }

    /// <summary>Goes forward in the browser history.</summary>
    void GoForward();

    /// <summary>Whether the page is idle now.</summary>
    bool IsIdle();
}
