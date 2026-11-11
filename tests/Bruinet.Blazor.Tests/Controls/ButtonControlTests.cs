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
