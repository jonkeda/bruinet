using Bruinet.Core.Configuration;

namespace Bruinet.Maui;

/// <summary>
/// Base class for all MAUI objects providing shared utilities.
/// Both page objects and controls inherit from this class.
/// </summary>
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
