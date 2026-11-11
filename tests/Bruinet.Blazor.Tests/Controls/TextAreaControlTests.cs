namespace Bruinet.Blazor.Tests.Controls;

public class TextAreaControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly TestPage _page;

    public TextAreaControlTests()
    {
        _mockContext = MockHtmlFactory.CreateMockContext();
        _mockElement = MockHtmlFactory.CreateMockElement();
        MockHtmlFactory.SetupFindElement(_mockContext, _mockElement);
        _page = new TestPage(_mockContext.Object);
    }

    [Fact]
    public void SetText_FillsElement()
    {
        _page.TestTextArea.SetText("hello");

        _mockElement.Verify(e => e.Fill("hello"), Times.Once);
    }

    [Fact]
    public void GetValue_ReturnsInputValue()
    {
        Assert.Equal("Test Text", _page.TestTextArea.GetValue());
    }

    [Fact]
    public void Append_FocusesAndSendsKeys()
    {
        _page.TestTextArea.Append("appended");

        _mockElement.Verify(e => e.Focus(), Times.Once);
        _mockElement.Verify(e => e.SendKeys("appended", It.IsAny<Bruinet.Core.TextInputMethod>()), Times.Once);
    }

    [Fact]
    public void Clear_CallsElementClear()
    {
        _page.TestTextArea.Clear();

        _mockElement.Verify(e => e.Clear(), Times.Once);
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }

        public TextAreaControl<TestPage> TestTextArea => new(this, "test-textarea");
    }
}
