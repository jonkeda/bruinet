using Bruinet.Scraper.Code;
using Bruinet.Scraper.Factory;
using Bruinet.Scraper.Testing;

namespace Bruinet.Scraper.Tests.Service;

// What a loop leaves behind that can be counted: reasons, the rules that stop a batch, the state and its ledger, and a run's tests one by one.
public sealed class FactoryLoopTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static LoopResult Loop(string group, int kept, params Reason[] reasons) => new()
    {
        Group = group,
        Batch = 1,
        Drafted = [.. Enumerable.Range(0, kept + reasons.Length).Select(n => $"Pages/P{n}.cs")],
        Kept = [.. Enumerable.Range(0, kept).Select(n => $"Pages/P{n}.cs")],
        Reasons = reasons,
        Seconds = new Dictionary<string, double> { ["draft"] = 30, ["test"] = 90 },
    };

    private static Reason Test(string code, string place = "/a") => new(code, "test", place, "ATests", "it said so", FixPlaces.Reader);

    [Theory]
    [InlineData("The form does not show the field 'Country'.", false, ReasonCodes.LabelNotFound, FixPlaces.Reader)]
    [InlineData("The page /project/view#/planning is not at its address after 15 s.", false, ReasonCodes.DidNotOpen, FixPlaces.DraftKind)]
    [InlineData("System.TimeoutException: Timeout 15000ms exceeded.", false, ReasonCodes.Timeout, FixPlaces.Environment)]
    [InlineData("Anything at all", true, ReasonCodes.NeedsRecord, FixPlaces.Records)]
    [InlineData("Something nobody foresaw", false, ReasonCodes.Unknown, FixPlaces.Person)]
    public void A_test_s_failure_is_a_reason_with_a_code_and_where_to_fix_it(string message, bool record, string code, string fixAt)
    {
        var reason = ReasonClassifier.OfTest(message, "/a", "ATests", record);

        Assert.Equal((code, fixAt, "test", "ATests"), (reason.Code, reason.FixAt, reason.Station, reason.Class));
    }

    [Fact]
    public void A_draft_that_was_refused_and_a_file_that_does_not_build_are_reasons_too()
    {
        var noPage = ReasonClassifier.OfDraft("No page class of Shop names the address /orders: draft or write that page first, or give its class.", "/orders › + Order");
        var build = ReasonClassifier.OfBuild(new CodeProblem("Pages/A.cs", 3, 9, "error", "CS0246", "The type or namespace name 'TimeRow<>' could not be found"), "/a", "APage");

        Assert.Equal((ReasonCodes.NoPageClass, FixPlaces.DraftKind), (noPage.Code, noPage.FixAt));
        Assert.Equal((ReasonCodes.DoesNotBuild, FixPlaces.Templates, "APage"), (build.Code, build.FixAt, build.Class));
        Assert.StartsWith("CS0246: ", build.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void A_batch_goes_on_while_its_loops_keep_something_and_no_reason_repeats()
    {
        Assert.Null(StopRules.Check([]));
        Assert.Null(StopRules.Check([Loop("/a", 2), Loop("/b", 1, Test(ReasonCodes.LabelNotFound)), Loop("/c", 2, Test(ReasonCodes.DidNotOpen))]));
        // A loop that owed nothing kept nothing, and that is no failure.
        Assert.Null(StopRules.Check([Loop("/a", 0), Loop("/b", 0)]));
    }

    [Fact]
    public void The_same_reason_three_times_in_a_batch_stops_it_and_names_the_reason()
    {
        var batch = new[] { Loop("/a", 1, Test(ReasonCodes.DidNotOpen, "/a")), Loop("/b", 1, Test(ReasonCodes.DidNotOpen, "/b"), Test(ReasonCodes.DidNotOpen, "/b2")) };

        var stop = StopRules.Check(batch);

        Assert.NotNull(stop);
        Assert.Contains("'did-not-open' came 3 times", stop, StringComparison.Ordinal);
        Assert.Contains("to fix at reader", stop, StringComparison.Ordinal);
        // A place with nothing to check is listed: it is no fault of the process, and such a loop did not fail.
        Assert.Null(StopRules.Check([Loop("/a", 0, Test(ReasonCodes.NothingToCheck)) with { Drafted = [] }, Loop("/b", 0, Test(ReasonCodes.NothingToCheck), Test(ReasonCodes.NothingToCheck)) with { Drafted = [] }]));
        // A test that passed the second time is said and does not stop a batch.
        Assert.Null(StopRules.Check([Loop("/a", 3, Test(ReasonCodes.PassedSecondTime), Test(ReasonCodes.PassedSecondTime), Test(ReasonCodes.PassedSecondTime))]));
    }

    [Fact]
    public void Two_loops_in_a_row_that_keep_nothing_stop_the_batch_and_a_loop_s_own_stop_does_at_once()
    {
        var nothing = StopRules.Check([Loop("/a", 2), Loop("/b", 0, Test(ReasonCodes.LabelNotFound)), Loop("/c", 0, Test(ReasonCodes.Timeout))]);

        Assert.Contains("2 loops in a row kept nothing (/b, /c)", nothing, StringComparison.Ordinal);
        Assert.Equal("the suite does not build", StopRules.Check([Loop("/a", 2) with { Stop = "the suite does not build" }]));
    }

    [Fact]
    public void The_state_is_kept_in_a_file_and_the_ledger_is_written_from_it()
    {
        Directory.CreateDirectory(_root);
        var state = new FactoryState();
        var first = state.Add(Loop("/day-off", 2) with { Tests = 1, PassedFirst = 1, Walk = "Done: 1 pages opened" });
        state.Add(Loop("/project", 0, Test(ReasonCodes.NeedsRecord, "/project/view#/planning"), Test(ReasonCodes.NeedsRecord, "/project/view#/waste")) with { Batch = 2, Stop = "a reason | repeated" });
        state.Notes.Add(new FactoryNote(2, new DateTimeOffset(2026, 11, 19, 14, 30, 0, TimeSpan.Zero), "Records come first."));
        state.Save(_root);

        var read = FactoryState.Load(_root);
        var ledger = FactoryLedger.ToMarkdown(read);

        Assert.Equal((1, 2, 3), (first.Number, read.Loops.Count, read.NextBatch));
        Assert.Equal(ReasonCodes.NeedsRecord, read.Loops[1].Reasons[0].Code);
        Assert.Contains("| 1 | 1 | `/day-off` | `P0`, `P1` | none | 1 of 1 | 2 | Done: 1 pages opened |  |", ledger, StringComparison.Ordinal);
        Assert.Contains("needs-record x2 **Stopped:** a reason / repeated", ledger, StringComparison.Ordinal);
        Assert.Contains("| needs-record | 2 | 1 | reader |", ledger, StringComparison.Ordinal);
        Assert.Contains("| 2 | 2026-11-19 14:30 | Records come first. |", ledger, StringComparison.Ordinal);
        Assert.Empty(FactoryState.Load(Path.Combine(_root, "nowhere")).Loops);
    }

    [Fact]
    public void A_results_file_gives_each_test_with_its_class_and_what_it_said()
    {
        const string Trx = """
            <?xml version="1.0" encoding="utf-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results>
                <UnitTestResult testId="1" testName="Shop.Tests.DayOffDialogTests.ShowsItsFields" duration="00:00:12.5000000" outcome="Passed" />
                <UnitTestResult testId="2" testName="Shop.Tests.OrdersOpensTests.Opens(href: &quot;/orders&quot;)" duration="00:00:15.0000000" outcome="Failed">
                  <Output><ErrorInfo><Message>The page /orders is not at its address after 15 s.</Message></ErrorInfo></Output>
                </UnitTestResult>
              </Results>
              <TestDefinitions>
                <UnitTest id="1"><TestMethod className="Shop.Tests.DayOffDialogTests" name="ShowsItsFields" /></UnitTest>
              </TestDefinitions>
            </TestRun>
            """;

        var tests = TestRunner.ReadTrx(Trx);

        Assert.Equal(new TestOutcome("DayOffDialogTests", "Shop.Tests.DayOffDialogTests.ShowsItsFields", true, null, 12.5), tests[0]);
        // Without its definition, a test's class is read from its name.
        Assert.Equal(("Opens", false, "The page /orders is not at its address after 15 s."), (tests[1].Class, tests[1].Passed, tests[1].Message));
    }
}
