using System.Text;
using System.Text.Json.Nodes;
using System.Globalization;

namespace Bruinet.Mocking.Data.Replay;

/// <summary>A command replay could not answer from the recording, or a recording made stale by a schema change.</summary>
public sealed class ReplayMissException(string message) : Exception(message);

/// <summary>
/// One test's recording or replay. Nothing in it is shared: tests in parallel each have their own, handed to the app
/// through its composition or <see cref="ReplayScope"/>. In replay a command is answered by the recorded exchange with the
/// same key: writes in their recorded order, as barriers; the reads between two writes in any order, and a read repeated
/// more often than when recorded answered again, since between two writes it returns the same rows. Volatile values are
/// bound, recorded to live, the first time they meet, and recorded results are rewritten through the bindings.
/// </summary>
public sealed class ReplaySession
{
    private readonly object _gate = new();
    private readonly Recording _recording;
    private readonly Recording? _previous;
    private readonly Schema? _schema;
    private readonly string? _stale;
    private readonly int[] _epochs;
    private readonly bool[] _used;
    private readonly Dictionary<string, JsonNode?> _bindings = new(StringComparer.Ordinal);
    private readonly List<string> _misses = [];
    private int _writes;

    /// <param name="mode">Record or replay.</param>
    /// <param name="path">The test's recording: <c>&lt;folder&gt;/&lt;head&gt;/&lt;test&gt;.json</c>, a file per test and head.</param>
    /// <param name="schema">The schema now, for the stamp; null to record and replay without one.</param>
    /// <param name="ignoreStamp">Replays a stale recording anyway, to see which commands a change touched.</param>
    public ReplaySession(ReplayMode mode, string path, Schema? schema = null, bool ignoreStamp = false)
    {
        if (mode == ReplayMode.Off)
        {
            throw new ArgumentException("A session records or replays; with replay off, give the app no session.", nameof(mode));
        }

        Mode = mode;
        Path = path;
        _schema = schema;
        if (mode == ReplayMode.Replay)
        {
            _recording = Recording.Load(path) ?? throw new ReplayMissException($"No recording at {path}: record the test first, twice.");
            if (!ignoreStamp && schema is not null && _recording.Stamp?.Differences(schema) is { Count: > 0 } differences)
            {
                _stale = $"Stale recording {System.IO.Path.GetFileName(path)}: re-record it, twice. Changed: {string.Join("; ", differences)}.";
            }
        }
        else
        {
            _recording = new Recording();
            _previous = Recording.Load(path);
            if (_previous is not null && schema is not null && _previous.Stamp?.Differences(schema) is not { Count: 0 })
            {
                // Recorded against other objects or versions: not the same run, so not a source of volatile values.
                _previous = null;
            }
        }

        _epochs = new int[_recording.Exchanges.Count];
        _used = new bool[_recording.Exchanges.Count];
        var writes = 0;
        for (var i = 0; i < _epochs.Length; i++)
        {
            _epochs[i] = writes;
            if (_recording.Exchanges[i].IsWrite)
            {
                writes++;
            }
        }
    }

    public ReplayMode Mode { get; }

    public string Path { get; }

    /// <summary>The misses so far: a test checks them at its end, as a miss inside the app may have been caught there.</summary>
    public IReadOnlyList<string> Misses
    {
        get
        {
            lock (_gate)
            {
                return [.. _misses];
            }
        }
    }

    /// <summary>Record mode: keeps <paramref name="exchange"/> in the order it happened.</summary>
    public void Record(Exchange exchange)
    {
        lock (_gate)
        {
            exchange.Index = _recording.Exchanges.Count;
            _recording.Exchanges.Add(exchange);
        }
    }

    /// <summary>Record mode: saves the recording, its volatile values found against the one it replaces, and its stamp.</summary>
    public void Finish()
    {
        if (Mode != ReplayMode.Record)
        {
            return;
        }

        lock (_gate)
        {
            if (_previous is not null)
            {
                _recording.MarkVolatile(_previous);
            }

            _recording.Stamp = _schema?.StampFor(_recording.Exchanges.SelectMany(exchange => _schema.Named(exchange.Text)));
            SqlRedaction.Apply(_recording, Path);
            _recording.Save(Path);
        }
    }

