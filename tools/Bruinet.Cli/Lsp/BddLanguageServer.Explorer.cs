using Bruinet.Bdd.Preview;
using Bruinet.Bdd.Workspace;
using StreamJsonRpc;

namespace Bruinet.Bdd.LanguageServer;

// The BDD Explorer (phase 4): a suite's steps and hooks, its pages, and its last results.
public sealed partial class BddLanguageServer
{
    /// <summary><c>bruinet/explorerHost</c>: the page the explorer's webview loads once; <c>bruinet/explorer</c> fills it.</summary>
    [JsonRpcMethod("bruinet/explorerHost")]
    public PreviewHostResult ExplorerHost() => new(BddExplorerWriter.Page);

    /// <summary><c>bruinet/explorer</c>: one view of a suite, the one picked or the first.</summary>
    [JsonRpcMethod("bruinet/explorer")]
    public ExplorerResult Explorer(ExplorerParams request)
    {
        IReadOnlyList<BddSuite> suites;
        string root;
        lock (_gate)
        {
            suites = _workspace?.Suites.OrderBy(suite => suite.Name, StringComparer.OrdinalIgnoreCase).ToList() ?? [];
            root = _workspace?.Root ?? "the solution";
        }

        var view = request.View is BddExplorerWriter.Pages or BddExplorerWriter.Results ? request.View : BddExplorerWriter.Steps;
        var suite = suites.FirstOrDefault(candidate => string.Equals(candidate.Name, request.Suite, StringComparison.OrdinalIgnoreCase)) ?? suites.FirstOrDefault();
        var names = suites.Select(candidate => candidate.Name).ToList();
        if (suite is null)
        {
            return new ExplorerResult(view, $"<p class=\"uat-banner\">No suite in {System.Net.WebUtility.HtmlEncode(root)}: no project under it, or listed by a solution in it, has a {BddFileFormat.ConfigFileName}. Open the solution that holds the suites.</p>", names, null);
        }

        if (view == BddExplorerWriter.Results)
        {
            return new ExplorerResult(view, BddExplorerWriter.ResultsView(suite, RunsOfSuite(suite)), names, suite.Name);
        }

        if (suite.TryGetCatalogs(out var reason) is not { } catalogs)
        {
            return new ExplorerResult(view, $"<p class=\"uat-banner\" data-mode=\"parse-only\">{System.Net.WebUtility.HtmlEncode(reason ?? "Not built.")}</p>", names, suite.Name);
        }

        var html = view == BddExplorerWriter.Pages
            ? BddExplorerWriter.PagesView(suite, catalogs)
            : BddExplorerWriter.StepsView(suite, catalogs, [.. suite.StepFiles.SelectMany(path => (BddScenarioFiles.ParsePhraseFile(path).Document?.Callables ?? []).Select(callable => callable with { Source = callable.Source with { FilePath = callable.Source.FilePath ?? path } }))]);
        return new ExplorerResult(view, html, names, suite.Name);
    }

    /// <summary>A suite of the workspace by its name; null when there is none.</summary>
    internal BddSuite? SuiteNamed(string name)
    {
        lock (_gate)
        {
            return _workspace?.Suites.FirstOrDefault(suite => string.Equals(suite.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    // The newest run of each scenario of the suite's feature files, with the scenario's heading line.
    private List<(string File, BddScenarioRun Run, int Line)> RunsOfSuite(BddSuite suite)
    {
        List<(string, BddScenarioRun, int)> runs = [];
        if (_results is not { } results)
        {
            return runs;
        }

        foreach (var path in suite.FeatureFiles.Concat(suite.LinkedFiles).Where(path => BddFileFormat.IsScenarioFile(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            IReadOnlyDictionary<string, BddScenarioRun> found;
            lock (results)
            {
                found = results.For(BddRunResults.RootFor(suite.ProjectDirectory), suite.Name, path);
            }

            if (found.Count == 0)
            {
                continue;
            }

            var scenarios = BddMarkdownParser.Parse(File.ReadAllText(path), path).Document?.Scenarios ?? [];
            foreach (var run in found.Values)
            {
                var line = scenarios.FirstOrDefault(scenario => scenario.Name == run.Scenario)?.Source.LineNumber ?? 1;
                runs.Add((Path.GetFullPath(path), run, line));
            }
        }

        return runs;
    }
}
