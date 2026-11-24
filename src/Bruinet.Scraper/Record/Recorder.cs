using System.Text.Json;
using System.Threading.Channels;
using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Recordings;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Record;

/// <summary>
/// Records what a person does in a page they browse themselves: between <see cref="StartAsync"/> and
/// <see cref="StopAsync"/> each press is an event of the session, and the page is captured when recording starts
/// and whenever it shows something else after a step. The Recorder presses and types nothing (AD-027).
/// </summary>
public sealed class Recorder : IAsyncDisposable
{
    private readonly IRecorderPage _page;
    private readonly TimeProvider _time;
    private readonly Channel<object> _queue = Channel.CreateUnbounded<object>(new() { SingleReader = true });
    private readonly Task _worker;
    private readonly object _gate = new();
    private TaskCompletionSource _ended = New();
    private ScrapeProject? _project;
    private RecordingSession? _session;
    private string? _lastHash;
    private DateTimeOffset _at;
    private bool _values;
    private int _pages;
    private int _presses;

    /// <summary>A recorder on a page; it records nothing until <see cref="StartAsync"/>.</summary>
    /// <param name="page">The page the person browses.</param>
    /// <param name="time">The clock; the system's when null.</param>
    public Recorder(IRecorderPage page, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        _page = page;
        _time = time ?? TimeProvider.System;
        _page.Message += OnMessage;
        _worker = Task.Run(WorkAsync);
    }

    /// <summary>Raised when a count or the state changed; on a thread of the Recorder's own.</summary>
    public event EventHandler? Changed;

    /// <summary>True between a start and a stop.</summary>
    public bool Recording => _session is not null;

    /// <summary>The project being recorded into; null when not recording.</summary>
    public ScrapeProject? Project => _project;

    /// <summary>The session being written; after a stop, the last one.</summary>
    public RecordingSession? Session { get; private set; }

    /// <summary>The pages captured in this recording, or the last one.</summary>
    public int Pages => _pages;

    /// <summary>The presses recorded in this recording, or the last one.</summary>
    public int Presses => _presses;

    /// <summary>What went wrong last while a page was recorded; null when nothing did.</summary>
    public string? Problem { get; private set; }

    /// <summary>Whether a captured page holds its fields' values as they are then.</summary>
    public bool RecordValues => _values;

