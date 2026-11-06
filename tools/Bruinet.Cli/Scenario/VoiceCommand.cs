using System.CommandLine;
using Bruinet.Narrator;

namespace Bruinet.Cli.Scenario;

/// <summary><c>bruinet voice</c>: a suite's voice clips against its video scripts, without recording anything.</summary>
internal static class VoiceCommand
{
    public static Command Build(CliIo io)
    {
        var suite = new Option<string?>("--suite") { Description = "The suite's project folder. Default: this one." };
        var fill = new Option<bool>("--fill") { Description = "Add what the local speech cache holds to the library. Costs nothing." };
        var ask = new Option<bool>("--ask") { Description = "As --fill, and buy the lines found nowhere from the voice's service." };
        var prune = new Option<bool>("--prune") { Description = "Delete the clips no script says any more." };
        var command = new Command("voice", "List the lines of a suite's video scripts that have no clip in its speech folder, and what they would cost.")
        {
            suite,
            fill,
            ask,
            prune,
        };
        command.Action(io, (parsed, cancellationToken) =>
        {
            var folder = Path.GetFullPath(parsed.GetValue(suite) ?? ".", io.CurrentDirectory);
            var mode = parsed.GetValue(ask) ? ClipLibraryMode.Buy : parsed.GetValue(fill) ? ClipLibraryMode.Fill : ClipLibraryMode.Count;
            return RunAsync(io, folder, mode, parsed.GetValue(prune), Environment.GetEnvironmentVariable, cancellationToken);
        });
        return command;
    }

    /// <summary>Goes through every line of the suite's scripts. Exit 0 when every line has a clip, 1 when some have none, 2 when a script could not be read.</summary>
    internal static async Task<int> RunAsync(CliIo io, string folder, ClipLibraryMode mode, bool prune, Func<string, string?> read, CancellationToken cancellationToken)
    {
        var library = ClipLibrary.Find(folder, read)!;
        library.Mode = mode;
        var problems = 0;
        var scratch = Directory.CreateTempSubdirectory("bruinet-voice-").FullName;
        try
        {
            foreach (var path in Scripts(folder))
            {
                var check = BddVideoScriptCheck.Check(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false), path);
                if (!check.Success)
                {
                    problems++;
                    await io.Error.WriteLineAsync($"{path} has errors, so its lines are left out.").ConfigureAwait(false);
                    continue;
                }

                var voice = check.Script!.Voice ?? read(BddRecordingSession.VoiceVariable);
                var instructions = check.Script.Instructions ?? read(BddRecordingSession.InstructionsVariable);
                try
                {
                    // A platform voice is free and keeps no clips.
                    var synthesizer = Speech.Find(voice, instructions, folder, read, library: library);
                    if (synthesizer is AzureSpeech or AzureOpenAiSpeech)
                    {
                        foreach (var scene in check.Scenes)
                        {
                            await BddSceneClips.SpeakAsync(scene, synthesizer, voice, scratch, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
                catch (VoiceNotConfiguredException exception)
                {
                    problems++;
                    await io.Error.WriteLineAsync($"{path}: {exception.Message}").ConfigureAwait(false);
                }
            }
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }

        return await ReportAsync(io, library, prune, problems).ConfigureAwait(false);
    }

    private static async Task<int> ReportAsync(CliIo io, ClipLibrary library, bool prune, int problems)
    {
        await io.Out.WriteLineAsync((library.Use ?? new BddSpeechUse(0, 0, 0)).Summary).ConfigureAwait(false);
        var missing = library.Missing.Distinct(StringComparer.Ordinal).ToList();
        foreach (var line in missing)
        {
            await io.Out.WriteLineAsync($"  no clip: {line}").ConfigureAwait(false);
        }

        if (missing.Count > 0)
        {
            await io.Out.WriteLineAsync($"{missing.Count} lines have no clip ({missing.Sum(line => line.Length)} characters).").ConfigureAwait(false);
        }

        if (prune)
        {
            // A script left out would lose the clips only it says.
            if (problems > 0)
            {
                await io.Error.WriteLineAsync("Nothing pruned: not every script could be read.").ConfigureAwait(false);
            }
            else
            {
                await io.Out.WriteLineAsync($"{library.Prune()} clips no script says were deleted.").ConfigureAwait(false);
            }
        }

        return problems > 0 ? 2 : missing.Count > 0 ? 1 : 0;
    }

    private static IEnumerable<string> Scripts(string folder)
        => Directory.EnumerateFiles(folder, "*" + BddVideoScript.Extension, SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(folder, path).Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Order(StringComparer.Ordinal);
}
