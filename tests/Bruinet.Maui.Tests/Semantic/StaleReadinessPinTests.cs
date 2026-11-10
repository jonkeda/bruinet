using Bruinet.Core.Collections;
using System.Diagnostics;
using Bruinet.Core.Logging;
using Bruinet.Maui.Containers;
using Bruinet.Maui.Context;
using Bruinet.Maui.Controls.Base;
using Bruinet.Maui.Controls.Dialogs;
using Bruinet.Maui.Pages;

namespace Bruinet.Maui.Tests.Semantic;

/// <summary>
/// Pins the behaviour the stale-readiness work sets out to fix, before it is fixed.
/// </summary>
/// <remarks>
/// <para>
/// Written before the stale-readiness work. Each test states the behaviour
/// the design requires and fails on the code as it was then, for the reason given in its summary.
/// The <c>Pin</c> trait names the step expected to make it pass, so each later step can run its
/// own: <c>dotnet test --filter "Pin=step4"</c>.
/// </para>
/// <para>
/// Tests with <c>Pin=guard</c> are the R0 guards ("app bugs stay failures"): they pass before the
/// change and must keep passing after it. A later step that makes one fail has turned an app bug
/// into a pass.
/// </para>
/// <para>
/// Assertions are on behaviour (timings, call counts, log entries, messages), not on types the
/// later steps introduce, so the file compiles against the code it pins.
/// </para>
/// </remarks>
public class StaleReadinessPinTests : SemanticControlTestsBase
{
    private readonly Mock<ITestLogger> _logger = new();
    private readonly Mock<IMauiElement> _app = new();
    private readonly List<IMauiElement> _rows = [];
    private readonly Mock<IMauiElement> _listRoot;

    public StaleReadinessPinTests()
    {
        Context.Setup(c => c.Timeouts).Returns(new TimeoutSettings
        {
            DefaultWait = TimeSpan.FromMilliseconds(1000),
            PageLoad = TimeSpan.FromMilliseconds(1000),
            ElementFind = TimeSpan.FromMilliseconds(1000),
            PollingInterval = TimeSpan.FromMilliseconds(1),
            Animation = TimeSpan.FromMilliseconds(300)
        });
        Context.Setup(c => c.Logger).Returns(_logger.Object);
        Context.Setup(c => c.AppElement).Returns(_app.Object);

        _listRoot = CreateElement("Rows", 0, 0, 300, 400);
        Context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Rows"))).Returns(_listRoot.Object);
        Context.Setup(c => c.FindElement(It.Is<Locator>(l => l.Value == "Rows"))).Returns(_listRoot.Object);
        _listRoot.Setup(e => e.FindElements(It.Is<Locator>(l => l.Value == "ListItem")))
            .Returns(() => _rows.ToArray());
    }

    #region Pages and objects under test

    private sealed class PinPage(IMauiTestContext context) : PageObjectBase<PinPage>(context)
    {
        public override string Name => "TestPage";

        public override bool IsLoaded() => true;

        public Button<PinPage> Target => new(this, "Target");

        public Label<PinPage> Status => new(this, "Target");

        public CheckBox<PinPage> Toggle => new(this, "Target");

        public ToolbarButton<PinPage> Toolbar => new(this, "Target");

        public PinRows Rows => new(this, "Rows");

        public PinClickRows ClickRows => new(this, "Rows");

        public PinGroupedRows GroupedRows => new(this, "Rows");

        public SlowContainer Slow => new(this, "Slow");
    }

    private sealed class BusyPage(IMauiTestContext context) : PageObjectBase<BusyPage>(context)
    {
        public override string Name => "TestPage";

        protected override BusySignalPolicy BusySignalPolicy => BusySignalPolicy.Required;

        public ContentDialog<BusyPage> Dialog => new(this);
    }

    private sealed class PinRows(IMauiScope<PinPage> scope, string automationId)
        : CollectionObjectBase<PinPage, PinRows, PinRow>(
            scope,
            automationId,
            ItemStrategy.ByLocator(Locator.ByControlType("ListItem")),
            (collection, itemRoot, index) => new PinRow(collection, itemRoot, index));

    private sealed class PinRow(PinRows collection, IMauiElement itemRoot, int index)
        : ItemObjectBase<PinRows, PinRow>(collection, itemRoot, index)
    {
        public Label<PinRow> Name => new(this, "RowName");
    }

    /// <summary>A grouped list: the platform restarts <c>PositionInSet</c> in each group.</summary>
    private sealed class PinGroupedRows(IMauiScope<PinPage> scope, string automationId)
        : CollectionObjectBase<PinPage, PinGroupedRows, PinGroupedRow>(
            scope,
            automationId,
            ItemStrategy.ByLocator(Locator.ByControlType("ListItem")),
            (collection, itemRoot, index) => new PinGroupedRow(collection, itemRoot, index))
    {
        protected override bool HasLogicalIndexes => false;
    }

