using System.Windows.Input;
using Bruinet.Narrator;
using Bruinet.Presenter.Commands;
using Bruinet.Presenter.Services;

namespace Bruinet.Presenter.ViewModels;

// Narrator mode: a video script open as a tree of scenes, run and recorded with the run engine.
public sealed partial class PresenterShellViewModel
{
    private bool _isNarratorMode;
    private bool _sceneRehearsal;
    private bool _recordingScenes;
    private bool _sceneRecording;

    /// <summary>The open video script.</summary>
    public NarratorViewModel Narrator { get; } = new();

    /// <summary>
    /// Narrator mode: a video script's scenes and their steps in a tree, the moves, and the
    /// recording. The third mode, beside Present and Workspace.
    /// </summary>
    public bool IsNarratorMode => _isNarratorMode;

    /// <summary>Whether the Scenarios section shows: not in Narrator mode, where the script's tree takes its place.</summary>
    public bool IsScenarioSectionVisible => !IsNarratorMode;

    /// <summary>Whether the workspace tree shows.</summary>
    public bool IsScenarioTreeVisible => IsScenariosExpanded && !IsNarratorMode;

    /// <summary>Switches to Narrator mode.</summary>
    public ICommand ShowNarratorModeCommand { get; private set; } = null!;

    /// <summary>Asks for a video script and opens it.</summary>
    public ICommand OpenScriptCommand { get; private set; } = null!;

    /// <summary>The Record button: in Narrator mode it records the checked scenes, elsewhere it turns recording on and off.</summary>
    public ICommand RecordCommand { get; private set; } = null!;

    /// <summary>Stitches the latest recording of every scene into the video.</summary>
    public ICommand StitchCommand { get; private set; } = null!;

    /// <summary>Opens the Recordings folder.</summary>
    public ICommand OpenRecordingsFolderCommand { get; private set; } = null!;

