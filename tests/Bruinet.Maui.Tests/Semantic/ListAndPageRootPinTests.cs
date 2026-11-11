using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Core.Logging;
using Bruinet.Maui.Containers;
using Bruinet.Maui.Context;
using Bruinet.Maui.Controls.Display;
using Bruinet.Maui.Interfaces;
using Bruinet.Maui.Pages;
using Moq;

namespace Bruinet.Maui.Tests.Semantic;

/// <summary>
/// Pins for what a Kofflard app found on its Windows and Android heads: a grouped list's repeated
/// <c>PositionInSet</c>, rows only partly on screen, items above the viewport, a scroll to the top that
/// stopped mid-list, and a page root that kept answering after it left the tree.
/// </summary>
public class ListAndPageRootPinTests : SemanticControlTestsBase
{
    private readonly List<IMauiElement> _rows = [];
    private readonly Mock<IMauiElement> _listRoot;

    public ListAndPageRootPinTests()
    {
        Context.Setup(c => c.Timeouts).Returns(new TimeoutSettings
        {
            DefaultWait = TimeSpan.FromMilliseconds(1000),
            PageLoad = TimeSpan.FromMilliseconds(1000),
            PollingInterval = TimeSpan.FromMilliseconds(1),
            Animation = TimeSpan.FromMilliseconds(300)
        });
        Context.Setup(c => c.Logger).Returns(new Mock<ITestLogger>().Object);
        Context.Setup(c => c.AppElement).Returns(new Mock<IMauiElement>().Object);

        _listRoot = CreateElement("Rows", 0, 0, 300, 400);
        _listRoot.Setup(e => e.ScrollContent(It.IsAny<int>(), It.IsAny<int>())).Returns(ScrollStep.NotMoved);
        Context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Rows"))).Returns(_listRoot.Object);
        Context.Setup(c => c.FindElement(It.Is<Locator>(l => l.Value == "Rows"))).Returns(_listRoot.Object);
        _listRoot.Setup(e => e.FindElements(It.Is<Locator>(l => l.Value == "ListItem")))
            .Returns(() => _rows.ToArray());
    }

    #region Pages and objects under test

    private sealed class ListPage(IMauiTestContext context) : PageObjectBase<ListPage>(context)
    {
        public override string Name => "TestPage";

        public override bool IsLoaded() => true;

        public Rows Rows => new(this, "Rows");

        public Label<ListPage> Status => new(this, "Status");
    }

    private sealed class Rows(IMauiScope<ListPage> scope, string automationId)
        : CollectionObjectBase<ListPage, Rows, Row>(
            scope,
            automationId,
            ItemStrategy.ByLocator(Locator.ByControlType("ListItem")),
            (collection, itemRoot, index) => new Row(collection, itemRoot, index));

    private sealed class Row(Rows collection, IMauiElement itemRoot, int index)
        : ItemObjectBase<Rows, Row>(collection, itemRoot, index)
    {
        public Label<Row> Name => new(this, "RowName");
    }

    private sealed class StackedPage(IMauiTestContext context) : PageObjectBase<StackedPage>(context)
    {
        public override string Name => "StackedPage";
    }

    private ListPage NewPage() => new(Context.Object);

