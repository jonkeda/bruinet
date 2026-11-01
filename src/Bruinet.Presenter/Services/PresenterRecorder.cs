using System.Globalization;
using Bruinet.Bdd.Runner;
using Bruinet.Narrator;
using Bruinet.Narrator.Windows;
using Bruinet.Recording;

namespace Bruinet.Presenter.Services;

/// <summary>What a Presenter recording uses: the settings, where the Settings page keeps them.</summary>
/// <param name="Folder">The Recordings folder; null for the suite's <c>TestResults\recordings</c>.</param>
/// <param name="Voice">The voice, by (part of) its name, when the video script names none; null for the default.</param>
/// <param name="Beat">The wait after a step, when the video script does not say.</param>
/// <param name="SpeakAloud">Whether each line is also played while the run waits for it.</param>
public sealed record PresenterRecordingSettings(string? Folder, string? Voice, TimeSpan Beat, bool SpeakAloud)
{
    /// <summary>The touch marks' accent setting, <c>#RRGGBB</c>; null for Exact red.</summary>
    public string? TouchColor { get; init; }
}

/// <summary>A recording Presenter made.</summary>
/// <param name="Video">The video file.</param>
/// <param name="Narrated">Whether a video script's scene narrated it.</param>
/// <param name="Warnings">What went less well than asked.</param>
public sealed record PresenterRecorded(string Video, bool Narrated, IReadOnlyList<string> Warnings);

/// <summary>A scene of a video script to record into its kept folder (<see cref="BddKeptScenes"/>).</summary>
/// <param name="Plan">The scene.</param>
/// <param name="Number">Its number in the script, from 1.</param>
/// <param name="Folder">Its folder; an earlier recording there is replaced.</param>
/// <param name="Hash">What it is recorded from, written to its timeline.</param>
/// <param name="Voice">The voice, by (part of) its name; null for the default.</param>
/// <param name="Beat">The wait after a step.</param>
/// <param name="SpeakAloud">Whether each line is also played while the run waits for it.</param>
/// <param name="Instructions">How a cloud voice says the lines; null for its own way.</param>
/// <param name="Pacing">The action pause and when lines are said; null for the default.</param>
public sealed record PresenterSceneRequest(BddVideoScenePlan Plan, int Number, string Folder, string Hash, string? Voice, TimeSpan Beat, bool SpeakAloud, string? Instructions = null, BddScenePacing? Pacing = null);

/// <summary>Records Presenter's runs, the way a gate run with <c>BRUINET_RECORD</c> does.</summary>
public interface IPresenterRecorder
{
    /// <summary>Whether recording can work here, and why not (no ffmpeg).</summary>
    string? Unavailable { get; }

    /// <summary>
    /// Starts recording a session's scenario: its runner paces it, and records from the
    /// first step. The scene a video script has for the scenario narrates it; without one it runs
    /// silently, with its beats.
    /// </summary>
    /// <param name="session">The session, before its first step.</param>
    /// <param name="workspacePath">The suite's folder.</param>
    /// <param name="featureFile">The scenario's feature file.</param>
    /// <param name="scenarioName">The scenario's name.</param>
    /// <param name="settings">The recording settings.</param>
    /// <param name="cancellationToken">Cancels the speech.</param>
    Task StartAsync(PresenterBddExecutionSession session, string workspacePath, string featureFile, string scenarioName, PresenterRecordingSettings settings, CancellationToken cancellationToken);

    /// <summary>After the run: the outro, then the video is composed. Null when nothing was being recorded.</summary>
    /// <param name="cancellationToken">Cancels the outro and the compose.</param>
    Task<PresenterRecorded?> FinishAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Rehearses a scene: each line is spoken and played before its step, nothing is recorded.
    /// False when this machine has no voice. <see cref="FinishSceneAsync"/> plays its outro.
    /// </summary>
    /// <param name="session">The session, before its first step.</param>
    /// <param name="scene">The scene.</param>
    /// <param name="voice">The voice, by (part of) its name; null for the default.</param>
    /// <param name="beat">The wait after a step.</param>
    /// <param name="cancellationToken">Cancels the speech.</param>
    Task<bool> RehearseAsync(PresenterBddExecutionSession session, BddVideoScenePlan scene, string? voice, TimeSpan beat, CancellationToken cancellationToken);

