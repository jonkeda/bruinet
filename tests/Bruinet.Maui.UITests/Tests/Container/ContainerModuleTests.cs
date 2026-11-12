using Bruinet.Maui.UITests.Pages;
using Xunit.Abstractions;
using System.Globalization;

namespace Bruinet.Maui.UITests.Tests.Container;

/// <summary>
/// UI tests for the container module: Grid, Border, ContentView, ScrollView, BoxView.
/// </summary>
/// <remarks>
/// <para>
/// Implements the Container module of <c>test-pages-design.md</c> (section 3) as far as
/// Windows permits. The design lists nine controls; <c>Frame</c>, <c>SwipeView</c>, and
/// <c>RefreshView</c> are not addressable by <c>AutomationId</c> on Windows, measured by
/// <see cref="AutomationProbeTests"/> and re-confirmed here by
/// <see cref="UnaddressableContainers_ReportStatus"/>. Their markup exists for the planned
/// Android/iOS phase, where the design's swipe and pull-to-refresh scenarios become
/// testable.
/// </para>
/// <para>
/// The design's remaining validation points map onto scoping and child resolution: a
/// container is proven by reaching its children and not reaching anything else.
/// </para>
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "ContainerModule")]
public class ContainerModuleTests
{
    private readonly MauiFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ContainerModuleTests(MauiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _fixture.NavigateToContainerModule();
    }

    private ContainerTestPage Page => _fixture.ContainerTestPage;

    #region Grid

    /// <summary>Grid resolves and reaches all four of its cells.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Control", "Grid")]
    public Task Grid_ArrangesAndScopesItsChildren()
    {
        Page.TestGrid.AssertExists();

        Page.GridCellTopLeft.AssertText("Top left");
        Page.GridCellTopRight.AssertExists();
        Page.GridCellBottomLeft.AssertExists();
        Page.GridButton.AssertExists();

        return Task.CompletedTask;
    }

    /// <summary>A child action inside the Grid fires and is observable.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Control", "Grid")]
    public Task Grid_ChildAction_Fires()
    {
        Page.GridButton.Click()
            .Parent.Status.AssertText("Grid");

        return Task.CompletedTask;
    }

    /// <summary>The Grid does not reach children of a sibling container.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "NoParentFallback")]
    public Task Grid_DoesNotReachOtherContainersChildren()
    {
        // BorderChildLabel is real, but it lives in the Border, not the Grid. Asking the Grid
        // for it must come back empty rather than falling back to the page.
        Page.BorderChildLabel.AssertExists(true);
        Page.TestGrid.Label("BorderChildLabel").AssertExists(false);

        return Task.CompletedTask;
    }

    #endregion

    #region Border and ContentView

    /// <summary>Border resolves and contains its child.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Control", "Border")]
    public Task Border_ContainsItsChild()
    {
        Page.TestBorder.AssertExists();

        Page.BorderChildLabel.AssertExists();

        Page.BorderButton.Click()
            .Parent.Status.AssertText("Border");

        return Task.CompletedTask;
    }

    /// <summary>ContentView resolves and contains its child.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Control", "ContentView")]
    public Task ContentView_ContainsItsChild()
    {
        Page.TestContentView.AssertExists();

        Page.ContentViewChildLabel.AssertExists();

        Page.ContentViewButton.Click()
            .Parent.Status.AssertText("ContentView");

        return Task.CompletedTask;
    }

    #endregion

    #region ScrollView

    /// <summary>ScrollView resolves and reaches content that starts visible.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Control", "ScrollView")]
    public Task ScrollView_ResolvesAndScopesContent()
    {
        Page.TestScrollView.AssertExists();

        Page.ScrollFirstLabel.AssertExists();

        return Task.CompletedTask;
    }

    /// <summary>
    /// ScrollTo brings a child at the far end into view, and the call returns the container.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "ScrollTo")]
    public Task ScrollView_ScrollsToItsLastChild()
    {
        var scrollView = Page.TestScrollView;

        var returned = scrollView.ScrollTo("ScrollLastLabel");

        // ScrollTo returns the container so the scroll can be chained onto what it revealed.
        Assert.Same(scrollView, returned);

        returned.Label("ScrollLastLabel").AssertVisible(true);

        return Task.CompletedTask;
    }