    /// <summary>A row whose name label is shown, hidden (off screen) or absent (left out of the tree).</summary>
    private static Mock<IMauiElement> RowOf(string name, int? positionInSet = null, bool nameShown = true, bool nameInTree = true)
    {
        var row = CreateElement("Row_" + name, 0, 0, 300, 40);
        row.Setup(e => e.AutomationId).Returns("Row_" + name);
        row.Setup(e => e.TagName).Returns("ListItem");
        row.Setup(e => e.InstanceKey).Returns("row:" + name);
        row.Setup(e => e.PositionInSet).Returns(positionInSet);
        var label = CreateElement("RowName", 0, 0, 100, 20);
        label.Setup(e => e.Text).Returns(name);
        label.Setup(e => e.Visible).Returns(nameShown);
        row.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "RowName")))
            .Returns(nameInTree ? label.Object : null);
        return row;
    }

    private static Func<Row, bool> Named(string name) => row => row.Name.GetText(50) == name;

    #endregion

    /// <summary>
    /// A grouped list restarts <c>PositionInSet</c> in each group. Two rows with one index are
    /// noticed, and the second is still searched, without <c>HasLogicalIndexes</c> overridden.
    /// </summary>
    [Fact]
    public void GroupedList_RepeatedPositions_AreNoticed_AndTheSecondRowIsFound()
    {
        _rows.AddRange([RowOf("Close first", positionInSet: 1).Object, RowOf("Stretch first", positionInSet: 1).Object]);

        var found = NewPage().Rows.FindItem(Named("Stretch first"), timeoutMs: 500);

        Assert.NotNull(found);
        Assert.Equal("Stretch first", found.Name.GetText());
    }

    /// <summary>
    /// A row only partly on screen whose label is off screen (Windows, iOS) is not this one yet,
    /// not a failed search.
    /// </summary>
    [Fact]
    public void RowPartlyOnScreen_NameOffScreen_IsSkipped()
    {
        _rows.AddRange([RowOf("Sliver", nameShown: false).Object, RowOf("Beta").Object]);

        Assert.NotNull(NewPage().Rows.FindItem(Named("Beta"), timeoutMs: 500));
    }

    /// <summary>The same row on Android, where the off-screen label is left out of the tree.</summary>
    [Fact]
    public void RowPartlyOnScreen_NameLeftOut_IsSkipped()
    {
        _rows.AddRange([RowOf("Sliver", nameInTree: false).Object, RowOf("Beta").Object]);

        Assert.NotNull(NewPage().Rows.FindItem(Named("Beta"), timeoutMs: 500));
    }

    /// <summary>
    /// When no row can be read at all, the predicate reads what no row has: that is reported, not
    /// scrolled past as rows on their way in.
    /// </summary>
    [Fact]
    public void NoRowReadable_IsReported()
    {
        _rows.AddRange([RowOf("A", nameInTree: false).Object, RowOf("B", nameInTree: false).Object]);

        Assert.ThrowsAny<Exception>(() => NewPage().Rows.FindItem(Named("A"), timeoutMs: 300));
    }

    /// <summary>
    /// An item above the viewport: the search reaches the end below, scrolls to the top once and
    /// finds it there.
    /// </summary>
    [Fact]
    public void ItemAboveTheViewport_IsFound_FromTheTop()
    {
        var top = new[] { RowOf("Alpha").Object, RowOf("Beta").Object };
        _rows.AddRange([RowOf("Gamma").Object, RowOf("Delta").Object]);
        _listRoot.Setup(e => e.ScrollContent(It.Is<int>(steps => steps < 0), It.IsAny<int>()))
            .Returns(() =>
            {
                if (_rows[0] == top[0])
                {
                    return ScrollStep.NotMoved;
                }

                _rows.Clear();
                _rows.AddRange(top);
                return ScrollStep.Moved;
            });

        var found = NewPage().Rows.FindItem(Named("Alpha"), timeoutMs: 800);

        Assert.NotNull(found);
    }

    /// <summary>
    /// A scroll to the top steps up while the list moves. It used to scroll the first realized row
    /// into view and stop: in a virtualized list that row is mid-list.
    /// </summary>
    [Fact]
    public void ScrollToTop_StepsUpPastTheFirstRealizedRow()
    {
        var top = new[] { RowOf("Alpha").Object, RowOf("Beta").Object };
        _rows.AddRange([RowOf("Gamma").Object, RowOf("Delta").Object]);
        var steps = 0;
        _listRoot.Setup(e => e.ScrollContent(It.Is<int>(s => s < 0), It.IsAny<int>()))
            .Returns(() =>
            {
                steps++;
                if (_rows[0] == top[0])
                {
                    return ScrollStep.NotMoved;
                }

                _rows.Clear();
                _rows.AddRange(top);
                return ScrollStep.Moved;
            });

        NewPage().Rows.ScrollToTop(timeoutMs: 500);

        Assert.Same(top[0], _rows[0]);
        Assert.True(steps >= 2, $"Expected steps until the list stopped moving; took {steps}.");
    }

    /// <summary>
    /// One read asks the page root for its bounds once. The page's probe and its <c>IsLoaded</c>
    /// both check the root, and on Android each check is a round trip: within one attempt the
    /// answer is kept (AttemptMemo).
    /// </summary>
    [Fact]
    public void ARead_AsksThePageRootForItsBoundsOnce()
    {
        var root = CreateElement("TestPage", 0, 0, 400, 800);
        var boundsReads = 0;
        root.Setup(e => e.Rect).Returns(() =>
        {
            boundsReads++;
            return new System.Drawing.Rectangle(0, 0, 400, 800);
        });
        root.Setup(e => e.TryFindElement(It.IsAny<Locator>())).Returns((Locator locator) => Context.Object.TryFindElement(locator));
        Context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "TestPage"))).Returns(root.Object);
        var status = CreateElement("Status", 0, 0, 100, 20);
        status.Setup(e => e.Text).Returns("Ready");
        Context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Status"))).Returns(status.Object);
        var page = NewPage();
        _ = page.ContainerRoot;
        boundsReads = 0;

        Assert.Equal("Ready", page.Status.GetText());

        Assert.Equal(1, boundsReads);
    }

    /// <summary>
    /// Where the driver finds only what is shown (Android), the check before a read does not ask the
    /// element whether it is visible: being found answers it. An explicit visibility question still
    /// asks (design-fewer-reads, D3).
    /// </summary>
    [Fact]
    public void FoundMeansShown_TheReadSkipsTheVisibilityCheck_AnAssertionStillAsks()
    {
        var status = CreateElement("Status", 0, 0, 100, 20);
        status.Setup(e => e.Text).Returns("Ready");
        status.As<IShownWhenFound>().Setup(e => e.IsShownWhenFound).Returns(true);
        Context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Status"))).Returns(status.Object);
        var page = NewPage();

        Assert.Equal("Ready", page.Status.GetText());
        status.Verify(e => e.Visible, Times.Never());

        page.Status.AssertVisible(true);
        status.Verify(e => e.Visible, Times.AtLeastOnce());
    }

    /// <summary>
    /// A page root that left the tree is found again, though it still answers as shown and full
    /// size: a page Shell navigated away from, on Windows, for about a second.
    /// </summary>
    [Fact]
    public void PageRoot_DetachedButAnswering_IsFoundAgain()
    {
        var covered = CreateElement("StackedPage", 0, 0, 400, 800);
        var coveredAttached = true;
        covered.As<IDetachable>().Setup(e => e.IsAttached).Returns(() => coveredAttached);
        var shown = CreateElement("StackedPage", 0, 0, 400, 800);
        shown.As<IDetachable>().Setup(e => e.IsAttached).Returns(true);
        var current = covered.Object;
        Context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "StackedPage"))).Returns(() => current);
        Context.Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "StackedPage"))).Returns(() => [current]);

        var page = new StackedPage(Context.Object);
        Assert.Same(covered.Object, page.ContainerRoot);

        coveredAttached = false;
        current = shown.Object;

        Assert.Same(shown.Object, page.ContainerRoot);
    }
}
