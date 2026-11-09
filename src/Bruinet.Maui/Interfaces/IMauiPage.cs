namespace Bruinet.Maui.Interfaces;

// The page's members are Core's ITestPage, by which UAT and test composition find MAUI pages.
// See .docs/decisions/ad-010-maui-ahead-of-core.md.
/// <summary>
/// A page of the MAUI app under test: a root scope with load and busy state, its root located by
/// its <see cref="ITestPage.Name"/> as an AutomationId.
/// </summary>
public interface IMauiPage : IMauiElementScope, ITestPage
{
}

/// <summary>
/// A page with a fluent self type. Pages are root scopes: they have no <c>Parent</c>.
/// </summary>
/// <typeparam name="TSelf">The page type itself (self-referencing).</typeparam>
public interface IMauiPage<TSelf> : IMauiPage, IMauiScope<TSelf>
    where TSelf : IMauiPage<TSelf>
{
}
