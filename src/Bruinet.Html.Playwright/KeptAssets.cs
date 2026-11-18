using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace Bruinet.Html.Playwright;

/// <summary>
/// The scripts, styles and fonts of the app under test, kept for the length of a test run and on disk between runs.
/// A browser context whose requests are routed (the host guard) keeps no cache of its own, so every page a test
/// opened fetched the app's scripts again: megabytes per page, and on a slow environment a page did not load in time.
/// What is kept is answered from here, to every context of the run. Between runs an asset is kept only when the site
/// gives it a version (an <c>ETag</c>), and the site is asked for that version first (a HEAD request, a few bytes):
/// the same version is answered from disk, another is fetched anew.
/// </summary>
internal static class KeptAssets
{
    /// <summary>The most that is kept in a run.</summary>
    public const long Limit = 400L * 1024 * 1024;

    private static readonly ConcurrentDictionary<string, Lazy<Task<(Dictionary<string, string> Headers, byte[] Body)?>>> Kept = new(StringComparer.Ordinal);
    private static long _bytes;

    /// <summary>Where assets are kept between runs; <c>BRUINET_KEPT_ASSETS=off</c> keeps none on disk.</summary>
    private static readonly string? Folder = Environment.GetEnvironmentVariable("BRUINET_KEPT_ASSETS") == "off"
        ? null
        : Path.Combine(Path.GetTempPath(), "bruinet-kept-assets");

    /// <summary>Answers the app's own scripts, styles and fonts from what is kept, on the hosts the context may go to.</summary>
    public static Task KeepAsync(IBrowserContext context, IReadOnlyList<string> hosts)
        => context.RouteAsync(
            url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && hosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase),
            async route =>
            {
                var request = route.Request;
                if (request.Method != "GET" || request.ResourceType is not ("script" or "stylesheet" or "font"))
                {
                    await route.ContinueAsync().ConfigureAwait(false);
                    return;
                }

                try
                {
                    // Fetched beside the page, with the context's session: a page that gives up waiting does not stop the
                    // fetch, so the next page finds the script here.
                    var kept = await Kept.GetOrAdd(request.Url, url => new Lazy<Task<(Dictionary<string, string> Headers, byte[] Body)?>>(() => FetchAsync(context, url))).Value.ConfigureAwait(false);
                    if (kept is { } found)
                    {
                        await route.FulfillAsync(new() { Status = 200, Headers = found.Headers, BodyBytes = found.Body }).ConfigureAwait(false);
                        return;
                    }

                    Kept.TryRemove(request.Url, out _);
                    await route.ContinueAsync().ConfigureAwait(false);
                }
                catch (Exception e) when (e is PlaywrightException or TimeoutException)
                {
                    // The page went elsewhere meanwhile: the request is over.
                }
            });

    // An asset as the site answers it now: from disk when the site says its version there still holds; null when the
    // site did not answer with it, or it is too much to keep.
    private static async Task<(Dictionary<string, string> Headers, byte[] Body)?> FetchAsync(IBrowserContext context, string url)
    {
        try
        {
            // What is on disk holds when the site still gives the address the same version: a few bytes to ask.
            if (Read(url) is { } onDisk)
            {
                var now = await context.APIRequest.HeadAsync(url, new() { Timeout = 30_000 }).ConfigureAwait(false);
                if (now.Status == 200 && now.Headers.TryGetValue("etag", out var etag) && etag == onDisk.ETag)
                    return Count(onDisk.Body.Length) ? (onDisk.Headers, onDisk.Body) : null;
            }

            var response = await context.APIRequest.GetAsync(url, new() { Timeout = 180_000 }).ConfigureAwait(false);
            if (response.Status != 200)
                return null;
            var body = await response.BodyAsync().ConfigureAwait(false);
            if (!Count(body.Length))
                return null;

            // The body is unpacked: what says how it was packed and how long it was no longer holds.
            var headers = response.Headers.Where(header => header.Key.ToLowerInvariant() is not ("content-encoding" or "content-length" or "transfer-encoding"))
                .ToDictionary(header => header.Key, header => header.Value, StringComparer.OrdinalIgnoreCase);
            Write(url, headers, body);
            return (headers, body);
        }
        catch (Exception e) when (e is PlaywrightException or TimeoutException)
        {
            return null;
        }
    }

    private static bool Count(long length)
    {
        if (Interlocked.Add(ref _bytes, length) <= Limit)
            return true;
        Interlocked.Add(ref _bytes, -length);
        return false;
    }

    private static string Name(string url) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..32];

    // What is on disk of an address, with the version the site gave it; null when there is none that can be read.
    private static (string ETag, Dictionary<string, string> Headers, byte[] Body)? Read(string url)
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

    // Kept on disk only with a version to ask the site about the next time.
    private static void Write(string url, Dictionary<string, string> headers, byte[] body)
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
