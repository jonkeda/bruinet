using Bruinet.Bdd.Runner;

namespace Bruinet.Presenter.Services;

public interface IBddExecutionService
{
    /// <summary>
    /// Variables set for a session's runner, and so for the app it starts: how its window is shown
    /// (<c>BRUINET_AUT_WINDOW</c>) and where (<c>BRUINET_AUT_BOUNDS</c>).
    /// </summary>
    IReadOnlyDictionary<string, string> WindowEnvironment
    {
        get => new Dictionary<string, string>();
        set { }
    }

    Task<PresenterBddExecutionSession> CreateSessionAsync(
        string workspacePath,
        string scenarioFilePath,
        string scenarioName,
        CancellationToken cancellationToken);

    /// <summary>A run of several scenarios, until the returned scope is disposed.</summary>
    IDisposable BeginRun() => NoRun.Instance;

    /// <summary>Completes when every ended session's scenario is closed, and its app with it.</summary>
    Task EndRunnersAsync() => Task.CompletedTask;

    /// <summary>Starts the suite's runner ahead of a session (AD-025): on opening a workspace, and after a build.</summary>
    /// <param name="workspacePath">The suite's folder.</param>
    Task WarmAsync(string workspacePath) => Task.CompletedTask;

    private sealed class NoRun : IDisposable
    {
        public static readonly NoRun Instance = new();

        public void Dispose()
        {
        }
    }
}

/// <summary>
/// One scenario, open in a runner process (AD-025): Presenter asks for a step at a time and holds
/// only what the runner answers, as text.
/// </summary>
public sealed class PresenterBddExecutionSession : IDisposable
{
    private readonly BddRunnerClient _runner;
    private readonly Action<BddRunnerClient, Task, bool> _release;
    private readonly List<BddRunnerStep> _steps = [];
    private bool _disposed;

    /// <summary>Creates the session for a scenario the runner has opened.</summary>
    /// <param name="runner">The runner.</param>
    /// <param name="opened">Its answer to the open request.</param>
    /// <param name="release">Called once when the session ends, with the runner, its closing of the scenario, and whether a debugger was put on it.</param>
    public PresenterBddExecutionSession(BddRunnerClient runner, BddRunnerOpened opened, Action<BddRunnerClient, Task, bool> release)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _release = release ?? throw new ArgumentNullException(nameof(release));
        ArgumentNullException.ThrowIfNull(opened);
        RunnerProcessId = runner.Process.Id;
        StepCount = opened.Steps;
        Next = opened.Next;
        DiscoveryReport = opened.Discovery;
        CommandCatalogReport = opened.Catalog;
    }

    public string DiscoveryReport { get; }

    public string CommandCatalogReport { get; }

    /// <summary>The session does not place the app; Presenter does, once its window is open.</summary>
    public string AutPlacementReport { get; } = "AUT placement:" + Environment.NewLine + "Result: not supported";

    /// <summary>The runner process the scenario is open in.</summary>
    public int RunnerProcessId { get; }

    /// <summary>How many steps the scenario runs.</summary>
    public int StepCount { get; }

    public int CompletedStepCount => _steps.Count;

    public bool HasNext => _steps.Count < StepCount;

    /// <summary>Where the next step is, as the runner says (its file, the calls around it, its page and arguments); null when none is left.</summary>
    public BddRunnerPosition? Next { get; private set; }

    /// <summary>Whether every step ran and passed.</summary>
    public bool Passed => !HasNext && _steps.Count > 0 && _steps.All(step => step.Status == BddStepResultStatus.Passed);

    /// <summary>Every step so far, as the diagnostics show them.</summary>
    public IReadOnlyList<string> StepsReport => _steps.Count == 0 ? [] : _steps[^1].Report;

    /// <summary>The runner's runtime trace so far.</summary>
    public IReadOnlyList<string> Diagnostics => _steps.Count == 0 ? [] : _steps[^1].Diagnostics;

    /// <summary>Called when the runner announces a spoken line, for Presenter to play.</summary>
    public Action<BddRunnerSay>? Said
    {
        get => _runner.Said;
        set => _runner.Said = value;
    }

    public Task<BddRunnerStep> RunNextAsync(CancellationToken cancellationToken)
        => StepAsync(new BddRunnerNext(), cancellationToken);

    /// <summary>Passes over the next step without running it (Run from here).</summary>
    /// <param name="message">Why it was skipped.</param>
    public Task<BddRunnerStep> SkipNextAsync(string? message = null)
        => StepAsync(new BddRunnerSkip(message), CancellationToken.None);

    /// <summary>Records the scenario as a scene, from its next step.</summary>
    /// <param name="scene">The scene.</param>
    public Task RecordAsync(BddRunnerRecord scene) => DoneAsync(scene);

    /// <summary>Paces the scenario by its spoken lines, without recording it.</summary>
    /// <param name="rehearsal">The lines and the beat.</param>
    public Task RehearseAsync(BddRunnerRehearse rehearsal) => DoneAsync(rehearsal);

    /// <summary>After the last step: the outro, and a recording's video and timeline are finished.</summary>
    /// <param name="cancellationToken">Cancels the outro.</param>
    public async Task<BddRunnerRecorded> FinishRecordingAsync(CancellationToken cancellationToken)
        => Expect<BddRunnerRecorded>(await _runner.RequestAsync(new BddRunnerFinishRecording(), cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Debug from here (AD-022): Windows asks which debugger to attach to the runner;
    /// once one is, the next step breaks just before its step definition runs.
    /// </summary>
    /// <param name="cancellationToken">Gives up the wait for the debugger.</param>
    public async Task DebugAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Debugged = true;
        Expect<BddRunnerDone>(await _runner.RequestAsync(new BddRunnerBreak(DebugAttachWait.TotalSeconds, Launch: true), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Whether a debugger was put on the runner: it then ends with the session.</summary>
    public bool Debugged { get; private set; }

    // How long the runner waits for the debugger Windows offers to start.
    private static readonly TimeSpan DebugAttachWait = TimeSpan.FromMinutes(2);

    /// <summary>Gives up the recording or rehearsal.</summary>
    public Task DropRecordingAsync() => DoneAsync(new BddRunnerDropRecording());

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // The request is in line before this returns, so the scenario is closed (its fixture and
        // app) before the runner answers whatever a next session asks of it.
        _release(_runner, _runner.RequestAsync(new BddRunnerClose(), CancellationToken.None), Debugged);
    }

    private async Task<BddRunnerStep> StepAsync(BddRunnerMessage request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!HasNext)
        {
            throw new InvalidOperationException("No BDD steps remain in this execution session.");
        }

        var step = Expect<BddRunnerStep>(await _runner.RequestAsync(request, cancellationToken).ConfigureAwait(false));
        _steps.Add(step);
        Next = step.Next;
        return step;
    }

    private async Task DoneAsync(BddRunnerMessage request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Expect<BddRunnerDone>(await _runner.RequestAsync(request, CancellationToken.None).ConfigureAwait(false));
    }

    private static T Expect<T>(BddRunnerMessage answer)
        where T : BddRunnerMessage
        => answer switch
        {
            T expected => expected,
            BddRunnerFailed failed => throw new InvalidOperationException(failed.Message),
            _ => throw new BddRunnerException($"The runner answered '{answer.GetType().Name}' where a '{typeof(T).Name}' was due."),
        };
}
