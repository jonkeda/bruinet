using Bruinet.Bdd.Runner;
using Bruinet.Core.Artifacts;
using StreamJsonRpc;

namespace Bruinet.Bdd.LanguageServer;

/// <summary>
/// Step through (AD-022): the server hosts the suite's runner process (AD-025),
/// pauses between its steps, and moves as the IDE's step commands ask. One run at a time; its runner
/// ends with the server.
/// </summary>
public sealed partial class BddLanguageServer
{
    private StepRun? _step;
    private BddRunnerJob? _runnerJob;
    private TaskCompletionSource<object> _stepEvent = NewStepEvent();

    /// <summary>
    /// The Step-through's next pause (<see cref="StepPausedParams"/>) or end (<see cref="StepEndedParams"/>), for a host
    /// that waits instead of listening (the MCP server): take it before a start or a move, then await it.
    /// </summary>
    internal Task<object> UpcomingStepEvent => Volatile.Read(ref _stepEvent).Task;

    private static TaskCompletionSource<object> NewStepEvent() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private object? _lastStepEvent;
    private long _stepEvents;

    /// <summary>How many pauses and ends were raised, and the last: for a host that stopped waiting before one came.</summary>
    internal (long Count, object? Last) LastStepEvent
    {
        get
        {
            lock (_gate)
            {
                return (_stepEvents, _lastStepEvent);
            }
        }
    }

    private void RaiseStepEvent(object stepEvent)
    {
        lock (_gate)
        {
            _lastStepEvent = stepEvent;
            _stepEvents++;
        }

        Interlocked.Exchange(ref _stepEvent, NewStepEvent()).TrySetResult(stepEvent);
    }

    /// <summary>The breakpoints file read between steps; the per-user one by default.</summary>
    public string BreakpointsPath { get; init; } = BddBreakpoints.DefaultPath;

    /// <summary>
    /// <c>bruinet/step/start</c>: opens a scenario in the suite's runner, paused before its first
    /// step (<c>bruinet/step/paused</c>). Its result is written as the gate writes it, marked <c>step</c>.
    /// </summary>
    [JsonRpcMethod("bruinet/step/start")]
    public async Task<StepStartResult> StepStartAsync(StepStartParams request)
    {
        var path = Key(request.Uri);
        var file = Context(path, request.Suite);
        if (file.Suite is not { } suite || !path.EndsWith(BddFileFormat.FeatureExtension, StringComparison.OrdinalIgnoreCase))
        {
            return StepStartResult.Not("No suite holds or links this feature file.");
        }

        if (file.Catalogs is null)
        {
            return StepStartResult.Not(file.Reason ?? $"{suite.Name} is not built yet.");
        }

        if (suite.RunnerAssemblies is not { } assemblies)
        {
            return StepStartResult.Not($"{suite.Name} has no built test project to run in. Build it first.");
        }

        var names = BddMarkdownParser.Parse(file.Text, path).Document?.Scenarios
            .Where(scenario => scenario.Source.LineNumber == request.Line)
            .Select(scenario => scenario.Name)
            .ToList() ?? [];
        if ((request.Scenario is { } wanted ? names.FirstOrDefault(name => name == wanted) : names.FirstOrDefault()) is not { } name)
        {
            return StepStartResult.Not($"There is no scenario heading at line {request.Line}.");
        }

        var run = new StepRun(request.Uri, name);
        lock (_gate)
        {
            if (_step is not null || _run is not null)
            {
                return StepStartResult.Not("A run on the suite's runner is going: stop it first.");
            }

            _step = run;
        }

        try
        {
            // The suite's warm runner, or a new one from a copy of its output (AD-025).
            run.Start = new BddRunnerStart(assemblies.Start, Environment: RunnerEnvironment(suite));
            run.Runner = await Warm.TakeAsync(run.Start).ConfigureAwait(false);
            var opened = await run.Runner.RequestAsync(new BddRunnerOpen(suite.ConfigPath, assemblies.Pages, path, name, "step")).ConfigureAwait(false);
            if (opened is not BddRunnerOpened ready)
            {
                throw new BddRunnerException(opened is BddRunnerFailed failed ? failed.Message : $"The runner answered {opened.GetType().Name}.");
            }

            run.Stepper = new BddRunnerStepper(ready.Next, run.NextAsync, (at, line) => BddBreakpoints.Lists(at, line, BreakpointsPath));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            await EndStepAsync(run, null, null).ConfigureAwait(false);
            return StepStartResult.Not(exception.Message);
        }

        _ = Task.Run(() => Report(run, run.Stepper.Paused));
        return new StepStartResult(true, null, name, suite.Name);
    }