    /// <summary>Replay mode: the recorded exchange answering <paramref name="live"/>, its results rewritten through the bindings.</summary>
    public Exchange Answer(Exchange live)
    {
        lock (_gate)
        {
            if (_stale is not null)
            {
                if (_misses.Count == 0)
                {
                    _misses.Add(_stale);
                }

                throw new ReplayMissException(_stale);
            }

            var key = Statements.Key(live.Text);
            Dictionary<string, JsonNode?>? bound = null;
            var index = -1;

            // An unused exchange first; then, for a read, one already used (a read repeated more often than when recorded).
            foreach (var reuse in new[] { false, true })
            {
                for (var i = 0; i < _recording.Exchanges.Count && index < 0; i++)
                {
                    var recorded = _recording.Exchanges[i];
                    if (_used[i] != reuse || (reuse && live.IsWrite) || recorded.Kind != live.Kind || Statements.Key(recorded.Text) != key)
                    {
                        continue;
                    }

                    // A write is only the next write; a read only one between the same two writes.
                    if (live.IsWrite ? !recorded.IsWrite || _epochs[i] != _writes || NextWrite() != i : recorded.IsWrite || _epochs[i] != _writes)
                    {
                        continue;
                    }

                    if (Matches(recorded, live, out bound))
                    {
                        index = i;
                    }
                }

                if (index >= 0)
                {
                    break;
                }
            }

            if (index < 0)
            {
                var miss = Describe(live, key);
                _misses.Add(miss);
                throw new ReplayMissException(miss);
            }

            _used[index] = true;
            foreach (var pair in bound!)
            {
                _bindings[pair.Key] = pair.Value;
            }

            if (live.IsWrite)
            {
                _writes++;
            }

            return Reorder(Rewrite(_recording.Exchanges[index]), live);
        }
    }

    /// <summary>A recorded value through the bindings: the live value a volatile one was bound to, else itself.</summary>
    public JsonNode? Bound(JsonNode? value)
    {
        lock (_gate)
        {
            return Values.Canonical(value) is { } canonical && _bindings.TryGetValue(canonical, out var live) ? live?.DeepClone() : value?.DeepClone();
        }
    }

    private int NextWrite()
    {
        for (var i = 0; i < _recording.Exchanges.Count; i++)
        {
            if (_recording.Exchanges[i].IsWrite && !_used[i])
            {
                return i;
            }
        }

        return -1;
    }

    private bool Matches(Exchange recorded, Exchange live, out Dictionary<string, JsonNode?> bound)
    {
        bound = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        if (recorded.Parameters.Count != live.Parameters.Count)
        {
            return false;
        }

        var actuals = Aligned(recorded, live);
        foreach (var pair in recorded.Parameters)
        {
            if (!actuals.TryGetValue(pair.Key, out var actual))
            {
                return false;
            }

            if (!recorded.Volatile.Contains(pair.Key))
            {
                if (!Values.Same(pair.Value, actual))
                {
                    return false;
                }

                continue;
            }

            // Bound already: the live value must be the one it was bound to. Not yet: bind it once this exchange matches.
            var canonical = Values.Canonical(pair.Value);
            if (canonical is not null && _bindings.TryGetValue(canonical, out var binding))
            {
                if (!Values.Same(binding, actual))
                {
                    return false;
                }
            }
            else if (canonical is not null)
            {
                bound[canonical] = actual?.DeepClone();
            }
        }

        return true;
    }

    private Exchange Rewrite(Exchange recorded)
    {
        if (_bindings.Count == 0 || recorded.Results is null)
        {
            return recorded;
        }

        return recorded.With([.. recorded.Results.Select(result => new RecordedResult(
            result.Columns,
            [.. result.Rows.Select(row => (IReadOnlyList<JsonNode?>)[.. row.Select(BoundUnlocked)])]))]);
    }

    private JsonNode? BoundUnlocked(JsonNode? value)
        => Values.Canonical(value) is { } canonical && _bindings.TryGetValue(canonical, out var live) ? live?.DeepClone() : value?.DeepClone();

    // The live parameters under the recorded names: an insert's by the column each fills, the others by their own name.
    private static Dictionary<string, JsonNode?> Aligned(Exchange recorded, Exchange live)
    {
        var aligned = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var pair in live.Parameters)
        {
            aligned[pair.Key] = pair.Value;
        }

        var was = Statements.InsertParameters(recorded.Text);
        if (was.Count == 0)
        {
            return aligned;
        }

        foreach (var pair in Statements.InsertParameters(live.Text))
        {
            if (was.TryGetValue(pair.Key, out var recordedName) && live.Parameters.TryGetValue(pair.Value, out var value))
            {
                aligned[recordedName] = value;
            }
        }

