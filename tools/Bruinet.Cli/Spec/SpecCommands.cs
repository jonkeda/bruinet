using System.CommandLine;
using Bruinet.Spec;

namespace Bruinet.Cli.Spec;

/// <summary><c>bruinet spec</c>: the Spec module's commands, on spec files (<c>*.spec.md</c>).</summary>
internal static class SpecCommands
{
    public static Command Build(CliIo io) => new("spec", "Spec files (*.spec.md): check them against the format (docs/spec/spec-format.md), and trace every rule to the tests that prove it.")
    {
        Check(io),
        Trace(io),
    };

    private static Command Check(CliIo io)
    {
        var paths = new Argument<string[]>("paths") { Description = "Spec files, or folders searched for *.spec.md.", Arity = ArgumentArity.OneOrMore };
        var command = new Command("check", "Check every spec against the format. Exit 1 when a spec has a finding.") { paths };
        command.Action(io, parsed =>
        {
            var files = parsed.GetValue(paths)!
                .SelectMany(given => Files(io, given))
                .ToList();
            if (files.Count == 0)
            {
                io.Error.WriteLine("No *.spec.md found.");
                return 2;
            }

            var failed = 0;
            foreach (var (shown, full) in files)
            {
                if (!File.Exists(full))
                {
                    io.Error.WriteLine($"{shown}: not found");
                    failed++;
                    continue;
                }

                var findings = SpecCheck.CheckFile(full);
                io.Out.WriteLine(findings.Count == 0 ? $"{shown}: follows the format" : $"{shown}: {findings.Count} finding(s)");
                foreach (var finding in findings)
                    io.Out.WriteLine($"  {finding}");
                failed += findings.Count > 0 ? 1 : 0;
            }

            return failed == 0 ? 0 : 1;
        });
        return command;
    }

    private static Command Trace(CliIo io)
    {
        var spec = new Argument<string>("spec") { Description = "The spec file." };
        var folders = new Argument<string[]>("tests") { Description = "Folders with the tests, searched for rule ids.", Arity = ArgumentArity.OneOrMore };
        var runnable = new Option<bool>("--runnable") { Description = "The spec's own examples run as scenarios, so each counts as a test of its rule." };
        var results = new Option<string[]>("--results")
        {
            Description = "Test results (.trx files or folders): every rule's tests must have run and passed.",
            AllowMultipleArgumentsPerToken = true,
        };
        var command = new Command("trace", "Find the tests that reach each rule of a spec; with --results, whether they passed. Exit 1 on a finding.")
        {
            spec, folders, runnable, results,
        };
        command.Action(io, parsed =>
        {
            var specPath = Full(io, parsed.GetValue(spec)!);
            var testFolders = parsed.GetValue(folders)!.Select(folder => Full(io, folder)).ToList();
            var resultPaths = (parsed.GetValue(results) ?? []).Select(path => Full(io, path)).ToList();
            var given = parsed.GetValue(folders)!.Concat(parsed.GetValue(results) ?? []).Prepend(parsed.GetValue(spec)!);
            if (given.FirstOrDefault(path => !File.Exists(Full(io, path)) && !Directory.Exists(Full(io, path))) is { } missing)
            {
                io.Error.WriteLine($"{missing}: not found");
                return 2;
            }

            var result = RuleTrace.TraceFile(specPath, testFolders, parsed.GetValue(runnable));
            var hasResults = parsed.GetResult(results) is not null;
            IReadOnlyDictionary<string, RuleState>? states = hasResults ? RuleResults.States(result, RuleResults.ReadTrx(resultPaths)) : null;
            foreach (var (rule, reaches) in result.Rules)
            {
                var state = states is null ? string.Empty : $" [{states[rule]}]";
                io.Out.WriteLine($"{rule}: {(reaches.Count == 0 ? "no test" : $"{reaches.Count} test(s)")}{state}");
                foreach (var reach in reaches)
                    io.Out.WriteLine($"  {reach.Name} ({reach.File}:{reach.Line})");
            }

            var findings = states is null
                ? result.Findings
                : result.Findings.Where(finding => finding.Code != "rule-untested").Concat(RuleResults.Findings(states)).ToList();
            var reached = result.Rules.Count(rule => rule.Value.Count > 0);
            var passing = states is null ? string.Empty : $", {states.Count(state => state.Value == RuleState.Passing)} passing";
            io.Out.WriteLine($"{reached} of {result.Rules.Count} rules reached by a test{passing}, {findings.Count} finding(s)");
            foreach (var finding in findings)
                io.Out.WriteLine($"  {finding}");

            return findings.Count == 0 ? 0 : 1;
        });
        return command;
    }

    // A folder gives its specs in order; a file is itself. Each is shown as given, read from its full path.
    private static IEnumerable<(string Shown, string Full)> Files(CliIo io, string given)
    {
        var full = Full(io, given);
        if (!Directory.Exists(full))
            return [(given, full)];
        return Directory.EnumerateFiles(full, "*.spec.md", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(file => (Path.Join(given, Path.GetRelativePath(full, file)), file));
    }

    private static string Full(CliIo io, string path) => Path.GetFullPath(path, io.CurrentDirectory);
}
