namespace Bruinet.Scraper.Snapshots;

/// <summary>
/// The frame a page shows its content in, when it has one: an app that keeps one address and opens each of its pages
/// in a frame (Exact Online's <c>MenuPortal.aspx</c> with its <c>MainWindow</c>). A capture of such a page is named
/// after the frame's address and title, not the shell's, so each page is a page of its own. Told from the capture
/// alone: a frame of the page's own site that covers most of the page. A widget in a frame (a payment box, an editor)
/// covers less, and the page keeps its own name.
/// </summary>
public static class ContentFrame
{
    /// <summary>How much of the page a frame covers at least, to be what the page shows.</summary>
    public const double MostOfThePage = 0.5;

    /// <summary>The frame's node (its <c>iframe</c>), or null when the page shows its content itself.</summary>
    public static ScrapeNode? Of(ScrapeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Nodes.Count == 0 || snapshot.Nodes[0].Bounds is not { } page || !Uri.TryCreate(snapshot.Location, UriKind.Absolute, out var location))
            return null;
        var whole = (double)page.Width * page.Height;
        if (whole <= 0)
            return null;
        return snapshot.Nodes
            .Where(node => node.Bounds is not null && IsOwnSite(node.Frame, location))
            .Select(node => (Node: node, Share: (double)node.Bounds!.Value.Width * node.Bounds.Value.Height / whole))
            .Where(frame => frame.Share >= MostOfThePage)
            .OrderByDescending(frame => frame.Share)
            .Select(frame => frame.Node)
            .FirstOrDefault();
    }

    /// <summary>The address a capture is named after: its content frame's, else the page's own.</summary>
    public static string Location(ScrapeSnapshot snapshot) => Of(snapshot)?.Frame ?? snapshot.Location;

    /// <summary>The title a capture is named after: its content frame's when it has one, else the page's own.</summary>
    public static string Title(ScrapeSnapshot snapshot) => Of(snapshot)?.FrameTitle is { Length: > 0 } title ? title : snapshot.Title;

    private static bool IsOwnSite(string? frame, Uri page) =>
        Uri.TryCreate(frame, UriKind.Absolute, out var address) && address.Scheme is "http" or "https"
        && string.Equals(address.Authority, page.Authority, StringComparison.OrdinalIgnoreCase);
}