    /// <summary>
    /// Starts recording one scene of a script into its kept folder: its lines are spoken, and the
    /// session's runner paces it and records from the first step. Nothing is composed.
    /// </summary>
    /// <param name="session">The session, before its first step.</param>
    /// <param name="scene">The scene.</param>
    /// <param name="cancellationToken">Cancels the speech.</param>
    Task StartSceneAsync(PresenterBddExecutionSession session, PresenterSceneRequest scene, CancellationToken cancellationToken);

    /// <summary>After the scene's last step: the outro, then its video and timeline are finished. Null for a rehearsal, or when no scene was being recorded.</summary>
    /// <param name="cancellationToken">Cancels the outro.</param>
    Task<BddSceneTimeline?> FinishSceneAsync(CancellationToken cancellationToken);

    /// <summary>Gives up the scene being recorded (the run was stopped): nothing of it is kept.</summary>
    Task DropSceneAsync();

    /// <summary>Stitches a script's kept scenes into one video; the scenes stay.</summary>
    /// <param name="title">The video's title.</param>
    /// <param name="scenesFolder">The script's scenes folder.</param>
    /// <param name="video">The video to write.</param>
    /// <param name="sceneCount">The script's scene count.</param>
    /// <param name="touches">The script's touch marks.</param>
    /// <param name="cancellationToken">Cancels ffmpeg.</param>
    Task<PresenterRecorded> StitchAsync(string title, string scenesFolder, string video, int sceneCount, BddTouchMarks touches, CancellationToken cancellationToken);

    /// <summary>Speaks a line aloud in a voice, to hear it before choosing it. False when this machine has no voice.</summary>
    /// <param name="text">What to say.</param>
    /// <param name="voice">The voice, by (part of) its name; null for the default.</param>
    /// <param name="cancellationToken">Stops it.</param>
    Task<bool> TryVoiceAsync(string text, string? voice, CancellationToken cancellationToken);

