using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using static Bruinet.Cli.CommonOptions;

namespace Bruinet.Cli.Selection;

/// <summary><c>bruinet tests</c>: which tests a change reaches, from the touch map a full run's timings make.</summary>
internal static class SelectionCommands
{
    public static Command Build(CliIo io) => new("tests", "Test selection: the touch map from a run's timings, and the tests a change reaches.")
    {
        TouchMapCommand(io),
        AffectedCommand(io),
    };

    private static Command TouchMapCommand(CliIo io)
    {
        var timings = new Argument<string[]>("timings") { Description = "A run's timings: a timings-*.csv, or the folder BRUINET_TIMINGS named.", Arity = ArgumentArity.OneOrMore };
        var suite = Suite();
        var output = new Option<string?>("--out") { Description = $"Write the map here. Default: {TouchMap.FileName} in the suite's folder." };
        var command = new Command("touch-map", "Write the suite's touch map from a full run's timings: which test used which page objects and controls.") { timings, suite, output };
        command.Action(io, parsed =>
        {
            var folder = Path.GetFullPath(parsed.GetValue(suite) ?? ".", io.CurrentDirectory);
            var files = parsed.GetValue(timings)!.SelectMany(path => TouchMap.CsvFiles(Path.GetFullPath(path, io.CurrentDirectory))).ToList();
            var map = TouchMap.Read(files, folder);
            var to = Path.GetFullPath(parsed.GetValue(output) ?? Path.Combine(folder, TouchMap.FileName), io.CurrentDirectory);
            map.Save(to);
            io.Error.WriteLine($"{map.Tests.Count} tests and scenarios from {files.Count} timings files; wrote {to}");
            return 0;
        });
        return command;
    }

    private static Command AffectedCommand(CliIo io)
    {
        var suite = Suite();
        var app = new Option<string[]>("--app") { Description = "An app the suite tests, its project folder. Default: the web projects the suite references." };
        var map = new Option<string?>("--map") { Description = $"The touch map. Default: {TouchMap.FileName} in the suite's folder." };
        var since = new Option<string>("--base") { Description = "Changed since this commit, with the working tree's changes and new files.", DefaultValueFactory = _ => "HEAD" };
        var files = new Option<string[]>("--files") { Description = "These changed files instead of git's.", AllowMultipleArgumentsPerToken = true };
        var always = new Option<string[]>("--always") { Description = "A filter term run whatever changed, such as a smoke set's Gate=Smoke." };
        var json = Json();
        var command = new Command("affected", "The suite's tests a change reaches, each with why, as one dotnet test --filter.") { suite, app, map, since, files, always, json };
        command.Action(io, parsed =>
        {
            var folder = Path.GetFullPath(parsed.GetValue(suite) ?? ".", io.CurrentDirectory);
            var touchMap = TouchMap.Load(Path.GetFullPath(parsed.GetValue(map) ?? Path.Combine(folder, TouchMap.FileName), io.CurrentDirectory));
            var changed = parsed.GetValue(files) is { Length: > 0 } given
                ? given.Select(file => Path.GetFullPath(file, io.CurrentDirectory)).ToList()
                : Changed(io.CurrentDirectory, parsed.GetValue(since)!);
            var apps = parsed.GetValue(app) is { Length: > 0 } named ? named.Select(path => Path.GetFullPath(path, io.CurrentDirectory)).ToList() : null;
            var affected = Affected.Of(folder, apps, changed, touchMap, parsed.GetValue(always));
            if (parsed.GetValue(json))
            {
                io.Out.WriteLine(JsonSerializer.Serialize(new { changed = changed.Count, whole = affected.Whole, filter = affected.Filter, picked = affected.Picked, notes = affected.Notes },
                    new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                return 0;
            }

            foreach (var note in affected.Notes)
                io.Error.WriteLine($"  {note}");
            if (affected.Whole is { } whole)
            {
                io.Out.WriteLine($"The whole suite: {whole}.");
            }
            else if (affected.Filter is { } filter)
            {
                foreach (var test in affected.Picked)
                    io.Out.WriteLine($"{test.Name}: {test.Why}");
                io.Out.WriteLine($"--filter \"{filter}\"");
            }
            else
            {
                io.Out.WriteLine($"Nothing to run: {changed.Count} changed files reach no test of the suite.");
            }

            return 0;
        });
        return command;
    }

    private static Option<string?> Suite() =>
        new("--suite") { Description = "The suite's project folder. Default: this one." };

    // The files changed since the commit, the working tree's included, and the new files git does not ignore.
    internal static List<string> Changed(string currentDirectory, string since)
    {
        var root = Git(currentDirectory, "rev-parse", "--show-toplevel").Single();
        return Git(currentDirectory, "diff", "--name-only", since)
            .Concat(Git(currentDirectory, "ls-files", "--others", "--exclude-standard", "--full-name", ":/"))
            .Select(file => Path.GetFullPath(file, root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> Git(string currentDirectory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = currentDirectory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var git = Process.Start(start) ?? throw new InvalidOperationException("git did not start.");
        var output = git.StandardOutput.ReadToEnd();
        var error = git.StandardError.ReadToEnd();
        git.WaitForExit();
        if (git.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', arguments)}: {error.Trim()}");
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