    /// <summary><c>bruinet/step/move</c>: a move from the paused step; <c>stop</c> also while a step runs.</summary>
    [JsonRpcMethod("bruinet/step/move")]
    public StepMoveResult StepMove(StepMoveParams request)
    {
        StepRun? run;
        lock (_gate)
        {
            run = _step;
        }

        if (run?.Stepper is not { } stepper)
        {
            return new StepMoveResult(false, "No Step-through run is going.");
        }

        if (request.Move == "stop")
        {
            run.Cancel.Cancel();
            _ = EndStepAsync(run, "stopped", null);
            return new StepMoveResult(true, null);
        }

        BddStepMove? move;
        switch (request.Move)
        {
            case "over":
                move = BddStepMove.Over;
                break;
            case "into":
                move = BddStepMove.Into;
                break;
            case "out":
                move = BddStepMove.Out;
                break;
            case "continue":
                move = null;
                break;
            default:
                return new StepMoveResult(false, $"'{request.Move}' is not a move: over, into, out, continue or stop.");
        }

        if (Interlocked.Exchange(ref run.Moving, 1) == 1)
        {
            return new StepMoveResult(false, "The run is not paused.");
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var stop = await stepper.MoveAsync(move, run.Cancel.Token).ConfigureAwait(false);
                Interlocked.Exchange(ref run.Moving, 0);
                await Report(run, stop).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (run.Cancel.IsCancellationRequested)
            {
                // Stopped: the stop ends the run and says so.
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                await EndStepAsync(run, "error", exception.Message).ConfigureAwait(false);
            }
        });
        return new StepMoveResult(true, null);
    }

    /// <summary><c>bruinet/step/screenshot</c>: a screenshot of the app, taken by the runner while the run is paused (D5).</summary>
    [JsonRpcMethod("bruinet/step/screenshot")]
    public async Task<StepScreenshotResult> StepScreenshotAsync()
    {
        StepRun? run;
        lock (_gate)
        {
            run = _step;
        }

        if (run?.Stepper?.Paused is not { } stop || run.Runner is not { } runner)
        {
            return new StepScreenshotResult(null, "No Step-through run is paused.");
        }

        // Holds the run still: a move while the runner takes it is refused.
        if (Interlocked.Exchange(ref run.Moving, 1) == 1)
        {
            return new StepScreenshotResult(null, "The run is not paused.");
        }

        try
        {
            var answer = await runner.RequestAsync(new BddRunnerScreenshot($"paused_line{stop.Line}"), run.Cancel.Token).ConfigureAwait(false);
            return answer switch
            {
                BddRunnerScreenshotTaken taken => new StepScreenshotResult(taken.Path, null),
                BddRunnerFailed failed => new StepScreenshotResult(null, failed.Message),
                _ => new StepScreenshotResult(null, $"The runner answered {answer.GetType().Name}."),
            };
        }
        catch (Exception exception) when (exception is BddRunnerException or IOException or OperationCanceledException or ObjectDisposedException)
        {
            return new StepScreenshotResult(null, exception.Message);
        }
        finally
        {
            Interlocked.Exchange(ref run.Moving, 0);
        }
    }

    /// <summary>
    /// <c>bruinet/step/debug</c>: Debug from here (D6). Answers the runner's process id at once; the
    /// runner then waits for the IDE's debugger, and the next move's first step breaks in it. A
    /// debugged runner ends with its run, so the debugging session does too.
    /// </summary>
    [JsonRpcMethod("bruinet/step/debug")]
    public StepDebugResult StepDebug()
    {
        StepRun? run;
        lock (_gate)
        {
            run = _step;
        }

        if (run?.Stepper?.Paused is null || run.Runner is not { } runner || Volatile.Read(ref run.Moving) == 1)
        {
            return new StepDebugResult(null, "No Step-through run is paused.");
        }

        run.Debugged = true;

        // Queued before the move the IDE sends once attached: that move's step waits for the break.
        _ = runner.RequestAsync(new BddRunnerBreak(DebugAttachWait.TotalSeconds), run.Cancel.Token);
        return new StepDebugResult(runner.Process.Id, null);
    }

    /// <summary>How long a runner waits for the IDE's debugger to attach after <c>bruinet/step/debug</c>.</summary>
    public TimeSpan DebugAttachWait { get; init; } = TimeSpan.FromSeconds(60);

    private BddRunnerJob RunnerJob
    {
        get
        {
            lock (_gate)
            {
                return _runnerJob ??= new BddRunnerJob();
            }
        }
    }

    private async Task Report(StepRun run, BddRunnerStop? stop)
    {
        if (run.Cancel.IsCancellationRequested)
        {
            return;
        }

        if (stop is null)
        {
            await EndStepAsync(run, run.Failure is null ? "passed" : "failed", run.Failure).ConfigureAwait(false);
            return;
        }

        var paused = new StepPausedParams(
            run.Uri,
            run.Scenario,
            stop.File is { } at ? new Uri(at).AbsoluteUri : null,
            stop.Line,
            stop.Text,
            stop.Depth,
            stop.IsCall,
            stop.Calls,
            stop.Failed is not null,
            stop.Failed?.Message,
            stop.Arguments,
            stop.App,
            stop.Page);

        // A stop since the check above ends the run: no pause to report.
        if (run.Cancel.IsCancellationRequested)
        {
            return;
        }

        RaiseStepEvent(paused);
        _ = _rpc?.NotifyWithParameterObjectAsync("bruinet/step/paused", paused);
    }

    // Closes the scenario, so a finished one keeps its result and the app ends, then ends the runner.
    private async Task EndStepAsync(StepRun run, string? status, string? message)
    {
        if (Interlocked.Exchange(ref run.Ended, 1) == 1)
        {
            return;
        }

        lock (_gate)
        {
            if (ReferenceEquals(_step, run))
            {
                _step = null;
            }
        }

        if (run.Runner is { } runner)
        {
            var closed = false;
            try
            {
                using var timeout = new CancellationTokenSource(BddRunnerClient.CancelGrace * 2);
                closed = await runner.RequestAsync(new BddRunnerClose(Keep: _warmRunner), timeout.Token).ConfigureAwait(false) is BddRunnerDone;
            }
            catch (Exception exception) when (exception is BddRunnerException or IOException or OperationCanceledException or ObjectDisposedException)
            {
                // A runner that cannot close the scenario is ended below all the same.
            }

            // A closed scenario leaves the runner ready for the next run; otherwise it ends.
            if (closed && !run.Debugged && run.Start is { } start)
            {
                ReturnRunner(runner, start);
            }
            else
            {
                await runner.DisposeAsync().ConfigureAwait(false);
            }
        }

        if (status is not null)
        {
            var ended = new StepEndedParams(run.Uri, run.Scenario, status, message);
            RaiseStepEvent(ended);
            _ = _rpc?.NotifyWithParameterObjectAsync("bruinet/step/ended", ended);
        }
    }

    private void DisposeStep()
    {
        StepRun? run;
        BddRunnerJob? job;
        lock (_gate)
        {
            run = _step;
            _step = null;
            job = _runnerJob;
            _runnerJob = null;
        }

        if (run is not null && Interlocked.Exchange(ref run.Ended, 1) == 0)
        {
            run.Cancel.Cancel();
            run.Runner?.DisposeAsync().AsTask().Wait(BddRunnerClient.CancelGrace);

            // A host waiting for the next pause hears the run ended with the server.
            RaiseStepEvent(new StepEndedParams(run.Uri, run.Scenario, "stopped", "The server ended."));
        }

        // Ends whatever is left: the runner, and the app it started.
        job?.Dispose();
    }

    private sealed class StepRun(string uri, string scenario)
    {
        public int Moving;
        public int Ended;

        public string Uri { get; } = uri;

        public string Scenario { get; } = scenario;

        public CancellationTokenSource Cancel { get; } = new();

        public BddRunnerStart? Start { get; set; }

        // A debugger is (or was to be) attached to its runner: the runner ends with the run.
        public bool Debugged { get; set; }

        public BddRunnerClient? Runner { get; set; }

        public BddRunnerStepper? Stepper { get; set; }

        // The first failed step's message: the run then ends "failed".
        public string? Failure { get; private set; }

        public async Task<BddRunnerStep> NextAsync(CancellationToken cancellationToken)
        {
            var answer = await Runner!.RequestAsync(new BddRunnerNext(), cancellationToken).ConfigureAwait(false);
            var step = answer as BddRunnerStep
                ?? throw new BddRunnerException(answer is BddRunnerFailed failed ? failed.Message : $"The runner answered {answer.GetType().Name}.");
            if (step.Status == BddStepResultStatus.Failed)
            {
                Failure ??= step.Message ?? "A step failed.";
            }

            return step;
        }
    }
}