    /// <summary>Speaks a line aloud with a cloud voice's settings as entered, before they are saved.</summary>
    /// <param name="text">The line.</param>
    /// <param name="voice">The cloud voice, <c>azure:&lt;name&gt;</c>.</param>
    /// <param name="secrets">The settings to try.</param>
    /// <param name="cancellationToken">Cancels the speech.</param>
    /// <exception cref="VoiceServiceException">The service could not make the line; its message says why.</exception>
    Task TryCloudVoiceAsync(string text, string voice, VoiceSecrets secrets, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Speaks a line aloud with Azure Speech's settings as entered, before they are saved.</summary>
    /// <param name="text">The line.</param>
    /// <param name="voice">The voice, <c>azure-speech-service:&lt;name&gt;</c>.</param>
    /// <param name="secrets">The settings to try.</param>
    /// <param name="cancellationToken">Cancels the speech.</param>
    /// <exception cref="VoiceServiceException">The service could not make the line; its message says why.</exception>
    Task TryAzureSpeechAsync(string text, string voice, SpeechSecrets secrets, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Asks Azure Speech for its voices of a locale, and keeps the list.</summary>
    /// <param name="secrets">The settings to ask with.</param>
    /// <param name="locale">A locale such as en-GB; empty for every voice.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="VoiceServiceException">The service could not be asked; its message says why.</exception>
    Task<IReadOnlyList<SpeechVoice>> ListSpeechVoicesAsync(SpeechSecrets secrets, string locale, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SpeechVoice>>([]);
}

/// <inheritdoc />
public sealed class PresenterRecorder : IPresenterRecorder
{
    private PresenterBddExecutionSession? _recording;
    private string? _recordingFolder;
    private PresenterBddExecutionSession? _scene;
    private string? _sceneFolder;
    private string? _title;
    private bool _narrated;
    private BddTouchMarks _touches = BddTouchMarks.Default;

    /// <inheritdoc />
    public string? Unavailable => Ffmpeg.Find() is null ? Ffmpeg.HowToGet : null;

    /// <inheritdoc />
    public async Task StartAsync(PresenterBddExecutionSession session, string workspacePath, string featureFile, string scenarioName, PresenterRecordingSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settings);
        if (Unavailable is not null)
        {
            throw new FfmpegNotFoundException();
        }

        var folder = settings.Folder is { Length: > 0 } named ? named : BddRecordings.DefaultFolder(workspacePath);
        var run = Path.Combine(folder, BddScenarioAddress.Slug(scenarioName) + BddVideoComposer.RunFolderExtension);
        TryDelete(run);
        var sceneFolder = Path.Combine(run, "scene-01");
        var address = BddScenarioAddress.Of(Path.GetRelativePath(workspacePath, featureFile), scenarioName);

        var clips = BddSceneClips.Silent;
        var name = scenarioName;
        var beat = settings.Beat;
        var pacing = BddScenePacing.Default;
        _touches = BddTouchMarks.For(null, setting: settings.TouchColor);
        if (BddRecordings.FindScene(workspacePath, featureFile, scenarioName) is { } found)
        {
            _touches = BddTouchMarks.For(found.Script, setting: settings.TouchColor);
            var voice = found.Script.Voice ?? settings.Voice;
            clips = await BddSceneClips.SpeakAsync(found.Scene, Speech.Find(voice, found.Script.Instructions, workspacePath), voice, sceneFolder, cancellationToken).ConfigureAwait(false);
            name = found.Scene.Scene.Name;
            beat = found.Script.Beat ?? beat;
            pacing = BddScenePacing.Of(found.Script);
        }

        _narrated = !ReferenceEquals(clips, BddSceneClips.Silent);
        _title = name;
        session.Said = Play;
        await session.RecordAsync(new BddRunnerRecord(1, name, address, sceneFolder, null, Clips(clips), beat.TotalMilliseconds, settings.SpeakAloud, pacing.Pause.TotalMilliseconds, pacing.Say == BddSayMode.Before)).ConfigureAwait(false);
        _recording = session;
        _recordingFolder = sceneFolder;
    }

    /// <inheritdoc />
    public async Task<bool> RehearseAsync(PresenterBddExecutionSession session, BddVideoScenePlan scene, string? voice, TimeSpan beat, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (Speech.Find(voice) is not { } speech)
        {
            return false;
        }

        var folder = Path.Combine(Path.GetTempPath(), "BruinetPresenter", "rehearsal");
        var clips = await BddSceneClips.SpeakAsync(scene, speech, voice, folder, cancellationToken).ConfigureAwait(false);
        session.Said = Play;
        await session.RehearseAsync(new BddRunnerRehearse(Clips(clips), beat.TotalMilliseconds, SpeakAloud: true)).ConfigureAwait(false);
        _scene = session;
        _sceneFolder = null;
        return true;
    }

    /// <inheritdoc />
    public async Task StartSceneAsync(PresenterBddExecutionSession session, PresenterSceneRequest scene, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(scene);
        if (Unavailable is not null)
        {
            throw new FfmpegNotFoundException();
        }

        // A scene recorded again replaces its earlier recording, whatever this one comes to.
        TryDelete(scene.Folder);
        var speech = Speech.Find(scene.Voice, scene.Instructions, scene.Folder);
        var clips = await BddSceneClips.SpeakAsync(scene.Plan, speech, scene.Voice, scene.Folder, cancellationToken).ConfigureAwait(false);
        session.Said = Play;
        await session.RecordAsync(new BddRunnerRecord(scene.Number, scene.Plan.Scene.Name, scene.Plan.Address, scene.Folder, scene.Hash, Clips(clips), scene.Beat.TotalMilliseconds, scene.SpeakAloud, (scene.Pacing ?? BddScenePacing.Default).Pause.TotalMilliseconds, scene.Pacing?.Say == BddSayMode.Before)).ConfigureAwait(false);
        _scene = session;
        _sceneFolder = scene.Folder;
    }

    /// <inheritdoc />
    public async Task<BddSceneTimeline?> FinishSceneAsync(CancellationToken cancellationToken)
    {
        if (_scene is not { } session)
        {
            return null;
        }

        _scene = null;
        _sceneFolder = null;
        var recorded = await session.FinishRecordingAsync(cancellationToken).ConfigureAwait(false);
        return recorded.Timeline is { } timeline ? BddSceneTimeline.Load(timeline) : null;
    }

    /// <inheritdoc />
    public async Task DropSceneAsync()
    {
        if (_scene is not { } session)
        {
            return;
        }

        var folder = _sceneFolder;
        _scene = null;
        _sceneFolder = null;
        try
        {
            await session.DropRecordingAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is BddRunnerException or ObjectDisposedException or InvalidOperationException)
        {
            // The runner ended with its run: there is no recording left to give up.
        }

        if (folder is not null)
        {
            TryDelete(folder);
        }
    }

    /// <inheritdoc />
    public async Task<PresenterRecorded> StitchAsync(string title, string scenesFolder, string video, int sceneCount, BddTouchMarks touches, CancellationToken cancellationToken)
    {
        var stitched = await BddVideoComposer.StitchAsync(title, scenesFolder, video, sceneCount, cancellationToken, touches).ConfigureAwait(false);
        return new PresenterRecorded(stitched.Video, true, stitched.Warnings);
    }

    /// <inheritdoc />
    public async Task TryCloudVoiceAsync(string text, string voice, VoiceSecrets secrets, CancellationToken cancellationToken)
    {
        var clip = await Speech.ClipAsync(new AzureOpenAiSpeech(secrets), text, voice, RehearsalFile("try-cloud"), cancellationToken).ConfigureAwait(false);
        await WindowsSpeech.PlayAsync(clip, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task TryAzureSpeechAsync(string text, string voice, SpeechSecrets secrets, CancellationToken cancellationToken)
    {
        var clip = await Speech.ClipAsync(new AzureSpeech(secrets), text, voice, RehearsalFile("try-speech"), cancellationToken).ConfigureAwait(false);
        await WindowsSpeech.PlayAsync(clip, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeechVoice>> ListSpeechVoicesAsync(SpeechSecrets secrets, string locale, CancellationToken cancellationToken)
    {
        // Asked for afresh: Load is the button that renews the kept list.
        var voices = await new AzureSpeechVoices(secrets).RefreshAsync(cancellationToken).ConfigureAwait(false);
        return AzureSpeechVoices.Of(voices, locale);
    }

    /// <inheritdoc />
    public async Task<bool> TryVoiceAsync(string text, string? voice, CancellationToken cancellationToken)
    {
        if (Speech.Find(voice) is not { } speech)
        {
            return false;
        }

        var clip = await Speech.ClipAsync(speech, text, voice, RehearsalFile("try-voice"), cancellationToken).ConfigureAwait(false);
        await WindowsSpeech.PlayAsync(clip, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<PresenterRecorded?> FinishAsync(CancellationToken cancellationToken)
    {
        if (_recording is not { } session || _recordingFolder is not { } folder)
        {
            return null;
        }

        _recording = null;
        _recordingFolder = null;
        var recorded = await session.FinishRecordingAsync(cancellationToken).ConfigureAwait(false);
        var video = await BddVideoComposer.ComposeAsync(_title ?? Path.GetFileName(folder), Path.GetDirectoryName(folder)!, cancellationToken, _touches).ConfigureAwait(false);
        return new PresenterRecorded(video.Video, _narrated, [.. recorded.Warnings, .. video.Warnings]);
    }

    // A file of its own for each Try: a clip still playing, or held by the player, is not written over. Older ones go when free.
    private static string RehearsalFile(string name)
    {
        var folder = Path.Combine(Path.GetTempPath(), "BruinetPresenter", "rehearsal");
        Directory.CreateDirectory(folder);
        foreach (var old in Directory.EnumerateFiles(folder, name + "-*.wav"))
        {
            try
            {
                File.Delete(old);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Still playing: the next Try removes it.
            }
        }

        return Path.Combine(folder, $"{name}-{Guid.NewGuid():N}.wav");
    }

    // The runner waits for a line and says so; the voice's player is Presenter's.
    private static void Play(BddRunnerSay say)
        => _ = PlayAsync(new SpeechClip(string.Empty, say.Wav, TimeSpan.FromSeconds(say.Seconds)));

    private static async Task PlayAsync(SpeechClip clip)
    {
        try
        {
            await WindowsSpeech.PlayAsync(clip, CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // A line that cannot be played is not heard; the run and its recording go on.
        }
    }

    private static BddRunnerClips Clips(BddSceneClips clips)
    {
        static BddRunnerClip? Clip(SpeechClip? clip) => clip is null ? null : new BddRunnerClip(clip.Text, clip.WavPath, clip.Duration.TotalSeconds);
        return new BddRunnerClips(Clip(clips.Intro), clips.Says.ToDictionary(pair => pair.Key, pair => Clip(pair.Value)!), Clip(clips.Outro));
    }

    // False when a file of the folder is still held: the scene then reads as it did, until it is recorded again.
    private static bool TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
