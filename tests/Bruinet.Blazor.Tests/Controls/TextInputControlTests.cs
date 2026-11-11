namespace Bruinet.Blazor.Tests.Controls;

public class TextInputControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly TestPage _page;

    public TextInputControlTests()
    {
        _mockContext = MockHtmlFactory.CreateMockContext();
        _mockElement = MockHtmlFactory.CreateMockElement();
        MockHtmlFactory.SetupFindElement(_mockContext, _mockElement);
        _page = new TestPage(_mockContext.Object);
    }

    [Fact]
    public void SetText_FillsElement()
    {
        _page.TestTextInput.SetText("hello");

        _mockElement.Verify(e => e.Fill("hello"), Times.Once);
    }

    [Fact]
    public void GetValue_ReturnsInputValue()
    {
        Assert.Equal("Test Text", _page.TestTextInput.GetValue());
    }

    [Fact]
    public void TypeText_CallsSendKeys()
    {
        _page.TestTextInput.TypeText("abc");

        _mockElement.Verify(e => e.SendKeys("abc", It.IsAny<Bruinet.Core.TextInputMethod>()), Times.Once);
    }

    [Fact]
    public void GetText_ReturnsText()
    {
        Assert.Equal("Test Text", _page.TestTextInput.GetText());
    }

    [Fact]
    public void Focus_CallsElementFocus()
    {
        _page.TestTextInput.Focus();

        _mockElement.Verify(e => e.Focus(), Times.Once);
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }

        public TextInputControl<TestPage> TestTextInput => new(this, "test-input");
    }
}
