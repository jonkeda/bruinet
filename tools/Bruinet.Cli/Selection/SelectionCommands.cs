using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using static Bruinet.Cli.CommonOptions;

namespace Bruinet.Cli.Selection;

/// <summary><c>bruinet tests</c>: which tests a change reaches, from the touch map a full run's timings make.</summary>
internal static class SelectionCommands
{
    public static Command Build(CliIo io) => new("tests", "Test selection and timings: where a run's time went, the touch map from its timings, and the tests a change reaches.")
    {
        TimingsCommand(io),
        TouchMapCommand(io),
        AffectedCommand(io),
    };

    private static Command TimingsCommand(CliIo io)
    {
        var timings = new Argument<string>("folder") { Description = "The folder BRUINET_TIMINGS named, or one timings-*.csv." };
        var top = new Option<int>("--top") { Description = "How many of the slowest to list per kind.", DefaultValueFactory = _ => 10 };
        var output = new Option<string?>("--out") { Description = $"Write the report here. Default: {TimingsReport.FileName} beside the timings." };
        var command = new Command("timings", "Write where a run's time went, from its timings: the host's start, time by kind, per test, and the slowest spans.") { timings, top, output };
        command.Action(io, parsed =>
        {
            var from = Path.GetFullPath(parsed.GetValue(timings)!, io.CurrentDirectory);
            var files = TouchMap.CsvFiles(from).ToList();
            if (files.Count == 0)
                throw new ArgumentException($"No timings-*.csv in {from}.");
            var report = TimingsReport.Read(files);
            var to = Path.GetFullPath(parsed.GetValue(output) ?? Path.Combine(Directory.Exists(from) ? from : Path.GetDirectoryName(from)!, TimingsReport.FileName), io.CurrentDirectory);
            File.WriteAllText(to, report.ToMarkdown(parsed.GetValue(top)));
            io.Error.WriteLine($"{report.Rows.Count} rows from {files.Count} timings files; wrote {to}");
            return 0;
        });
        return command;
    }

    private static Command TouchMapCommand(CliIo io)
    {
        var timings = new Argument<string[]>("timings") { Description = "A run's timings: a timings-*.csv, or the folder BRUINET_TIMINGS named.", Arity = ArgumentArity.OneOrMore };
        var project = Project();
        var output = new Option<string?>("--out") { Description = $"Write the map here. Default: {TouchMap.FileName} in the project's folder." };
        var command = new Command("touch-map", "Write the suite's touch map from a full run's timings: which test used which page objects and controls.") { timings, project, output };
        command.Action(io, parsed =>
        {
            var folder = Path.GetFullPath(parsed.GetValue(project) ?? ".", io.CurrentDirectory);
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
        var project = Project();
        var app = new Option<string[]>("--app") { Description = "An app the suite tests, its project folder. Default: the web projects the suite references (repeatable)." };
        var map = new Option<string?>("--map") { Description = $"The touch map. Default: {TouchMap.FileName} in the project's folder." };
        var since = new Option<string>("--base") { Description = "Changed since this commit, with the working tree's changes and new files.", DefaultValueFactory = _ => "HEAD" };
        var files = new Option<string[]>("--files") { Description = "These changed files instead of git's.", AllowMultipleArgumentsPerToken = true };
        var always = new Option<string[]>("--always") { Description = "A filter term run whatever changed, such as a smoke set's Gate=Smoke (repeatable)." };
        var json = Json();
        var command = new Command("affected", "The suite's tests a change reaches, each with why, as one dotnet test --filter.") { project, app, map, since, files, always, json };
        command.Action(io, parsed =>
        {
            var folder = Path.GetFullPath(parsed.GetValue(project) ?? ".", io.CurrentDirectory);
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

    private static Option<string?> Project() =>
        new("--project", "-p") { Description = "The test project's folder. Default: this one." };

    // The files changed since the commit, the working tree's included, and the new files git does not ignore.
    internal static List<string> Changed(string currentDirectory, string since)
    {
        // A revision that starts with '-' is read by git as an option (--output=<file> writes a file); it comes
        // from the command line or an MCP argument, so it is refused, and passed after --end-of-options (review 09).
        if (since.StartsWith('-'))
        {
            throw new InvalidOperationException($"'{since}' is not a commit: a base names a commit, branch or tag.");
        }

        var root = Git(currentDirectory, "rev-parse", "--show-toplevel").Single();
        return Git(currentDirectory, "diff", "--name-only", "--end-of-options", since)
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
