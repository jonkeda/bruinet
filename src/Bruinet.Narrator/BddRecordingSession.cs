using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Bruinet.Core.Artifacts;
using Bruinet.Recording;

namespace Bruinet.Narrator;

/// <summary>
/// A test run that records (<c>BRUINET_RECORD</c>): the video script whose scenes it records, or
/// every scenario, and where the scenes go. One per test process; each scenario asks it for its scene.
/// </summary>
/// <remarks>
/// <c>BRUINET_RECORD</c>: a <c>*.video.md</c> (full, or relative to the suite's folder) records its
/// scenes; <c>1</c> records every scenario, silently. <c>BRUINET_RECORD_DIR</c>: the Recordings
/// folder, the run's results folder when not set. <c>BRUINET_RECORD_OUT</c>: the scenes folder, for a
/// host that composes the video after the run. <c>BRUINET_VOICE</c>, <c>BRUINET_BEAT</c> and
/// <c>BRUINET_PAUSE</c> (ms): the settings, when the script does not say.
/// </remarks>
public sealed class BddRecordingSession
{
    /// <summary>What to record.</summary>
    public const string Variable = "BRUINET_RECORD";

    /// <summary>The Recordings folder.</summary>
    public const string FolderVariable = "BRUINET_RECORD_DIR";

    /// <summary>The scenes folder of this run.</summary>
    public const string OutputVariable = "BRUINET_RECORD_OUT";

    /// <summary>The voice, when the script does not name one.</summary>
    public const string VoiceVariable = "BRUINET_VOICE";

    /// <summary>How a cloud voice says the lines, when the script does not say.</summary>
    public const string InstructionsVariable = "BRUINET_VOICE_INSTRUCTIONS";

    /// <summary>The beat in milliseconds, when the script does not say.</summary>
    public const string BeatVariable = "BRUINET_BEAT";

    /// <summary>The least milliseconds an action takes in the video, when the script does not say.</summary>
    public const string PauseVariable = "BRUINET_PAUSE";

    /// <summary>
    /// When set with a video script: its scenes are kept in <c>&lt;folder&gt;\&lt;script&gt;.scenes</c> and
    /// not deleted when the video is made, so scenes can be recorded again one at a time, here or
    /// in Presenter's Narrator mode, and stitched again.
    /// </summary>
    public const string KeepVariable = "BRUINET_RECORD_KEEP";

    /// <summary>The beat when nothing says: long enough to see a step's result.</summary>
    public static readonly TimeSpan DefaultBeat = TimeSpan.FromMilliseconds(600);

    private readonly string _configDirectory;
    private readonly ISpeechSynthesizer? _synthesizer;
    private readonly SemaphoreSlim _composing = new(1, 1);
    private bool _composed;

    /// <summary>Creates a session.</summary>
    /// <param name="configDirectory">The suite's folder, which scenario addresses are relative to.</param>
    /// <param name="script">The video script's check; null to record every scenario.</param>
    /// <param name="scenesFolder">Where a script's scenes go; the Recordings folder, where each scenario gets its own, without one.</param>
    /// <param name="synthesizer">The voice; null to estimate the lines' lengths only.</param>
    /// <param name="voice">The voice's name, when the script names none.</param>
    /// <param name="beat">The beat, when the script does not say.</param>
    /// <param name="instructions">How a cloud voice says the lines, when the script does not say.</param>
    public BddRecordingSession(string configDirectory, BddVideoScriptCheck? script, string scenesFolder, ISpeechSynthesizer? synthesizer, string? voice, TimeSpan beat, string? instructions = null)
    {
        _configDirectory = configDirectory ?? throw new ArgumentNullException(nameof(configDirectory));
        Script = script;
        ScenesFolder = scenesFolder ?? throw new ArgumentNullException(nameof(scenesFolder));
        _synthesizer = synthesizer;
        Voice = script?.Script?.Voice ?? voice;
        Instructions = script?.Script?.Instructions ?? instructions;
        Beat = script?.Script?.Beat ?? beat;
    }

    /// <summary>Whether the script's scenes folder is kept when the video is made (<see cref="BddKeptScenes"/>).</summary>
    public bool KeepScenes { get; init; }

