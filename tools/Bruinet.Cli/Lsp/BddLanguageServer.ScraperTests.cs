using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;
using Bruinet.Scraper.Testing;
using StreamJsonRpc;

namespace Bruinet.Bdd.LanguageServer;

// The Scraper view's Tests step: record a test, assert, resolve, draft, and run a draft once.
public sealed partial class BddLanguageServer
{
    /// <summary><c>bruinet/scraper/tests</c>: the project's test recordings, resolved, with their drafts.</summary>
    [JsonRpcMethod("bruinet/scraper/tests")]
    public ScraperResult<IReadOnlyList<RecordedTest>> ScraperTests(ScraperProjectParams request)
        => Answer(() => Scraper.Tests(request.Project));

    /// <summary><c>bruinet/scraper/startTest</c>: opens the recording browser on a test recording: actions, captures and the toolbar.</summary>
    [JsonRpcMethod("bruinet/scraper/startTest")]
    public Task<ScraperResult<RecordingState>> ScraperStartTest(ScraperStartParams request)
        => Answer(() => Scraper.StartRecordingAsync(request.Project, request.Session ?? "test", request.Url, autoCapture: true, actions: true));

    /// <summary><c>bruinet/scraper/assert</c>: the person clicks an element in the recording browser and chooses a check; null when they cancel.</summary>
    [JsonRpcMethod("bruinet/scraper/assert")]
    public Task<ScraperResult<RecordingEvent?>> ScraperAssert()
        => Answer(() => Scraper.AssertAsync());

    /// <summary><c>bruinet/scraper/testBrief</c>: a gap of a test recording as a brief for the agent; the first gaps' when no event is given.</summary>
    [JsonRpcMethod("bruinet/scraper/testBrief")]
    public ScraperResult<IReadOnlyList<string>> ScraperTestBrief(ScraperTestBriefParams request)
        => Answer(() => Scraper.TestBriefs(request.Project, request.Session, request.Event));

    /// <summary><c>bruinet/scraper/draftTest</c>: drafts a test recording as an xUnit test, a scenario or both.</summary>
    [JsonRpcMethod("bruinet/scraper/draftTest")]
    public ScraperResult<DraftTestResult> ScraperDraftTest(ScraperTestParams request)
        => Answer(() => Scraper.DraftTest(request.Project, request.Session, Target(request.Target), request.Name));

    /// <summary>
    /// <c>bruinet/scraper/runTest</c>: runs each draft of a test recording once, built first: an xUnit draft by
    /// its class, a scenario as the IDEs' Run does (<c>bruinet/runTarget</c>).
    /// </summary>
    [JsonRpcMethod("bruinet/scraper/runTest")]
    public Task<ScraperResult<IReadOnlyList<TestRunResult>>> ScraperRunTest(ScraperTestParams request)
        => Answer(async () =>
        {
            var test = Scraper.Tests(request.Project).FirstOrDefault(t => t.Session == request.Session)
                ?? throw new ScraperProblem($"No test recording '{request.Session}'.");
            if (test.Drafts.Count == 0)
                throw new ScraperProblem($"{test.Name} is not drafted yet: draft it first.");
            var results = new List<TestRunResult>();
            foreach (var draft in test.Drafts)
            {
                if (draft.Target == TestTarget.Xunit)
                {
                    results.Add(await TestRunner.RunAsync(Scraper.ProjectFile(request.Project), $"FullyQualifiedName~{draft.Class}").ConfigureAwait(false));
                    continue;
                }
                var target = RunTarget(new RunTargetParams(new Uri(draft.Path).AbsoluteUri, null, null));
                results.Add(target is { CanRun: true, Project: { } project, Filter: { } filter }
                    ? await TestRunner.RunAsync(project, filter).ConfigureAwait(false)
                    : new TestRunResult(target.Project ?? "", target.Filter ?? "", false, 0, 0, 0, $"{Path.GetFileName(draft.Path)} cannot run by address: {target.Reason}", []));
            }
            return (IReadOnlyList<TestRunResult>)results;
        });

    private static TestTarget? Target(string? name) =>
        name is null ? null : TestSettings.ParseTarget(name) ?? throw new ScraperProblem($"A target is xunit, scenario or both, not '{name}'.");
}

/// <summary>A test recording's parameters: the project, the session, and for a draft its target and name.</summary>
public sealed record ScraperTestParams(string? Project, string Session, string? Target, string? Name);

/// <summary><c>bruinet/scraper/testBrief</c>'s parameters: the project, the session, and the gap's event (the first gaps when null).</summary>
public sealed record ScraperTestBriefParams(string? Project, string Session, int? Event);
