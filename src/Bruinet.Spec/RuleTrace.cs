using System.Text.RegularExpressions;

namespace Bruinet.Spec;

/// <summary>A test or scenario that names a rule.</summary>
/// <param name="Rule">The rule's id, such as <c>TOD.02.R3</c>.</param>
/// <param name="Name">The test method's or the scenario's name.</param>
/// <param name="File">The file it is in.</param>
/// <param name="Line">The 1-based line of the trait or tag.</param>
/// <param name="Container">A C# test's class; null for a scenario.</param>
public sealed record RuleReach(string Rule, string Name, string File, int Line, string? Container = null);

/// <summary>A spec's rules and the tests that reach them.</summary>
/// <param name="Rules">Every rule of the spec, with the tests that name it; none for an untested rule.</param>
/// <param name="Findings">An untested rule, or a test naming a rule the spec does not have.</param>
public sealed record RuleTraceResult(
    IReadOnlyDictionary<string, IReadOnlyList<RuleReach>> Rules,
    IReadOnlyList<SpecFinding> Findings);

/// <summary>
/// The work check: which tests reach a spec's rules, read from the test sources as text, so nothing
/// is built or run. A C# test names a rule with <c>[Trait("Rule", "TOD.02.R3")]</c>; a scenario
/// in a <c>.feature.md</c> or <c>.feature</c> file with the tag <c>@TOD.02.R3</c>.
/// </summary>
public static partial class RuleTrace
{
    /// <summary>Traces the rules of the spec in <paramref name="specPath"/> through the files under <paramref name="testFolders"/>.</summary>
    public static RuleTraceResult TraceFile(string specPath, IEnumerable<string> testFolders) =>
        Trace(File.ReadAllText(specPath), testFolders.SelectMany(Sources));

    /// <summary>
    /// As <see cref="TraceFile(string, IEnumerable{string})"/>, and when <paramref name="runnable"/> the spec's own examples
    /// count as tests of their rules: a suite that runs its specs (a Bruinet suite with a <c>Specs</c> row) names each
    /// example's test case after its rule (<c>B7.INV.01.R1 The page at a glance</c>).
    /// </summary>
    public static RuleTraceResult TraceFile(string specPath, IEnumerable<string> testFolders, bool runnable)
    {
        var sources = testFolders.SelectMany(Sources);
        return Trace(
            File.ReadAllText(specPath),
            runnable ? sources.Prepend((Path.GetFileName(specPath), SpecAsSource(File.ReadAllText(specPath)))) : sources);
    }

    /// <summary>
    /// A runnable spec as a test source: each example's heading (<c>### R1 …</c>, or <c>#### R1 …</c> in a chapter) is a
    /// test of its rule, tagged as a feature file's scenario is.
    /// </summary>
    public static string SpecAsSource(string spec)
    {
        var journey = SpecCheck.JourneyId(spec);
        var lines = spec.ReplaceLineEndings("\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (ExampleHeading().Match(lines[i]) is { Success: true } heading)
            {
                // The tag, then the name the test case starts with: the rule's id and a space, so R1 is not R10.
                lines[i] = $"@{journey}.R{heading.Groups[1].Value}\n## Scenario: {journey}.R{heading.Groups[1].Value} ";
            }
        }

        return string.Join('\n', lines);
    }

    [GeneratedRegex(@"^#{3,4}\s+R(\d+)\b")]
    private static partial Regex ExampleHeading();

    /// <summary>Traces a spec's rules through test sources given as (file, text).</summary>
    public static RuleTraceResult Trace(string spec, IEnumerable<(string File, string Text)> sources)
    {
        var journey = SpecCheck.JourneyId(spec);
        var rules = SpecCheck.RuleIds(spec);
        var prefix = journey + ".R";
        var reaches = sources
            .SelectMany(source => Reaches(source.File, source.Text))
            .Where(reach => reach.Rule.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();

        var findings = new List<SpecFinding>();
        foreach (var unknown in reaches.Where(reach => !rules.Contains(reach.Rule)))
        {
            findings.Add(new(0, "rule-unknown", $"{unknown.File}:{unknown.Line} {unknown.Name} names {unknown.Rule}, which the spec does not have."));
        }

        var byRule = rules.ToDictionary(
            rule => rule,
            rule => (IReadOnlyList<RuleReach>)reaches.Where(reach => reach.Rule == rule).ToList());

        foreach (var rule in rules.Where(rule => byRule[rule].Count == 0))
        {
            findings.Add(new(0, "rule-untested", $"{rule} is reached by no test."));
        }

        return new(byRule, findings);
    }

    private static IEnumerable<(string File, string Text)> Sources(string folder) => Directory
        .EnumerateFiles(folder, "*", SearchOption.AllDirectories)
        .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".feature.md", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".feature", StringComparison.OrdinalIgnoreCase))
        .Where(path => !InBuildOutput(path))
        .Order()
        .Select(path => (Path.GetRelativePath(folder, path), File.ReadAllText(path)));

    private static bool InBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "bin" or "obj");

    private static IEnumerable<RuleReach> Reaches(string file, string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var isCode = file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

        for (var i = 0; i < lines.Length; i++)
        {
            var rules = isCode
                ? RuleTrait().Matches(lines[i]).Select(match => match.Groups[1].Value)
                : lines[i].TrimStart().StartsWith('@')
                    ? lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(tag => tag.StartsWith('@')).Select(tag => tag[1..])
                    : [];

            foreach (var rule in rules.ToList())
            {
                yield return new(rule, NameAfter(lines, i, isCode), file, i + 1, isCode ? ClassBefore(lines, i) : null);
            }
        }
    }

    private static string NameAfter(string[] lines, int from, bool isCode)
    {
        for (var i = from + 1; i < lines.Length; i++)
        {
            var match = isCode ? MethodName().Match(lines[i]) : ScenarioName().Match(lines[i]);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        return "(no name)";
    }

    private static string? ClassBefore(string[] lines, int from)
    {
        for (var i = from; i >= 0; i--)
        {
            var match = ClassName().Match(lines[i]);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    [GeneratedRegex(@"\bclass\s+(\w+)")]
    private static partial Regex ClassName();

    [GeneratedRegex(@"Trait\(\s*""Rule""\s*,\s*""([^""]+)""\s*\)")]
    private static partial Regex RuleTrait();

    [GeneratedRegex(@"^\s*(?:public|internal|private|protected)\b[^=(]*?\b(\w+)\s*\(")]
    private static partial Regex MethodName();

    [GeneratedRegex(@"^\s*(?:##\s*)?Scenario(?: Outline)?:\s*(.+)$")]
    private static partial Regex ScenarioName();
}
