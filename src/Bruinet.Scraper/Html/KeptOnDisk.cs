using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bruinet.Scraper.Html;

/// <summary>
/// An app's scripts, styles and fonts kept on disk between runs, in the folder the UI suites' browser uses too
/// (<c>Bruinet.Html.Playwright</c>'s <c>KeptAssets</c>, the same files). An asset is kept only with the version the
/// site gave it (its <c>ETag</c>); whoever reads it asks the site for that version first.
/// </summary>
internal static class KeptOnDisk
{
    /// <summary>Where assets are kept; null when <c>BRUINET_KEPT_ASSETS=off</c>.</summary>
    private static readonly string? Folder = Environment.GetEnvironmentVariable("BRUINET_KEPT_ASSETS") == "off"
        ? null
        : Path.Combine(Path.GetTempPath(), "bruinet-kept-assets");

    private static string Name(string url) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..32];

    /// <summary>What is on disk of an address, with the version the site gave it; null when there is none that can be read.</summary>
    public static (string ETag, Dictionary<string, string> Headers, byte[] Body)? Read(string url)
    {
        if (Folder is null)
            return null;
        try
        {
            var about = Path.Combine(Folder, Name(url) + ".json");
            var body = Path.Combine(Folder, Name(url) + ".bin");
            if (!File.Exists(about) || !File.Exists(body))
                return null;
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(about));
            return headers is not null && new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase).TryGetValue("etag", out var etag) && etag.Length > 0
                ? (etag, new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase), File.ReadAllBytes(body))
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Keeps an asset on disk, when the site gave it a version to ask about the next time.</summary>
    public static void Write(string url, Dictionary<string, string> headers, byte[] body)
    {
        if (Folder is null || !headers.TryGetValue("etag", out var etag) || etag.Length == 0)
            return;
        try
        {
            Directory.CreateDirectory(Folder);
            var temporary = Path.Combine(Folder, Name(url) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllBytes(temporary, body);
            File.Move(temporary, Path.Combine(Folder, Name(url) + ".bin"), overwrite: true);
            File.WriteAllText(Path.Combine(Folder, Name(url) + ".json"), JsonSerializer.Serialize(headers));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Another run writes the same file: one of the two is kept.
        }
    }
}
