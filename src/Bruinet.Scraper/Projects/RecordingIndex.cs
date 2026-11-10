using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Projects;

/// <summary>A project's recordings by page: built in memory when a project is opened, never stored.</summary>
public sealed class RecordingIndex
{
    private readonly Dictionary<string, RecordingSession> _sessions;

    private RecordingIndex(IReadOnlyList<RecordedPage> pages, Dictionary<string, RecordingSession> sessions)
    {
        Pages = pages;
        _sessions = sessions;
    }

    /// <summary>Every recorded page, by key, with its captures newest first.</summary>
    public IReadOnlyList<RecordedPage> Pages { get; }

    /// <summary>Builds the index of some sessions.</summary>
    public static RecordingIndex Build(IEnumerable<RecordingSession> sessions)
    {
        var bySession = new Dictionary<string, RecordingSession>(StringComparer.Ordinal);
        var captures = new List<PageCapture>();
        foreach (var session in sessions)
        {
            bySession[session.Id] = session;
            captures.AddRange(session.Events.Where(e => e.Kind == RecordingEvent.PageKind && e.Snapshot is not null).Select(e => new PageCapture(session.Id, e)));
        }
        var pages = captures
            .GroupBy(c => c.Event.Page, StringComparer.Ordinal)
            .Select(g => new RecordedPage(g.Key, g.OrderByDescending(c => c.Event.At).ThenByDescending(c => c.Session, StringComparer.Ordinal).ToArray()))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .ToArray();
        return new RecordingIndex(pages, bySession);
    }

    /// <summary>The page with this key, or null.</summary>
    public RecordedPage? Page(string key) => Pages.FirstOrDefault(p => p.Key == key);

    /// <summary>A capture's snapshot.</summary>
    public ScrapeSnapshot Read(PageCapture capture) => _sessions[capture.Session].ReadSnapshot(capture.Event);

    /// <summary>
    /// The nodes of each page's newest capture whose text, name or an attribute value contains
    /// <paramref name="text"/> (ignoring case).
    /// </summary>
    public IEnumerable<(RecordedPage Page, ScrapeNode Node)> Search(string text)
    {
        foreach (var page in Pages)
        {
            foreach (var node in Read(page.Latest).Nodes)
            {
                if (Contains(node.Text, text) || Contains(node.Name, text) || node.Attributes.Values.Any(v => Contains(v, text)))
                    yield return (page, node);
            }
        }
    }

    private static bool Contains(string? value, string text) =>
        value is not null && value.Contains(text, StringComparison.OrdinalIgnoreCase);
}

/// <summary>A page and its captures, newest first.</summary>
public sealed record RecordedPage(string Key, IReadOnlyList<PageCapture> Captures)
{
    /// <summary>The newest capture.</summary>
    public PageCapture Latest => Captures[0];
}

/// <summary>A page capture: the session it is in and its event.</summary>
public sealed record PageCapture(string Session, RecordingEvent Event);
