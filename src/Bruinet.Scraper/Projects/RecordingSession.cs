using System.Text.Json;
using System.Text.Json.Serialization;
using Bruinet.Scraper.Snapshots;
using Bruinet.Scraper.Recordings;

namespace Bruinet.Scraper.Projects;

/// <summary>
/// One recording session: a folder with <c>recording.json</c>, the ordered list of what happened, and a
/// snapshot file (and screenshot) per captured page. A session the Recorder made has the person's presses in the
/// same list, each pointing at a node of the page capture before it.
/// </summary>
public sealed class RecordingSession
{
    /// <summary>The session's event list file.</summary>
    public const string FileName = "recording.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // A locator's quotes and a label's accents as they are: the file is read by people too.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly List<RecordingEvent> _events;
    private readonly IReadOnlyList<TextMask> _masks;

    private RecordingSession(string folder, string name, List<RecordingEvent> events, IReadOnlyList<TextMask> masks)
    {
        Folder = folder;
        Name = name;
        _events = events;
        _masks = masks;
    }

    /// <summary>The session's folder.</summary>
    public string Folder { get; }

    /// <summary>The session's folder name, which is its id in a project.</summary>
    public string Id => Path.GetFileName(Folder);

    /// <summary>What the user called it.</summary>
    public string Name { get; }

    /// <summary>What happened, in order.</summary>
    public IReadOnlyList<RecordingEvent> Events => _events;

    internal static RecordingSession Create(string folder, string name, IReadOnlyList<TextMask> masks)
    {
        Directory.CreateDirectory(folder);
        var session = new RecordingSession(folder, name, [], masks);
        session.Save();
        return session;
    }

    /// <summary>Opens a session folder.</summary>
    /// <param name="folder">The session's folder.</param>
    /// <param name="masks">The project's masks, applied to pages added from now on.</param>
    /// <exception cref="FormatException">Its <c>recording.json</c> cannot be read.</exception>
    public static RecordingSession Open(string folder, IReadOnlyList<TextMask> masks)
    {
        var path = Path.Combine(folder, FileName);
        RecordingFile file;
        try
        {
            file = JsonSerializer.Deserialize<RecordingFile>(File.ReadAllText(path), Json) ?? new RecordingFile();
        }
        catch (JsonException e)
        {
            throw new FormatException($"{path}: {e.Message}", e);
        }
        return new RecordingSession(folder, file.Name ?? Path.GetFileName(folder), file.Events ?? [], masks);
    }

    /// <summary>
    /// Adds a captured page: masks the snapshot with the project's masks, writes it (and the screenshot),
    /// and appends the event.
    /// </summary>
    /// <param name="snapshot">The page as captured.</param>
    /// <param name="screenshot">A PNG of it, or null.</param>
    /// <param name="pageName">What the user called the page; its title when null.</param>
    /// <param name="insteadOfLast">
    /// True when this capture is the same page as the session's last event, read again later (the Recorder: the page
    /// changed and no press came between): it takes that event's place, so one capture stands for the page as it
    /// ended up. Without such a last event it is added as any other.
    /// </param>
    public RecordingEvent AddPage(ScrapeSnapshot snapshot, byte[]? screenshot = null, string? pageName = null, bool insteadOfLast = false)
    {
        snapshot = Redactor.Apply(snapshot, _masks);
        if (insteadOfLast && _events.Count > 0 && _events[^1] is { Kind: RecordingEvent.PageKind } last && last.Page == PageKey.Of(ContentFrame.Location(snapshot)))
        {
            foreach (var file in new[] { last.Snapshot, last.Screenshot }.Where(file => file is not null))
                File.Delete(Path.Combine(Folder, file!));
            _events.RemoveAt(_events.Count - 1);
        }

        var number = _events.Count + 1;
        // An app that shows each page in a frame under one address: the capture is named after the frame's page.
        var location = ContentFrame.Location(snapshot);
        var title = ContentFrame.Title(snapshot);
        var baseName = $"{number:000}-{Slug.Of(pageName ?? title, "page")}";
        var snapshotFile = baseName + SnapshotFile.Extension;
        SnapshotFile.Write(snapshot, Path.Combine(Folder, snapshotFile));
        string? screenshotFile = null;
        if (screenshot is not null)
        {
            screenshotFile = baseName + ".png";
            File.WriteAllBytes(Path.Combine(Folder, screenshotFile), screenshot);
        }
        var e = new RecordingEvent
        {
            Number = number,
            Kind = RecordingEvent.PageKind,
            At = snapshot.CapturedAt,
            Location = location,
            Shell = location == snapshot.Location ? null : snapshot.Location,
            Page = PageKey.Of(location),
            Title = title,
            Name = pageName,
            Snapshot = snapshotFile,
            Screenshot = screenshotFile,
            Nodes = snapshot.Nodes.Count,
        };
        _events.Add(e);
        Save();
        return e;
    }

    /// <summary>Adds a person's press: what was pressed and where, after the page capture it was pressed on.</summary>
    /// <param name="location">Where the page was.</param>
    /// <param name="name">What was pressed, as the page says it.</param>
    /// <param name="role">What it is.</param>
    /// <param name="target">A locator of it, or null.</param>
    /// <param name="node">Its number in the last page capture, or 0.</param>
    /// <param name="at">When.</param>
    public RecordingEvent AddPress(string location, string name, string role, string? target, int node, DateTimeOffset at)
    {
        var e = new RecordingEvent
        {
            Number = _events.Count + 1,
            Kind = RecordingEvent.PressKind,
            At = at,
            Location = Redactor.Mask(location, _masks) ?? "",
            Page = PageKey.Of(location),
            Name = Redactor.Mask(name, _masks),
            Role = role,
            Target = target,
            Node = node,
        };
        _events.Add(e);
        Save();
        return e;
    }

    /// <summary>The snapshot a page event wrote.</summary>
    /// <exception cref="InvalidOperationException">The event has no snapshot.</exception>
    public ScrapeSnapshot ReadSnapshot(RecordingEvent e) =>
        e.Snapshot is null
            ? throw new InvalidOperationException($"Event {e.Number} of {Id} has no snapshot.")
            : SnapshotFile.Read(Path.Combine(Folder, e.Snapshot));

    private void Save()
    {
        var file = new RecordingFile { Name = Name, Events = _events };
        File.WriteAllText(Path.Combine(Folder, FileName), JsonSerializer.Serialize(file, Json).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
    }

    private sealed class RecordingFile
    {
        public string? Name { get; set; }

        public List<RecordingEvent>? Events { get; set; }
    }
}
