using System.Globalization;
using System.CommandLine;
using Bruinet.Narrator;

namespace Bruinet.Cli.Scenario;

/// <summary>What <c>bruinet voice</c> does with a test project's clips.</summary>
internal enum VoiceAct
{
    /// <summary>Lists the lines with no clip; writes nothing.</summary>
    List,

    /// <summary>Adds what the local speech cache holds; costs nothing.</summary>
    Fill,

    /// <summary>As <see cref="Fill"/>, and buys the rest, after asking.</summary>
    Buy,

    /// <summary>Deletes the clips no script says, after asking.</summary>
    Prune,

    /// <summary>Writes the index beside the scripts.</summary>
    Index,
}

/// <summary><c>bruinet voice</c>: a test project's voice clips against its video scripts, without recording anything.</summary>
internal static class VoiceCommand
{
    public static Command Build(CliIo io) => new("voice", "A test project's voice clips against its video scripts, without recording anything.")
    {
        Make(io, "list", VoiceAct.List, "List the lines of the video scripts that have no clip in the speech folder, and what they would cost."),
        Make(io, "fill", VoiceAct.Fill, "Add what the local speech cache holds to the speech folder. Costs nothing."),
        Make(io, "buy", VoiceAct.Buy, "As fill, and buy the lines found nowhere from the voice's service. Asks first."),
        Make(io, "prune", VoiceAct.Prune, "Delete the clips no script says any more. Asks first."),
        Make(io, "index", VoiceAct.Index, $"Write {IndexFile} beside the video scripts: which clip says which line."),
    };

    private static Command Make(CliIo io, string name, VoiceAct act, string description)
    {
        var project = new Option<string?>("--project", "-p") { Description = "The test project's folder. Default: this one." };
        var yes = act is VoiceAct.Buy or VoiceAct.Prune ? CommonOptions.Yes() : null;
        var command = new Command(name, description) { project };
        if (yes is not null)
        {
            command.Add(yes);
        }

        command.Action(io, (parsed, cancellationToken) => RunAsync(
            io, Path.GetFullPath(parsed.GetValue(project) ?? ".", io.CurrentDirectory), act, yes is not null && parsed.GetValue(yes), Environment.GetEnvironmentVariable, cancellationToken));
        return command;
    }

    /// <summary>The list of a folder's clips, beside its video scripts.</summary>
    internal const string IndexFile = "speech-clips.md";

    /// <summary>
    /// Goes through every line of the project's scripts and does <paramref name="act"/>. Exit 0 when done and nothing is
    /// missing, 1 when lines have no clip or a script has errors, 2 when not confirmed.
    /// </summary>
    internal static async Task<int> RunAsync(CliIo io, string folder, VoiceAct act, bool yes, Func<string, string?> read, CancellationToken cancellationToken)
    {
        var library = Library(folder, read, act == VoiceAct.Fill ? ClipLibraryMode.Fill : ClipLibraryMode.Count);
        var pass = await PassAsync(io, library, folder, read, cancellationToken).ConfigureAwait(false);
        switch (act)
        {
            case VoiceAct.Buy:
                return await BuyAsync(io, library, folder, read, pass.Problems, yes, cancellationToken).ConfigureAwait(false);
            case VoiceAct.Prune:
                return await PruneAsync(io, library, pass.Problems, yes, cancellationToken).ConfigureAwait(false);
            case VoiceAct.Index:
                foreach (var group in pass.Indexed.GroupBy(script => Path.GetDirectoryName(script.Script)!, StringComparer.OrdinalIgnoreCase))
                {
                    var file = Path.Combine(group.Key, IndexFile);
                    await File.WriteAllTextAsync(file, Index(group.Key, group.ToList(), library), cancellationToken).ConfigureAwait(false);
                    await io.Out.WriteLineAsync(Path.GetRelativePath(folder, file)).ConfigureAwait(false);
                }

                return pass.Problems > 0 ? 1 : 0;
            default:
                return await ReportAsync(io, library, pass.Problems).ConfigureAwait(false);
        }
    }

    private static ClipLibrary Library(string folder, Func<string, string?> read, ClipLibraryMode mode)
    {
        var library = ClipLibrary.Find(folder, read)
            ?? throw new ArgumentException($"No speech folder for {folder}: it is in no project, and {ClipLibrary.Variable} is not set.");
        library.Mode = mode;
        return library;
    }

