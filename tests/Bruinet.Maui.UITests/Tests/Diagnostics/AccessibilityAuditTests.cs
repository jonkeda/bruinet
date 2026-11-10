using Bruinet.Maui.Testing;
using Bruinet.Core.Artifacts;
using Bruinet.Maui.FlaUI;
using Bruinet.Maui.FlaUI.Bridge;
using Bruinet.Maui.UITests.Pages;
using Xunit;
using Xunit.Abstractions;

namespace Bruinet.Maui.UITests.Tests.Diagnostics;

/// <summary>
/// Step 29: what each instrumented element offers somebody who is not using a pointer.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the output that makes the programme an accessibility improvement rather than a way
/// to test around a defect.</b> The bridge does not fix a gesture-only control - it lets Bruinet
/// drive one, and a custom pattern only Bruinet knows about is worth nothing to a person on a
/// switch device. Instrumenting an element should be the moment somebody asks whether it is
/// gesture-<i>only</i>. This is the list that asks, written to
/// <c>TestResults/&lt;run-id&gt;/suites/&lt;suite&gt;/attachments</c> per <c>AD-007</c>.
/// </para>
/// <para>
/// <b>What it asserts, and what it deliberately does not.</b> The backlog is not asserted empty
/// and never will be: the sample app contains gesture-only controls on purpose, because they are
/// what the bridge exists to reach. Asserting emptiness would fail forever and teach everyone to
/// ignore it. What is asserted is that the audit still tells the four cases apart - a control
/// nothing but a pointer reaches, one no accessibility tree contains, one with a keyboard route,
/// and an element that only answers reads. Those are the classifications the report's value rests
/// on, and each is pinned to a named element that would notice if MAUI's tree changed underneath
/// it.
/// </para>
/// <para>
/// <b>Pages, because publication follows the page.</b> Elements publish on load and withdraw on
/// unload, so the audit sees one page at a time. This walks the pages that declare verbs and
/// merges. Its coverage is therefore that list and not the whole app, which is worth knowing when
/// reading the artifact: a short backlog can mean a healthy app or a short walk.
/// </para>
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "Diagnostics")]
[Trait("Stage", "Background")]
public class AccessibilityAuditTests
{
    /// <summary>
    /// The pages that declare verbs.
    /// </summary>
    /// <remarks>
    /// Listed rather than derived: nothing on this side can read the app's markup, and walking
    /// every page would double the navigation for pages that publish nothing. The one way this
    /// list can be wrong is a declaration added to a page not named here, which silently narrows
    /// the audit - so the report says which pages it walked, and a reader who sees a page missing
    /// knows what to add.
    /// </remarks>
    private static readonly SamplePage[] PagesThatDeclareVerbs =
    [
        SamplePage.Gestures,
        SamplePage.Container,
        SamplePage.Text,
        SamplePage.Display,
        SamplePage.Selection,
        SamplePage.DateTime,
        SamplePage.Scroll,
        SamplePage.GridCollection,
        SamplePage.Navigation,
        SamplePage.AutomationProbe,
    ];

    private const int PublishSettleMs = 3_000;
    private const int PublishPollMs = 100;

