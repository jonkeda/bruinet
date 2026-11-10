namespace Bruinet.Blazor.Tests.Controls;

public class ButtonControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly TestPage _page;

    public ButtonControlTests()
    {
        _mockContext = MockHtmlFactory.CreateMockContext();
        _mockElement = MockHtmlFactory.CreateMockElement();
        MockHtmlFactory.SetupFindElement(_mockContext, _mockElement);
        _page = new TestPage(_mockContext.Object);
    }

    [Fact]
    public void Click_CallsElementClick()
    {
        _page.TestButton.Click();

        _mockElement.Verify(e => e.Click(), Times.Once);
    }

    [Fact]
    public void IsExists_WhenExists_ReturnsTrue()
    {
        Assert.True(_page.TestButton.Exists());
    }

    [Fact]
    public void IsExists_WhenNotFound_ReturnsFalse()
    {
        MockHtmlFactory.SetupElementNotFound(_mockContext);

        Assert.False(_page.TestButton.Exists());
    }

    [Fact]
    public void IsVisible_WhenVisible_ReturnsTrue()
    {
        Assert.Equal(true, _page.TestButton.IsVisible());
    }

    [Fact]
    public void IsVisible_WhenNotVisible_ReturnsFalse()
    {
        _mockElement.Setup(e => e.Visible).Returns(false);

        Assert.Equal(false, _page.TestButton.IsVisible());
    }

    [Fact]
    public void IsEnabled_WhenEnabled_ReturnsTrue()
    {
        Assert.Equal(true, _page.TestButton.IsEnabled());
    }

    [Fact]
    public void IsEnabled_WhenDisabled_ReturnsFalse()
    {
        _mockElement.Setup(e => e.Enabled).Returns(false);

        Assert.Equal(false, _page.TestButton.IsEnabled());
    }

    [Fact]
    public void GetText_ReturnsText()
    {
        Assert.Equal("Test Text", _page.TestButton.GetText());
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }

        public ButtonControl<TestPage> TestButton => new(this, "test-btn");
    }
}
