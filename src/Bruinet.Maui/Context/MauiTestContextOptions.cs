using Bruinet.Maui.Configuration;

namespace Bruinet.Maui.Context;

/// <summary>
/// Configuration options for creating a MAUI test context.
/// </summary>
public class MauiTestContextOptions : Bruinet.Core.Configuration.TestContextOptionsBase
{
    /// <summary>
    /// Pre-created driver instance (for testing or custom drivers).
    /// If set, factory is not used and DriverOptions are ignored.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IMauiDriver? Driver { get; set; }

    /// <summary>
    /// Whether the context disposes the injected <see cref="Driver"/> with itself. False by default:
    /// the caller owns what it injects. The MauiHeadless head sets it, as the headless app it made lives
    /// exactly as long as the context, and a restart must end the old app before it starts the new one.
    /// </summary>
    public bool OwnsDriver { get; set; }

    /// <summary>
    /// Gets or sets the driver options for the factory.
    /// Required when Driver is not set.
    /// </summary>
    public MauiDriverOptions? DriverOptions { get; set; }




}
