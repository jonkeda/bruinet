namespace Bruinet.Maui.Configuration;

using System.Text.Json;
using Bruinet.Core.Configuration;

/// <summary>
/// MAUI test platform configuration.
/// Loads from bruinet.maui.config.json
/// </summary>
public class BruinetMauiConfiguration : BruinetConfigurationBase
{
    /// <summary>
    /// Framework-level options (Windows interaction, automation, etc.).
    /// </summary>
    public FrameworkOptions Framework { get; set; } = new();

    /// <summary>
    /// MAUI platform-specific options (Appium/FlaUI driver configuration).
    /// </summary>
    public MauiOptions Maui { get; set; } = new();

    /// <summary>
    /// Loads MAUI configuration from bruinet.maui.config.json
    /// </summary>
    /// <remarks>
    /// The <c>APPIUM_*</c> environment variables override the file, so a CI job or local script
    /// can choose the platform and device without editing it. See
    /// <see cref="ApplyEnvironment(Func{string, string?})"/>.
    /// </remarks>
    /// <param name="configPath">Optional custom config file path. If null, uses default location.</param>
    /// <returns>Loaded configuration or defaults if file not found.</returns>
    public static BruinetMauiConfiguration Load(string? configPath = null)
    {
        var configuration = LoadCore(configPath);

        configuration.ApplyEnvironment(Environment.GetEnvironmentVariable);

        return configuration;
    }

    private static BruinetMauiConfiguration LoadCore(string? configPath)
    {
        if (configPath == null)
        {
            return LoadFromJson<BruinetMauiConfiguration>("bruinet.maui.config.json");
        }

        try
        {
            return JsonSerializer.Deserialize<BruinetMauiConfiguration>(
                File.ReadAllText(configPath))
            ?? new BruinetMauiConfiguration();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse: {configPath}", ex);
        }
    }

    /// <summary>
    /// Overrides <see cref="Maui"/> with the <c>APPIUM_*</c> variables that are set:
    /// <c>APPIUM_PLATFORM</c>, <c>APPIUM_SERVER_URI</c>, <c>APPIUM_APP_PATH</c>,
    /// <c>APPIUM_DEVICE_NAME</c>, <c>APPIUM_PLATFORM_VERSION</c> and <c>APPIUM_UDID</c>.
    /// An unset or blank variable keeps the file's value.
    /// </summary>
    /// <param name="read">Returns a variable's value, or null when it is not set.</param>
    public void ApplyEnvironment(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);

        ApplyPlatformOverride(read("APPIUM_PLATFORM"));

        if (Present(read("APPIUM_SERVER_URI")) is { } server)
        {
            Maui.ServerUri = Uri.TryCreate(server, UriKind.Absolute, out _)
                ? server
                : throw new InvalidOperationException($"APPIUM_SERVER_URI '{server}' is not an absolute URI.");
        }

        Maui.AppPath = Present(read("APPIUM_APP_PATH")) ?? Maui.AppPath;
        Maui.DeviceName = Present(read("APPIUM_DEVICE_NAME")) ?? Maui.DeviceName;
        Maui.PlatformVersion = Present(read("APPIUM_PLATFORM_VERSION")) ?? Maui.PlatformVersion;
        Maui.Udid = Present(read("APPIUM_UDID")) ?? Maui.Udid;
    }

    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void ApplyPlatformOverride(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return;

        Maui.Platform = requested.Trim().ToLowerInvariant() switch
        {
            "windows" => MauiPlatform.Windows,
            "android" => MauiPlatform.Android,
            "ios" => MauiPlatform.iOS,
            _ => throw new InvalidOperationException(
                $"APPIUM_PLATFORM has unrecognized value '{requested}'. " +
                "Expected 'windows', 'android', or 'ios'.")
        };
    }
}