    /// <summary>The video script's check; null when every scenario is recorded.</summary>
    public BddVideoScriptCheck? Script { get; }

    /// <summary>Where a script's scenes go (a folder per scene); without a script, the Recordings folder.</summary>
    public string ScenesFolder { get; }

    /// <summary>The voice's name; null for the default voice.</summary>
    public string? Voice { get; }

    /// <summary>How a cloud voice says the lines; null for its own way.</summary>
    public string? Instructions { get; }

    /// <summary>The wait after a step.</summary>
    public TimeSpan Beat { get; }

    /// <summary>The pause of each action and when a line is said: the script's, else the settings'.</summary>
    public BddScenePacing Pacing { get; init; } = BddScenePacing.Default;

    /// <summary>The composed video's touch marks: the script's, with <see cref="BddTouchMarks.ColorVariable"/>'s accent when it names none.</summary>
    public BddTouchMarks Touches { get; init; } = BddTouchMarks.Default;

    /// <summary>
    /// The run's session from the environment; null when <c>BRUINET_RECORD</c> is not set. Throws
    /// when the script has errors, or ffmpeg is missing: a recording that cannot be made should
    /// not look like a test run that passed.
    /// </summary>
    /// <param name="configDirectory">The suite's folder.</param>
    /// <param name="read">Reads a process variable; <see cref="Environment.GetEnvironmentVariable(string)"/> by default.</param>
    [SuppressMessage("Maintainability", "CA1502", Justification = "Known debt (AD-033): split when it next changes.")]
    public static BddRecordingSession? FromEnvironment(string configDirectory, Func<string, string?>? read = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        var record = read(Variable)?.Trim();
        if (string.IsNullOrEmpty(record) || record is "0" || record.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Ffmpeg.Find(read) is null)
        {
            throw new FfmpegNotFoundException();
        }

        BddVideoScriptCheck? script = null;
        var name = "scenarios";
        if (record is not ("1" or "true" or "TRUE" or "True"))
        {
            var path = Path.IsPathRooted(record) ? record : Path.GetFullPath(Path.Combine(configDirectory, record));
            script = BddVideoScriptCheck.Check(File.ReadAllText(path), path);
            if (!script.Success)
            {
                throw new InvalidOperationException(
                    $"The video script {path} has errors:{Environment.NewLine}{string.Join(Environment.NewLine, script.Diagnostics)}");
            }

            name = Path.GetFileName(path)[..^BddVideoScript.Extension.Length];
        }

        var folder = read(FolderVariable) is { Length: > 0 } named ? named : DefaultTestArtifactPathProvider.Create().RunDirectory;
        var keep = script is not null && read(KeepVariable)?.Trim() is { Length: > 0 } kept && kept is not "0" && !kept.Equals("false", StringComparison.OrdinalIgnoreCase);
        var scenes = read(OutputVariable) is { Length: > 0 } output
            ? output
            : script is null ? folder
            : keep ? BddKeptScenes.Folder(folder, script.Script!.FilePath)
            : Path.Combine(folder, name + BddVideoComposer.RunFolderExtension);
        if (script is not null && !keep && Directory.Exists(scenes))
        {
            Directory.Delete(scenes, recursive: true);
        }

        var beat = int.TryParse(read(BeatVariable), NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds)
            ? TimeSpan.FromMilliseconds(milliseconds)
            : DefaultBeat;
        var pause = script?.Script?.Pause
            ?? (int.TryParse(read(PauseVariable), NumberStyles.None, CultureInfo.InvariantCulture, out var pauseMs) ? TimeSpan.FromMilliseconds(Math.Min(pauseMs, BddScenePacing.MaxPauseMs)) : BddScenePacing.DefaultPause);
        var pacing = new BddScenePacing(pause, script?.Script?.Say ?? BddSayMode.While);
        var voice = script?.Script?.Voice ?? read(VoiceVariable);
        var instructions = script?.Script?.Instructions ?? read(InstructionsVariable);
        var synthesizer = Speech.Find(voice, instructions, configDirectory, read);
        return new BddRecordingSession(configDirectory, script, scenes, synthesizer, voice, beat, instructions) { KeepScenes = keep, Touches = BddTouchMarks.For(script?.Script, read), Pacing = pacing };
    }

