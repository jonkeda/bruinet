namespace Bruinet.Blazor;

/// <summary>
/// Blazor application-specific configuration options.
/// </summary>
public class BlazorOptions
{
    /// <summary>
    /// Base URL of the Blazor application (e.g., https://localhost:5001).
    /// Default: https://localhost:5001
    /// </summary>
    public string AppUrl { get; set; } = "https://localhost:5001";

    /// <summary>
    /// Full path to the Blazor application project or binary.
    /// Null = assume application is already running at AppUrl.
    /// </summary>
    public string? AppPath { get; set; }

    /// <summary>
    /// How long to wait for the application to be ready. Default: 30 seconds. In a settings file, a
    /// number of milliseconds or a TimeSpan string.
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(Bruinet.Core.Configuration.MillisecondsTimeSpanConverter))]
    public TimeSpan ReadyTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
