using Frainet.Specs;

// frainet check <spec or folder>...: every *.spec.md checked against docs/spec-format.md; exit 1 on a finding.
// frainet trace <spec> <tests folder>... [--results <trx or folder>...]: every rule of the spec reached by a test; with
// results, every rule's tests ran and passed (F1); exit 1 on a finding.
return args switch
{
    ["check", _, ..] => Check(args[1..]),
    ["trace", _, _, ..] => Trace(args[1], args[2..]),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("Usage: frainet check <spec.md or folder>...");
    Console.Error.WriteLine("       frainet trace <spec.md> <tests folder>... [--runnable] [--results <file.trx or folder>...]");
    return 2;
}

static int Check(string[] paths)
{
    var files = paths
        .SelectMany<string, string>(path => Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*.spec.md", SearchOption.AllDirectories).Order()
            : [path])
        .ToList();

    if (files.Count == 0)
    {
        Console.Error.WriteLine("No *.spec.md found.");
        return 2;
    }

    var failed = 0;
    foreach (var file in files)
    {
        if (!File.Exists(file))
        {
            Console.Error.WriteLine($"{file}: not found");
            failed++;
            continue;
        }

        var findings = SpecCheck.CheckFile(file);
        Console.WriteLine(findings.Count == 0 ? $"{file}: follows the format" : $"{file}: {findings.Count} finding(s)");
        foreach (var finding in findings)
        {
            Console.WriteLine($"  {finding}");
        }

        failed += findings.Count > 0 ? 1 : 0;
    }

    return failed == 0 ? 0 : 1;
}

static int Trace(string spec, string[] rest)
{
    var runnable = rest.Contains("--runnable");
    rest = [.. rest.Where(arg => arg != "--runnable")];
    var at = Array.IndexOf(rest, "--results");
    var folders = at < 0 ? rest : rest[..at];
    var resultPaths = at < 0 ? [] : rest[(at + 1)..];
    foreach (var missing in folders.Concat(resultPaths).Prepend(spec).Where(path => !File.Exists(path) && !Directory.Exists(path)))
    {
        Console.Error.WriteLine($"{missing}: not found");
        return 2;
    }

    var result = RuleTrace.TraceFile(spec, folders, runnable);
    IReadOnlyDictionary<string, RuleState>? states = at < 0 ? null : RuleResults.States(result, RuleResults.ReadTrx(resultPaths));
    foreach (var (rule, reaches) in result.Rules)
    {
        var state = states is null ? string.Empty : $" [{states[rule]}]";
        Console.WriteLine($"{rule}: {(reaches.Count == 0 ? "no test" : $"{reaches.Count} test(s)")}{state}");
        foreach (var reach in reaches)
        {
            Console.WriteLine($"  {reach.Name} ({reach.File}:{reach.Line})");
        }
    }

    var findings = states is null
        ? result.Findings
        : result.Findings.Where(finding => finding.Code != "rule-untested").Concat(RuleResults.Findings(states)).ToList();
    var reached = result.Rules.Count(rule => rule.Value.Count > 0);
    var passing = states is null ? string.Empty : $", {states.Count(state => state.Value == RuleState.Passing)} passing";
    Console.WriteLine($"{reached} of {result.Rules.Count} rules reached by a test{passing}, {findings.Count} finding(s)");
    foreach (var finding in findings)
    {
        Console.WriteLine($"  {finding}");
    }

    return findings.Count == 0 ? 0 : 1;
}
