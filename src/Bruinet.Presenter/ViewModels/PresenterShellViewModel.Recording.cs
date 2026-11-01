using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using Bruinet.Narrator.Windows;
using Bruinet.Presenter.Commands;
using Bruinet.Presenter.Services;

namespace Bruinet.Presenter.ViewModels;

// Record (the narrator): each run is recorded on video while Record is on, narrated by the scene a
// video script has for its scenario; the settings are the Recordings folder, the voice, the beat
// and Speak aloud.
public sealed partial class PresenterShellViewModel
{
    private const string DefaultVoiceChoice = "Default voice";

    private IPresenterRecorder _recorder = null!;
    private bool _isRecording;
    private string _recordingsFolder = string.Empty;
    private string? _voice;
    private int _beat;
    private bool _speakAloud;
    private string? _touchColor;
    private string _touchColorText = string.Empty;
    private string? _lastRecording;
    private string _recordingStatus = string.Empty;
    private string _voiceStatus = string.Empty;

    /// <summary>Whether runs are recorded on video; the Record button turns it on and off.</summary>
    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (SetProperty(ref _isRecording, value))
            {
                OnPropertyChanged(nameof(RecordDescription));
            }
        }
    }

    /// <summary>The Record button's tooltip.</summary>
    public string RecordDescription => IsNarratorMode
        ? $"Record the checked scenes ({Narrator.CheckedScenes.Count}), each into its own kept recording; then Stitch joins them."
        : IsRecording
            ? "Recording is on: each run is recorded on video. Click to stop recording."
            : "Record: record the next runs on video, with the voice of a video script's scene when it has one.";

    /// <summary>Turns recording on and off.</summary>
    public ICommand ToggleRecordCommand { get; private set; } = null!;

    /// <summary>Opens the last recording in the system's video player.</summary>
    public ICommand WatchRecordingCommand { get; private set; } = null!;

    /// <summary>Whether there is a recording to watch.</summary>
    public bool HasRecording => _lastRecording is not null;

    /// <summary>What the last recording made, or why it could not.</summary>
    public string RecordingStatus
    {
        get => _recordingStatus;
        private set => SetProperty(ref _recordingStatus, value);
    }

    /// <summary>The Recordings folder setting; empty for the suite's <c>TestResults\recordings</c>.</summary>
    public string RecordingsFolderText
    {
        get => _recordingsFolder;
        set
        {
            if (SetProperty(ref _recordingsFolder, value ?? string.Empty))
            {
                SaveRecordingSettings();
            }
        }
    }

    private static readonly IReadOnlyList<string> WindowsVoices = WindowsSpeech.Voices;

    /// <summary>The voices on this machine, the default first, then the cloud voices when one is set up.</summary>
    public IReadOnlyList<string> VoiceChoices =>
    [
        DefaultVoiceChoice,
        .. WindowsVoices,
        .. _cloud.IsSetUp ? CloudVoices.Select(voice => Bruinet.Narrator.AzureOpenAiSpeech.Prefix + voice) : [],
        .. SpeechVoicesForPicker(),
    ];

    /// <summary>The voice setting, by its name on the Settings page.</summary>
    public string VoiceChoice
    {
        get => _voice ?? DefaultVoiceChoice;
        set
        {
            var voice = value is null or DefaultVoiceChoice ? null : value;
            if (!string.Equals(voice, _voice, StringComparison.Ordinal))
            {
                _voice = voice;
                OnPropertyChanged();
                SaveRecordingSettings();
            }
        }
    }

    /// <summary>Speaks a sample line in the chosen voice.</summary>
    public ICommand TryVoiceCommand { get; private set; } = null!;

    /// <summary>What Try did: the voice that spoke, or that this machine has none.</summary>
    public string VoiceStatus
    {
        get => _voiceStatus;
        private set => SetProperty(ref _voiceStatus, value);
    }

    /// <summary>Says so when the open video script names a voice of its own: that one is used, not the setting.</summary>
    public string ScriptVoiceNote => Narrator.Check?.Script?.Voice is { Length: > 0 } voice
        ? $"The open script asks for {voice}: that voice is used for it."
        : string.Empty;

    /// <summary>Whether <see cref="ScriptVoiceNote"/> has anything to show.</summary>
    public bool HasScriptVoiceNote => ScriptVoiceNote.Length > 0;

    private async Task TryVoiceAsync()
    {
        VoiceStatus = "Speaking...";
        try
        {
            var spoke = await _recorder.TryVoiceAsync("This is the voice of your video.", _voice, CancellationToken.None).ConfigureAwait(true);
            VoiceStatus = spoke
                ? $"Spoken in {VoiceChoice}."
                : "This machine has no voice: add one in Settings, Time and language, Speech.";
        }
        catch (Exception ex)
        {
            VoiceStatus = $"The voice could not be tried: {ex.Message}";
        }
    }

    /// <summary>The beat setting in milliseconds, as the Settings page edits it.</summary>
    public string BeatText
    {
        get => _beat.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var beat) && beat <= MaxExecutionDelayMilliseconds && beat != _beat)
            {
                _beat = beat;
                OnPropertyChanged();
                SaveRecordingSettings();
            }
        }
    }

    /// <summary>The touch marks' accent as the Settings page edits it: <c>#RRGGBB</c>, or empty for Exact red. Saved when it is one of those.</summary>
    public string TouchColorText
    {
        get => _touchColorText;
        set
        {
            if (SetProperty(ref _touchColorText, value ?? string.Empty))
            {
                var color = _touchColorText.Trim();
                if (color.Length == 0 || Bruinet.Narrator.BddTouchMarks.IsColor(color))
                {
                    _touchColor = color.Length == 0 ? null : color.ToUpperInvariant();
                    SaveRecordingSettings();
                }
            }
        }
    }

    /// <summary>Whether a recording also plays each line aloud while the run waits for it.</summary>
    public bool SpeakAloud
    {
        get => _speakAloud;
        set
        {
            if (SetProperty(ref _speakAloud, value))
            {
                SaveRecordingSettings();
            }
        }
    }

    private void InitializeRecording(PresenterUserSettings settings, IPresenterRecorder? recorder)
    {
        _recorder = recorder ?? new PresenterRecorder();
        _recordingsFolder = settings.RecordingsFolder ?? string.Empty;
        _voice = settings.Voice;
        _beat = settings.Beat;
        _speakAloud = settings.SpeakAloud;
        _touchColor = Bruinet.Narrator.BddTouchMarks.IsColor(settings.TouchColor) ? settings.TouchColor : null;
        _touchColorText = _touchColor ?? string.Empty;
        ToggleRecordCommand = new RelayCommand(ToggleRecord);
        WatchRecordingCommand = new RelayCommand(WatchRecording, () => HasRecording);
        TryVoiceCommand = new AsyncRelayCommand(TryVoiceAsync);
    }

    private void ToggleRecord()
    {
        if (!IsRecording && _recorder.Unavailable is { } reason)
        {
            RecordingStatus = reason;
            return;
        }

        IsRecording = !IsRecording;
        RecordingStatus = IsRecording ? "Recording is on: the next run is recorded." : "Recording is off.";
    }

    // After the session is created, before its first step: the recording starts at that step.
    private async Task StartRecordingAsync(PresenterBddExecutionSession session, BddScenarioViewModel scenario)
    {
        // Narrator mode runs scenes; the Record toggle is Present mode's.
        if (IsNarratorMode)
        {
            await StartSceneAsync(session, scenario).ConfigureAwait(true);
            return;
        }

        if (!IsRecording || WorkspacePath is null)
        {
            return;
        }

        try
        {
            var settings = new PresenterRecordingSettings(_recordingsFolder.Length > 0 ? _recordingsFolder : null, _voice, TimeSpan.FromMilliseconds(_beat), _speakAloud) { TouchColor = _touchColor };
            await _recorder.StartAsync(session, WorkspacePath, scenario.FilePath, scenario.Name, settings, _executionCancellation?.Token ?? CancellationToken.None).ConfigureAwait(true);
            RecordingStatus = $"Recording {scenario.Name}...";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordingStatus = $"Not recorded: {ex.Message}";
        }
    }

    // When the run ends, however it ends: the outro plays and the video is composed, while the
    // next run may already start.
    private void FinishRecording()
    {
        if (_sceneRecording || _sceneRehearsal)
        {
            // The run ended before its scene did (Stop, or it could not start): the scene is dropped.
            _sceneRecording = false;
            _sceneRehearsal = false;
            _ = _recorder.DropSceneAsync();
            return;
        }

        _ = FinishRecordingAsync();
    }

    private async Task FinishRecordingAsync()
    {
        try
        {
            if (await _recorder.FinishAsync(CancellationToken.None).ConfigureAwait(true) is not { } recorded)
            {
                return;
            }

            _lastRecording = recorded.Video;
            OnPropertyChanged(nameof(HasRecording));
            (WatchRecordingCommand as RelayCommand)?.NotifyCanExecuteChanged();
            RecordingStatus = $"Recorded {Path.GetFileName(recorded.Video)}{(recorded.Narrated ? string.Empty : " (no video script narrates this scenario)")}"
                + (recorded.Warnings.Count > 0 ? $". {recorded.Warnings[0]}" : string.Empty);
        }
        catch (Exception ex)
        {
            RecordingStatus = $"The recording failed: {ex.Message}";
        }
    }

    private void WatchRecording()
    {
        if (_lastRecording is { } video && File.Exists(video))
        {
            // The system's video player: Presenter has none.
            using (Process.Start(new ProcessStartInfo(video) { UseShellExecute = true }))
            {
            }
        }
    }

    private void SaveRecordingSettings()
    {
        try
        {
            var settings = _settingsService.Load();
            settings.RecordingsFolder = _recordingsFolder.Length > 0 ? _recordingsFolder : null;
            settings.Voice = _voice;
            settings.Beat = _beat;
            settings.SpeakAloud = _speakAloud;
            settings.TouchColor = _touchColor;
            _settingsService.Save(settings);
        }
        catch (Exception ex)
        {
            DiagnosticsText = $"Could not save the recording settings: {ex.Message}";
        }

        // The voice and the beat are part of what a scene is recorded from; the folder is where its recording is.
        RefreshSceneStates(resetChecks: true);
    }
}
