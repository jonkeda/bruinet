namespace Bruinet.Html.Pages;

/// <summary>
/// A page's address with a record's id in it, written with a placeholder: <c>/orders/{id}</c>,
/// <c>/contact#/view/{id}</c>. The Scraper names such a page this way; a page object given one is at its address on
/// any record's page.
/// </summary>
internal static class PageAddress
{
    /// <summary>What stands for a record's id in a page's address.</summary>
    public const string Id = "{id}";

    /// <summary>Whether an address has an id's placeholder in it.</summary>
    /// <param name="address">The page's address.</param>
    public static bool HasId(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return address.Contains(Id, StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether a location is the page of some record of this address: its path, and its hash route when the address
    /// has one, segment by segment, an id's placeholder for any one segment. A query is not part of it.
    /// </summary>
    /// <param name="address">The page's address, with <c>{id}</c> where a record's id is.</param>
    /// <param name="location">Where the browser is.</param>
    public static bool Matches(string address, Uri location)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(location);
        var route = location.Fragment.Split('?')[0];
        var actual = address.Contains('#', StringComparison.Ordinal) ? location.AbsolutePath + route : location.AbsolutePath;
        var wanted = address.TrimEnd('/').Split('/');
        var found = actual.TrimEnd('/').Split('/');
        return wanted.Length == found.Length
            && wanted.Zip(found).All(pair => pair.First == Id ? pair.Second.Length > 0 : string.Equals(pair.First, pair.Second, StringComparison.Ordinal));
    }
}
