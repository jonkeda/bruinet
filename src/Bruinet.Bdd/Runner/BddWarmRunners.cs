using System.Diagnostics;

namespace Bruinet.Bdd.Runner;

/// <summary>
/// Runners kept ready, one per suite output, each started from a copy of the output
/// (<see cref="BddShadowCopy"/>), so a run starts at once and a build goes through while one waits
/// (AD-025). A host takes one for a run and gives it back after; one whose output
/// has changed since its copy is replaced.
/// </summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed class BddWarmRunners : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _waiting = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<BddRunnerClient, Entry> _taken = [];

    // Every runner not ended yet, starting ones included: their copies are not cleaned.
    private readonly HashSet<Entry> _live = [];
    private static int _pools;
    private readonly string? _root;

    // This pool's copies: another pool in the process never cleans them.
    private readonly string _owner = $"{BddShadowCopy.ProcessOwner}-{Interlocked.Increment(ref _pools)}";
    private readonly Action<Process>? _started;
    private bool _disposed;

    /// <summary>Creates the pool.</summary>
    /// <param name="root">Where the copies go; <see cref="BddShadowCopy.DefaultRoot"/> by default.</param>
    /// <param name="started">Told of each runner process started, to end it with the host (a job object).</param>
    public BddWarmRunners(string? root = null, Action<Process>? started = null)
    {
        _root = root;
        _started = started;
    }

    /// <summary>Starts a runner for <paramref name="start"/> in the background, unless a current one waits already.</summary>
    /// <param name="start">How the suite's runner starts, from its output.</param>
    /// <param name="prepare">What the new runner does before it waits, such as making the suite's fixture (<see cref="BddRunnerPrepare"/>); a failure leaves it to the run.</param>
    /// <returns>Completes when it is ready; a failure is the next take's to report.</returns>
    public Task WarmAsync(BddRunnerStart start, Func<BddRunnerClient, Task>? prepare = null)
    {
        ArgumentNullException.ThrowIfNull(start);
        Entry? stale;
        Entry entry;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var fingerprint = BddShadowCopy.Fingerprint(Output(start));
            if (_waiting.TryGetValue(Output(start), out var waiting) && waiting.Fits(start, fingerprint))
            {
                return waiting.Ready;
            }

            stale = waiting;
            entry = Start(start, fingerprint, prepare);
            _waiting[Output(start)] = entry;
        }

        _ = EndAsync(stale);
        return entry.Ready;
    }

    /// <summary>A runner for <paramref name="start"/>: the waiting one when it is current, else a new one from a fresh copy.</summary>
    /// <param name="start">How the suite's runner starts, from its output.</param>
    /// <param name="cancellationToken">Cancels the wait for a new runner.</param>
    public async Task<BddRunnerClient> TakeAsync(BddRunnerStart start, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);
        Entry? stale = null;
        Entry entry;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var fingerprint = BddShadowCopy.Fingerprint(Output(start));
            if (_waiting.Remove(Output(start), out var waiting) && waiting.Fits(start, fingerprint))
            {
                entry = waiting;
            }
            else
            {
                stale = waiting;
                entry = Start(start, fingerprint);
            }
        }

        _ = EndAsync(stale);
        BddRunnerClient runner;
        try
        {
            runner = await entry.Ready.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _ = EndAsync(entry);
            throw;
        }

        if (runner.Process.HasExited)
        {
            // Ended while it waited (a crash, or killed): one more, new.
            _ = EndAsync(entry);
            return await TakeAsync(start, cancellationToken).ConfigureAwait(false);
        }

        lock (_gate)
        {
            _taken[runner] = entry;
        }

        return runner;
    }

    /// <summary>
    /// Gives a runner back after a run, its scenario closed: it waits for the next run, unless its
    /// output changed since its copy, another waits already, or it has ended. Otherwise it ends.
    /// </summary>
    /// <param name="runner">A runner <see cref="TakeAsync"/> gave.</param>
    /// <param name="prepare">What it does again before it waits, such as making the suite's next fixture; the next take waits for it.</param>
    public void Return(BddRunnerClient runner, Func<BddRunnerClient, Task>? prepare = null)
    {
        ArgumentNullException.ThrowIfNull(runner);
        Entry? end;
        lock (_gate)
        {
            if (!_taken.Remove(runner, out var entry))
            {
                end = null;
                _ = runner.DisposeAsync().AsTask();
            }
            else if (!_disposed && !runner.Process.HasExited && !_waiting.ContainsKey(entry.Key)
                     && BddShadowCopy.Fingerprint(entry.Key) == entry.Fingerprint)
            {
                if (prepare is not null)
                {
                    entry.Ready = PrepareAsync(runner, prepare);
                }

                _waiting[entry.Key] = entry;
                end = null;
            }
            else
            {
                end = entry;
            }
        }

        _ = EndAsync(end);
    }

    /// <summary>Ends the runner waiting for a suite: its last feature file closed and it sat idle, say.</summary>
    /// <param name="start">How the suite's runner starts, from its output.</param>
    public Task EndAsync(BddRunnerStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        Entry? waiting;
        lock (_gate)
        {
            _waiting.Remove(Output(start), out waiting);
        }

        return EndAsync(waiting);
    }

    /// <summary>Whether a runner waits for the suite now, ready or starting.</summary>
    /// <param name="start">How the suite's runner starts, from its output.</param>
    public bool IsWarm(BddRunnerStart start)
    {
        lock (_gate)
        {
            return _waiting.ContainsKey(Output(start));
        }
    }

    /// <summary>Ends every runner, waiting or taken.</summary>
    public async ValueTask DisposeAsync()
    {
        List<Entry> all;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            all = [.. _live];
            _waiting.Clear();
            _taken.Clear();
        }

        await Task.WhenAll(all.Select(EndAsync)).ConfigureAwait(false);
    }

    private static string Output(BddRunnerStart start) => Path.GetDirectoryName(Path.GetFullPath(start.PagesAssembly))!;

    // Copies the output and starts a runner there, off the caller's thread: the copy takes a moment.
    private Entry Start(BddRunnerStart start, string fingerprint, Func<BddRunnerClient, Task>? prepare = null)
    {
        var entry = new Entry(Output(start), start, fingerprint) { Copy = BddShadowCopy.NewFolder(Output(start), _root, _owner) };
        _live.Add(entry);
        entry.Ready = Task.Run(async () =>
        {
            BddShadowCopy.CleanEnded(_root);
            BddShadowCopy.Make(entry.Key, entry.Copy);
            var runner = await BddRunnerClient.StartAsync(BddShadowCopy.Into(start, entry.Copy)).ConfigureAwait(false);
            _started?.Invoke(runner.Process);
            return prepare is null ? runner : await PrepareAsync(runner, prepare).ConfigureAwait(false);
        });
        return entry;
    }

    // The run before closed its fixture: the next is made now, while nothing waits on the runner.
    private static async Task<BddRunnerClient> PrepareAsync(BddRunnerClient runner, Func<BddRunnerClient, Task> prepare)
    {
        try
        {
            await prepare(runner).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is BddRunnerException or IOException or OperationCanceledException)
        {
            // The run makes what was not prepared, and says what went wrong then.
        }

        return runner;
    }

    private async Task EndAsync(Entry? entry)
    {
        if (entry is null)
        {
            return;
        }

        try
        {
            var runner = await entry.Ready.ConfigureAwait(false);
            lock (_gate)
            {
                _taken.Remove(runner);
            }

            await runner.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is BddRunnerException or IOException or OperationCanceledException or FileNotFoundException or UnauthorizedAccessException)
        {
            // It never started: nothing to end.
        }

        List<string> inUse;
        lock (_gate)
        {
            _live.Remove(entry);
            inUse = [.. _live.Where(other => other.Key.Equals(entry.Key, StringComparison.OrdinalIgnoreCase)).Select(other => other.Copy)];
        }

        BddShadowCopy.Clean(entry.Key, inUse, _root, _owner);
    }

    private sealed class Entry(string key, BddRunnerStart start, string fingerprint)
    {
        public string Key { get; } = key;

        public BddRunnerStart Start { get; } = start;

        public string Fingerprint { get; } = fingerprint;

        public Task<BddRunnerClient> Ready { get; set; } = null!;

        public required string Copy { get; init; }

        // The same suite, started the same way, from the output as it is now.
        public bool Fits(BddRunnerStart start, string fingerprint)
            => fingerprint == Fingerprint
               && string.Equals(start.WorkingDirectory, Start.WorkingDirectory, StringComparison.OrdinalIgnoreCase)
               && Same(start.Environment, Start.Environment);

        private static bool Same(IReadOnlyDictionary<string, string>? left, IReadOnlyDictionary<string, string>? right)
            => (left?.Count ?? 0) == (right?.Count ?? 0)
               && (left ?? new Dictionary<string, string>()).All(pair => right is not null && right.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }
}