    /// <summary>Sets <see cref="RecordValues"/>; while recording, from the next capture on.</summary>
    public async Task SetRecordValuesAsync(bool values, CancellationToken cancellationToken = default)
    {
        _values = values;
        if (Recording)
            await _page.RunAsync(RecorderScripts.Set(on: true, values), cancellationToken).ConfigureAwait(false);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Why a recording cannot start in this folder at this address, or null when it can: the folder is a Scraper
    /// project, and the address is on one of its Explore hosts.
    /// </summary>
    public static string? Refusal(string? folder, string? address)
    {
        if (string.IsNullOrWhiteSpace(folder) || !File.Exists(Path.Combine(folder, ScraperConfig.FileName)))
            return $"Choose a Scraper project to save in: a folder with a {ScraperConfig.FileName}.";
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return "Open the page to start on.";
        ScrapeProject project;
        try
        {
            project = ScrapeProject.Open(folder);
        }
        catch (FormatException e)
        {
            return e.Message;
        }

        return MayRecord(project, uri)
            ? null
            : $"{uri.Authority} is not one of this project's Explore hosts ({string.Join(", ", project.Config.Explore.Hosts)}). Add it to ## Explore hosts in {ScraperConfig.FileName}.";
    }

    /// <summary>Starts a recording into a new session of the project in <paramref name="folder"/>.</summary>
    /// <exception cref="InvalidOperationException">A recording is on, or <see cref="Refusal"/> says why not.</exception>
    public async Task<RecordingSession> StartAsync(string folder, string address, CancellationToken cancellationToken = default)
    {
        if (Recording)
            throw new InvalidOperationException("A recording is on already.");
        if (Refusal(folder, address) is { } refusal)
            throw new InvalidOperationException(refusal);
        var project = ScrapeProject.Open(folder);
        var session = project.StartSession("record", _time.GetUtcNow());
        lock (_gate)
        {
            _project = project;
            _lastHash = null;
            _pages = 0;
            _presses = 0;
            Problem = null;
            Session = session;
            _session = session;
        }

        await _page.RunAsync(RecorderScripts.Set(on: true, _values), cancellationToken).ConfigureAwait(false);
        Changed?.Invoke(this, EventArgs.Empty);
        return session;
    }

    /// <summary>Stops the recording, once what the page posted until now is written. Null when none was on.</summary>
    public async Task<RecordingSession?> StopAsync(CancellationToken cancellationToken = default)
    {
        if (!Recording)
            return null;
        await _page.RunAsync(RecorderScripts.Set(on: false, _values), cancellationToken).ConfigureAwait(false);
        var stopped = new TaskCompletionSource<RecordingSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Writer.TryWrite(stopped);
        return await stopped.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Waits until a recording ends, one that is on or the next one. False when the time ran out first.</summary>
    public async Task<bool> WaitEndedAsync(TimeSpan limit, CancellationToken cancellationToken = default)
    {
        Task ended;
        lock (_gate)
            ended = _ended.Task;
        try
        {
            await ended.WaitAsync(limit, _time, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>Completes when everything the page posted until now is written.</summary>
    public Task IdleAsync()
    {
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Writer.TryWrite(idle);
        return idle.Task;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _page.Message -= OnMessage;
        _queue.Writer.TryComplete();
        await _worker.ConfigureAwait(false);
    }

    private void OnMessage(object? sender, string message) => _queue.Writer.TryWrite(message);

    private async Task WorkAsync()
    {
        await foreach (var item in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            switch (item)
            {
                case TaskCompletionSource idle:
                    idle.TrySetResult();
                    break;
                case TaskCompletionSource<RecordingSession?> stop:
                    stop.TrySetResult(End());
                    break;
                case string message:
                    await HandleAsync(message).ConfigureAwait(false);
                    break;
            }
        }
    }

    private RecordingSession? End()
    {
        RecordingSession? session;
        TaskCompletionSource ended;
        lock (_gate)
        {
            session = _session;
            _session = null;
            _project = null;
            ended = _ended;
            _ended = New();
        }

        ended.TrySetResult();
        Changed?.Invoke(this, EventArgs.Empty);
        return session;
    }

    private async Task HandleAsync(string json)
    {
        Posted? posted;
        try
        {
            posted = JsonSerializer.Deserialize<Posted>(json, PostedJson);
        }
        catch (JsonException)
        {
            return; // not the watcher's: a page may post to the host too
        }

        if (posted?.Kind is null)
            return;
        var (project, session) = (_project, _session);
        if (posted.Kind == "hello")
        {
            // A new document: its watcher is off until told otherwise.
            if (session is not null)
                await _page.RunAsync(RecorderScripts.Set(on: true, _values)).ConfigureAwait(false);
            return;
        }

        if (project is null || session is null || posted.Capture is null)
            return;
        try
        {
            Write(project, session, posted);
            Problem = null;
        }
        catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
        {
            Problem = e.Message;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Write(ScrapeProject project, RecordingSession session, Posted posted)
    {
        var now = _time.GetUtcNow();
        _at = now > _at ? now : _at.AddTicks(1); // two events never share a time
        var snapshot = CapturedDocument.Read(posted.Capture!, _at);
        // A page of another site (a sign-in provider, a link out) is not the project's to record.
        if (!Uri.TryCreate(snapshot.Location, UriKind.Absolute, out var location) || !MayRecord(project, location))
            return;

        // The same page as the last capture, read again: one capture. A press points at a node of that one.
        var hash = SnapshotFile.ContentHash(snapshot);
        if (hash != _lastHash)
        {
            session.AddPage(snapshot);
            _lastHash = hash;
            _pages++;
        }

        if (posted.Kind != "press")
            return;
        var node = posted.Node >= 1 && posted.Node <= snapshot.Nodes.Count ? posted.Node : 0;
        var target = node > 0 ? PressTarget.Of(snapshot, snapshot[node], project.Config.TestIds) : null;
        session.AddPress(ContentFrame.Location(snapshot), posted.Name ?? "", posted.Role ?? "button", target, node, _at.AddTicks(1));
        _at = _at.AddTicks(1);
        _presses++;
    }

    private static bool MayRecord(ScrapeProject project, Uri location) =>
        RequestGuard.IsExploreHost(location, project.Config.Explore) || project.IsOwnPage(location.AbsoluteUri);

    private static TaskCompletionSource New() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static readonly JsonSerializerOptions PostedJson = new() { PropertyNameCaseInsensitive = true };

    private sealed record Posted(string? Kind, string? Name, string? Role, int Node, string? Capture);
}

/// <summary>A locator for what a person pressed, from what the capture holds of it.</summary>
public static class PressTarget
{
    /// <summary>A CSS locator of a node that only it answers to in its capture: a test id, an id, an accessible name, a name; null when it has none.</summary>
    public static string? Of(ScrapeSnapshot snapshot, ScrapeNode node, IReadOnlyList<string> testIds)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(testIds);
        bool Unique(Func<ScrapeNode, bool> match) => snapshot.Nodes.Count(match) == 1;
        foreach (var attribute in testIds)
        {
            if (node.Attribute(attribute) is { Length: > 0 } id && Unique(n => n.Attribute(attribute) == id))
                return $"css:[{attribute}='{id}']";
        }

        if (node.Attribute("id") is { Length: > 0 } elementId && !elementId.Any(char.IsDigit) && Unique(n => n.Attribute("id") == elementId))
            return $"css:#{elementId}";
        if (node.Name is { Length: > 0 } name && Unique(n => n.Name == name && n.Type == node.Type))
            return $"css:{node.Type}[aria-label='{name}']";
        if (node.Attribute("name") is { Length: > 0 } formName && Unique(n => n.Attribute("name") == formName))
            return $"css:{node.Type}[name='{formName}']";
        return null;
    }
}
