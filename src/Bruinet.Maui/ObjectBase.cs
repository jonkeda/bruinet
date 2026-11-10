using Bruinet.Core.Configuration;

namespace Bruinet.Maui;

/// <summary>
/// Base class for all MAUI objects providing shared utilities.
/// Both page objects and controls inherit from this class.
/// </summary>
/// <remarks>
/// MAUI keeps its own object base rather than Core's <c>ObjectBase&lt;TContext&gt;</c> (public API review,
/// proposal 5, rule 6): its scopes run their waits through <c>RootedScopeBase</c> and <c>Call.Run</c>, not
/// through the shared probe, so this base holds only the context and budgets.
/// </remarks>
public abstract class ObjectBase
{
    /// <summary>
    /// Gets the MAUI test context.
    /// </summary>
    public abstract IMauiTestContext Context { get; }

    /// <summary>
    /// Gets the default timeout in milliseconds.
    /// </summary>
    protected int DefaultTimeoutMs => Context.Timeouts.DefaultWait.ToMs();

    /// <summary>
    /// Gets the polling interval in milliseconds.
    /// </summary>
    protected int PollingIntervalMs => Context.Timeouts.PollingInterval.ToMs();
}
