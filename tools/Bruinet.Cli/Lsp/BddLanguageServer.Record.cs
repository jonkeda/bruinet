using System.Globalization;
using Bruinet.Bdd.Workspace;
using Bruinet.Narrator;
using Bruinet.Recording;
using StreamJsonRpc;

namespace Bruinet.Bdd.LanguageServer;

// Record (the narrator): the gate run that records a video script's scenes, or one scenario, and
// the recordings the preview's Watch lists. The run itself is the IDE's, as for Run.
public sealed partial class BddLanguageServer
{
    /// <summary>The folder beside the server with the recorder packages a suite cannot reference (<c>BRUINET_RECORDERS</c>).</summary>
    internal static string RecordersFolder => Path.Combine(AppContext.BaseDirectory, "recorders");

    /// <summary>
    /// <c>bruinet/recordTarget</c>: how to record a video script, or the scenario at a heading's
    /// line: the project, the filter and the variables of the gate run, and where the video will be.
    /// </summary>
    [JsonRpcMethod("bruinet/recordTarget")]
    public RecordTargetResult RecordTarget(RecordTargetParams request)
    {
        var path = Key(request.Uri);
        var file = Context(path, request.Suite);
        if (file.Suite is not { } suite)
        {
            return RecordTargetResult.Not("No suite holds this file.");
        }

        if (Ffmpeg.Find() is null)
        {
            return RecordTargetResult.Not(Ffmpeg.HowToGet);
        }

        if (suite.TryGetCatalogs(out var reason) is null || suite.ProjectFile is not { } project)
        {
            return RecordTargetResult.Not(reason ?? $"{suite.Name} has no project file.");
        }

        if (suite.IsLoaded && !suite.HasScenarioTheory)
        {
            return RecordTargetResult.Not($"{suite.Name} runs its scenarios with [Theory], so they have no address to record by. Change it to [BddScenarioTheory].");
        }

        var folder = RecordingsFolder(request.RecordingsFolder, suite);
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [BddRecordingSession.FolderVariable] = folder,
        };
        if (request.Voice is { Length: > 0 } voice)
        {
            variables[BddRecordingSession.VoiceVariable] = voice;
        }

        if (request.Beat is { } beat)
        {
            variables[BddRecordingSession.BeatVariable] = beat.ToString(CultureInfo.InvariantCulture);
        }

        if (BddTouchMarks.IsColor(request.TouchColor))
        {
            variables[BddTouchMarks.ColorVariable] = request.TouchColor!.Trim();
        }

        if (OperatingSystem.IsWindows() && Directory.Exists(RecordersFolder))
        {
            variables[RecordingPackages.Variable] = RecordersFolder;
        }

        if (BddVideoScript.IsVideoScript(path))
        {
            var check = BddVideoScriptCheck.Check(file.Text, path);
            if (!check.Success || check.Scenes.Count == 0)
            {
                return RecordTargetResult.Not(check.Success ? "The video script has no scenes." : $"The video script has errors: {check.Diagnostics.First(diagnostic => diagnostic.Severity == BddDiagnosticSeverity.Error).Message}");
            }

            var name = Path.GetFileName(path)[..^BddVideoScript.Extension.Length];
            var scenes = Path.Combine(folder, name + BddVideoComposer.RunFolderExtension);
            variables[BddRecordingSession.Variable] = path;
            variables[BddRecordingSession.OutputVariable] = scenes;
            var filter = string.Join('|', check.Scenes.Select(scene => $"{BddScenarioAddress.Trait}={BddScenarioAddress.Of(suite.RelativeToConfig(scene.FeatureFile), scene.Scenario.Name)}").Distinct(StringComparer.Ordinal));
            return new RecordTargetResult(true, null, project, filter, variables, $"{name}.mp4", folder, suite.Name);
        }

        var run = RunTarget(new RunTargetParams(request.Uri, request.Line, request.Suite));
        if (!run.CanRun || request.Line is null)
        {
            return RecordTargetResult.Not(run.Reason ?? "Record a scenario from its heading, or a video script.");
        }

        variables[BddRecordingSession.Variable] = "1";
        return new RecordTargetResult(true, null, run.Project, run.Filter, variables, null, folder, suite.Name);
    }

    /// <summary><c>bruinet/recordings</c>: the videos of a video script, or of a feature file's scenarios, newest first.</summary>
    [JsonRpcMethod("bruinet/recordings")]
    public IReadOnlyList<RecordingEntry> Recordings(RecordingsParams request)
    {
        var path = Key(request.Uri);
        var file = Context(path, request.Suite);
        if (file.Suite is not { } suite)
        {
            return [];
        }

        return RecordingsOf(path, RecordingsFolder(request.RecordingsFolder, suite));
    }

    // The videos the preview lists: a video script's, or a feature file's scenarios'.
    private IReadOnlyList<Bruinet.Bdd.Preview.BddPreviewRecording> PreviewRecordings(BddServerFile file, string? setting)
        => file.Suite is { } suite && RecordState(file) is not null
            ? RecordingsOf(file.Path, RecordingsFolder(setting, suite)).Select(video => new Bruinet.Bdd.Preview.BddPreviewRecording(video.Path, video.Name, video.Written)).ToList()
            : [];

    // Whether Record can work for the file: ffmpeg first, then a suite that runs scenarios by address.
    private static Bruinet.Bdd.Preview.BddPreviewRunState? RecordState(BddServerFile file)
    {
        if (!BddVideoScript.IsVideoScript(file.Path) && !file.Path.EndsWith(BddFileFormat.FeatureExtension, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (file.Suite is not { } suite)
        {
            return new(false, "No suite holds this file.");
        }

        if (Ffmpeg.Find() is null)
        {
            return new(false, Ffmpeg.HowToGet);
        }

        return suite.IsLoaded && !suite.HasScenarioTheory
            ? new(false, $"{suite.Name} runs its scenarios with [Theory], so they have no address to record by. Change it to [BddScenarioTheory].")
            : new(true);
    }

    // A script's videos start with its name; a feature file's with one of its scenarios' slugs.
    private IReadOnlyList<RecordingEntry> RecordingsOf(string path, string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        IReadOnlyList<string> prefixes = BddVideoScript.IsVideoScript(path)
            ? [Path.GetFileName(path)[..^BddVideoScript.Extension.Length] + "-"]
            : BddMarkdownParser.Parse(Context(path).Text, path).Document?.Scenarios.Select(scenario => BddScenarioAddress.Slug(scenario.Name) + "-").ToList() ?? [];
        return new DirectoryInfo(folder).EnumerateFiles("*.mp4")
            .Where(video => prefixes.Any(prefix => video.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(video => video.LastWriteTimeUtc)
            .Select(video => new RecordingEntry(video.FullName, video.Name, video.LastWriteTime, File.Exists(Path.ChangeExtension(video.FullName, ".srt")) ? Path.ChangeExtension(video.FullName, ".srt") : null))
            .ToList();
    }

    // The setting's folder, else the recordings folder in the suite's results.
    private static string RecordingsFolder(string? setting, BddSuite suite)
        => setting is { Length: > 0 } ? Path.GetFullPath(setting) : BddRecordings.DefaultFolder(suite.ProjectDirectory);
}