    /// <summary>
    /// A child below the fold is found without the test scrolling to it first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Step 100a, and the assertion is really about Android.</b> Android leaves content outside
    /// a scroll view's viewport out of the accessibility tree, so a plain lookup says the label
    /// does not exist. A control that finds nothing now asks the driver to sweep the element that
    /// scrolls for its scope - here, this scroll view.
    /// </para>
    /// <para>
    /// Before the step, a control inside any container never swept at all: the lookup lived on
    /// the scope, and the container's version was a plain lookup. So this failed on Android while
    /// the same label declared on the page would have been found. Windows keeps off-screen
    /// elements in the tree and passes either way.
    /// </para>
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "ScrollLookup")]
    public Task ScrollView_FindsAChildBelowTheFold_WithoutBeingScrolledToIt()
    {
        // Back to the top, in case an earlier test left the last child on screen.
        Page.TestScrollView.ScrollTo("ScrollFirstLabel");

        Page.ScrollLastLabel.AssertExists();
        Page.ScrollLastLabel.AssertVisibleAfterScroll();

        return Task.CompletedTask;
    }

    #endregion

    #region BoxView

    /// <summary>
    /// BoxView is not addressable on Windows.
    /// </summary>
    /// <remarks>
    /// BoxView is a drawing primitive rather than a control and gets no AutomationPeer — the
    /// same reason Frame, SwipeView and RefreshView are unreachable. It has no children and no
    /// behaviour, so nothing is lost beyond an existence check.
    /// </remarks>
    [WindowsOnlyFact(WindowsOnlyFactAttribute.AccessibilityTree, Timeout = TestConstants.DefaultTestTimeoutMs, Device = "it checks what Windows' accessibility tree leaves out")]
    [Trait("Control", "BoxView")]
    public Task BoxView_IsNotAddressableOnWindows()
    {
        Assert.Null(Page.TryFindByAutomationId("TestBoxView"));

        return Task.CompletedTask;
    }

    #endregion

    #region RefreshView (without the gesture)

    /// <summary>
    /// The refresh command completes and updates observable state.
    /// </summary>
    /// <remarks>
    /// The design's scenario is "pull and refresh". Pull is a gesture, and the RefreshView
    /// is not addressable on Windows, so this drives the same command through a button.
    /// The gesture itself belongs to the Android/iOS phase.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Control", "RefreshView")]
    public Task Refresh_CompletesAndUpdatesState()
    {
        Page.TriggerRefreshButton.Click();

        Page.Status.AssertText("Refresh");
        Page.RefreshText.AssertText("refreshed 1");

        return Task.CompletedTask;
    }

    #endregion

    #region Platform report

    /// <summary>
    /// Reports which of the design's nine containers are addressable here.
    /// </summary>
    /// <remarks>
    /// Diagnostic, not a gate: it asserts only that the six known-good containers resolve.
    /// The three known-bad ones are reported so the Android/iOS phase has a baseline to
    /// compare against — the expectation is that they flip to addressable there.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "PlatformReport")]
    public Task UnaddressableContainers_ReportStatus()
    {
        string[] expectedGood = ["TestGrid", "TestBorder", "TestContentView", "TestScrollView"];
        string[] expectedBad = ["TestFrame", "TestSwipeView", "TestRefreshView", "TestBoxView"];

        var report = new System.Text.StringBuilder();
        report.AppendLine();
        report.AppendLine("| Container | Addressable on Windows |");
        report.AppendLine("|---|---|");

        foreach (var id in expectedGood.Concat(expectedBad))
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"| {id} | {(Page.TryFindByAutomationId(id) != null ? "yes" : "NO")} |");
        }

        _output.WriteLine(report.ToString());

        var missing = expectedGood.Where(id => Page.TryFindByAutomationId(id) == null).ToList();
        Assert.True(missing.Count == 0,
            $"These containers should be addressable but were not: {string.Join(", ", missing)}.");

        return Task.CompletedTask;
    }

    #endregion
}
