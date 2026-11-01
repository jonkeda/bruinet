using System.Windows.Input;
using Bruinet.Presenter.Commands;
using Bruinet.Presenter.Services;
using Bruinet.Uat;

namespace Bruinet.Presenter.ViewModels;

// The run engine: a session per scenario, run, step and pause, shared by Present mode and
// Narrator mode.
public sealed partial class PresenterShellViewModel
{
    // Run (F5): on from where a paused run is, else the selection's scenarios from the start.
    private Task RunAsync()
        => IsPaused ? MoveAsync(PresenterMove.Play) : IsNarratorMode ? RunCheckedScenesAsync() : RunSelectedNodeAsync();

    private Task RunSelectedNodeAsync()
    {
        if (WorkspacePath is null)
        {
            StatusSummary = "No workspace loaded";
            return Task.CompletedTask;
        }

        var scenarios = GetRunnableScenarios(SelectedWorkspaceNode).ToArray();
        if (scenarios.Length == 0)
        {
            StatusSummary = "No runnable scenarios below selection";
            return Task.CompletedTask;
        }

        return RunScenariosAsync(scenarios, CaptureRunExecutionOptions(SelectedWorkspaceNode, scenarios), stopAtFailure: true);
    }

    // Runs the scenarios in order, a session each. Present mode stops at the first that does not
    // pass; Narrator mode goes on to the next scene. Stop, or a breakpoint, ends it either way.
    private async Task RunScenariosAsync(
        IReadOnlyList<BddScenarioViewModel> scenarios,
        PresenterRunExecutionOptions options,
        bool stopAtFailure,
        Action<BddScenarioViewModel, RunOutcome>? ended = null)
    {
        _activeRunOptions = options;
        _stepTimings.Clear();
        RunScopeText = FormatRunScope(options);
        ExecutionTimingText = FormatExecutionTiming(options);
        DiagnosticsText = FormatRunStartedDetails(options);
        StatusSummary = $"Starting: {options.SelectedNodeName}";
        RefreshSelectedNodeDetails();

        // One runner process for the scenarios of the run; it ends with the run.
        using var run = _executionService.BeginRun();
        var passed = 0;
        foreach (var scenario in scenarios)
        {
            SelectedScenario = scenario;
            var outcome = await RunScenarioAutoAsync(scenario, options).ConfigureAwait(true);
            ended?.Invoke(scenario, outcome);
            if (outcome == RunOutcome.Paused)
            {
                // A breakpoint: the run waits there; Run goes on with this scenario only.
                return;
            }

            if (outcome == RunOutcome.Passed)
            {
                passed++;
            }
            else if (stopAtFailure || outcome == RunOutcome.Stopped)
            {
                StatusSummary = $"Failed: {passed}/{scenarios.Count} scenarios passed";
                RefreshSelectedNodeDetails();
                return;
            }
        }

        StatusSummary = passed == scenarios.Count
            ? $"Passed: {passed}/{scenarios.Count} scenarios"
            : $"Failed: {passed}/{scenarios.Count} scenarios passed";
        DiagnosticsText = FormatRunCompletedDetails(options);
        ExecutionTimingText = FormatExecutionTiming(options);
        RefreshSelectedNodeDetails();
    }

    // Stop ends the run; with no run on, it closes an app kept open after one.
    private void Stop()
    {
        if (_activeSession is null)
        {
            var closed = _endedSession is not null;
            ReleaseEndedSession();
            StatusSummary = closed ? "Closed the app" : "Stopped";
            RaiseStepping();
            return;
        }

        _executionCancellation?.Cancel();
        FinishActiveSession();
        StatusSummary = "Stopped";
        RaiseStepping();
    }

    // Step (Space, Right, PageDown): with no run on, starts one paused before its first row.
    private Task StepAsync()
        => _activeSession is null ? StartPausedAsync(null) : MoveAsync(PresenterMove.Step);

    private async Task RestartAsync()
    {
        var scenario = _activeScenario ?? SelectedScenario;
        StopActiveSession();
        await StartPausedAsync(scenario).ConfigureAwait(true);
    }

