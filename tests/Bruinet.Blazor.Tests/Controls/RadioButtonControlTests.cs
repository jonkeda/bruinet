namespace Bruinet.Blazor.Tests.Controls;

public class RadioButtonControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly TestPage _page;

    public RadioButtonControlTests()
    {
        _mockContext = MockHtmlFactory.CreateMockContext();
        _mockElement = MockHtmlFactory.CreateMockToggleElement(isChecked: false);
        MockHtmlFactory.SetupFindElement(_mockContext, _mockElement);
        _page = new TestPage(_mockContext.Object);
    }

    [Fact]
    public void IsChecked_WhenChecked_ReturnsTrue()
    {
        _mockElement.Setup(e => e.IsChecked).Returns(true);

        Assert.True(_page.TestRadio.IsChecked());
    }

    [Fact]
    public void IsChecked_WhenNotChecked_ReturnsFalse()
    {
        Assert.False(_page.TestRadio.IsChecked());
    }

    [Fact]
    public void Select_CallsElementCheck()
    {
        _page.TestRadio.Select();

        _mockElement.Verify(e => e.Check(), Times.Once);
    }

    [Fact]
    public void Click_CallsElementClick()
    {
        _page.TestRadio.Click();

        _mockElement.Verify(e => e.Click(), Times.Once);
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }

        public RadioButtonControl<TestPage> TestRadio => new(this, "test-radio");
    }
}
