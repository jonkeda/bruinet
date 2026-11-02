using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Bruinet.Bdd.LanguageServer;

/// <summary>
/// <c>bruinet mcp-host</c>: the MCP server an agent's session starts once, which runs the real server
/// (<c>bruinet mcp</c>) as a child from a snapshot of a build folder and starts it again when that folder gets a new
/// build. The session keeps its connection; the agent is told the tools changed (<c>notifications/tools/list_changed</c>).
/// Neither process holds the build folder, so a build or an update can always write it: while Bruinet is developed,
/// a session never has to restart for a new server.
/// </summary>
public sealed class McpReloadingHost : IDisposable
{
    // Written by the update into the build folder after its last file: a new value is a new build.
    public const string StampFile = "mcp.stamp";

    private readonly string _build;
    private readonly string _runs;
    private readonly IReadOnlyList<string> _serverArguments;
    private readonly object _gate = new();
    private readonly HashSet<string> _pending = new(StringComparer.Ordinal);
    private TextWriter? _client;
    private Process? _child;
    private string? _stamp;
    private string? _snapshot;
    private string? _initialize;
    private bool _swapping;

    /// <param name="build">The folder a build of the CLI is copied into (<c>.bruinet/mcp</c>).</param>
    /// <param name="runs">Where each server's snapshot goes (<c>.bruinet/run</c>).</param>
    /// <param name="serverArguments">The child's arguments after <c>bruinet.dll</c>: <c>mcp --solution ...</c>.</param>
    public McpReloadingHost(string build, string runs, IReadOnlyList<string> serverArguments)
    {
        _build = Path.GetFullPath(build);
        _runs = Path.GetFullPath(runs);
        _serverArguments = serverArguments;
    }

    /// <summary>How often the build folder's stamp is looked at.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>How long a swap waits for the requests in flight before it goes ahead.</summary>
    public TimeSpan Drain { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Raised after the server was started again on a new build: its stamp.</summary>
    public event Action<string>? Reloaded;

    /// <summary>Serves until the input ends: each line to the server, each of its lines back.</summary>
    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken = default)
    {
        _client = output;
        CleanRuns();
        Start(replay: false);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var watch = Task.Run(() => WatchAsync(stop.Token), CancellationToken.None);
        try
        {
            while (await input.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (line.Trim().Length == 0)
                {
                    continue;
                }

                await ToServerAsync(line).ConfigureAwait(false);
            }
        }
        finally
        {
            await stop.CancelAsync().ConfigureAwait(false);
            await watch.ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose() => Stop(_child);

    // A request is remembered until answered (a swap waits for it); the first initialize is kept to replay.
    private async Task ToServerAsync(string line)
    {
        while (true)
        {
            Process child;
            lock (_gate)
            {
                if (_swapping)
                {
                    Monitor.Wait(_gate, TimeSpan.FromMilliseconds(100));
                    continue;
                }

                child = _child!;
                if (Message(line) is { } message)
                {
                    if ((string?)message["method"] == "initialize")
                    {
                        _initialize ??= line;
                    }

                    if (message["id"] is { } id && message["method"] is not null)
                    {
                        _pending.Add(id.ToJsonString());
                    }
                }
            }

            await child.StandardInput.WriteLineAsync(line).ConfigureAwait(false);
            await child.StandardInput.FlushAsync().ConfigureAwait(false);
            return;
        }
    }

    // The server's lines to the client; the answer to the replayed initialize is not passed on.
    private void FromServer(Process child, string line, bool replaying)
    {
        var message = Message(line);
        lock (_gate)
        {
            if (!ReferenceEquals(child, _child))
            {
                return;
            }

            if (message?["id"] is { } id && message["method"] is null)
            {
                _pending.Remove(id.ToJsonString());
                if (replaying && id.ToJsonString() == ReplayId)
                {
                    return;
                }
            }

            // The tools may change: say so, so the client listens for list_changed.
            if (message?["result"]?["capabilities"]?["tools"] is JsonObject tools)
            {
                tools["listChanged"] = true;
                line = message.ToJsonString();
            }

            _client!.WriteLine(line);
            _client.Flush();
            Monitor.PulseAll(_gate);
        }
    }

    private const string ReplayId = "\"bruinet-mcp-host-replay\"";

    private void Start(bool replay)
    {
        _stamp = Stamp();
        var snapshot = Path.Combine(_runs, $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Environment.ProcessId}-{Guid.NewGuid():N}"[..48]);
        CopyFolder(_build, snapshot);
        _snapshot = snapshot;
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add("exec");
        info.ArgumentList.Add(Path.Combine(snapshot, "bruinet.dll"));
        foreach (var argument in _serverArguments)
        {
            info.ArgumentList.Add(argument);
        }

        var child = Process.Start(info) ?? throw new InvalidOperationException("The MCP server did not start.");
        lock (_gate)
        {
            _child = child;
            _pending.Clear();
        }

        _ = Task.Run(async () =>
        {
            while (await child.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                FromServer(child, line, replay);
            }
        });

        // A new server learns the session as the first did: the client's initialize again, its answer kept back.
        if (replay && _initialize is { } initialize && Message(initialize) is { } message)
        {
            message["id"] = JsonNode.Parse(ReplayId);
            child.StandardInput.WriteLine(message.ToJsonString());
            child.StandardInput.WriteLine("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
            child.StandardInput.Flush();
        }
    }

    private async Task WatchAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Poll, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (Stamp() is { } stamp && stamp != _stamp)
            {
                Swap(stamp);
            }
        }
    }

    // Waits for the requests in flight (a long run_scenario included), then the new server takes over.
    private void Swap(string stamp)
    {
        Process? old;
        var waited = Stopwatch.StartNew();
        lock (_gate)
        {
            _swapping = true;
            while (_pending.Count > 0 && waited.Elapsed < Drain)
            {
                Monitor.Wait(_gate, TimeSpan.FromMilliseconds(200));
            }

            old = _child;
        }

        try
        {
            Start(replay: true);
        }
        finally
        {
            lock (_gate)
            {
                _swapping = false;
                Monitor.PulseAll(_gate);
                _client!.WriteLine("""{"jsonrpc":"2.0","method":"notifications/tools/list_changed"}""");
                _client.Flush();
            }
        }

        Stop(old);
        CleanRuns();
        Reloaded?.Invoke(stamp);
    }

    private string? Stamp()
    {
        var path = Path.Combine(_build, StampFile);
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : File.GetLastWriteTimeUtc(Path.Combine(_build, "bruinet.dll")).Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (IOException)
        {
            // The update is writing it: the next look.
            return _stamp;
        }
    }

    private static void Stop(Process? child)
    {
        if (child is null)
        {
            return;
        }

        try
        {
            child.StandardInput.Close();
            if (!child.WaitForExit(TimeSpan.FromSeconds(5)))
            {
                child.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            Trace.TraceWarning($"The old MCP server did not stop cleanly: {exception.Message}");
        }
        finally
        {
            child.Dispose();
        }
    }

    // Snapshots no server holds any more; never the one running now, whose files load as they are first used.
    private void CleanRuns()
    {
        if (!Directory.Exists(_runs))
        {
            return;
        }

        foreach (var folder in Directory.EnumerateDirectories(_runs).Where(folder => !string.Equals(folder, _snapshot, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Trace.TraceInformation($"Still in use, kept: {folder}");
            }
        }
    }

    private static void CopyFolder(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(from, file);
            if (relative.Contains(".stale-", StringComparison.Ordinal))
            {
                continue;
            }

            var target = Path.Combine(to, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static JsonObject? Message(string line)
    {
        try
        {
            return JsonNode.Parse(line) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
