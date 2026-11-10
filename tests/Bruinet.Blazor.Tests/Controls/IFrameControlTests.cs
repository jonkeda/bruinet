using Bruinet.Core.Exceptions;

namespace Bruinet.Blazor.Tests.Controls;

public class IFrameControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly TestPage _page;

    public IFrameControlTests()
    {
        _mockContext = MockHtmlFactory.CreateMockContext();
        _mockElement = MockHtmlFactory.CreateMockElement();
        MockHtmlFactory.SetupFindElement(_mockContext, _mockElement);
        _page = new TestPage(_mockContext.Object);
    }

    [Fact]
    public void GetSource_ReturnsAttribute()
    {
        MockHtmlFactory.SetupDomAttribute(_mockElement, "src", "frame.html");
        Assert.Equal("frame.html", _page.TestIFrame.GetSource());
    }

    [Fact]
    public void GetTitle_ReturnsAttribute()
    {
        MockHtmlFactory.SetupDomAttribute(_mockElement, "title", "My Frame");
        Assert.Equal("My Frame", _page.TestIFrame.GetTitle());
    }

    [Fact]
    public void GetName_ReturnsAttribute()
    {
        MockHtmlFactory.SetupDomAttribute(_mockElement, "name", "frame1");
        Assert.Equal("frame1", _page.TestIFrame.GetName());
    }

    [Fact]
    public void ClickInside_CallsEvaluate()
    {
        _page.TestIFrame.ClickInside("#inner-button");
        _mockElement.Verify(e => e.Evaluate(It.Is<string>(s =>
            s.Contains("contentDocument") && s.Contains("#inner-button") && s.Contains("click()"))), Times.Once);
    }

    [Fact]
    public void FillInside_CallsEvaluate()
    {
        _mockElement.Setup(e => e.Evaluate<string?>(It.Is<string>(s => s.EndsWith("#inner-input').value"))))
            .Returns("hello");

        _page.TestIFrame.FillInside("#inner-input", "hello");
        _mockElement.Verify(e => e.Evaluate(It.Is<string>(s =>
            s.Contains("contentDocument") && s.Contains("#inner-input") && s.Contains("hello"))), Times.Once);
    }

    [Fact]
    public void GetTextInside_ReturnsText()
    {
        _mockElement.Setup(e => e.Evaluate<string?>(It.Is<string>(s =>
            s.Contains("contentDocument") && s.Contains("#inner-element") && s.Contains("textContent"))))
            .Returns("Inner text");
        Assert.Equal("Inner text", _page.TestIFrame.GetTextInside("#inner-element"));
    }

    [Fact]
    public void IsElementInside_WhenExists_ReturnsTrue()
    {
        _mockElement.Setup(e => e.Evaluate<bool>(It.Is<string>(s =>
            s.Contains("contentDocument") && s.Contains("#inner-element") && s.Contains("!== null"))))
            .Returns(true);
        Assert.True(_page.TestIFrame.IsElementInside("#inner-element"));
    }

    [Fact]
    public void IsElementInside_WhenNotExists_ReturnsFalse()
    {
        _mockElement.Setup(e => e.Evaluate<bool>(It.Is<string>(s =>
            s.Contains("contentDocument") && s.Contains("#missing"))))
            .Returns(false);
        Assert.False(_page.TestIFrame.IsElementInside("#missing"));
    }

    [Fact]
    public void AssertSource_WhenMatches_DoesNotThrow()
    {
        MockHtmlFactory.SetupDomAttribute(_mockElement, "src", "frame.html");
        _page.TestIFrame.AssertSource("frame.html");
    }

    [Fact]
    public void AssertSource_WhenMismatch_Throws()
    {
        MockHtmlFactory.SetupDomAttribute(_mockElement, "src", "other.html");
        Assert.Throws<ControlAssertionException>(() => _page.TestIFrame.AssertSource("frame.html"));
    }

    [Fact]
    public void AssertSourceContains_WhenContains_DoesNotThrow()
    {
        MockHtmlFactory.SetupDomAttribute(_mockElement, "src", "path/to/frame.html");
        _page.TestIFrame.AssertSourceContains("frame.html");
    }

    [Fact]
    public void IsExists_WhenExists_ReturnsTrue()
    {
        Assert.True(_page.TestIFrame.Exists());
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }
        public IFrameControl<TestPage> TestIFrame => new(this, "test-iframe");
    }
}