    private void InitializeNarrator()
    {
        ShowNarratorModeCommand = new RelayCommand(ShowNarratorMode);
        OpenScriptCommand = new AsyncRelayCommand(OpenScriptAsync);
        RecordCommand = new AsyncRelayCommand(RecordAsync, () => !(IsNarratorMode && _isMoving));
        StitchCommand = new AsyncRelayCommand(StitchAsync, () => Narrator.CanStitch && !_isMoving);
        OpenRecordingsFolderCommand = new RelayCommand(OpenRecordingsFolder, () => WorkspacePath is not null);
        Narrator.SceneSelected += OnSceneSelected;
        Narrator.OpenVideo = OpenVideo;
        Narrator.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NarratorViewModel.CanStitch))
            {
                (StitchCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            }
        };
        Narrator.CheckedChanged += () => OnPropertyChanged(nameof(RecordDescription));
    }

    private Task RecordAsync()
    {
        if (IsNarratorMode)
        {
            return RecordCheckedScenesAsync();
        }

        ToggleRecord();
        return Task.CompletedTask;
    }

    // Where videos and the script's kept scenes go: the setting, or the suite's TestResults\recordings.
    private string? RecordingsFolder()
        => _recordingsFolder.Length > 0 ? _recordingsFolder : WorkspacePath is { } workspace ? BddRecordings.DefaultFolder(workspace) : null;

    private string? ScenesFolder()
        => Narrator.ScriptPath is { } script && RecordingsFolder() is { } recordings ? BddKeptScenes.Folder(recordings, script) : null;

    // Each scene's kept recording, read again: after a scene is recorded, and when what a
    // recording is made from changes (the voice, the beat, the script).
    private void RefreshSceneStates(bool resetChecks)
    {
        if (!Narrator.HasScript)
        {
            return;
        }

        Narrator.RefreshStates(ScenesFolder(), _voice, TimeSpan.FromMilliseconds(_beat), resetChecks);
        OnPropertyChanged(nameof(RecordDescription));
        OnPropertyChanged(nameof(ScriptVoiceNote));
        OnPropertyChanged(nameof(HasScriptVoiceNote));
    }

    private static void ResetSceneRows(IReadOnlyList<NarratorSceneViewModel> scenes)
    {
        // The rows of an earlier run would read as this run's until their scene's turn.
        foreach (var scene in scenes)
        {
            scene.Scenario!.Status = "wait";
            foreach (var step in scene.Scenario.Steps)
            {
                step.Status = "wait";
            }
        }
    }

    // Record in Narrator mode: the checked scenes, one after another, each into its own kept
    // folder. A scene that passes is unchecked; when every scene is recorded the video is stitched.
    private async Task RecordCheckedScenesAsync()
    {
        if (_recorder.Unavailable is { } reason)
        {
            RecordingStatus = reason;
            return;
        }

        if (WorkspacePath is null || !Narrator.HasScript || ScenesFolder() is null)
        {
            StatusSummary = "No video script open";
            return;
        }

        var scenes = Narrator.CheckedScenes;
        if (scenes.Count == 0)
        {
            StatusSummary = "No scenes checked";
            return;
        }

        RefreshSceneStates(resetChecks: false);
        ResetSceneRows(scenes);
        BddScenarioViewModel[] scenarios = [.. scenes.Select(scene => scene.Scenario!)];
        var options = CaptureRunExecutionOptions("Script", Narrator.ScriptName, scenarios) with { EffectiveDelayMilliseconds = 0 };
        var wasRecording = IsRecording;
        _recordingScenes = true;
        IsRecording = true;
        try
        {
            await RunScenariosAsync(scenarios, options, stopAtFailure: false, ended: OnSceneRecorded).ConfigureAwait(true);
        }
        finally
        {
            _recordingScenes = false;
            IsRecording = wasRecording;
        }

        RefreshSceneStates(resetChecks: false);
        var recorded = scenes.Count(scene => scene.State == BddSceneState.Recorded);
        RecordingStatus = $"Recorded {recorded} of {scenes.Count} scenes";
        if (Narrator.Scenes.All(scene => scene.State == BddSceneState.Recorded))
        {
            await StitchAsync().ConfigureAwait(true);
        }
    }

    private void OnSceneRecorded(BddScenarioViewModel scenario, RunOutcome outcome)
    {
        RefreshSceneStates(resetChecks: false);
        if (Narrator.SceneOf(scenario) is { State: BddSceneState.Recorded } scene)
        {
            scene.Node.IsChecked = false;
        }
    }

    // Stitch: the latest recording of every scene, in the script's order, into one video. A scene
    // that failed or is out of date is stitched too, and named, so a draft can be watched.
    private async Task StitchAsync()
    {
        if (_recorder.Unavailable is { } reason)
        {
            RecordingStatus = reason;
            return;
        }

        if (ScenesFolder() is not { } folder || RecordingsFolder() is not { } recordings || Narrator.Check?.Script is not { } script)
        {
            return;
        }

        RefreshSceneStates(resetChecks: false);
        var missing = Narrator.Scenes.Where(scene => scene.State == BddSceneState.NotRecorded).Select(scene => scene.Number).ToList();
        if (missing.Count > 0)
        {
            RecordingStatus = $"Not stitched: no recording yet of scene {string.Join(", ", missing)}.";
            return;
        }

        var video = Path.Combine(recordings, $"{BddKeptScenes.NameOf(script.FilePath)}.mp4");
        RecordingStatus = "Stitching...";
        try
        {
            var made = await _recorder.StitchAsync(script.Title, folder, video, script.Scenes.Count, BddTouchMarks.For(script, setting: _touchColor), CancellationToken.None).ConfigureAwait(true);
            _lastRecording = made.Video;
            OnPropertyChanged(nameof(HasRecording));
            (WatchRecordingCommand as RelayCommand)?.NotifyCanExecuteChanged();
            var drafts = Narrator.Scenes
                .Where(scene => scene.State != BddSceneState.Recorded)
                .Select(scene => $"scene {scene.Number} {(scene.State == BddSceneState.Failed ? "failed" : "is out of date")}")
                .ToList();
            RecordingStatus = $"Stitched {Path.GetFileName(made.Video)}"
                + (drafts.Count > 0 ? $" (a draft: {string.Join(", ", drafts)})" : string.Empty)
                + (made.Warnings.Count > 0 ? $". {made.Warnings[0]}" : string.Empty);
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Not stitched: {ex.Message}";
        }
    }

    private void OpenRecordingsFolder()
    {
        if (RecordingsFolder() is { } folder)
        {
            Directory.CreateDirectory(folder);
            OpenVideo(folder);
        }
    }

    // The system's player, or Explorer for a folder: Presenter has neither.
    private static void OpenVideo(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            using (System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }))
            {
            }
        }
    }

    // The Steps list shows the selected scene's scenario, unless a run is on: it follows that.
    private void OnSceneSelected(NarratorSceneViewModel? scene)
    {
        if (_activeSession is null && scene?.Scenario is { } scenario)
        {
            SelectedScenario = scenario;
        }
    }

    // Run in Narrator mode: a rehearsal of the checked scenes, in the script's order. A scene that
    // fails does not stop the rest.
    private Task RunCheckedScenesAsync()
    {
        if (WorkspacePath is null || !Narrator.HasScript)
        {
            StatusSummary = "No video script open";
            return Task.CompletedTask;
        }

        var scenes = Narrator.CheckedScenes;
        if (scenes.Count == 0)
        {
            StatusSummary = "No scenes checked";
            return Task.CompletedTask;
        }

        ResetSceneRows(scenes);
        BddScenarioViewModel[] scenarios = [.. scenes.Select(scene => scene.Scenario!)];
        var options = CaptureRunExecutionOptions("Script", Narrator.ScriptName, scenarios);
        if (SpeakAloud)
        {
            // The lines and the script's beat set the pace, as in a recording.
            options = options with { EffectiveDelayMilliseconds = 0 };
        }

        return RunScenariosAsync(scenarios, options, stopAtFailure: false);
    }

    // Before a scene's first step: its recording starts, or a rehearsal with Speak aloud on gets
    // a pacer that plays each line before its step.
    private async Task StartSceneAsync(PresenterBddExecutionSession session, BddScenarioViewModel scenario)
    {
        _sceneRehearsal = false;
        if (_recordingScenes)
        {
            if (Narrator.SceneOf(scenario) is { Plan: { } recordedPlan } scene && ScenesFolder() is { } folder && Narrator.Check?.Script is { } recordedScript)
            {
                var request = new PresenterSceneRequest(
                    recordedPlan,
                    scene.Number,
                    BddKeptScenes.SceneFolder(folder, scene.Number),
                    scene.Hash,
                    recordedScript.Voice ?? _voice,
                    recordedScript.Beat ?? TimeSpan.FromMilliseconds(_beat),
                    _speakAloud,
                    recordedScript.Instructions,
                    BddScenePacing.Of(recordedScript));
                RecordingStatus = $"Speaking scene {scene.Number}: {scene.Scene.Name}";
                try
                {
                    await _recorder.StartSceneAsync(session, request, _executionCancellation?.Token ?? CancellationToken.None).ConfigureAwait(true);
                    _sceneRecording = true;
                    RecordingStatus = $"Recording scene {scene.Number} of {Narrator.Scenes.Count}: {scene.Scene.Name}";
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    RecordingStatus = $"Scene {scene.Number} not recorded: {ex.Message}";
                }
            }

            return;
        }

        if (!SpeakAloud || Narrator.SceneOf(scenario) is not { Plan: { } plan } || Narrator.Check?.Script is not { } script)
        {
            return;
        }

        try
        {
            _sceneRehearsal = await _recorder
                .RehearseAsync(session, plan, script.Voice ?? _voice, script.Beat ?? TimeSpan.FromMilliseconds(_beat), _executionCancellation?.Token ?? CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordingStatus = $"Not spoken: {ex.Message}";
        }
    }

    // After a scene's last step, before its app closes: the outro, and the scene's recording is finished.
    private async Task EndSceneAsync()
    {
        if (_sceneRecording)
        {
            _sceneRecording = false;
            try
            {
                await _recorder.FinishSceneAsync(CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                RecordingStatus = $"The scene's recording failed: {ex.Message}";
            }

            return;
        }

        if (!_sceneRehearsal)
        {
            return;
        }

        _sceneRehearsal = false;
        try
        {
            await _recorder.FinishSceneAsync(_executionCancellation?.Token ?? CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is OperationCanceledException or Bruinet.Bdd.Runner.BddRunnerException)
        {
            // Stopped while the outro played.
        }
    }

    // Entering the mode with no script open: the workspace's last one, or its first.
    private void ShowNarratorMode()
    {
        SetNarratorMode(true);
        if (!Narrator.HasScript && DefaultScript() is { } script)
        {
            OpenScript(script);
        }
    }

    private void SetNarratorMode(bool on)
    {
        if (_isNarratorMode == on)
        {
            return;
        }

        _isNarratorMode = on;
        if (on && _isPresentMode)
        {
            _isPresentMode = false;
            OnPropertyChanged(nameof(IsPresentMode));
        }

        OnPropertyChanged(nameof(IsNarratorMode));
        OnPropertyChanged(nameof(IsWorkspaceMode));
        OnPropertyChanged(nameof(IsScenarioSectionVisible));
        OnPropertyChanged(nameof(IsScenarioTreeVisible));
        if (on)
        {
            SelectedTab = TreeTabName;
        }
    }

    private async Task OpenScriptAsync()
    {
        try
        {
            if (await _folderPickerService.PickScriptAsync().ConfigureAwait(true) is { } path)
            {
                OpenScript(path);
            }
        }
        catch (Exception ex)
        {
            StatusSummary = "The script could not be opened";
            DiagnosticsText = Describe(ex);
        }
    }

    /// <summary>
    /// Opens a video script in Narrator mode. Its suite is found by walking up from the script to
    /// the config, and loaded as the workspace when it is not the one open.
    /// </summary>
    /// <param name="path">The <c>*.video.md</c> file.</param>
    public void OpenScript(string path)
    {
        Narrator.Open(path);
        if (Narrator.ConfigDirectory is { } suite && !SamePath(suite, WorkspacePath))
        {
            // Loading reopens the script against the new workspace (OnWorkspaceLoaded).
            LoadWorkspace(suite, recordRecent: true);
        }
        else
        {
            Narrator.Link(Scenarios);
            RefreshSceneStates(resetChecks: true);
        }

        SetNarratorMode(true);
        OnSceneSelected(Narrator.Scenes.FirstOrDefault(scene => scene.CanRun));
        StatusSummary = Narrator.ConfigDirectory is null
            ? "No config above the script: its scenarios cannot be found"
            : $"Opened {Narrator.ScriptName}: {Narrator.Summary}";
        SaveLastScript(path);
    }

    // After a workspace load: the open script is read again, or closed when it is another suite's.
    private void OnWorkspaceLoaded()
    {
        if (Narrator.ScriptPath is not { } script)
        {
            return;
        }

        if (!File.Exists(script) || !SamePath(BddVideoScriptCheck.FindConfigDirectory(Path.GetDirectoryName(script)), WorkspacePath))
        {
            Narrator.Close();
            return;
        }

        Narrator.Open(script);
        Narrator.Link(Scenarios);
        RefreshSceneStates(resetChecks: true);
    }

    private string? DefaultScript()
    {
        if (WorkspacePath is not { } workspace)
        {
            return null;
        }

        if (_settingsService.Load().LastScript is { Length: > 0 } last && File.Exists(last) &&
            SamePath(BddVideoScriptCheck.FindConfigDirectory(Path.GetDirectoryName(last)), workspace))
        {
            return last;
        }

        return Directory.EnumerateFiles(workspace, "*" + BddVideoScript.Extension, SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(workspace, path).Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private void SaveLastScript(string path)
    {
        try
        {
            var settings = _settingsService.Load();
            settings.LastScript = Path.GetFullPath(path);
            _settingsService.Save(settings);
        }
        catch (Exception ex)
        {
            DiagnosticsText = $"Could not save the last script: {ex.Message}";
        }
    }

    private static bool SamePath(string? left, string? right)
        => left is not null && right is not null &&
           string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
}
