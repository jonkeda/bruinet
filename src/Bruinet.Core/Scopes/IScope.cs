using Bruinet.Core.Locators;

namespace Bruinet.Core.Scopes;

/// <summary>
/// Anything controls can be declared in: a page, a container, a collection row, the app root or
/// the test context. The element-free half of every stack's scope (AD-010): each stack's scope
/// interface derives from this one and adds its typed lookups, its context and its page.
/// Checks answer about now and never wait; <see cref="WaitReady"/> and <see cref="WaitFirst"/> are
/// one call each, on one budget.
/// </summary>
public interface IScope
{
    /// <summary>The locator strategy used when a control is declared with a plain string.</summary>
    LocatorStrategy DefaultLocatorStrategy { get; }

    /// <summary>
    /// One readiness probe, no waiting: whether this scope can be used now, having asked its
    /// parent first when it inherits the parent's readiness. A scope that is not ready says which
    /// scope in the chain it was, and what it found.
    /// </summary>
    ScopeReadiness ProbeReadiness();

    /// <summary>Whether the scope is ready for interaction now: one probe.</summary>
    bool IsReady() => ProbeReadiness().IsReady;

    /// <summary>Waits, as one call, until the scope is ready for interaction.</summary>
    /// <param name="timeoutMs">The whole budget; the default wait when null.</param>
    /// <returns>Whether the scope became ready within the budget.</returns>
    bool WaitReady(int? timeoutMs = null);

    /// <summary>
    /// Waits, as one call, until one of <paramref name="outcomes"/> holds, and returns its index;
    /// the earlier outcome wins when several hold. Each attempt checks only that the app is
    /// reachable, so an outcome may be that this scope, or its parent, is gone.
    /// </summary>
    /// <param name="timeoutMs">The whole budget; the default wait when null.</param>
    /// <param name="outcomes">Answers about now, such as <c>page.IsReady</c> or <c>control.Exists</c>; none may wait itself.</param>
    /// <returns>The index of the outcome that held, or -1 when none did within the budget.</returns>
    int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes);
}
