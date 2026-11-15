using System.Xml.Linq;

namespace Frainet.Specs;

/// <summary>What a test run says about one rule (F1): named by a test is not proven.</summary>
public enum RuleState
{
    /// <summary>No test names the rule.</summary>
    NotReached,

    /// <summary>A test names the rule, and the results hold no run of it (or of one of its tests).</summary>
    NotRun,

    /// <summary>A test that names the rule failed.</summary>
    Failing,

    /// <summary>Every test that names the rule ran and passed.</summary>
    Passing,
}

/// <summary>One test's outcome in a run.</summary>
/// <param name="Name">The test's name as the run reports it, such as <c>Ns.ThingTests.One_Holds</c>.</param>
/// <param name="Outcome">Passed, Failed, NotExecuted, …, as the results file says.</param>
public sealed record TestOutcome(string Name, string Outcome)
{
    /// <summary>Whether the test passed.</summary>
    public bool Passed => string.Equals(Outcome, "Passed", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The rules' states from test results (F1). Results are read from Visual Studio's <c>.trx</c> files, as
/// <c>dotnet test --logger trx</c> writes them. A C# test is matched by its class and method; a scenario by its name.
/// </summary>
public static class RuleResults
{
    /// <summary>Reads every test outcome in the <c>.trx</c> files given, or found under the folders given.</summary>
    public static IReadOnlyList<TestOutcome> ReadTrx(IEnumerable<string> paths) => paths
        .SelectMany<string, string>(path => Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*.trx", SearchOption.AllDirectories).Order()
            : [path])
        .SelectMany(path => ParseTrx(File.ReadAllText(path)))
        .ToList();

    /// <summary>The test outcomes in one <c>.trx</c> document.</summary>
    public static IEnumerable<TestOutcome> ParseTrx(string trx) => XDocument.Parse(trx)
        .Descendants()
        .Where(element => element.Name.LocalName == "UnitTestResult")
        .Select(element => new TestOutcome(
            (string?)element.Attribute("testName") ?? string.Empty,
            (string?)element.Attribute("outcome") ?? "NotExecuted"));

    /// <summary>Each rule's state from the tests that reach it and the outcomes of a run.</summary>
    public static IReadOnlyDictionary<string, RuleState> States(RuleTraceResult trace, IReadOnlyList<TestOutcome> outcomes)
        => trace.Rules.ToDictionary(rule => rule.Key, rule => StateOf(rule.Value, outcomes));

    /// <summary>The findings of <see cref="States"/>: a rule not reached, not run, or failing.</summary>
    public static IReadOnlyList<SpecFinding> Findings(IReadOnlyDictionary<string, RuleState> states) => states
        .Where(state => state.Value != RuleState.Passing)
        .Select(state => state.Value switch
        {
            RuleState.NotReached => new SpecFinding(0, "rule-untested", $"{state.Key} is reached by no test."),
            RuleState.NotRun => new SpecFinding(0, "rule-not-run", $"{state.Key} has tests, and none of them ran."),
            _ => new SpecFinding(0, "rule-failing", $"{state.Key} has a failing test."),
        })
        .ToList();

    private static RuleState StateOf(IReadOnlyList<RuleReach> reaches, IReadOnlyList<TestOutcome> outcomes)
    {
        if (reaches.Count == 0)
        {
            return RuleState.NotReached;
        }

        var runs = reaches.Select(reach => outcomes.Where(outcome => Matches(reach, outcome)).ToList()).ToList();
        return runs.Any(run => run.Any(outcome => !outcome.Passed)) ? RuleState.Failing
            : runs.Any(run => run.Count == 0) ? RuleState.NotRun
            : RuleState.Passing;
    }

    // "Ns.Class.Method", "Ns.Class.Method(value: 1)"; a scenario's test carries its name in its arguments.
    private static bool Matches(RuleReach reach, TestOutcome outcome)
    {
        var name = outcome.Name;
        if (reach.File.EndsWith(".spec.md", StringComparison.OrdinalIgnoreCase))
        {
            // A runnable spec's example: its case is named after its rule, "B7.INV.01.R1 The page at a glance", so R1 is not R10.
            return name.Contains($"\"{reach.Rule} ", StringComparison.Ordinal);
        }

        var call = name.IndexOf('(', StringComparison.Ordinal);
        var head = call < 0 ? name : name[..call];
        if (reach.Container is { } container)
        {
            return head == $"{container}.{reach.Name}" || head.EndsWith($".{container}.{reach.Name}", StringComparison.Ordinal);
        }

        return call >= 0 && name[call..].Contains(reach.Name, StringComparison.Ordinal);
    }
}