    private sealed class PinGroupedRow(PinGroupedRows collection, IMauiElement itemRoot, int index)
        : ItemObjectBase<PinGroupedRows, PinGroupedRow>(collection, itemRoot, index)
    {
        public Label<PinGroupedRow> Name => new(this, "RowName");
    }

    /// <summary>Rows with members of their own (clicked, read), not only children.</summary>
    private sealed class PinClickRows(IMauiScope<PinPage> scope, string automationId)
        : CollectionObjectBase<PinPage, PinClickRows, PinClickRow>(
            scope,
            automationId,
            ItemStrategy.ByLocator(Locator.ByControlType("ListItem")),
            (collection, itemRoot, index) => new PinClickRow(collection, itemRoot, index));

    private sealed class PinClickRow(PinClickRows collection, IMauiElement itemRoot, int index)
        : ClickableItemBase<PinClickRows, PinClickRow>(collection, itemRoot, index);

    /// <summary>A container whose content becomes ready only after a few checks.</summary>
    private sealed class SlowContainer(IMauiScope<PinPage> parentScope, string locatorValue)
        : ContainerObjectBase<PinPage, SlowContainer>(parentScope, locatorValue)
    {
        public int ContentChecks { get; private set; }

        public Label<SlowContainer> Status => new(this, "SlowStatus");

        protected override ScopeReadiness ProbeContentReadiness(IMauiElement root)
            => ++ContentChecks >= 3 ? ContentReady() : ContentNotReady("still loading");
    }

    private PinPage NewPage() => new(Context.Object);

    /// <summary>Makes the page root answer <c>Target</c> with whatever <paramref name="next"/> gives.</summary>
    private void GivenTarget(Func<IMauiElement?> next)
        => Context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Target"))).Returns(next);

    private static Mock<IMauiElement> Element(bool visible = true, bool enabled = true, string? text = null)
    {
        var element = CreateElement("Target", 0, 0, 40, 20);
        element.Setup(e => e.Visible).Returns(visible);
        element.Setup(e => e.Enabled).Returns(enabled);
        element.Setup(e => e.Text).Returns(text);
        return element;
    }

