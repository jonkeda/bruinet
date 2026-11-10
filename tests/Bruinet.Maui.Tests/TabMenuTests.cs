using Bruinet.Maui.Controls.Navigation;

namespace Bruinet.Maui.Tests;

/// <summary>
/// Unit tests for the tab menu, over mocked elements.
/// </summary>
/// <remarks>
/// <see cref="TabItem{TParent}.IsSelectedCore"/> cannot be covered by the sample app: its tab
/// bar is built from plain buttons, which expose neither the selection pattern nor a checked
/// state (confirmed against the running app). These tests stand in for a platform that does
/// expose it, until the Shell sample gives tabs that really are selectable.
/// </remarks>
public class TabMenuTests
{
    private readonly Mock<IMauiTestContext> _context;
    private readonly Dictionary<string, Mock<IMauiElement>> _buttons = new();
    private readonly Dictionary<string, Mock<IMauiElement>> _tabRoots = new();

    public TabMenuTests()
    {
        _context = new Mock<IMauiTestContext>();
        _context.Setup(c => c.Timeouts).Returns(new TimeoutSettings
        {
            DefaultWait = TimeSpan.FromMilliseconds(300),
            PageLoad = TimeSpan.FromMilliseconds(300),
            PollingInterval = TimeSpan.FromMilliseconds(20)
        });
        _context.Setup(c => c.DefaultLocatorStrategy).Returns(LocatorStrategy.AutomationId);

        var pageRoot = new Mock<IMauiElement>();
        pageRoot.Setup(e => e.Visible).Returns(true);
        pageRoot.Setup(e => e.TagName).Returns("Page");
        pageRoot.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 400, 800));
        pageRoot.Setup(e => e.TryFindElement(It.IsAny<Locator>()))
            .Returns((Locator locator) => _context.Object.FindElement(locator));
        _context.Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "TestPage")))
            .Returns([pageRoot.Object]);
    }

    [Fact]
    [Trait("Control", "TabMenu")]
    public void Tabs_AreFoundByTheirCaption()
    {
        var tabs = SetupTabs(("Home", false), ("Search", false));

        Assert.Equal(2, tabs.GetItemCount());
        Assert.Equal(1, tabs["Search"].Index);
        Assert.Equal("Search", tabs["Search"].GetText());
    }

    [Fact]
    [Trait("Method", "IsSelected")]
    public void Tab_ReportsSelection_FromItsButton()
    {
        var tabs = SetupTabs(("Home", false), ("Search", true));

        Assert.False(tabs["Home"].IsSelected());
        Assert.True(tabs["Search"].IsSelected());
    }

    /// <summary>
    /// A bar whose tabs expose nothing answers false, rather than guessing from app state.
    /// </summary>
    [Fact]
    [Trait("Method", "IsSelected")]
    public void Tab_WithoutSelectionState_ReportsFalse()
    {
        var tabs = SetupTabs(("Home", false), ("Search", false));

        Assert.False(tabs["Home"].IsSelected());
        Assert.False(tabs["Search"].IsSelected());
    }

    /// <summary>
    /// A tab's root is a layout with no command, so the click must reach the button inside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Carried over from the test that covered the old parallel-list matching, which asserted
    /// the same thing about the surface it picked.
    /// </para>
    /// <para>
    /// <b>Invoked, not selected</b>, even though a tab is conceptually one of a group and
    /// <c>TabItem</c> derives from <c>SelectableItemBase</c>. This tab bar is built from plain
    /// buttons, and a button exposes Invoke and no SelectionItem - measured, when inheriting
    /// <c>Select</c> failed against the real app with "'TabMenuView_Button' does not expose the
    /// UI Automation SelectionItemPattern". Under the old activation ladder this control worked
    /// by falling through from SelectionItem to Invoke, so the mismatch was always there and
    /// never said so.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Method", "Click")]
    public void Tab_Click_ActivatesTheButton_NotTheTabItself()
    {
        var tabs = SetupTabs(("Home", false), ("Search", false));

        tabs["Search"].Click();

        _buttons["Search"].Verify(e => e.Invoke(), Times.Once);
        _buttons["Search"].Verify(e => e.Click(), Times.Never);
        _tabRoots["Search"].Verify(e => e.Click(), Times.Never);
    }

    #region Mock helpers

    /// <summary>
    /// Builds a tab bar whose tabs each hold a button carrying the caption and the selection.
    /// </summary>
    private TabMenu<TestPage> SetupTabs(params (string Caption, bool Selected)[] tabs)
    {
        var root = new Mock<IMauiElement>();
        root.Setup(e => e.Visible).Returns(true);
        root.Setup(e => e.TagName).Returns("Group");
        root.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 300, 60));

        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "TabMenuView")))
            .Returns(root.Object);
        _context.Setup(c => c.FindElement(It.Is<Locator>(l => l.Value == "TabMenuView")))
            .Returns(root.Object);

        var tabRoots = new List<IMauiElement>();
        foreach (var (caption, selected) in tabs)
        {
            var button = new Mock<IMauiElement>();
            button.Setup(e => e.Visible).Returns(true);
            button.Setup(e => e.Enabled).Returns(true);
            button.Setup(e => e.Text).Returns(caption);
            button.Setup(e => e.Selected).Returns(selected);
            button.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 80, 40));

            var tabRoot = new Mock<IMauiElement>();
            tabRoot.Setup(e => e.Visible).Returns(true);
            tabRoot.Setup(e => e.Enabled).Returns(true);
            tabRoot.Setup(e => e.TagName).Returns("Group");
            // The tab's own element carries no text, as on Windows.
            tabRoot.Setup(e => e.Text).Returns(string.Empty);
            tabRoot.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 80, 60));

            tabRoot.Setup(e => e.TryFindElement(
                    It.Is<Locator>(l => l.Value == "TabMenuView_Button")))
                .Returns(button.Object);

            // No caption label: the caption is read from the button instead.
            tabRoot.Setup(e => e.TryFindElement(
                    It.Is<Locator>(l => l.Value == "TabMenuView_Caption")))
                .Returns((IMauiElement?)null);

            _buttons[caption] = button;
            _tabRoots[caption] = tabRoot;
            tabRoots.Add(tabRoot.Object);
        }

        root.Setup(e => e.FindElements(
                It.Is<Locator>(l => l.Value == "TabMenuView_Grid")))
            .Returns(tabRoots);

        return new TabMenu<TestPage>(new TestPage(_context.Object));
    }

    private class TestPage : PageObjectBase<TestPage>
    {
        public TestPage(IMauiTestContext context) : base(context) { }

        public override string Name => "TestPage";
        public override bool IsLoaded() => true;
    }

    #endregion
}
