using System.Text.Json;

namespace Bruinet.Scraper.Record;

/// <summary>
/// What the Recorder remembers of a project between runs, in <c>recorder.settings.json</c> beside the project's
/// <c>scraper.config.md</c>: where it was, the addresses opened by hand before, and the values switch.
/// </summary>
public sealed record RecorderSettings
{
    /// <summary>The file's name.</summary>
    public const string FileName = "recorder.settings.json";

    /// <summary>How many earlier addresses are kept.</summary>
    public const int MaxAddresses = 20;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>The address the Recorder was on when it closed; null when never.</summary>
    public string? Address { get; init; }

    /// <summary>The addresses typed, picked or passed in before, the last first, none twice.</summary>
    public IReadOnlyList<string> Addresses { get; init; } = [];

    /// <summary>The values switch as it was left.</summary>
    public bool RecordValues { get; init; }

    /// <summary>A folder's settings; the empty ones when it has no file, or one that cannot be read.</summary>
    public static RecorderSettings Load(string folder)
    {
        var path = Path.Combine(folder, FileName);
        if (!File.Exists(path))
            return new RecorderSettings();
        try
        {
            return JsonSerializer.Deserialize<RecorderSettings>(File.ReadAllText(path), Json) ?? new RecorderSettings();
        }
        catch (JsonException)
        {
            return new RecorderSettings(); // written by hand and broken: started over, not a reason to refuse to open
        }
    }

    /// <summary>Writes the settings into a folder.</summary>
    public void Save(string folder) =>
        File.WriteAllText(Path.Combine(folder, FileName), JsonSerializer.Serialize(this, Json).ReplaceLineEndings("\n") + "\n");

    /// <summary>The settings with an address opened by hand: first in the list, and where the Recorder is.</summary>
    public RecorderSettings Opened(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return this with
        {
            Address = address,
            Addresses = [address, .. Addresses.Where(other => !string.Equals(other, address, StringComparison.OrdinalIgnoreCase)).Take(MaxAddresses - 1)],
        };
    }
}
