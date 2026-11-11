using System.Globalization;
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
        var index = new Option<bool>("--index") { Description = $"Write {IndexFile} beside the video scripts: which clip says which line." };
        var command = new Command("voice", "List the lines of a suite's video scripts that have no clip in its speech folder, and what they would cost.")
        {
            suite,
            fill,
            ask,
            prune,
            index,
        };
        command.Action(io, (parsed, cancellationToken) =>
        {
            var folder = Path.GetFullPath(parsed.GetValue(suite) ?? ".", io.CurrentDirectory);
            var mode = parsed.GetValue(ask) ? ClipLibraryMode.Buy : parsed.GetValue(fill) ? ClipLibraryMode.Fill : ClipLibraryMode.Count;
            return RunAsync(io, folder, mode, parsed.GetValue(prune), Environment.GetEnvironmentVariable, cancellationToken, parsed.GetValue(index));
        });
        return command;
    }

    /// <summary>The list of a folder's clips, beside its video scripts.</summary>
    internal const string IndexFile = "speech-clips.md";

    /// <summary>Goes through every line of the suite's scripts. Exit 0 when every line has a clip, 1 when some have none, 2 when a script could not be read.</summary>
    internal static async Task<int> RunAsync(CliIo io, string folder, ClipLibraryMode mode, bool prune, Func<string, string?> read, CancellationToken cancellationToken, bool index = false)
    {
        var library = ClipLibrary.Find(folder, read)!;
        library.Mode = mode;
        var problems = 0;
        var indexed = new List<(string Script, string Title, IReadOnlyList<IndexRow> Rows)>();
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
                        var rows = new List<IndexRow>();
                        foreach (var scene in check.Scenes)
                        {
                            var before = library.Lines.Count;
                            await BddSceneClips.SpeakAsync(scene, synthesizer, voice, scratch, cancellationToken).ConfigureAwait(false);
                            rows.AddRange(RowsOf(scene, library.Lines.Skip(before).ToList()));
                        }

                        indexed.Add((path, check.Script.Title, rows));
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

        var exit = await ReportAsync(io, library, prune, problems).ConfigureAwait(false);
        if (index)
        {
            foreach (var group in indexed.GroupBy(script => Path.GetDirectoryName(script.Script)!, StringComparer.OrdinalIgnoreCase))
            {
                var file = Path.Combine(group.Key, IndexFile);
                await File.WriteAllTextAsync(file, Index(group.Key, group.ToList(), library), cancellationToken).ConfigureAwait(false);
                await io.Out.WriteLineAsync($"Wrote {Path.GetRelativePath(folder, file)}.").ConfigureAwait(false);
            }
        }

        return exit;
    }

    /// <summary>A line of the index: where in the video it is said, and its clip.</summary>
    internal sealed record IndexRow(string Scene, string Where, string Line, string Key);

    // The voice is asked for the intro, each step's line in step order, then the outro: the same order as SpeakAsync.
    private static IEnumerable<IndexRow> RowsOf(BddVideoScenePlan scene, IReadOnlyList<ClipLine> lines)
    {
        var places = new List<(string Where, string Text)>();
        if (scene.Scene.Intro is { } intro)
        {
            places.Add(("Intro", intro));
        }

        places.AddRange(scene.Says.OrderBy(pair => pair.Key).Select(pair => (pair.Value.Step, pair.Value.Say)));
        if (scene.Scene.Outro is { } outro)
        {
            places.Add(("Outro", outro));
        }

        for (var i = 0; i < Math.Min(places.Count, lines.Count); i++)
        {
            yield return new IndexRow(scene.Scene.Name, places[i].Where, SpokenLine.Parse(places[i].Text).Plain, lines[i].Key);
        }
    }

    private static string Index(string folder, IReadOnlyList<(string Script, string Title, IReadOnlyList<IndexRow> Rows)> scripts, ClipLibrary library)
    {
        var clips = Path.GetRelativePath(folder, library.Folder).Replace('\\', '/');
        var text = new System.Text.StringBuilder();
        text.Append("# Speech clips\n\n");
        text.Append(CultureInfo.InvariantCulture, $"Written by `bruinet voice --index`; do not edit it by hand. The clips are in [{clips}]({clips}), one `.opus` per spoken line. ");
        text.Append("A clip is named by a hash of the voice and the words, so a reworded line gets a new clip and the old one goes with `bruinet voice --prune`.\n");
        foreach (var (script, title, rows) in scripts.OrderBy(script => script.Script, StringComparer.Ordinal))
        {
            text.Append(CultureInfo.InvariantCulture, $"\n## {title}\n\nScript: [{Path.GetFileName(script)}]({Path.GetFileName(script)})\n\n");
            text.Append("| Clip | Where | Line |\n| --- | --- | --- |\n");
            foreach (var row in rows)
            {
                var clip = library.Has(row.Key) ? $"[{row.Key}{ClipLibrary.Extension}]({clips}/{row.Key}{ClipLibrary.Extension})" : $"no clip yet (`{row.Key}`)";
                text.Append(CultureInfo.InvariantCulture, $"| {clip} | {Cell(row.Scene)}: {Cell(row.Where)} | {Cell(row.Line)} |\n");
            }
        }

        return text.ToString();
    }

    private static string Cell(string text) => string.Join(' ', text.Split((char[])['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Replace("|", "\\|", StringComparison.Ordinal);

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

        // A script with errors is something found, as a failing check is (D3): 1, not "could not run".
        return problems > 0 || missing.Count > 0 ? 1 : 0;
    }

    private static IEnumerable<string> Scripts(string folder)
        => Directory.EnumerateFiles(folder, "*" + BddVideoScript.Extension, SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(folder, path).Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Order(StringComparer.Ordinal);
}
