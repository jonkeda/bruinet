using Bruinet.Core.Exceptions;

namespace Bruinet.Blazor.Tests.Controls;

public class NavMenuControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly Mock<IHtmlElement> _mockItem1;
    private readonly Mock<IHtmlElement> _mockItem2;
    private readonly Mock<IHtmlElement> _mockItem3;
    private readonly TestPage _page;

    public NavMenuControlTests()
    {
        _mockContext = MockHtmlFactory.CreateMockContext();
        _mockElement = MockHtmlFactory.CreateMockElement();
        MockHtmlFactory.SetupFindElement(_mockContext, _mockElement);

        _mockItem1 = CreateNavItem("Home");
        _mockItem2 = CreateNavItem("About");
        _mockItem3 = CreateNavItem("Contact");

        var items = new List<IHtmlElement> { _mockItem1.Object, _mockItem2.Object, _mockItem3.Object }.AsReadOnly();
        _mockElement.Setup(e => e.FindElements(It.IsAny<Locator>()))
            .Returns(items);

        _page = new TestPage(_mockContext.Object);
    }

    [Fact]
    public void GetItemCount_ReturnsCorrectCount()
    {
        Assert.Equal(3, _page.TestNav.GetItemCount());
    }

    [Fact]
    public void GetItems_ReturnsItemTexts()
    {
        var items = _page.TestNav.GetItems();
        Assert.NotNull(items);
        Assert.Equal(3, items.Count);
        Assert.Equal("Home", items[0]);
        Assert.Equal("About", items[1]);
        Assert.Equal("Contact", items[2]);
    }

    [Fact]
    public void NavigateTo_ClicksMatchingItem()
    {
        _page.TestNav.NavigateTo("About");
        _mockItem2.Verify(e => e.Click(), Times.Once);
    }

    [Fact]
    public void NavigateTo_NotFound_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _page.TestNav.NavigateTo("Missing"));
    }

    [Fact]
    public void NavigateToIndex_ClicksItemAtIndex()
    {
        _page.TestNav.NavigateToIndex(2);
        _mockItem3.Verify(e => e.Click(), Times.Once);
    }

    [Fact]
    public void NavigateToIndex_OutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _page.TestNav.NavigateToIndex(5));
    }

    [Fact]
    public void WaitItemsHasItem_WhenExists_ReturnsTrue()
    {
        Assert.True(_page.TestNav.WaitItemsHasItem("Home"));
    }

    [Fact]
    public void WaitItemsHasItem_WhenNotExists_ReturnsFalse()
    {
        Assert.False(_page.TestNav.WaitItemsHasItem("Missing", 0));
    }

    [Fact]
    public void GetItemHref_ReturnsHref()
    {
        _mockItem1.Setup(e => e.GetAttribute("href")).Returns("/home");
        Assert.Equal("/home", _page.TestNav.GetItemHref("Home"));
    }

    [Fact]
    public void AssertItemCount_WhenMatches_DoesNotThrow()
    {
        _page.TestNav.AssertItemCount(3);
    }

    [Fact]
    public void AssertItemCount_WhenMismatch_Throws()
    {
        Assert.Throws<ControlAssertionException>(() => _page.TestNav.AssertItemCount(5));
    }

    [Fact]
    public void AssertItemsHasItem_WhenExists_DoesNotThrow()
    {
        _page.TestNav.AssertItemsHasItem("Home");
    }

    [Fact]
    public void AssertItemsHasItem_WhenNotExists_Throws()
    {
        Assert.Throws<ControlAssertionException>(() => _page.TestNav.AssertItemsHasItem("Missing", timeoutMs: 0));
    }

    private static Mock<IHtmlElement> CreateNavItem(string text)
    {
        var mock = new Mock<IHtmlElement>();
        mock.Setup(e => e.Text).Returns(text);
        mock.Setup(e => e.Visible).Returns(true);
        mock.Setup(e => e.Enabled).Returns(true);
        return mock;
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }
        public NavMenuControl<TestPage> TestNav => new(this, "test-nav");
    }
}