    // Starts a run of the scenario (or the selection's first) and pauses before its first row,
    // so what is about to happen can be said before it happens.
    private async Task StartPausedAsync(BddScenarioViewModel? scenario)
    {
        if (WorkspacePath is null)
        {
            StatusSummary = "No workspace loaded";
            return;
        }

        // Narrator mode steps through the scene shown, or the first checked one.
        scenario ??= IsNarratorMode
            ? Narrator.SceneOf(SelectedScenario)?.Scenario ?? Narrator.CheckedScenes.FirstOrDefault()?.Scenario
            : GetRunnableScenarios(SelectedWorkspaceNode).FirstOrDefault() ?? SelectedScenario;
        if (scenario is null)
        {
            StatusSummary = "No runnable scenario below selection";
            return;
        }

        if (!ReferenceEquals(SelectedScenario, scenario))
        {
            SelectedScenario = scenario;
        }

        await StartStepSessionAsync(scenario).ConfigureAwait(true);
        if (_activeSession is not null && StepRows.Count > 0)
        {
            Pause(StepRows[0]);
        }
    }

    private async Task StartStepSessionAsync(BddScenarioViewModel scenario)
    {
        if (WorkspacePath is null)
        {
            return;
        }

        StopActiveSession();
        ResetScenario(scenario);
        _executionCancellation = new CancellationTokenSource();
        StatusSummary = $"Starting: {scenario.Name}";

        try
        {
            SnapshotWindows();
            _activeSession = await _executionService
                .CreateSessionAsync(WorkspacePath, scenario.FilePath, scenario.Name, _executionCancellation.Token)
                .ConfigureAwait(true);
            _activeScenario = scenario;
            TryPlaceApp();
            await StartRecordingAsync(_activeSession, scenario).ConfigureAwait(true);
            DiscoveryText = _activeSession.DiscoveryReport;
            CommandCatalogText = _activeSession.CommandCatalogReport;
            AutPlacementText = _placedThisRun ? AutPlacementText : _activeSession.AutPlacementReport;
            DiagnosticsText = string.Join(
                Environment.NewLine,
                "Step session ready.",
                AutPlacementText);
        }
        catch (Exception ex)
        {
            scenario.Status = "fail";
            StatusSummary = $"Failed to start: {scenario.Name}";
            DiagnosticsText = Describe(ex);
            FinishActiveSession();
        }
        finally
        {
            UpdateScenarioListText();
            RefreshSelectedNodeDetails();
        }
    }

    private async Task<RunOutcome> RunScenarioAutoAsync(
        BddScenarioViewModel scenario,
        PresenterRunExecutionOptions options)
    {
        if (WorkspacePath is null)
        {
            return RunOutcome.Failed;
        }

        StopActiveSession();
        ResetScenario(scenario, clearTimings: false);
        _executionCancellation = new CancellationTokenSource();
        StatusSummary = $"Starting: {scenario.Name}";
        scenario.Status = "run";
        UpdateScenarioListText();
        _isMoving = true;
        RaiseStepping();

        try
        {
            SnapshotWindows();
            _activeSession = await _executionService
                .CreateSessionAsync(WorkspacePath, scenario.FilePath, scenario.Name, _executionCancellation.Token)
                .ConfigureAwait(true);
            _activeScenario = scenario;
            TryPlaceApp();
            await StartRecordingAsync(_activeSession, scenario).ConfigureAwait(true);
            DiscoveryText = _activeSession.DiscoveryReport;
            CommandCatalogText = _activeSession.CommandCatalogReport;
            AutPlacementText = _placedThisRun ? AutPlacementText : _activeSession.AutPlacementReport;
            RefreshBreakpoints(scenario);
            return await AdvanceAsync(_activeSession, scenario, new StepPlan(int.MaxValue, true, null), -1, options, checkFirst: true)
                .ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            scenario.Status = "cancel";
            StatusSummary = $"Canceled: {scenario.Name}";
            MarkRunningSteps("cancel");
            FinishActiveSession();
            RefreshSelectedNodeDetails();
            return RunOutcome.Stopped;
        }
        catch (Exception ex)
        {
            scenario.Status = "fail";
            StatusSummary = $"Failed: {scenario.Name}: {Describe(ex)}";
            DiagnosticsText = Describe(ex);
            MarkRunningSteps("fail");
            FinishActiveSession();
            RefreshSelectedNodeDetails();
            return RunOutcome.Failed;
        }
        finally
        {
            _isMoving = false;
            RaiseStepping();
            UpdateScenarioListText();
        }
    }

