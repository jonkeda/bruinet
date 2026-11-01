using System.Text.RegularExpressions;

namespace Bruinet.Scraper.Projects;

/// <summary>
/// Which page a location is, whatever record it shows: the path and hash route without the host, the
/// query, and ids. <c>https://app/invoices/1234/edit?tab=2</c> and <c>…/invoices/99/edit</c> are the page
/// <c>/invoices/{id}/edit</c>.
/// </summary>
public static partial class PageKey
{
    /// <summary>The page key of a location.</summary>
    public static string Of(string location)
    {
        if (!Uri.TryCreate(location, UriKind.Absolute, out var uri))
            return location;
        var key = Normalize(uri.AbsolutePath);
        // A hash route (#/orders/12) is the page in a single-page app; a plain anchor (#top) is not.
        if (uri.Fragment.StartsWith("#/", StringComparison.Ordinal))
        {
            var route = uri.Fragment[1..];
            var query = route.IndexOf('?', StringComparison.Ordinal);
            key = (key == "/" ? "" : key) + "#" + Normalize(query < 0 ? route : route[..query]);
        }
        return key;
    }

    private static string Normalize(string path)
    {
        var segments = path.Split('/').Select(s => Id().IsMatch(s) ? "{id}" : Uri.UnescapeDataString(s));
        var normalized = string.Join('/', segments).TrimEnd('/');
        return normalized.Length == 0 ? "/" : normalized;
    }

    // A number, a GUID, or a long hex or base64-like token.
    [GeneratedRegex(@"^(\d+|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}|[0-9a-fA-F]{16,}|[A-Za-z0-9_-]{24,})$")]
    private static partial Regex Id();
}
