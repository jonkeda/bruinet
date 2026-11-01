namespace Bruinet.Core.Configuration;

using System.Text.Json;

/// <summary>
/// Base class for all platform-specific configurations.
/// Provides common loading infrastructure and shared configuration.
/// </summary>
public abstract class BruinetConfigurationBase
{
    /// <summary>
    /// Shared artifact configuration across all platforms.
    /// </summary>
    public ArtifactsOptions Artifacts { get; set; } = new();

    /// <summary>
    /// Loads configuration from JSON file relative to AppContext.BaseDirectory.
    /// </summary>
    protected static T LoadFromJson<T>(string configFileName) where T : new()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, configFileName);

        if (!File.Exists(configPath))
        {
            return new T();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(configPath))
                ?? new T();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse configuration file: {configPath}", ex);
        }
    }
}