    private readonly MauiFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AccessibilityAuditTests(MauiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Walks the app, writes the backlog, and checks the audit still tells the cases apart.
    /// </summary>
    [DeviceFact("it drives the Windows driver (FlaUI) itself", Timeout = TestConstants.LongTestTimeoutMs)]
    public Task Audit_ReportsWhatEachInstrumentedElementOffersWithoutAPointer()
    {
        var driver = (FlaUIMauiDriver)_fixture.Context.Driver;
        var report = Walk(driver);

        var path = Write(report);
        _output.WriteLine($"Written to {path}");
        _output.WriteLine(report.ToMarkdown());

        Assert.True(
            report.Findings.Count > 0,
            "The audit found no instrumented elements at all, so it is measuring nothing. Either "
            + "the app published no bridge or the walk opened no page that declares verbs.");

        // Every verdict is a claim about a real control. Naming one of each keeps the audit
        // honest: a classifier that answered the same thing everywhere would still produce a
        // plausible-looking report.
        AssertVerdict(report, "TestSwipeView", AccessibilityVerdict.Unreachable);
        AssertVerdict(report, "GestureSwipeTarget", AccessibilityVerdict.PointerOnly);
        AssertVerdict(report, "TestEntry", AccessibilityVerdict.HasAnotherRoute);
        AssertVerdict(report, "TestImage", AccessibilityVerdict.NotAnAffordance);
        AssertVerdict(report, "TextTestPage", AccessibilityVerdict.AddressedToTheApp);


        return Task.CompletedTask;
    }

    /// <summary>
    /// An element that only answers reads is never called an accessibility defect.
    /// </summary>
    /// <remarks>
    /// <b>The distinction the whole report rests on.</b> The sample declares <c>GetState</c> on
    /// labels, images and progress bars; a report that called those defects would be almost
    /// entirely noise, and a backlog that is mostly noise is one nobody reads. Asserted here as
    /// well as in the contract's own unit test because this is where getting it wrong would
    /// actually cost something.
    /// </remarks>
    [DeviceFact("it drives the Windows driver (FlaUI) itself", Timeout = TestConstants.LongTestTimeoutMs)]
    public Task Audit_LeavesReadOnlyElementsOffTheBacklog()
    {
        var driver = (FlaUIMauiDriver)_fixture.Context.Driver;
        _fixture.Open(SamplePage.Display);

        var report = driver.AuditGestureAccessibility(nameof(SamplePage.Display));

        Assert.True(
            report.Findings.Count > 0,
            "Nothing was published on the Display page, so this asserts nothing.");

        var wrongly = report.Backlog
            .Where(finding => finding.ActionVerbs.Count == 0)
            .Select(finding => finding.AutomationId)
            .ToArray();

        Assert.True(
            wrongly.Length == 0,
            "These elements declare no user action and were still put on the accessibility "
            + $"backlog: {string.Join(", ", wrongly)}. Every verb they declare is a read, so "
            + "there is nothing a person does to them and nothing to make reachable.");

        return Task.CompletedTask;
    }

    /// <summary>Runs the audit on each page in turn and merges the passes.</summary>
    private AccessibilityAuditReport Walk(FlaUIMauiDriver driver)
    {
        var report = AuditWhenSettled(driver, "Hub");

        foreach (var page in PagesThatDeclareVerbs)
        {
            _fixture.Open(page);
            report.Merge(AuditWhenSettled(driver, page.ToString()));
        }

        return report;
    }

    /// <summary>
    /// Audits once the page has finished publishing itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Publication is asynchronous and opening a page does not wait for it.</b> Elements
    /// publish on <c>Loaded</c>, one at a time, so an audit taken the instant after a click sees
    /// however much of the page had got there. The first run of this test audited the Gestures
    /// page - six declarations, the most in the app - and found nothing at all, while the
    /// quicker pages after it looked fine. A silently short audit is the worst failure this
    /// report has, because a missing element reads as an element with nothing wrong.
    /// </para>
    /// <para>
    /// <b>Stable rather than non-empty.</b> Waiting for the first finding would still catch a
    /// page half-published; waiting for the count to stop moving catches the whole page, and
    /// costs one extra poll on a page that was ready immediately.
    /// </para>
    /// </remarks>
    private static AccessibilityAuditReport AuditWhenSettled(FlaUIMauiDriver driver, string page)
    {
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        var report = driver.AuditGestureAccessibility(page);

        while (deadline.ElapsedMilliseconds < PublishSettleMs)
        {
            Thread.Sleep(PublishPollMs);

            var again = driver.AuditGestureAccessibility(page);

            if (again.Findings.Count == report.Findings.Count && report.Findings.Count > 0)
            {
                return again;
            }

            report = again;
        }

        return report;
    }

    /// <summary>
    /// Writes the report where every other artifact goes.
    /// </summary>
    /// <remarks>
    /// <c>AD-007</c>: <c>TestResults/&lt;run-id&gt;/suites/&lt;suite&gt;/</c>. Attachments rather
    /// than logs, because this is a product of the run that somebody reads, not a trace of it.
    /// </remarks>
    private static string Write(AccessibilityAuditReport report)
    {
        var paths = DefaultTestArtifactPathProvider.Create();
        paths.EnsureDirectories();

        var path = Path.Combine(paths.AttachmentsDirectory, "accessibility-audit.md");
        File.WriteAllText(path, report.ToMarkdown());

        return path;
    }

    private void AssertVerdict(
        AccessibilityAuditReport report, string automationId, AccessibilityVerdict expected)
    {
        var finding = report.Findings.FirstOrDefault(f => f.AutomationId == automationId);

        Assert.True(
            finding is not null,
            $"'{automationId}' was not published on the bridge during the walk, so the audit "
            + "cannot classify it. Either the declaration was removed from the app's markup or "
            + "its page is missing from PagesThatDeclareVerbs.");

        Assert.True(
            finding!.Verdict == expected,
            $"'{automationId}' audited as {finding.Verdict}, expected {expected}. "
            + $"In tree: {finding.InTree}; keyboard focusable: {finding.KeyboardFocusable}; "
            + $"invoke: {finding.SupportsInvoke}; declares: {string.Join(", ", finding.Verbs)}.");
    }
}
