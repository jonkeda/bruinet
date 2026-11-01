using Bruinet.Maui.Configuration;

namespace Bruinet.Maui.Context;

/// <summary>
/// Configuration options for creating a MAUI test context.
/// </summary>
public class MauiTestContextOptions
{
    /// <summary>
    /// Pre-created driver instance (for testing or custom drivers).
    /// If set, factory is not used and DriverOptions are ignored.
    /// </summary>
    public IMauiDriver? Driver { get; init; }

    /// <summary>
    /// Whether the context disposes the injected <see cref="Driver"/> with itself. False by default:
    /// the caller owns what it injects. The MauiHeadless head sets it, as the headless app it made lives
    /// exactly as long as the context, and a restart must end the old app before it starts the new one.
    /// </summary>
    public bool OwnsDriver { get; init; }

    /// <summary>
    /// Gets or sets the driver options for the factory.
    /// Required when Driver is not set.
    /// </summary>
    public MauiDriverOptions? DriverOptions { get; init; }

    /// <summary>
    /// Gets or sets the timeout configuration.
    /// If null, default timeouts will be used.
    /// </summary>
    public TimeoutSettings? Timeouts { get; init; }

    /// <summary>
    /// Gets or sets the test logger.
    /// If null, a no-op logger will be used.
    /// </summary>
    public ITestLogger? Logger { get; init; }

    /// <summary>
    /// When a successful call is still logged as a near-miss. If null,
    /// <see cref="NearMissSettings.Default"/>.
    /// </summary>
    public NearMissSettings? NearMiss { get; init; }

    /// <summary>
    /// A name for the context in call logs, such as the app's name when a test runs several
    /// (<c>Daan's PC</c>). If null, the logs say <c>Test</c>.
    /// </summary>
    public string? Name { get; init; }
}