    private sealed record Pass(int Problems, IReadOnlyList<(string Script, string Title, IReadOnlyList<IndexRow> Rows)> Indexed);

    // Every script's lines through the library; a script with errors is left out and counted.
    private static async Task<Pass> PassAsync(CliIo io, ClipLibrary library, string folder, Func<string, string?> read, CancellationToken cancellationToken)
    {
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

        return new Pass(problems, indexed);
    }

    // The count pass says what buying costs; only then, asked, the pass that buys.
    private static async Task<int> BuyAsync(CliIo io, ClipLibrary counted, string folder, Func<string, string?> read, int problems, bool yes, CancellationToken cancellationToken)
    {
        var missing = counted.Missing.Distinct(StringComparer.Ordinal).ToList();
        if (missing.Count == 0)
        {
            return await ReportAsync(io, counted, problems).ConfigureAwait(false);
        }

        await io.Error.WriteLineAsync($"{missing.Count} lines have no clip anywhere ({missing.Sum(line => line.Length)} characters).").ConfigureAwait(false);
        if (!yes && !await CommonOptions.ConfirmAsync(io, "Buy them from the voice's service?", cancellationToken).ConfigureAwait(false))
        {
            await io.Error.WriteLineAsync("Nothing bought.").ConfigureAwait(false);
            return 2;
        }

        var library = Library(folder, read, ClipLibraryMode.Buy);
        var pass = await PassAsync(io, library, folder, read, cancellationToken).ConfigureAwait(false);
        return await ReportAsync(io, library, pass.Problems).ConfigureAwait(false);
    }

    private static async Task<int> PruneAsync(CliIo io, ClipLibrary library, int problems, bool yes, CancellationToken cancellationToken)
    {
        // A script left out would lose the clips only it says.
        if (problems > 0)
        {
            await io.Error.WriteLineAsync("Nothing pruned: not every script could be read.").ConfigureAwait(false);
            return 1;
        }

        var unreached = library.Unreached();
        await io.Error.WriteLineAsync($"{unreached.Count} clips no script says.").ConfigureAwait(false);
        if (unreached.Count == 0)
        {
            return 0;
        }

        if (!yes && !await CommonOptions.ConfirmAsync(io, "Delete them?", cancellationToken).ConfigureAwait(false))
        {
            await io.Error.WriteLineAsync("Nothing deleted.").ConfigureAwait(false);
            return 2;
        }

        library.Prune();
        foreach (var clip in unreached)
        {
            await io.Out.WriteLineAsync(Path.GetFileName(clip)).ConfigureAwait(false);
        }

        await io.Error.WriteLineAsync($"{unreached.Count} clips no script says were deleted.").ConfigureAwait(false);
        return 0;
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
        text.Append(CultureInfo.InvariantCulture, $"Written by `bruinet voice index`; do not edit it by hand. The clips are in [{clips}]({clips}), one `.opus` per spoken line. ");
        text.Append("A clip is named by a hash of the voice and the words, so a reworded line gets a new clip and the old one goes with `bruinet voice prune`.\n");
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

    // The lines with no clip on stdout, the answer; the counts on stderr.
    private static async Task<int> ReportAsync(CliIo io, ClipLibrary library, int problems)
    {
        await io.Error.WriteLineAsync((library.Use ?? new BddSpeechUse(0, 0, 0)).Summary).ConfigureAwait(false);
        var missing = library.Missing.Distinct(StringComparer.Ordinal).ToList();
        foreach (var line in missing)
        {
            await io.Out.WriteLineAsync($"no clip: {line}").ConfigureAwait(false);
        }

        if (missing.Count > 0)
        {
            await io.Error.WriteLineAsync($"{missing.Count} lines have no clip ({missing.Sum(line => line.Length)} characters).").ConfigureAwait(false);
        }

        // A script with errors is something found, as a failing check is (D3): 1, not "could not run".
        return problems > 0 || missing.Count > 0 ? 1 : 0;
    }

    private static IEnumerable<string> Scripts(string folder)
        => Directory.EnumerateFiles(folder, "*" + BddVideoScript.Extension, SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(folder, path).Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Order(StringComparer.Ordinal);
}