    // One move from the row the run is paused before (P2 of the Presenter update).
    private async Task MoveAsync(PresenterMove move, BddStepRowViewModel? until = null, BddStepRowViewModel? from = null)
    {
        if (_activeSession is not { } session || _activeScenario is not { } scenario || _isMoving || CurrentRow is not { } at)
        {
            return;
        }

        var start = at.Index;
        if (from is not null)
        {
            // Run from here: the rows before it are skipped, not run; the app must already be there.
            while (session.HasNext && session.CompletedStepCount < from.FirstStep)
            {
                var index = session.CompletedStepCount;
                await session.SkipNextAsync("Skipped by Run from here.").ConfigureAwait(true);
                SetStepStatus(index, "skip");
            }

            start = from.Index;
            move = PresenterMove.Play;
        }

        var plan = until is not null
            ? PresenterStepping.PlanUntil(StepRows, start, until.Index)
            : PresenterStepping.Plan(StepRows, start, move);
        if (plan is not { } planned)
        {
            return;
        }

        _isMoving = true;
        RaiseStepping();
        _executionCancellation ??= new CancellationTokenSource();
        PresenterRunExecutionOptions? pacing = null;
        if (planned.CheckBreaks)
        {
            RefreshBreakpoints(scenario);
            pacing = CaptureRunExecutionOptions(SelectedWorkspaceNode, [scenario]);
            _activeRunOptions ??= pacing;
        }

        try
        {
            // A move from a pause runs the paused row first, so its own breakpoint does not stop it again.
            await AdvanceAsync(session, scenario, planned, start, pacing, checkFirst: from is not null).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            scenario.Status = "cancel";
            StatusSummary = $"Canceled: {scenario.Name}";
            MarkRunningSteps("cancel");
            FinishActiveSession();
        }
        finally
        {
            _isMoving = false;
            RaiseStepping();
            UpdateScenarioListText();
            RefreshSelectedNodeDetails();
        }
    }

    // Runs steps until the plan's count, a breakpoint, a failure or the end; then pauses, or ends the run.
    private async Task<RunOutcome> AdvanceAsync(
        PresenterBddExecutionSession session,
        BddScenarioViewModel scenario,
        StepPlan plan,
        int fromRow,
        PresenterRunExecutionOptions? pacing,
        bool checkFirst)
    {
        var first = true;
        while (session.HasNext && session.CompletedStepCount < plan.Until)
        {
            var step = session.CompletedStepCount;
            // A recording does not stop at a breakpoint: a rehearsal does.
            if (plan.CheckBreaks && !_recordingScenes && (checkFirst || !first) && PresenterStepping.BreakBefore(StepRows, step) is { } breakRow)
            {
                Pause(breakRow, "Breakpoint");
                return RunOutcome.Paused;
            }

            if (plan.CheckBreaks && !_recordingScenes && (checkFirst || !first) && PresenterStepping.BreaksInStepFile(session.Next, scenario.FilePath, _breakpoints.Path) && RowOfStep(step) is { } innerRow)
            {
                Pause(innerRow, "Breakpoint");
                return RunOutcome.Paused;
            }

            first = false;
            var result = await RunNextStepAsync(session, scenario).ConfigureAwait(true);
            if (!ReferenceEquals(_activeSession, session))
            {
                return RunOutcome.Stopped;
            }

            if (result.Status is BddStepResultStatus.Failed or BddStepResultStatus.Canceled)
            {
                MarkRemainingSteps("skip");
                scenario.Status = result.Status == BddStepResultStatus.Canceled ? "cancel" : "fail";
                StatusSummary = $"{ScenarioStatusText(result.Status)}: {scenario.Name}: {result.Message}";
                DiagnosticsText = FormatExecutionDetails(session, result.Message);
                await EndSceneAsync().ConfigureAwait(true);
                FinishActiveSession();
                return RunOutcome.Failed;
            }

            if (pacing is not null)
            {
                await WaitBeforeNextStepAsync(session, scenario, pacing).ConfigureAwait(true);
                if (!ReferenceEquals(_activeSession, session))
                {
                    return RunOutcome.Stopped;
                }
            }
        }

        if (!session.HasNext)
        {
            scenario.Status = "pass";
            StatusSummary = $"Passed: {scenario.Name}";
            DiagnosticsText = FormatExecutionDetails(session, null);
            await EndSceneAsync().ConfigureAwait(true);
            FinishActiveSession();
            return RunOutcome.Passed;
        }

        var stop = plan.StopRow ?? PresenterStepping.PositionAfter(StepRows, session.CompletedStepCount, fromRow);
        Pause(StepRows[Math.Min(stop, StepRows.Count - 1)]);
        return RunOutcome.Paused;
    }

