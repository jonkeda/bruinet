using Bruinet.Maui.UITests.Pages;
using Xunit.Abstractions;
using System.Globalization;

namespace Bruinet.Maui.UITests.Tests.Navigation;

/// <summary>
/// Phase B probe: which navigation surfaces are addressable on Windows.
/// </summary>
/// <remarks>
/// <para>
/// Toolbars and menus are known trouble. MAUI does not reliably propagate
/// <c>AutomationId</c> to Shell and menu chrome — dotnet/maui#3996 — which is why
/// <c>TabbedPageAutomationMapper</c> and <c>ShellAutomationMapper</c> exist in the sample
/// app at all. This measures the ground truth before any test assumes it.
/// </para>
/// <para>
/// Only the in-page surfaces are asserted, because those are the ones the demo view
/// controls directly. Page-level chrome (<c>ToolbarItem</c>, <c>MenuBarItem</c>) is
/// reported, not asserted: a negative there is a finding to record, exactly as
/// <c>SwipeView</c>/<c>RefreshView</c> were in the Phase 0 layout probe.
/// </para>
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "NavigationProbe")]
public class NavigationProbeTests
{
    /// <summary>
    /// In-page surfaces the demo view declares. These back the Phase B tests, so they
    /// must resolve.
    /// </summary>
    private static readonly string[] InPageSubjects =
    [
        "PrimaryToolbar",
        "SecondaryToolbar",
        "ActionsMenu",
        "ActionsMenuTrigger",
        "TabMenuView",
    ];

    /// <summary>
    /// Page-level chrome. Reported only — MAUI may not surface these at all.
    /// </summary>
    private static readonly (string Id, string Name)[] ChromeSubjects =
    [
        ("PageToolbarRefresh", "Refresh"),
        ("PageToolbarAbout", "About"),
        ("PageMenuFile", "File"),
        ("PageMenuFileNew", "New"),
    ];

    private readonly MauiFixture _fixture;
    private readonly ITestOutputHelper _output;

    public NavigationProbeTests(MauiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _fixture.NavigateToNavigationDemo();
    }

    private NavigationDemoPage Page => _fixture.NavigationDemoPage;

    /// <summary>The demo page rendered. Guards every reading below.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task NavigationPage_Renders()
    {
        Page.PageTitle.AssertExists();
        return Task.CompletedTask;
    }

    /// <summary>
    /// The in-page navigation surfaces resolve. These are asserted, not merely reported:
    /// the Phase B tests depend on them.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task InPageSurfaces_AreAddressable()
    {
        var missing = InPageSubjects
            .Where(id => Page.TryFindByAutomationId(id) == null)
            .ToList();

        Assert.True(missing.Count == 0,
            $"These in-page navigation surfaces were not addressable: {string.Join(", ", missing)}. "
            + "They are declared inside AutomationContainer in NavigationDemoView.xaml, so a "
            + "failure here means the automation handlers are not registered or the markup changed.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Measures page-level toolbar and menu chrome and reports a table. Records findings;
    /// does not fail on a negative, because a negative is the answer we came for.
    /// </summary>
    [ProbeFact("reports which page chrome is addressable", Timeout = TestConstants.LongTestTimeoutMs)]
    public Task PageChrome_ReportsAddressability()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine();
        report.AppendLine("| Page chrome | By AutomationId | By Name |");
        report.AppendLine("|---|---|---|");

        foreach (var (id, name) in ChromeSubjects)
        {
            var byId = Page.TryFindByAutomationId(id) != null;
            var byName = Page.TryFindByName(name) != null;
            report.AppendLine(CultureInfo.InvariantCulture, $"| {id} | {(byId ? "yes" : "NO")} | {(byName ? "yes" : "NO")} |");
        }

        _output.WriteLine(report.ToString());
        return Task.CompletedTask;
    }
}