        return aligned;
    }

    // The recorded columns in the order the live query asks for them: matched as a set, read by ordinal.
    private static Exchange Reorder(Exchange recorded, Exchange live)
    {
        if (recorded.Results is not { Count: > 0 } results
            || Statements.Projection(Statements.Normalize(recorded.Text)) is not { } was
            || Statements.Projection(Statements.Normalize(live.Text)) is not { } now
            || was.Columns.SequenceEqual(now.Columns)
            || was.Columns.Count != results[0].Columns.Count)
        {
            return recorded;
        }

        var first = results[0];
        var recordedColumns = was.Columns.ToList();
        var order = now.Columns.Select(column => recordedColumns.IndexOf(column)).ToArray();
        var reordered = new RecordedResult(
            [.. order.Select(i => first.Columns[i])],
            [.. first.Rows.Select(row => (IReadOnlyList<JsonNode?>)[.. order.Select(i => row[i])])]);
        return recorded.With([reordered, .. results.Skip(1)]);
    }

    private string Describe(Exchange live, string key)
    {
        var text = new StringBuilder($"SQL replay miss in {System.IO.Path.GetFileName(Path)} after {_writes} writes: no recorded {live.Kind} answers\n  {key}\n  with {Show(live.Parameters)}");
        var unused = _recording.Exchanges.Where((exchange, i) => !_used[i]).ToList();
        var same = unused.Where(exchange => Statements.Key(exchange.Text) == key).ToList();
        if (same.Count > 0)
        {
            return text.Append("\nNearest, the same text with other values:\n  ")
                .Append(string.Join("\n  ", same.Select(exchange => $"#{exchange.Index} {(exchange.IsWrite ? "write" : "read")} with {Show(exchange.Parameters)}, volatile [{string.Join(", ", exchange.Volatile)}]")))
                .ToString();
        }

        // Another text: the unused command of the same kind and place that shares the longest start, and where they part.
        var nearest = unused
            .Where(exchange => exchange.Kind == live.Kind && exchange.IsWrite == live.IsWrite && _epochs[exchange.Index] == _writes)
            .OrderByDescending(exchange => Common(Statements.Key(exchange.Text), key))
            .FirstOrDefault();
        if (nearest is null)
        {
            return text.Append("\nNo recorded command is left at this point: the app sends a command it didn't send when recorded.").ToString();
        }

        var recordedKey = Statements.Key(nearest.Text);
        var at = Common(recordedKey, key);
        return text.Append(FormattableString.Invariant($"\nNearest, #{nearest.Index}, parts at character {at}:\n  recorded ...{Around(recordedKey, at)}...\n  now      ...{Around(key, at)}...")).ToString();
    }

    private static int Common(string left, string right)
    {
        var i = 0;
        while (i < left.Length && i < right.Length && left[i] == right[i])
        {
            i++;
        }

        return i;
    }

    private static string Around(string text, int at)
    {
        var start = Math.Max(0, at - 20);
        return text.Substring(start, Math.Min(text.Length - start, 60));
    }

    private static string Show(Dictionary<string, JsonNode?> parameters)
        => "{" + string.Join(", ", parameters.Select(pair => $"{pair.Key}={Values.Canonical(SqlRedaction.Shown(pair.Key, pair.Value)) ?? "null"}")) + "}";
}

/// <summary>
/// The session of the async flow that runs now, for code that makes its connections itself (a static factory, a
/// <see cref="ReplayingDbProviderFactory"/>): a test enters its own, so tests in parallel never see each other's.
/// </summary>
public static class ReplayScope
{
    private static readonly AsyncLocal<ReplaySession?> Now = new();

    /// <summary>The session of this flow, else the <see cref="Fallback"/>'s; null outside one.</summary>
    public static ReplaySession? Current => Now.Value ?? Fallback?.Invoke();

    /// <summary>
    /// Where the session is found when the flow has none: a host whose requests don't flow an <c>AsyncLocal</c> (System.Web's
    /// pipeline) keeps it per request and reads it here. Set once by that host; it resolves per call, so requests in
    /// parallel each get their own.
    /// </summary>
    public static Func<ReplaySession?>? Fallback { get; set; }

    /// <summary>Enters <paramref name="session"/> for this flow and what it starts; disposing puts back the one before.</summary>
    public static IDisposable Enter(ReplaySession? session)
    {
        var before = Now.Value;
        Now.Value = session;
        return new Restore(before);
    }

    private sealed class Restore(ReplaySession? before) : IDisposable
    {
        public void Dispose() => Now.Value = before;
    }
}
