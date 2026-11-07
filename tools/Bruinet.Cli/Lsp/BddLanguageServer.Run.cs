using Bruinet.Bdd.Runner;
using Bruinet.Bdd.Workspace;
using Bruinet.Core.Artifacts;
using StreamJsonRpc;

namespace Bruinet.Bdd.LanguageServer;

/// <summary>
/// Run on the runner (AD-022): the preview's Run runs the scenarios in the suite's
/// runner process (AD-025), from the source (I1), with no stops; Run as test stays the IDE's
/// <c>dotnet test</c>. One run at a time, a Step-through run included.
/// </summary>
public sealed partial class BddLanguageServer
{
    private RunnerRun? _run;

    /// <summary>
    /// <c>bruinet/run/start</c>: runs the scenario at a heading's line (an outline's every row), or
    /// every scenario of the file, in the suite's runner; <c>bruinet/run/ended</c> says how each went.
    /// Each result is written as the gate writes it, marked <c>run</c>.
    /// </summary>
    [JsonRpcMethod("bruinet/run/start")]
    public async Task<RunStartResult> RunStartAsync(RunStartParams request)
        => (await StartRunAsync(request, CancellationToken.None).ConfigureAwait(false)).Result;

    /// <summary>
    /// A Run on the runner to its end, for a host that waits instead of listening for
    /// <c>bruinet/run/ended</c> (the MCP server's <c>run_scenario</c>): the run's end, or null with
    /// why it did not start. <paramref name="cancellationToken"/> cancels it as <c>bruinet/run/cancel</c> does.
    /// </summary>
    public async Task<(RunStartResult Started, RunEndedParams? Ended)> RunToEndAsync(RunStartParams request, CancellationToken cancellationToken = default)
    {
        var (started, run) = await StartRunAsync(request, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return (started, null);
        }

        using var cancel = cancellationToken.Register(run.Cancel.Cancel);
        try
        {
            return (started, await run.Ended.Task.ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return (started, new RunEndedParams(run.Uri, "error", exception.Message, run.Results));
        }
    }

    private async Task<(RunStartResult Result, RunnerRun? Run)> StartRunAsync(RunStartParams request, CancellationToken cancellationToken)
    {
        var path = Key(request.Uri);
        var file = Context(path, request.Suite);
        if (file.Suite is not { } suite || !path.EndsWith(BddFileFormat.FeatureExtension, StringComparison.OrdinalIgnoreCase))
        {
            return (RunStartResult.Not("No suite holds or links this feature file."), null);
        }

        if (file.Catalogs is null)
        {
            return (RunStartResult.Not(file.Reason ?? $"{suite.Name} is not built yet."), null);
        }

        if (suite.RunnerAssemblies is not { } assemblies)
        {
            return (RunStartResult.Not($"{suite.Name} has no built test project to run in. Build it first."), null);
        }

        var names = BddMarkdownParser.Parse(file.Text, path).Document?.Scenarios
            .Where(scenario => request.Line is not { } line || scenario.Source.LineNumber == line)
            .Select(scenario => scenario.Name)
            .ToList() ?? [];
        if (names.Count == 0)
        {
            return (RunStartResult.Not(request.Line is { } at ? $"There is no scenario heading at line {at}." : "The file has no scenarios."), null);
        }

        var run = new RunnerRun(request.Uri, names) { Keep = _warmRunner };
        lock (_gate)
        {
            if (_step is not null || _run is not null)
            {
                return (RunStartResult.Not("A run on the suite's runner is going: wait for it, or stop it first."), null);
            }

            _run = run;
        }

        try
        {
            // The suite's warm runner, or a new one from a copy of its output (AD-025).
            var environment = RunnerEnvironment(suite);
            foreach (var (name, value) in request.Environment ?? new Dictionary<string, string>())
            {
                environment[name] = value;
            }

            run.Start = new BddRunnerStart(assemblies.Start, suite.WorkingDirectory, environment);
            run.Runner = await Warm.TakeAsync(run.Start, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            await EndRunAsync(run).ConfigureAwait(false);
            return (RunStartResult.Not(exception is OperationCanceledException ? "Canceled while the suite's runner started." : exception.Message), null);
        }

        var relative = suite.RelativeToConfig(path);
        // Not the caller's token: the run ends by its own cancel (run.Cancel), which RunToEndAsync ties to it.
        _ = Task.Run(() => RunAllAsync(run, suite.ConfigPath, assemblies.Pages, path, relative), CancellationToken.None);
        return (new RunStartResult(true, null, names, suite.Name), run);
    }

    /// <summary><c>bruinet/run/cancel</c>: cancels the step that runs and ends the run, <c>canceled</c>.</summary>
    [JsonRpcMethod("bruinet/run/cancel")]
    public StepMoveResult RunCancel()
    {
        RunnerRun? run;
        lock (_gate)
        {
            run = _run;
        }

        if (run is null)
        {
            return new StepMoveResult(false, "No run on the suite's runner is going.");
        }

        run.Cancel.Cancel();
        return new StepMoveResult(true, null);
    }

    // What a runner a host starts needs: results where the preview reads them (P5 of AD-022); the
    // scenario files from the source, so an edit runs without a build (I1).
    private static Dictionary<string, string> RunnerEnvironment(BddSuite suite)
        => new()
        {
            [TestArtifactOptions.RootDirectoryEnvironmentVariable] = BddRunResults.RootFor(suite.ProjectDirectory),
            [BddScenarioSource.SourceDirectoryEnvironmentVariable] = suite.ProjectDirectory,
        };

    // Each scenario in turn, a step at a time to its end or its first failure; the runner records it.
    private async Task RunAllAsync(RunnerRun run, string config, string pages, string feature, string relative)
    {
        string? error = null;
        try
        {
            foreach (var name in run.Scenarios)
            {
                run.Cancel.Token.ThrowIfCancellationRequested();
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var result = await RunOneAsync(run, config, pages, feature, name).ConfigureAwait(false);
                run.Results.Add(result with { Address = BddScenarioAddress.Of(relative, name), DurationMs = clock.ElapsedMilliseconds });
            }
        }
        catch (OperationCanceledException) when (run.Cancel.IsCancellationRequested)
        {
            // Canceled: the results so far, and the run ends canceled.
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            error = exception.Message;
        }

        try
        {
            await EndRunAsync(run).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            error ??= exception.Message;
        }
        finally
        {
            // Always ended: a host waiting for it (RunToEndAsync) never hangs on a runner that would not close.
            var status = run.Cancel.IsCancellationRequested ? "canceled"
                : error is not null ? "error"
                : run.Results.Count > 0 && run.Results.All(result => result.Status == "skipped") ? "skipped"
                : run.Results.All(result => result.Status is "passed" or "skipped") ? "passed" : "failed";
            var ended = new RunEndedParams(run.Uri, status, error, run.Results);
            run.Ended.TrySetResult(ended);
            _ = _rpc?.NotifyWithParameterObjectAsync("bruinet/run/ended", ended);
        }
    }

    private static async Task<RunScenarioResult> RunOneAsync(RunnerRun run, string config, string pages, string feature, string name)
    {
        var runner = run.Runner!;
        var opened = await runner.RequestAsync(new BddRunnerOpen(config, pages, feature, name, "run"), run.Cancel.Token).ConfigureAwait(false);
        if (opened is not BddRunnerOpened open)
        {
            return new RunScenarioResult(name, "error", opened is BddRunnerFailed failed ? failed.Message : $"The runner answered {opened.GetType().Name}.");
        }

        try
        {
            if (open.Skip is { } why)
            {
                return new RunScenarioResult(name, "skipped", why);
            }

            while (true)
            {
                var answer = await runner.RequestAsync(new BddRunnerNext(), run.Cancel.Token).ConfigureAwait(false);
                if (answer is not BddRunnerStep step)
                {
                    return new RunScenarioResult(name, "error", answer is BddRunnerFailed failed ? failed.Message : $"The runner answered {answer.GetType().Name}.");
                }

                switch (step.Status)
                {
                    case BddStepResultStatus.Failed:
                        return new RunScenarioResult(name, "failed", step.Message, step.Ran is { } ran ? new RunFailedStep(ran.File, ran.Line, ran.Text) : null);
                    case BddStepResultStatus.Canceled:
                        throw new OperationCanceledException(run.Cancel.Token);
                }

                if (!step.HasNext)
                {
                    return new RunScenarioResult(name, "passed", null);
                }
            }
        }
        finally
        {
            // Closes the fixture and its app; a scenario left early keeps no result.
            try
            {
                using var timeout = new CancellationTokenSource(BddRunnerClient.CancelGrace * 2);
                run.Closed = await runner.RequestAsync(new BddRunnerClose(Keep: run.Keep), timeout.Token).ConfigureAwait(false) is BddRunnerDone;
            }
            catch (Exception exception) when (exception is BddRunnerException or IOException or OperationCanceledException or ObjectDisposedException)
            {
                // A runner that cannot close the scenario is ended with the run all the same.
                run.Closed = false;
            }
        }
    }

    private async Task EndRunAsync(RunnerRun run)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_run, run))
            {
                _run = null;
            }
        }

        // A runner whose last scenario closed waits for the next run; otherwise it ends.
        if (run.Runner is { } runner)
        {
            if (run.Closed && !run.Cancel.IsCancellationRequested && run.Start is { } start)
            {
                ReturnRunner(runner, start);
            }
            else
            {
                await runner.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private void DisposeRun()
    {
        RunnerRun? run;
        lock (_gate)
        {
            run = _run;
            _run = null;
        }

        if (run is not null)
        {
            run.Cancel.Cancel();
            run.Runner?.DisposeAsync().AsTask().Wait(BddRunnerClient.CancelGrace);
        }
    }

    private sealed class RunnerRun(string uri, IReadOnlyList<string> scenarios)
    {
        public string Uri { get; } = uri;

        public IReadOnlyList<string> Scenarios { get; } = scenarios;

        public List<RunScenarioResult> Results { get; } = [];

        // The run's end, for a host that waits for it (RunToEndAsync).
        public TaskCompletionSource<RunEndedParams> Ended { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationTokenSource Cancel { get; } = new();

        public BddRunnerStart? Start { get; set; }

        public BddRunnerClient? Runner { get; set; }

        // Whether the last scenario opened was closed: the runner is clean for the next run.
        public bool Closed { get; set; } = true;

        // Whether a fixture that starts clean without a launch stays, with its app, for the next scenario (I3).
        public bool Keep { get; init; }
    }
}
