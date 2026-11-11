namespace Bruinet.Blazor.Tests.Controls;

public class ListControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly TestPage _page;

    public ListControlTests()
    {
        _mockContext = MockHtmlFactory.CreateMockContext();
        _mockElement = MockHtmlFactory.CreateMockElement();
        MockHtmlFactory.SetupFindElement(_mockContext, _mockElement);

        var childElements = new List<IHtmlElement>
        {
            CreateChildElement("Item 1").Object,
            CreateChildElement("Item 2").Object,
            CreateChildElement("Item 3").Object,
        };
        _mockElement.Setup(e => e.FindElements(It.IsAny<Locator>()))
            .Returns(childElements.AsReadOnly());

        _page = new TestPage(_mockContext.Object);
    }

    [Fact]
    public void ItemCount_ReturnsCorrectCount()
    {
        Assert.Equal(3, _page.TestList.GetItemCount());
    }

    [Fact]
    public void GetItemText_ReturnsText()
    {
        Assert.Equal("Item 1", _page.TestList.GetItemText(0));
    }

    [Fact]
    public void GetItemText_SecondItem_ReturnsText()
    {
        Assert.Equal("Item 2", _page.TestList.GetItemText(1));
    }

    [Fact]
    public void GetItemText_OutOfRange_ReturnsNull()
    {
        Assert.Null(_page.TestList.GetItemText(5));
    }

    [Fact]
    public void GetItemTexts_ReturnsAllTexts()
    {
        var texts = _page.TestList.GetItemTexts();

        Assert.NotNull(texts);

        Assert.Equal(3, texts.Count);
        Assert.Equal("Item 1", texts[0]);
        Assert.Equal("Item 2", texts[1]);
        Assert.Equal("Item 3", texts[2]);
    }

    private static Mock<IHtmlElement> CreateChildElement(string? text)
    {
        var mock = new Mock<IHtmlElement>();
        mock.Setup(e => e.Text).Returns(text);
        return mock;
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }

        public ListControl<TestPage> TestList => new(this, "test-list");
    }
}
