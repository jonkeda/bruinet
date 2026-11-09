using Bruinet.Core.Scopes;

namespace Bruinet.NativeAndroid.Interfaces;

// R9: NativeAndroid owns this contract and does not derive from Core's scopes. See AD-010.

/// <summary>
/// Where controls are found: the app, a page, a container, a dialog or a list row. A scope says
/// whether it is ready, asking its parent first, and finds views inside itself in one attempt.
/// </summary>
public interface INativeAndroidElementScope
{
    /// <summary>The test context the scope belongs to.</summary>
    INativeAndroidTestContext Context { get; }

    /// <summary>The strategy a plain string locator uses in this scope: a resource id.</summary>
    LocatorStrategy DefaultLocatorStrategy { get; }

    /// <summary>The page the scope is on; null for the app root.</summary>
    INativeAndroidPage? Page { get; }

    /// <summary>One probe of the scope's readiness, its parents first. Never waits.</summary>
    ScopeReadiness ProbeReadiness();

    /// <summary>Whether the scope is ready now.</summary>
    bool IsReady() => ProbeReadiness().IsReady;

    /// <summary>Waits until the scope is ready, as one call.</summary>
    bool WaitReady(int? timeoutMs = null);

    /// <summary>
    /// Waits, as one call, until one of <paramref name="outcomes"/> holds, and returns its index;
    /// the earlier outcome wins when several hold. Each attempt checks only that the app is
    /// reachable, so an outcome may be that this scope, or its parent, is gone.
    /// </summary>
    /// <param name="timeoutMs">The whole budget; the default wait when null.</param>
    /// <param name="outcomes">Answers about now, such as <c>page.IsReady</c> or <c>control.IsExists</c>; none may wait itself.</param>
    /// <returns>The index of the outcome that held, or -1 when none did within the budget.</returns>
    int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes);

    /// <summary>The first matching view in the scope now, or null. One attempt.</summary>
    INativeAndroidElement? TryFindElement(Locator locator);

    /// <summary>The first matching view in the scope now. One attempt.</summary>
    INativeAndroidElement FindElement(Locator locator);

    /// <summary>The matching views in the scope now. One attempt.</summary>
    IReadOnlyList<INativeAndroidElement> FindElements(Locator locator);
}
