namespace Bruinet.Blazor.Tests.Controls;

public class LinkControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly TestPage _page;

    public LinkControlTests()
    {
        _mockContext = MockHtmlFactory.CreateMockContext();
        _mockElement = MockHtmlFactory.CreateMockElement();
        MockHtmlFactory.SetupFindElement(_mockContext, _mockElement);
        _page = new TestPage(_mockContext.Object);
    }

    [Fact]
    public void Click_CallsElementClick()
    {
        _page.TestLink.Click();

        _mockElement.Verify(e => e.Click(), Times.Once);
    }

    [Fact]
    public void GetText_ReturnsLinkText()
    {
        Assert.Equal("Test Text", _page.TestLink.GetText());
    }

    [Fact]
    public void GetHref_ReturnsHref()
    {
        _mockElement.Setup(e => e.GetAttribute("href")).Returns("https://example.com");

        Assert.Equal("https://example.com", _page.TestLink.GetHref());
    }

    [Fact]
    public void IsExists_WhenExists_ReturnsTrue()
    {
        Assert.True(_page.TestLink.Exists());
    }

    [Fact]
    public void IsVisible_WhenVisible_ReturnsTrue()
    {
        Assert.True(_page.TestLink.IsVisible());
    }

    [Fact]
    public void IsEnabled_WhenEnabled_ReturnsTrue()
    {
        Assert.True(_page.TestLink.IsEnabled());
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }

        public LinkControl<TestPage> TestLink => new(this, "test-link");
    }
}
