using System.Text.Json;
using System.Text.Json.Serialization;
using Bruinet.Scraper.Snapshots;
using Bruinet.Scraper.Recordings;

namespace Bruinet.Scraper.Projects;

/// <summary>
/// One recording session: a folder with <c>recording.json</c>, the ordered list of what happened, and a
/// snapshot file (and screenshot) per captured page. A test recording has the person's actions in the
/// same list, each pointing at a node of its page's latest capture.
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
    public RecordingEvent AddPage(ScrapeSnapshot snapshot, byte[]? screenshot = null, string? pageName = null)
    {
        snapshot = Redactor.Apply(snapshot, _masks);
        var number = _events.Count + 1;
        var baseName = $"{number:000}-{Slug.Of(pageName ?? snapshot.Title, "page")}";
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
            Location = snapshot.Location,
            Page = PageKey.Of(snapshot.Location),
            Title = snapshot.Title,
            Name = pageName,
            Snapshot = snapshotFile,
            Screenshot = screenshotFile,
            Nodes = snapshot.Nodes.Count,
        };
        _events.Add(e);
        Save();
        return e;
    }

    /// <summary>
    /// Appends an action: a click, a value, a check or another of <see cref="RecordingEvent.ActionKinds"/>.
    /// A value or choice right after a click on the same node replaces the click, and a field's value
    /// replaces its value just before. The project's masks are applied first, and a secret keeps only its
    /// placeholder.
    /// </summary>
    /// <param name="action">The action; its number is assigned here.</param>
    /// <returns>The event as stored.</returns>
    /// <exception cref="ArgumentException">It is not an action, an action on an element names neither a node nor a path, or an assert has no known check.</exception>
    public RecordingEvent AddAction(RecordingEvent action)
    {
        if (!RecordingEvent.ActionKinds.Contains(action.Kind))
            throw new ArgumentException($"'{action.Kind}' is not an action kind.", nameof(action));
        if (RecordingEvent.ElementKinds.Contains(action.Kind) && action.Node is null && action.Path is null)
            throw new ArgumentException($"A {action.Kind} names the node or the path it acted on.", nameof(action));
        if (action.Kind == RecordingEvent.AssertKind)
        {
            if (action.Check is null || !AssertCheck.All.Contains(action.Check))
                throw new ArgumentException($"An assert's check is one of {string.Join(", ", AssertCheck.All)}, not '{action.Check}'.", nameof(action));
            if (action.Check != AssertCheck.Page && action.Node is null && action.Path is null)
                throw new ArgumentException($"A '{action.Check}' assert names the node or the path it checks.", nameof(action));
        }
        action = Redactor.Apply(action, _masks);
        RecordingEvent e;
        if (_events.Count > 0 && Merges(_events[^1], action))
        {
            e = action with { Number = _events.Count };
            _events[^1] = e;
        }
        else
        {
            e = action with { Number = _events.Count + 1 };
            _events.Add(e);
        }
        Save();
        return e;
    }

    // A click into a field, then its value, is the value; a field typed into twice is its last value; a
    // click on a select or checkbox, then the choice, is the choice. Only right after each other: a capture
    // between them may number the node differently.
    private static bool Merges(RecordingEvent previous, RecordingEvent next) =>
        previous.Node is { } node && next.Node == node && previous.Capture == next.Capture && previous.Page == next.Page
        && (previous.Kind, next.Kind) switch
        {
            (RecordingEvent.ClickKind, RecordingEvent.InputKind or RecordingEvent.SelectKind or RecordingEvent.CheckKind) => true,
            (RecordingEvent.InputKind, RecordingEvent.InputKind) => true,
            _ => false,
        };

    /// <summary>
    /// The capture an action's node is in: its <see cref="RecordingEvent.Capture"/>, else the latest page
    /// event before it with the same page key, or null when its page was not captured first.
    /// </summary>
    public RecordingEvent? CaptureOf(RecordingEvent action)
    {
        if (action.Capture is { } number && number >= 1 && number <= _events.Count && _events[number - 1].Kind == RecordingEvent.PageKind)
            return _events[number - 1];
        for (var i = action.Number - 2; i >= 0; i--)
        {
            var e = _events[i];
            if (e.Kind == RecordingEvent.PageKind && e.Snapshot is not null && e.Page == action.Page)
                return e;
        }
        return null;
    }

    /// <summary>A short hash of <c>recording.json</c>: a drafted test names it, to tell when the recording changed since.</summary>
    public string Hash()
    {
        var text = File.ReadAllText(Path.Combine(Folder, FileName)).Replace("\r\n", "\n", StringComparison.Ordinal);
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..8];
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
