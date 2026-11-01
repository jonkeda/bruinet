using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;
using Bruinet.Scraper.Testing;

namespace Bruinet.Scraper.Tests.Testing;

public sealed class RecordedTestsTests : IAsyncLifetime
{
    private const string TestsConfig = "\n## Tests\n\n| Field | Value |\n| --- | --- |\n| Target | both |\n| Base | Sample.UITests.SampleTestBase |\n";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));
    private ScraperService _service = null!;
    private ScrapeProject _project = null!;
    private RecordingSession _session = null!;

    public Task InitializeAsync()
    {
        (_project, _session) = UserForm.Make(_root, TestsConfig);
        _session.AddAction(new RecordingEvent { Kind = RecordingEvent.ClickKind, Location = "http://localhost:5180/userform", Page = "/userform", Node = 11 });
        _service = new ScraperService(_root);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _service.DisposeAsync();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void A_test_recording_is_listed_with_its_drafts_which_go_stale_when_the_recording_changes()
    {
        Assert.Empty(Assert.Single(_service.Tests(null)).Drafts);

        var drafted = _service.DraftTest(null, _session.Id);
        var test = Assert.Single(_service.Tests(null));

        Assert.All(drafted.Files, f => Assert.True(f.Written, f.Note));
        Assert.Equal([TestTarget.Xunit, TestTarget.Scenario], test.Drafts.Select(d => d.Target));
        Assert.Equal("Sample.UITests.Tests.Recorded.UserFormTests", test.Drafts[0].Class);
        Assert.All(test.Drafts, d => Assert.False(d.Stale));

        _session.AddAction(new RecordingEvent { Kind = RecordingEvent.CheckKind, Location = "http://localhost:5180/userform", Page = "/userform", Node = 7, Checked = true });

        Assert.All(Assert.Single(_service.Tests(null)).Drafts, d => Assert.True(d.Stale));
    }

    [Fact]
    public void Tests_is_the_workflow_s_eighth_step_to_finish_until_drafted_without_gaps()
    {
        var before = _service.Progress(null).Steps[^1];
        _service.DraftTest(null, _session.Id);
        var after = _service.Progress(null).Steps[^1];

        Assert.Equal((8, "Tests", StepState.Attention), (before.Step, before.Name, before.State));
        Assert.Equal((StepState.Done, "1 test"), (after.State, after.Summary));
    }

    [Fact]
    public void A_run_s_result_is_read_from_its_summary_lines()
    {
        var passed = TestRunner.Read("p.csproj", "f", 0, ["build...", "Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 5 s - x.dll (net10.0)"]);
        var failed = TestRunner.Read("p.csproj", "f", 1, ["Failed!  - Failed:     1, Passed:     2, Skipped:     0, Total:     3, Duration: 5 s - x.dll (net10.0)"]);
        var none = TestRunner.Read("p.csproj", "f", 0, ["No test matches the given testcase filter"]);

        Assert.Equal((true, 1, 0, null), (passed.Passed, passed.PassedCount, passed.FailedCount, passed.Problem));
        Assert.Empty(passed.Output);
        Assert.Equal((false, "1 of 3 failed."), (failed.Passed, failed.Problem));
        Assert.Single(failed.Output);
        Assert.False(none.Passed);
        Assert.Contains("No test ran", none.Problem);
    }
}