    // The run waits before a row: the row is highlighted, and the moves say what they will do.
    private void Pause(BddStepRowViewModel row, string reason = "Paused")
    {
        CurrentRow = row;
        StatusSummary = $"{reason} before: {row.Label}";
        RaiseStepping();
        RefreshPreview();
    }

    private void RaiseStepping()
    {
        OnPropertyChanged(nameof(IsPaused));
        foreach (var command in new[] { RunCommand, NextStepCommand, StepIntoCommand, StepOutCommand, RestartCommand, RecordCommand, StitchCommand })
        {
            (command as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }

        foreach (var row in StepRows)
        {
            row.RefreshMenu();
        }
    }

    // The shared breakpoints file, read again before a run goes on, so one set in an IDE counts.
    private void RefreshBreakpoints(BddScenarioViewModel scenario)
    {
        var lines = _breakpoints.Lines(scenario.FilePath);
        foreach (var row in scenario.Rows)
        {
            row.HasBreakpoint = row.CanBreak && lines.Contains(row.LineNumber);
        }
    }

    /// <inheritdoc />
    public bool CanRunUntil(BddStepRowViewModel row)
        => WorkspacePath is not null && !_isMoving && (_activeSession is null || (CurrentRow is { } at && row.Index > at.Index));

    /// <inheritdoc />
    public bool CanRunFrom(BddStepRowViewModel row)
        => IsPaused && CurrentRow is { } at && row.Index > at.Index && !row.IsInCall;

    /// <inheritdoc />
    public async void RunUntil(BddStepRowViewModel row)
    {
        if (_activeSession is null)
        {
            await StartPausedAsync(SelectedScenario).ConfigureAwait(true);
        }

        if (CurrentRow is { } at && row.Index > at.Index)
        {
            await MoveAsync(PresenterMove.Step, until: row).ConfigureAwait(true);
        }
    }

    /// <inheritdoc />
    public async void RunFrom(BddStepRowViewModel row)
    {
        if (CanRunFrom(row))
        {
            await MoveAsync(PresenterMove.Play, from: row).ConfigureAwait(true);
        }
    }

    /// <inheritdoc />
    public bool CanDebugFrom(BddStepRowViewModel row)
        => IsPaused && !_isMoving && ReferenceEquals(CurrentRow, row) && _activeSession is not null;

    /// <inheritdoc />
    public async void DebugFrom(BddStepRowViewModel row)
    {
        if (!CanDebugFrom(row) || _activeSession is not { } session)
        {
            return;
        }

        StatusSummary = "Pick a debugger in the dialog Windows shows: the step then stops in it.";
        try
        {
            await session.DebugAsync(_executionCancellation?.Token ?? CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Bruinet.Bdd.Runner.BddRunnerException or OperationCanceledException)
        {
            StatusSummary = $"Debug from here: {exception.Message}";
            return;
        }

        StatusSummary = "Debugger attached: the step stops in its definition.";
        await MoveAsync(PresenterMove.Step).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public void ToggleBreakpoint(BddStepRowViewModel row)
    {
        if (row.CanBreak && SelectedScenario is { } scenario)
        {
            row.HasBreakpoint = _breakpoints.Toggle(scenario.FilePath, row.LineNumber);
        }
    }

    /// <summary>
    /// The keys, for a keyboard or a presentation clicker (which sends PageDown and PageUp).
    /// Space, Right and PageDown step; Down goes into a call, Up out of it; F5 runs; Escape
    /// stops; Ctrl+R restarts.
    /// </summary>
    /// <param name="key">The key's name, as Windows gives it (<c>Space</c>, <c>PageDown</c>, <c>F5</c> ...).</param>
    /// <param name="control">Whether Ctrl is down.</param>
    /// <returns>Whether the key was one of these.</returns>
    public bool HandleKey(string key, bool control)
    {
        var command = (key, control) switch
        {
            ("Space" or "Right" or "PageDown", false) => NextStepCommand,
            ("Down", false) => StepIntoCommand,
            ("Up", false) => StepOutCommand,
            ("F5", false) => RunCommand,
            ("Escape", false) => StopCommand,
            ("R", true) => RestartCommand,
            _ => null,
        };
        if (command is null)
        {
            return false;
        }

        if (command.CanExecute(null))
        {
            command.Execute(null);
        }

        return true;
    }

    // The preview's paused bar sends the same moves.
    private void OnPreviewMessage(string type, System.Text.Json.JsonElement message)
    {
        if (type != "debug" || !message.TryGetProperty("move", out var move))
        {
            return;
        }

        var command = move.GetString() switch
        {
            "continue" => RunCommand,
            "over" => NextStepCommand,
            "into" => StepIntoCommand,
            "out" => StepOutCommand,
            "stop" => StopCommand,
            _ => null,
        };
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }

    private enum RunOutcome
    {
        Passed,
        Failed,
        Paused,
        Stopped,
    }

    private async Task<Bruinet.Bdd.Runner.BddRunnerStep> RunNextStepAsync(
        PresenterBddExecutionSession session,
        BddScenarioViewModel scenario)
    {
        var index = session.CompletedStepCount;
        var startedAt = DateTimeOffset.Now;
        var stepText = GetScenarioStepText(scenario, index);
        CurrentRow = RowOfStep(index);
        SetStepStatus(index, "run");
        StatusSummary = stepText;
        RefreshSelectedNodeDetails();
        await Task.Yield();

        var cancellationToken = _executionCancellation?.Token ?? CancellationToken.None;
        var result = await Task.Run(
            () => session.RunNextAsync(cancellationToken),
            cancellationToken).ConfigureAwait(true);

        var completedAt = DateTimeOffset.Now;
        _stepTimings.Add(new PresenterStepTiming(
            index + 1,
            stepText,
            startedAt,
            completedAt));
        ExecutionTimingText = FormatExecutionTiming(_activeRunOptions);

        if (index >= 0 && index < Steps.Count)
        {
            Steps[index].Message = result.Status == BddStepResultStatus.Passed ? null : result.Message;
        }

        SetStepStatus(index, StepStatusText(result.Status));
        TryPlaceApp();
        RunProgress = scenario.Steps.Count == 0
            ? 0
            : (double)Math.Min(index + 1, scenario.Steps.Count) / scenario.Steps.Count;

        if (result.Status is BddStepResultStatus.Failed or BddStepResultStatus.Canceled)
        {
            DiagnosticsText = FormatExecutionDetails(session, result.Message);
        }

        RefreshSelectedNodeDetails();
        return result;
    }

    private async Task WaitBeforeNextStepAsync(
        PresenterBddExecutionSession session,
        BddScenarioViewModel scenario,
        PresenterRunExecutionOptions options)
    {
        if (!session.HasNext)
        {
            return;
        }

        var delayMilliseconds = options.EffectiveDelayMilliseconds;
        if (delayMilliseconds <= 0)
        {
            return;
        }

        if (_stepTimings.Count > 0)
        {
            var timing = _stepTimings[^1];
            timing.DelayAfterMilliseconds = delayMilliseconds;
            timing.WaitStartedAt = DateTimeOffset.Now;
        }

        StatusSummary = GetScenarioStepText(scenario, session.CompletedStepCount);
        DiagnosticsText = FormatExecutionDetails(session, null);
        ExecutionTimingText = FormatExecutionTiming(options);
        RefreshSelectedNodeDetails();
        await Task.Delay(delayMilliseconds, _executionCancellation?.Token ?? CancellationToken.None).ConfigureAwait(true);

        if (_stepTimings.Count > 0)
        {
            _stepTimings[^1].WaitCompletedAt = DateTimeOffset.Now;
            ExecutionTimingText = FormatExecutionTiming(options);
        }
    }

    private void StopActiveSession()
    {
        FinishRecording();
        CurrentRow = null;
        _executionCancellation?.Cancel();
        _executionCancellation?.Dispose();
        _executionCancellation = null;
        _activeSession?.Dispose();
        _activeSession = null;
        _activeScenario = null;
        _windowService?.SetBarTopmost(false);
    }

    // A run ended (passed, failed or stopped): its app is closed, or kept open as the setting says.
    private void FinishActiveSession()
    {
        FinishRecording();
        CurrentRow = null;
        _executionCancellation?.Dispose();
        _executionCancellation = null;
        if (RunEnd == PresenterRunEnd.KeepOpen && _activeSession is not null)
        {
            ReleaseEndedSession();
            _endedSession = _activeSession;
        }
        else
        {
            _activeSession?.Dispose();
        }

        _activeSession = null;
        _activeScenario = null;
        _windowService?.SetBarTopmost(false);
    }
}