    private static Mock<IMauiElement> Row(string automationId, string name)
    {
        var row = CreateElement(automationId, 0, 0, 300, 40);
        row.Setup(e => e.AutomationId).Returns(automationId);
        row.Setup(e => e.TagName).Returns("ListItem");
        var label = CreateElement("RowName", 0, 0, 100, 20);
        label.Setup(e => e.Text).Returns(name);
        row.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "RowName"))).Returns(label.Object);
        return row;
    }

    private static long Timed(Action act)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            act();
        }
        catch
        {
            // Timing only; the caller asserts on the outcome separately.
        }

        return stopwatch.ElapsedMilliseconds;
    }

    #endregion

    #region Original plan (a)-(e): one budget, found again on every attempt, one log unit

    /// <summary>
    /// (a) An element that is being replaced must not use up the call: the next attempt finds the
    /// new one. Fails today: the first element's nested visibility wait takes the whole budget.
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void A_Click_FindsTheElementAgain_WhenTheFirstOneNeverBecomesVisible()
    {
        var dead = Element(visible: false);
        var good = Element();
        var lookups = 0;
        GivenTarget(() => ++lookups == 1 ? dead.Object : good.Object);

        NewPage().Target.Click(timeoutMs: 1000);

        good.Verify(e => e.Invoke(), Times.Once);
        dead.Verify(e => e.Invoke(), Times.Never);
    }

    /// <summary>
    /// (b) The caller's timeout is the call's budget. Fails today: the nested visibility wait uses
    /// DefaultWait (1000 ms here) whatever the caller asked for.
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void B_Click_HonoursTheCallersTimeout_WhenTheElementNeverBecomesVisible()
    {
        var hidden = Element(visible: false);
        GivenTarget(() => hidden.Object);

        Exception? error = null;
        var elapsed = Timed(() => error = Record.Exception(() => NewPage().Target.Click(timeoutMs: 300)));

        Assert.NotNull(error);
        Assert.True(elapsed < 800, $"Click(timeoutMs: 300) took {elapsed} ms.");
    }

    /// <summary>
    /// (c) One public call is one log entry/exit pair. Fails today: the nested visibility wait
    /// logs a pair of its own.
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void C_Click_WritesOneLogEntryAndExitPair()
    {
        var reads = 0;
        var element = Element();
        element.Setup(e => e.Visible).Returns(() => ++reads > 1);
        GivenTarget(() => element.Object);

        NewPage().Target.Click(timeoutMs: 1000);

        _logger.Verify(l => l.Write(It.Is<LogEvent>(e => e.Kind == LogEventKind.Entry)), Times.Once);
        _logger.Verify(l => l.Write(It.Is<LogEvent>(e => e.Kind == LogEventKind.Exit)), Times.Once);
    }

    /// <summary>(d) As (a), for <c>Get*</c>.</summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void D_GetText_FindsTheElementAgain_WhenTheFirstOneNeverBecomesVisible()
    {
        var dead = Element(visible: false, text: "dead");
        var good = Element(text: "good");
        var lookups = 0;
        GivenTarget(() => ++lookups == 1 ? dead.Object : good.Object);

        Assert.Equal("good", NewPage().Status.GetText(timeoutMs: 1000));
    }

    /// <summary>(d) As (a), for <c>Wait*</c>.</summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void D_WaitText_FindsTheElementAgain_WhenTheFirstOneNeverBecomesVisible()
    {
        var dead = Element(visible: false, text: "dead");
        var good = Element(text: "good");
        var lookups = 0;
        GivenTarget(() => ++lookups == 1 ? dead.Object : good.Object);

        Assert.True(NewPage().Status.WaitText("good", timeoutMs: 1000));
    }

    /// <summary>
    /// (d) As (a), for <c>Assert*</c>. Fails today: the assert keeps the first element it found and
    /// re-reads it until the budget runs out.
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void D_AssertText_FindsTheElementAgain_WhenTheFirstOneNeverBecomesVisible()
    {
        var dead = Element(visible: false, text: "dead");
        var good = Element(text: "good");
        var lookups = 0;
        GivenTarget(() => ++lookups == 1 ? dead.Object : good.Object);

        NewPage().Status.AssertText("good", timeoutMs: 1000);
    }

    /// <summary>
    /// (e) An action that throws runs once, and its failure is in the call's log entry. Fails
    /// today: the log pair closes before the action runs, and says Success.
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void E_Click_WhenTheActionThrows_InvokesOnceAndLogsTheError()
    {
        var element = Element();
        element.Setup(e => e.Invoke()).Throws(new InvalidOperationException("boom"));
        GivenTarget(() => element.Object);

        Assert.Throws<InvalidOperationException>(() => NewPage().Target.Click(timeoutMs: 1000));

        element.Verify(e => e.Invoke(), Times.Once);
        _logger.Verify(l => l.Write(It.Is<LogEvent>(e => e.Kind == LogEventKind.Exit && e.Result == LogResult.Error && (e.Message != null && e.Message.Contains("boom")))),
            Times.Once);
    }

    #endregion

    #region Trace (f)-(h): lookups make one attempt

    /// <summary>
    /// (f) A control on AppRoot honours the caller's budget when it is missing. Fails today:
    /// MauiTestContext.FindElement waits ElementFind (3000 ms) inside every poll tick.
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void F_AppRootControl_HonoursTheCallersTimeout_WhenMissing()
    {
        var driver = new Mock<IMauiDriver>();
        driver.Setup(d => d.FindElements(It.IsAny<Locator>())).Returns([]);
        driver.Setup(d => d.TryFindElement(It.IsAny<Locator>())).Returns((IMauiElement?)null);
        driver.Setup(d => d.AppElement).Returns(_app.Object);
        using var context = new MauiTestContext(new MauiTestContextOptions
        {
            Driver = driver.Object,
            Timeouts = new TimeoutSettings { DefaultWait = TimeSpan.FromMilliseconds(5000), ElementFind = TimeSpan.FromMilliseconds(3000), PollingInterval = TimeSpan.FromMilliseconds(10) }
        });
        var button = new Button<AppRoot>(new AppRoot(context), "Missing");

        var elapsed = Timed(() => button.Click(timeoutMs: 300));

        Assert.True(elapsed < 1500, $"Click(timeoutMs: 300) on a missing AppRoot control took {elapsed} ms.");
    }

    /// <summary>
    /// (g) A lookup that fails with an error is not "absent". Fails today: MauiTestContext's
    /// TryFindElement turns every exception into null, so WaitExists(false) passes on a broken
    /// driver.
    /// </summary>
    [Fact]
    [Trait("Pin", "step3")]
    public void G_WaitExistsFalse_DoesNotPass_WhenTheLookupThrows()
    {
        var driver = new Mock<IMauiDriver>();
        driver.Setup(d => d.FindElements(It.IsAny<Locator>())).Throws(new InvalidOperationException("driver broke"));
        driver.Setup(d => d.TryFindElement(It.IsAny<Locator>())).Throws(new InvalidOperationException("driver broke"));
        driver.Setup(d => d.AppElement).Returns(_app.Object);
        using var context = new MauiTestContext(new MauiTestContextOptions
        {
            Driver = driver.Object,
            Timeouts = new TimeoutSettings { DefaultWait = TimeSpan.FromMilliseconds(300), PollingInterval = TimeSpan.FromMilliseconds(10) }
        });
        var button = new Button<AppRoot>(new AppRoot(context), "Anything");

        var passed = false;
        var error = Record.Exception(() => passed = button.WaitExists(false, timeoutMs: 300));

        Assert.False(passed && error == null, "WaitExists(false) passed although every lookup threw.");
    }

    /// <summary>
    /// (h) A missing control sweeps the scroller at most once per Animation interval. Fails
    /// today: it sweeps on every poll tick.
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void H_MissingControl_SweepsAtMostOncePerAnimationInterval()
    {
        GivenTarget(() => null);
        var sweeps = 0;
        _app.Setup(a => a.TryFindByScrolling(It.IsAny<Locator>())).Returns(() =>
        {
            sweeps++;
            return null;
        });

        _ = Timed(() => NewPage().Status.GetText(timeoutMs: 400));

        Assert.True(sweeps <= 3, $"{sweeps} sweeps in 400 ms; at most one per 300 ms expected.");
    }

    #endregion

    #region Scopes and collections

    /// <summary>
    /// (A3) A dialog shown over a busy page can be answered. Fails today: the dialog's button
    /// waits for the page underneath to stop being busy, which it cannot while the dialog is open.
    /// </summary>
    [Fact]
    [Trait("Pin", "step5")]
    public void A3_DialogButton_IsUsable_WhileThePageUnderneathIsBusy()
    {
        var busy = CreateElement("Busy", 0, 0, 1, 1);
        busy.Setup(e => e.Text).Returns("True");
        Context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Busy"))).Returns(busy.Object);
        var dialogRoot = CreateElement("ContentDialog", 0, 0, 300, 200);
        var delete = CreateInvokableElement("DialogDelete", 10, 150, 80, 40);
        dialogRoot.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "Delete"))).Returns(delete.Object);
        _app.Setup(a => a.TryFindActiveDialog()).Returns(dialogRoot.Object);

        new BusyPage(Context.Object).Dialog.DialogButton("Delete").Click(timeoutMs: 500);

        delete.Verify(e => e.Invoke(), Times.Once);
    }

    /// <summary>
    /// (A1) A container's own readiness is on its children's path. Fails today: a child's call
    /// never asks the container, only the page.
    /// </summary>
    [Fact]
    [Trait("Pin", "step5")]
    public void A1_ChildCall_WaitsForItsContainersContentReadiness()
    {
        var slowRoot = CreateElement("Slow", 0, 0, 300, 300);
        Context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Slow"))).Returns(slowRoot.Object);
        Context.Setup(c => c.FindElement(It.Is<Locator>(l => l.Value == "Slow"))).Returns(slowRoot.Object);
        var status = CreateElement("SlowStatus", 0, 0, 100, 20);
        status.Setup(e => e.Text).Returns("ready");
        slowRoot.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "SlowStatus"))).Returns(status.Object);
        var container = NewPage().Slow;

        Assert.Equal("ready", container.Status.GetText(timeoutMs: 1000));
        Assert.True(container.ContentChecks >= 3, $"The container's readiness was checked {container.ContentChecks} times.");
    }

    /// <summary>
    /// (B3) A row never acts on another item. Fails today: a row whose root died is re-found by
    /// index, and the index now holds a different item, whose text is returned as the row's.
    /// </summary>
    [Fact]
    [Trait("Pin", "step7")]
    public void B3_Row_DoesNotAnswerForAnotherItem_AfterItsItemIsRemoved()
    {
        var a = Row("Row_A", "A");
        var b = Row("Row_B", "B");
        var c = Row("Row_C", "C");
        _rows.AddRange([a.Object, b.Object]);
        var row = NewPage().Rows.Item(1);

        // B is deleted: its element collapses, and C takes index 1.
        b.Setup(e => e.Rect).Returns(System.Drawing.Rectangle.Empty);
        _rows.Clear();
        _rows.AddRange([a.Object, c.Object]);

        var error = Record.Exception(() => row.Name.GetText(timeoutMs: 300));

        Assert.NotNull(error);
    }

    /// <summary>
    /// (Q7) A row whose element the list recycled for another item finds its own item again by
    /// its logical index, and reads that item, not the one now in its old element.
    /// </summary>
    [Fact]
    [Trait("Pin", "step7")]
    public void Row_FindsItsItemAgain_WhenItsElementIsRecycled()
    {
        var first = Row("Row", "Item 0");
        var second = Row("Row", "Item 1");
        first.Setup(e => e.PositionInSet).Returns(1);
        var secondPosition = 2;
        second.Setup(e => e.PositionInSet).Returns(() => secondPosition);
        _rows.AddRange([first.Object, second.Object]);
        var row = NewPage().Rows.Item(1);
        Assert.Equal(ItemKey.Logical(1), row.Key);

        // The list scrolls: the second element now shows item 7, and item 1 moves to a new element.
        secondPosition = 8;
        var moved = Row("Row", "Item 1 (moved)");
        moved.Setup(e => e.PositionInSet).Returns(2);
        _rows.Add(moved.Object);

        Assert.Equal("Item 1 (moved)", row.Name.GetText(timeoutMs: 500));
    }

    /// <summary>
    /// (Q7, review) A row's own member - not only a control inside it - reads its own item after
    /// the list recycled the element it had already used for another item.
    /// </summary>
    [Fact]
    [Trait("Pin", "review")]
    public void Row_OwnRead_FindsItsItemAgain_WhenItsElementIsRecycled()
    {
        var (row, second, _) = GivenRecycledClickRow();

        Assert.Equal("Item 1 (moved)", row.GetText(timeoutMs: 500));
        Assert.Equal("Item 7", second.Object.Text);
    }

    /// <summary>
    /// (Q7, R0, review) A row's own action never lands on the item now shown in its old element.
    /// </summary>
    [Fact]
    [Trait("Pin", "review")]
    public void Row_OwnClick_ActsOnItsOwnItem_WhenItsElementIsRecycled()
    {
        var (row, second, moved) = GivenRecycledClickRow();

        row.Click(timeoutMs: 500);

        moved.Verify(e => e.Invoke(), Times.Once);
        second.Verify(e => e.Invoke(), Times.Never);
    }

    /// <summary>
    /// A clickable row that has used its element once, after which the list recycles that element
    /// for item 7 and shows item 1 in a new element.
    /// </summary>
    private (PinClickRow Row, Mock<IMauiElement> Recycled, Mock<IMauiElement> Moved) GivenRecycledClickRow()
    {
        var first = ClickRow("Item 0", () => 1);
        var secondPosition = 2;
        var secondText = "Item 1";
        var second = ClickRow("Item 1", () => secondPosition);
        second.Setup(e => e.Text).Returns(() => secondText);
        _rows.AddRange([first.Object, second.Object]);

        var row = NewPage().ClickRows.Item(1);
        Assert.Equal("Item 1", row.GetText(timeoutMs: 500));

        secondPosition = 8;
        secondText = "Item 7";
        var moved = ClickRow("Item 1 (moved)", () => 2);
        _rows.Add(moved.Object);

        return (row, second, moved);
    }

    private static Mock<IMauiElement> ClickRow(string text, Func<int> positionInSet)
    {
        var row = Row("Row", text);
        row.Setup(e => e.Text).Returns(text);
        row.Setup(e => e.PositionInSet).Returns(positionInSet);
        return row;
    }

    /// <summary>
    /// A grouped list repeats <c>PositionInSet</c> per group, so its rows are found by position and
    /// a search reaches the second group.
    /// </summary>
    [Fact]
    [Trait("Pin", "grouped")]
    public void GroupedRows_IgnorePerGroupPositions()
    {
        var first = ClickRow("Item 1", () => 1);
        var second = ClickRow("Item 9", () => 1);
        _rows.AddRange([first.Object, second.Object]);
        var rows = NewPage().GroupedRows;

        Assert.Equal(ItemKey.Position(1), rows.Item(1).Key);
        Assert.Equal(1, rows.ItemWhere(row => row.Name.GetText() == "Item 9", timeoutMs: 500).Index);
    }

    /// <summary>
    /// (R0, review) <c>SelectItem</c> activates once: a failure after the activation - here the
    /// row turning stale - ends the call instead of activating again.
    /// </summary>
    [Fact]
    [Trait("Pin", "review")]
    public void SelectItem_FailureAfterActivating_IsNotRepeated()
    {
        var row = Row("Row_A", "A");
        row.Setup(e => e.Select()).Throws(new StaleElementException());
        _rows.Add(row.Object);

        Assert.ThrowsAny<Exception>(() => NewPage().Rows.SelectItem(0, timeoutMs: 500));

        row.Verify(e => e.Select(), Times.Once);
    }

    /// <summary>
    /// (Step 8, kept) <c>SelectItem</c> asks again while the platform answers that it activated
    /// nothing (a row without a size yet), and succeeds once it does.
    /// </summary>
    [Fact]
    [Trait("Pin", "review")]
    public void SelectItem_NothingActivatedYet_IsAskedAgainWithinTheBudget()
    {
        var reads = 0;
        var row = Row("Row_A", "A");
        row.Setup(e => e.Rect).Returns(() => ++reads < 6
            ? System.Drawing.Rectangle.Empty
            : new System.Drawing.Rectangle(0, 0, 300, 40));
        _rows.Add(row.Object);

        NewPage().Rows.SelectItem(0, timeoutMs: 1000);

        row.Verify(e => e.Select(), Times.Once);
    }

    /// <summary>
    /// (D1) ScrollToItem honours the caller's budget. Fails today: each scroll step that jumps
    /// waits up to DefaultWait for the list to settle, whatever the caller asked for.
    /// </summary>
    [Fact]
    [Trait("Pin", "step7")]
    public void D1_ScrollToItem_HonoursTheCallersTimeout()
    {
        _rows.AddRange([Row("Row_A", "A").Object, Row("Row_B", "B").Object]);
        _listRoot.Setup(e => e.ScrollTowards(It.IsAny<int>())).Returns(ScrollStep.Jumped);

        var elapsed = Timed(() => NewPage().Rows.ScrollToItem(5, timeoutMs: 300));

        Assert.True(elapsed < 800, $"ScrollToItem(5, timeoutMs: 300) took {elapsed} ms.");
    }

    /// <summary>
    /// (D23) A scroll to the end steps on without asking a last row already on screen to scroll
    /// itself into view: that request moved nothing and cost iOS a whole attempt per step.
    /// </summary>
    [Fact]
    [Trait("Pin", "review")]
    public void ScrollToEnd_StepsOn_WithoutPullingALastRowAlreadyOnScreen()
    {
        var rows = new List<Mock<IMauiElement>> { Row("Row_0", "0") };
        _rows.Add(rows[0].Object);
        _listRoot.Setup(e => e.ScrollTowards(It.IsAny<int>())).Returns(() =>
        {
            if (rows.Count == 3)
            {
                return ScrollStep.NotMoved;
            }

            var row = Row($"Row_{rows.Count}", rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            rows.Add(row);
            _rows.Add(row.Object);
            return ScrollStep.Unconfirmed;
        });

        NewPage().Rows.ScrollToEnd(timeoutMs: 5000);

        _listRoot.Verify(e => e.ScrollTowards(It.IsAny<int>()), Times.AtLeast(3));
        Assert.All(rows, row => row.Verify(e => e.ScrollIntoView(It.IsAny<int>()), Times.Never));
    }

    /// <summary>
    /// (D6) Item(index) waits like Item(key). Fails today: it answers "no item" at once, while a
    /// row that appears a moment later is exactly what a test is waiting for.
    /// </summary>
    [Fact]
    [Trait("Pin", "step7")]
    public void D6_ItemByIndex_WaitsForTheRowToAppear()
    {
        var reads = 0;
        var a = Row("Row_A", "A");
        _listRoot.Setup(e => e.FindElements(It.Is<Locator>(l => l.Value == "ListItem")))
            .Returns(() => ++reads < 4 ? [] : [a.Object]);

        var item = NewPage().Rows.Item(0);

        Assert.Equal("A", item.Name.GetText());
    }

    /// <summary>
    /// (R2, R3, review) A scroll below a call gets what is left of the call's budget, never a
    /// default of its own.
    /// </summary>
    [Fact]
    [Trait("Pin", "review")]
    public void ScrollIntoView_IsGivenTheCallsRemainingBudget()
    {
        var element = Element();
        GivenTarget(() => element.Object);
        var given = new List<int>();
        element.Setup(e => e.ScrollIntoView(It.IsAny<int>())).Callback<int>(given.Add);

        NewPage().Target.ScrollIntoView();

        var budget = Assert.Single(given);
        Assert.InRange(budget, 1, 1000);
    }

    /// <summary>(R2, R3, review) A row scrolled into view by a collection call gets the call's budget.</summary>
    [Fact]
    [Trait("Pin", "review")]
    public void ScrollToTop_ScrollsTheFirstRowWithinTheCallsBudget()
    {
        var row = Row("Row_A", "A");
        var given = new List<int>();
        row.Setup(e => e.ScrollIntoView(It.IsAny<int>())).Callback<int>(given.Add);
        _rows.Add(row.Object);

        NewPage().Rows.ScrollToTop(timeoutMs: 300);

        Assert.NotEmpty(given);
        Assert.All(given, budget => Assert.InRange(budget, 0, 300));
    }

    /// <summary>
    /// (Design 2.1, review) An unexpected exception on every attempt fails the call naming its type
    /// and how many attempts raised it, with the exception itself inside.
    /// </summary>
    [Fact]
    [Trait("Pin", "review")]
    public void UnexpectedException_OnEveryAttempt_IsNamedWithItsCount()
    {
        var element = Element();
        element.Setup(e => e.Text).Throws(new InvalidOperationException("the platform refused"));
        GivenTarget(() => element.Object);

        var error = Assert.Throws<WaitTimeoutException>(() => NewPage().Status.GetText(timeoutMs: 200));

        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Matches(@"\d+ of \d+ attempts raised InvalidOperationException", error.Message);
        Assert.Contains("the platform refused", error.Message);
    }

    /// <summary>(X5, review) The near-miss thresholds come from the context's settings.</summary>
    [Fact]
    [Trait("Pin", "review")]
    public void NearMiss_ThresholdsComeFromTheContext()
    {
        Context.Setup(c => c.NearMiss).Returns(new NearMissSettings(Replacements: 1, BudgetShare: 1.0));
        var reads = 0;
        var first = Element(text: "first");
        first.Setup(e => e.InstanceKey).Returns("1");
        first.Setup(e => e.Visible).Returns(false);
        var second = Element(text: "second");
        second.Setup(e => e.InstanceKey).Returns("2");
        GivenTarget(() => ++reads == 1 ? first.Object : second.Object);

        Assert.Equal("second", NewPage().Status.GetText(timeoutMs: 1000));

        _logger.Verify(l => l.Write(It.Is<LogEvent>(e => e.Kind == LogEventKind.Exit && e.Result == LogResult.Warning && (e.Message!.StartsWith("near-miss")))),
            Times.Once);
    }

    #endregion

    #region Second review

    /// <summary>
    /// (S1, review 2) An assert finds its element again on every attempt, like every other call,
    /// so an element replaced while still alive is compared as the new one. Failed before the fix:
    /// <c>RunAssertWithElement</c> held the first element and released it only on a stale read, so
    /// the assert compared "first" until the budget ran out.
    /// </summary>
    [Fact]
    [Trait("Pin", "review2")]
    public void Assert_FindsTheElementAgain_WhenItIsReplacedWhileAlive()
    {
        var reads = 0;
        var first = Element(text: "first");
        first.Setup(e => e.InstanceKey).Returns("1");
        var second = Element(text: "second");
        second.Setup(e => e.InstanceKey).Returns("2");
        GivenTarget(() => ++reads == 1 ? first.Object : second.Object);

        NewPage().Status.AssertText("second", timeoutMs: 500);
    }

    /// <summary>
    /// (S1, review 2) An assert re-checks visibility on every attempt, as a read does: an element
    /// that is not visible yet is waited for rather than read through.
    /// </summary>
    [Fact]
    [Trait("Pin", "review2")]
    public void Assert_WaitsForVisibility_OnEveryAttempt()
    {
        var reads = 0;
        var element = Element(text: "value");
        element.Setup(e => e.Visible).Returns(() => ++reads >= 3);
        GivenTarget(() => element.Object);

        NewPage().Status.AssertText("value", timeoutMs: 1000);

        Assert.True(reads >= 3);
    }

    /// <summary>
    /// (D3, R0, review 2) A route the platform does not have fails the call at once, naming the
    /// route, instead of being asked again until the budget runs out and reported as a timeout.
    /// </summary>
    [Fact]
    [Trait("Pin", "review2")]
    public void RouteUnavailable_FailsAtOnce()
    {
        var element = Element();
        element.Setup(e => e.Text).Throws(new RouteUnavailableException("no route for this"));
        GivenTarget(() => element.Object);

        var elapsed = Timed(() => NewPage().Status.GetText(timeoutMs: 1000));
        var error = Assert.Throws<RouteUnavailableException>(() => NewPage().Status.GetText(timeoutMs: 1000));

        Assert.Contains("no route for this", error.Message);
        Assert.True(elapsed < 300, $"the call took {elapsed} ms; it should not wait out its budget");
    }

    /// <summary>
    /// (D3, review 2) An unexpected exception that is <i>not</i> a missing route is still retried
    /// within the budget, as design 11's D3 says.
    /// </summary>
    [Fact]
    [Trait("Pin", "review2")]
    public void UnexpectedException_ThatIsNotAMissingRoute_IsStillRetried()
    {
        var element = Element();
        element.Setup(e => e.Text).Throws(new InvalidOperationException("the platform refused"));
        GivenTarget(() => element.Object);

        var error = Assert.Throws<WaitTimeoutException>(() => NewPage().Status.GetText(timeoutMs: 200));

        Assert.Matches(@"\d+ of \d+ attempts raised InvalidOperationException", error.Message);
    }

    #endregion

    #region R0 guards (pass before and after) and near-misses

    /// <summary>
    /// (Step 8) An action the platform reports as not performed - the app's bridge did not answer
    /// yet - is asked again within the budget, and the call succeeds once it is performed.
    /// </summary>
    [Fact]
    [Trait("Pin", "step8")]
    public void Toolbar_ActionNotPerformedYet_IsAskedAgainWithinTheBudget()
    {
        var element = Element();
        var asks = 0;
        element.Setup(e => e.InvokeToolbarItem("Target")).Callback(() =>
        {
            if (++asks < 3)
            {
                throw new ElementNotReadyException(Locator.ByAutomationId("Target"), NotReadyReason.Other,
                    "no element published on the app's bridge answers InvokeToolbarItem.");
            }
        });
        GivenTarget(() => element.Object);

        NewPage().Toolbar.Click(timeoutMs: 1000);

        Assert.Equal(3, asks);
    }

    /// <summary>
    /// Guard (step 8): a toolbar item that stays disabled fails within its budget, as "disabled".
    /// </summary>
    [Fact]
    [Trait("Pin", "guard")]
    public void Guard_Toolbar_FailsWithinItsBudget_WhenTheItemStaysDisabled()
    {
        var element = Element();
        element.Setup(e => e.InvokeToolbarItem("Target")).Throws(
            new ElementNotReadyException(Locator.ByAutomationId("Target"), NotReadyReason.Disabled,
                "the toolbar item is disabled"));
        GivenTarget(() => element.Object);

        Exception? error = null;
        var elapsed = Timed(() => error = Record.Exception(() => NewPage().Toolbar.Click(timeoutMs: 300)));

        var notReady = Assert.IsType<ElementNotReadyException>(error);
        Assert.Equal(NotReadyReason.Disabled, notReady.Reason);
        Assert.True(elapsed < 800, $"Click(timeoutMs: 300) took {elapsed} ms.");
    }

    /// <summary>
    /// Guard (step 8): any other failure of an action ends the call at once and is not repeated -
    /// the action may have taken effect.
    /// </summary>
    [Fact]
    [Trait("Pin", "guard")]
    public void Guard_Toolbar_OtherActionFailure_IsNotRepeated()
    {
        var element = Element();
        element.Setup(e => e.InvokeToolbarItem("Target")).Throws(new BruinetException("the app threw"));
        GivenTarget(() => element.Object);

        Assert.Throws<BruinetException>(() => NewPage().Toolbar.Click(timeoutMs: 1000));

        element.Verify(e => e.InvokeToolbarItem("Target"), Times.Once);
    }

    /// <summary>
    /// Guard: a command that never re-enables fails the call within its budget, says so, and is
    /// never invoked.
    /// </summary>
    [Fact]
    [Trait("Pin", "guard")]
    public void Guard_Click_FailsWithinItsBudget_WhenTheControlStaysDisabled()
    {
        var element = Element(enabled: false);
        GivenTarget(() => element.Object);

        Exception? error = null;
        var elapsed = Timed(() => error = Record.Exception(() => NewPage().Target.Click(timeoutMs: 300)));

        Assert.NotNull(error);
        Assert.Contains("disabled", error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(elapsed < 800, $"Click(timeoutMs: 300) took {elapsed} ms.");
        element.Verify(e => e.Invoke(), Times.Never);
    }

    /// <summary>
    /// Guard: an action whose effect never shows fails, and is not repeated.
    /// </summary>
    [Fact]
    [Trait("Pin", "guard")]
    public void Guard_Toggle_FailsAndActsOnce_WhenTheStateNeverChanges()
    {
        var element = Element();
        element.Setup(e => e.Checked).Returns(false);
        GivenTarget(() => element.Object);

        Assert.ThrowsAny<Exception>(() => NewPage().Toggle.Toggle(timeoutMs: 300));

        element.Verify(e => e.Toggle(), Times.Once);
    }

    /// <summary>
    /// A control replaced after its action says so, and the action is not repeated: the effect
    /// cannot be confirmed on an element the action never touched.
    /// </summary>
    [Fact]
    [Trait("Pin", "step6")]
    public void Toggle_ReplacedAfterTheAction_SaysSoAndActsOnce()
    {
        var reads = 0;
        var element = Element();
        element.Setup(e => e.Checked).Returns(() => ++reads == 1 ? false : throw new StaleElementException());
        GivenTarget(() => element.Object);

        Exception? error = null;
        var elapsed = Timed(() => error = Record.Exception(() => NewPage().Toggle.Toggle(timeoutMs: 1000)));

        var stale = Assert.IsType<StaleElementException>(error);
        Assert.Contains("replaced after Toggle", stale.Message);
        Assert.Contains("not repeated", stale.Message);
        element.Verify(e => e.Toggle(), Times.Once);
        Assert.True(elapsed < 800, $"The replaced element took {elapsed} ms to report.");
    }

    /// <summary>
    /// Guard (written in step 3, when the type exists): an app that is gone fails the call at once
    /// instead of waiting out the budget. Fails in step 3: every exception is still retried.
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void AppUnavailable_FailsTheCallAtOnce()
    {
        var element = Element();
        element.Setup(e => e.Visible).Throws(new AppUnavailableException("the application process has exited."));
        GivenTarget(() => element.Object);

        Exception? error = null;
        var elapsed = Timed(() => error = Record.Exception(() => NewPage().Target.Click(timeoutMs: 1000)));

        Assert.IsType<AppUnavailableException>(error);
        Assert.True(elapsed < 300, $"Click on a closed app took {elapsed} ms of its 1000 ms budget.");
    }

    /// <summary>
    /// A call that succeeds only after trouble passes, and says so in the log (R0 near-miss).
    /// Fails today: nothing reports a near-miss.
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void NearMiss_SlowToBecomeVisible_PassesAndLogsAWarning()
    {
        var stopwatch = Stopwatch.StartNew();
        var element = Element(text: "late");
        element.Setup(e => e.Visible).Returns(() => stopwatch.ElapsedMilliseconds > 250);
        GivenTarget(() => element.Object);

        Assert.Equal("late", NewPage().Status.GetText(timeoutMs: 400));

        _logger.Verify(l => l.Write(It.Is<LogEvent>(e => e.Kind == LogEventKind.Exit && e.Result == LogResult.Warning)), Times.AtLeastOnce);
    }

    #endregion
}
