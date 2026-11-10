namespace Bruinet.Blazor.Tests.Controls;

public class CheckBoxControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly TestPage _page;

    public CheckBoxControlTests()
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

        Assert.True(_page.TestCheckBox.IsChecked());
    }

    [Fact]
    public void IsChecked_WhenNotChecked_ReturnsFalse()
    {
        Assert.False(_page.TestCheckBox.IsChecked());
    }

    [Fact]
    public void Check_CallsElementCheck()
    {
        _page.TestCheckBox.Check();

        _mockElement.Verify(e => e.Check(), Times.Once);
    }

    [Fact]
    public void Uncheck_WhenChecked_CallsElementUncheck()
    {
        _page.TestCheckBox.Check();
        _page.TestCheckBox.Uncheck();

        _mockElement.Verify(e => e.Uncheck(), Times.Once);
    }

    [Fact]
    public void Uncheck_WhenUnchecked_DoesNothing()
    {
        _page.TestCheckBox.Uncheck();

        _mockElement.Verify(e => e.Uncheck(), Times.Never);
    }

    [Fact]
    public void Toggle_WhenUnchecked_CallsCheck()
    {
        _page.TestCheckBox.Toggle();

        _mockElement.Verify(e => e.Check(), Times.Once);
    }

    [Fact]
    public void Toggle_WhenChecked_CallsUncheck()
    {
        var isChecked = true;
        _mockElement.Setup(e => e.IsChecked).Returns(() => isChecked);
        _mockElement.Setup(e => e.Uncheck()).Callback(() => isChecked = false);

        _page.TestCheckBox.Toggle();

        _mockElement.Verify(e => e.Uncheck(), Times.Once);
    }

    [Fact]
    public void Click_CallsElementClick()
    {
        _page.TestCheckBox.Click();

        _mockElement.Verify(e => e.Click(), Times.Once);
    }

    [Fact]
    public void IsExists_WhenExists_ReturnsTrue()
    {
        Assert.True(_page.TestCheckBox.Exists());
    }

    [Fact]
    public void IsVisible_WhenVisible_ReturnsTrue()
    {
        Assert.Equal(true, _page.TestCheckBox.IsVisible());
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }

        public CheckBoxControl<TestPage> TestCheckBox => new(this, "test-checkbox");
    }
}