    /// <summary>
    /// The scene a scenario records, with its lines spoken into its folder; null when the script has
    /// no scene for it (it runs unrecorded).
    /// </summary>
    /// <param name="featureFile">The scenario's feature file, as the run reads it.</param>
    /// <param name="scenarioName">The scenario's name.</param>
    /// <param name="cancellationToken">Cancels the speech.</param>
    public async Task<BddSceneRequest?> SceneForAsync(string featureFile, string scenarioName, CancellationToken cancellationToken = default)
    {
        var address = BddScenarioAddress.Of(Path.GetRelativePath(_configDirectory, featureFile), scenarioName);
        if (Script is null)
        {
            var run = Path.Combine(ScenesFolder, BddScenarioAddress.Slug(scenarioName) + BddVideoComposer.RunFolderExtension);
            if (Directory.Exists(run))
            {
                Directory.Delete(run, recursive: true);
            }

            return new BddSceneRequest(1, scenarioName, address, Path.Combine(run, "scene-01"), BddSceneClips.Silent, Beat) { Pacing = Pacing };
        }

        var scenes = Script.Script!.Scenes;
        foreach (var plan in Script.Scenes)
        {
            var planned = BddScenarioAddress.Of(Path.GetRelativePath(Script.ConfigDirectory!, plan.FeatureFile), plan.Scenario.Name);
            if (string.Equals(planned, address, StringComparison.OrdinalIgnoreCase))
            {
                var number = IndexOf(scenes, plan.Scene) + 1;
                var folder = SceneFolder(number);
                var clips = await BddSceneClips.SpeakAsync(plan, _synthesizer, Voice, folder, cancellationToken).ConfigureAwait(false);
                return new BddSceneRequest(number, plan.Scene.Name, address, folder, clips, Beat) { Hash = BddKeptScenes.Hash(plan, Voice, Beat, Instructions, Pacing), Pacing = Pacing };
            }
        }

        return null;
    }

    /// <summary>
    /// Finishes a scene, and composes the video once it is whole: a scenario on its own at once, a
    /// script's scenes when the last of them has finished in this run.
    /// </summary>
    /// <param name="recording">The scene's recording, after its scenario ran.</param>
    /// <param name="cancellationToken">Cancels the outro and the compose.</param>
    /// <returns>The video, when this scene made it whole; null otherwise.</returns>
    public async Task<BddComposedVideo?> FinishAsync(BddSceneRecording recording, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recording);
        var scene = await recording.FinishAsync(cancellationToken).ConfigureAwait(false);
        if (Script is null)
        {
            return await BddVideoComposer.ComposeAsync(scene.Name, Path.GetDirectoryName(recording.Request.Folder)!, cancellationToken, Touches).ConfigureAwait(false);
        }

        await _composing.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_composed || !BddVideoComposer.IsComplete(ScenesFolder, Script.Script!.Scenes.Count))
            {
                return null;
            }

            _composed = true;
            if (KeepScenes)
            {
                var video = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(ScenesFolder))!, $"{BddKeptScenes.NameOf(Script.Script.FilePath)}.mp4");
                return await BddVideoComposer.StitchAsync(Script.Script.Title, ScenesFolder, video, Script.Script.Scenes.Count, cancellationToken, Touches).ConfigureAwait(false);
            }

            return await BddVideoComposer.ComposeAsync(Script.Script.Title, ScenesFolder, cancellationToken, Touches).ConfigureAwait(false);
        }
        finally
        {
            _composing.Release();
        }
    }

    private string SceneFolder(int number) => Path.Combine(ScenesFolder, $"scene-{number:00}");

    private static int IndexOf(IReadOnlyList<BddVideoScene> scenes, BddVideoScene scene)
    {
        for (var index = 0; index < scenes.Count; index++)
        {
            if (ReferenceEquals(scenes[index], scene))
            {
                return index;
            }
        }

        return -1;
    }
}
