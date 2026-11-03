using StreamJsonRpc;

namespace Bruinet.Bdd.LanguageServer;

// What an IDE's test explorer needs (phase 4, the VS Code client): every suite's scenarios with the
// address each runs by (R1), and a file's last results.
public sealed partial class BddLanguageServer
{
    /// <summary><c>bruinet/scenarios</c>: each suite, whether it runs by address, and its scenarios by file.</summary>
    [JsonRpcMethod("bruinet/scenarios")]
    public IReadOnlyList<SuiteScenarios> Scenarios()
    {
        IReadOnlyList<Workspace.BddSuite> suites;
        lock (_gate)
        {
            suites = _workspace?.Suites.OrderBy(suite => suite.Name, StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        }

        List<SuiteScenarios> result = [];
        foreach (var suite in suites)
        {
            var bound = suite.TryGetCatalogs(out var reason) is not null;
            var runnable = bound && suite.HasScenarioTheory && suite.ProjectFile is not null;
            reason = !bound ? reason
                : suite.ProjectFile is null ? $"{suite.Name} has no project file."
                : !suite.HasScenarioTheory ? $"{suite.Name} runs its scenarios with [Theory], so one has no address to run by. Change it to [UatScenarioTheory]."
                : null;
            List<FileScenarios> files = [];
            foreach (var path in suite.FeatureFiles.Concat(suite.LinkedFiles)
                .Where(path => path.EndsWith(BddFileFormat.FeatureExtension, StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var relative = suite.RelativeToConfig(path);
                var scenarios = BddMarkdownParser.Parse(Context(path).Text, path).Document?.Scenarios ?? [];

                // An outline's rows share its heading: one test with each row's address.
                var tests = scenarios
                    .GroupBy(scenario => scenario.Source.LineNumber)
                    .Select(rows => new ScenarioTest(
                        rows.First().OutlineName ?? rows.First().Name,
                        rows.Key,
                        string.Join('|', rows.Select(row => $"{BddScenarioAddress.Trait}={BddScenarioAddress.Of(relative, row.Name)}"))))
                    .ToList();
                files.Add(new FileScenarios(new Uri(path).AbsoluteUri, relative, tests));
            }

            result.Add(new SuiteScenarios(suite.Name, suite.ProjectFile, runnable, reason, files));
        }

        return result;
    }

    /// <summary>The newest run of each of a feature file's scenarios, by name, as its report has it: for a host that reads the steps.</summary>
    /// <param name="uri">The feature file.</param>
    /// <param name="suite">The suite a linked file runs in; its own when null.</param>
    /// <param name="accept">Only the runs it takes (the newest that failed, the newest that passed); every run when null.</param>
    internal IReadOnlyDictionary<string, Bruinet.Bdd.Preview.BddScenarioRun> NewestRuns(string uri, string? suite, Func<Bruinet.Bdd.Preview.BddScenarioRun, bool>? accept = null)
        => RunsOf(Context(Key(uri), suite), accept);

    /// <summary>The scenarios whose heading is at <paramref name="line"/> (an outline's every row), or every one of the file.</summary>
    internal IReadOnlyList<string> ScenarioNames(string uri, int? line)
    {
        var path = Key(uri);
        return BddMarkdownParser.Parse(Context(path).Text, path).Document?.Scenarios
            .Where(scenario => line is not { } at || scenario.Source.LineNumber == at)
            .Select(scenario => scenario.Name)
            .ToList() ?? [];
    }

    /// <summary><c>bruinet/lastRuns</c>: the newest run of each of a feature file's scenarios, with its failed step.</summary>
    [JsonRpcMethod("bruinet/lastRuns")]
    public IReadOnlyList<ScenarioResult> LastRuns(PreviewParams request)
    {
        var file = Context(Key(request.Uri), request.Suite);
        return RunsOf(file).Values
            .Select(run => run.Steps.FirstOrDefault(step => string.Equals(step.Status, "Failed", StringComparison.OrdinalIgnoreCase)) is { } failed
                ? new ScenarioResult(run.Scenario, run.Status, run.DurationMs, failed.Message, failed.WrittenLine)
                : new ScenarioResult(run.Scenario, run.Status, run.DurationMs, null, null))
            .OrderBy(result => result.Scenario, StringComparer.Ordinal)
            .ToList();
    }
}
