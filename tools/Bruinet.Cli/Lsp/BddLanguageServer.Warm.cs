using Bruinet.Bdd.Runner;
using StreamJsonRpc;

namespace Bruinet.Bdd.LanguageServer;

/// <summary>
/// The warm runner (AD-025): a suite's runner starts, from a copy of its build output,
/// when one of its feature files opens and again after a build, so a Run or Step through starts at once
/// and a build goes through while it waits. It ends once the suite's last feature file has been closed
/// for the idle minutes, and with the server.
/// </summary>
public sealed partial class BddLanguageServer
{
    private readonly Dictionary<string, Timer> _idle = new(StringComparer.OrdinalIgnoreCase);

    // Each suite's config and Pages assembly, by its runner's start, for making its fixture ahead.
    private readonly Dictionary<string, (string Config, string Pages)> _suiteFiles = new(StringComparer.OrdinalIgnoreCase);
    private BddWarmRunners? _warm;
    private bool _warmRunner = true;
    private TimeSpan _warmIdle = TimeSpan.FromMinutes(10);

    /// <summary><c>bruinet/runner/warm</c>: starts the runner of the suite that holds a feature file, and answers once it is ready.</summary>
    [JsonRpcMethod("bruinet/runner/warm")]
    public async Task<RunnerWarmResult> RunnerWarmAsync(RunnerWarmParams request)
    {
        if (RunnerStartFor(Key(request.Uri), request.Suite) is not { } start)
        {
            return new RunnerWarmResult(false, "No built suite holds this feature file.");
        }

        try
        {
            await Warm.WarmAsync(start, Prepare(start)).ConfigureAwait(false);
            return new RunnerWarmResult(true, null);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new RunnerWarmResult(false, exception.Message);
        }
    }

    private BddWarmRunners Warm
    {
        get
        {
            lock (_gate)
            {
                return _warm ??= new BddWarmRunners(started: process => RunnerJob.Add(process));
            }
        }
    }

    private void WarmSettings(InitializationOptions? options)
    {
        _warmRunner = options?.WarmRunner ?? true;
        _warmIdle = TimeSpan.FromMinutes(options?.WarmRunnerIdleMinutes is { } minutes and > 0 ? minutes : 10);
    }

    // How the runner of the suite that holds a file starts; null when no built suite does.
    private BddRunnerStart? RunnerStartFor(string path, string? suiteName)
    {
        if (!path.EndsWith(BddFileFormat.FeatureExtension, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // As a run finds it, so the warm runner is the one a run takes.
        var suite = Context(path, suiteName).Suite;
        if (suite?.RunnerAssemblies is not { } assemblies)
        {
            return null;
        }

        lock (_gate)
        {
            _suiteFiles[assemblies.Start] = (suite.ConfigPath, assemblies.Pages);
        }

        return new BddRunnerStart(assemblies.Start, suite.WorkingDirectory, RunnerEnvironment(suite));
    }

    // I3: the warm runner makes the suite's fixture, and so starts its app, before the first run.
    private Func<BddRunnerClient, Task>? Prepare(BddRunnerStart start)
    {
        lock (_gate)
        {
            if (!_suiteFiles.TryGetValue(start.PagesAssembly, out var files))
            {
                return null;
            }

            return async runner =>
            {
                using var timeout = new CancellationTokenSource(BddRunnerClient.ConnectTimeout * 4);
                await runner.RequestAsync(new BddRunnerPrepare(files.Config, files.Pages), timeout.Token).ConfigureAwait(false);
            };
        }
    }

    // A feature file opened: its suite's runner gets ready, and no longer waits to end.
    private void WarmOnOpen(string path)
    {
        if (!_warmRunner || RunnerStartFor(path, null) is not { } start)
        {
            return;
        }

        lock (_gate)
        {
            if (_idle.Remove(start.PagesAssembly, out var timer))
            {
                timer.Dispose();
            }
        }

        _ = WarmQuietlyAsync(start);
    }

    // A feature file closed: when it was its suite's last, the runner ends after the idle minutes.
    private void WarmOnClose(string path)
    {
        if (RunnerStartFor(path, null) is not { } start)
        {
            return;
        }

        lock (_gate)
        {
            var stillOpen = _documents.Keys.Any(open => RunnerStartFor(open, null)?.PagesAssembly.Equals(start.PagesAssembly, StringComparison.OrdinalIgnoreCase) == true);
            if (stillOpen || _idle.ContainsKey(start.PagesAssembly))
            {
                return;
            }

            _idle[start.PagesAssembly] = new Timer(_ => EndIdle(start), null, _warmIdle, Timeout.InfiniteTimeSpan);
        }
    }

    private void EndIdle(BddRunnerStart start)
    {
        lock (_gate)
        {
            if (_idle.Remove(start.PagesAssembly, out var timer))
            {
                timer.Dispose();
            }
        }

        _ = Warm.EndAsync(start);
    }

    // After a build: each open suite's runner again, from the new output.
    private void WarmAfterBuild(IEnumerable<string> open)
    {
        if (!_warmRunner)
        {
            return;
        }

        foreach (var start in open.Select(path => RunnerStartFor(path, null)).OfType<BddRunnerStart>().DistinctBy(start => start.PagesAssembly, StringComparer.OrdinalIgnoreCase))
        {
            _ = WarmQuietlyAsync(start);
        }
    }

    // A runner a run took goes back to wait, making the suite's next fixture (I3), unless warm runners are off.
    private void ReturnRunner(BddRunnerClient runner, BddRunnerStart start)
    {
        Warm.Return(runner, _warmRunner ? Prepare(start) : null);
        if (!_warmRunner)
        {
            _ = Warm.EndAsync(start);
        }
    }

    // A failure to start is the next run's to report.
    private async Task WarmQuietlyAsync(BddRunnerStart start)
    {
        try
        {
            await Warm.WarmAsync(start, Prepare(start)).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Not ready: a Run or Step through starts one itself, and says why it cannot.
        }
    }

    private void DisposeWarm()
    {
        BddWarmRunners? warm;
        List<Timer> timers;
        lock (_gate)
        {
            warm = _warm;
            _warm = null;
            timers = [.. _idle.Values];
            _idle.Clear();
        }

        timers.ForEach(timer => timer.Dispose());
        warm?.DisposeAsync().AsTask().Wait(BddRunnerClient.CancelGrace);
    }
}
