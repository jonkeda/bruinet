using Bruinet.Core.Scopes;

namespace Bruinet.NativeAndroid.Interfaces;

// The page's members are Core's ITestPage, by which UAT and test composition find NativeAndroid
// pages; Android adds the page's activity. See AD-010.

/// <summary>
/// One screen of the app: loaded, busy and titled as the other stacks' pages are. Its title is its
/// toolbar title, else its activity.
/// </summary>
public interface INativeAndroidPage : INativeAndroidElementScope, ITestPage
{
    /// <summary>The activity the page is shown in (<c>.LoginActivity</c>); null when any will do.</summary>
    string? Activity { get; }
}

/// <summary>A page that returns itself, so its controls' actions chain back to it.</summary>
/// <typeparam name="TSelf">The page's own type.</typeparam>
public interface INativeAndroidPage<TSelf> : INativeAndroidPage, INativeAndroidScope<TSelf>
    where TSelf : INativeAndroidPage<TSelf>
{
}
