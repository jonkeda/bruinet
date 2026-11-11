namespace Bruinet.Blazor.Tests.Controls;

public class ProgressControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly TestPage _page;

    public ProgressControlTests()
    {
        _mockContext = MockHtmlFactory.CreateMockContext();
        _mockElement = MockHtmlFactory.CreateMockElement();
        MockHtmlFactory.SetupFindElement(_mockContext, _mockElement);
        _page = new TestPage(_mockContext.Object);
    }

    [Fact]
    public void GetValue_ReturnsValue()
    {
        _mockElement.Setup(e => e.GetAttribute("value")).Returns("75");

        Assert.Equal(75, _page.TestProgress.GetValue());
    }

    [Fact]
    public void GetMax_ReturnsMax()
    {
        _mockElement.Setup(e => e.GetAttribute("max")).Returns("200");

        Assert.Equal(200, _page.TestProgress.GetMax());
    }

    [Fact]
    public void GetMax_WhenNoAttribute_ReturnsDefault100()
    {
        _mockElement.Setup(e => e.GetAttribute("max")).Returns((string?)null);

        Assert.Equal(100, _page.TestProgress.GetMax());
    }

    [Fact]
    public void GetPercentage_ReturnsCorrectPercentage()
    {
        _mockElement.Setup(e => e.GetAttribute("value")).Returns("75");
        _mockElement.Setup(e => e.GetAttribute("max")).Returns("150");

        Assert.Equal(50, _page.TestProgress.GetPercentage());
    }

    [Fact]
    public void GetValue_WhenNoAttribute_ReturnsZero()
    {
        _mockElement.Setup(e => e.GetAttribute("value")).Returns((string?)null);

        Assert.Equal(0, _page.TestProgress.GetValue());
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }

        public ProgressControl<TestPage> TestProgress => new(this, "test-progress");
    }
}
