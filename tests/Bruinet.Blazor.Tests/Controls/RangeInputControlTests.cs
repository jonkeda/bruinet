namespace Bruinet.Blazor.Tests.Controls;

public class RangeInputControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly TestPage _page;

    public RangeInputControlTests()
    {
        _mockContext = MockHtmlFactory.CreateMockContext();
        // The range starts at 75; filling another value changes it (step 6 confirms that).
        _mockElement = MockHtmlFactory.CreateMockElement("75");
        MockHtmlFactory.SetupFindElement(_mockContext, _mockElement);

        _mockElement.Setup(e => e.GetAttribute("min")).Returns("0");
        _mockElement.Setup(e => e.GetAttribute("max")).Returns("100");
        _mockElement.Setup(e => e.GetAttribute("step")).Returns("1");

        _page = new TestPage(_mockContext.Object);
    }

    [Fact]
    public void GetValue_ReturnsDoubleValue()
    {
        Assert.Equal(75, _page.TestRange.GetValue());
    }

    [Fact]
    public void GetValue_WhenNonNumeric_ReturnsNull()
    {
        _mockElement.Setup(e => e.InputValue).Returns("abc");

        Assert.Null(_page.TestRange.GetValue());
    }

    [Fact]
    public void SetValue_FillsValue()
    {
        _page.TestRange.SetValue(50.5);

        _mockElement.Verify(e => e.Fill("50.5"), Times.Once);
    }

    [Fact]
    public void GetMinimum_ReturnsAttribute()
    {
        Assert.Equal(0, _page.TestRange.GetMinimum());
    }

    [Fact]
    public void GetMaximum_ReturnsAttribute()
    {
        Assert.Equal(100, _page.TestRange.GetMaximum());
    }

    [Fact]
    public void GetStep_ReturnsAttribute()
    {
        Assert.Equal(1, _page.TestRange.GetStep());
    }

    [Fact]
    public void Increment_FillsTheNextStep()
    {
        _page.TestRange.Increment();

        _mockElement.Verify(e => e.Fill("76"), Times.Once);
    }

    [Fact]
    public void SetValue_FillsWholeNumber()
    {
        _page.TestRange.SetValue(42);

        _mockElement.Verify(e => e.Fill("42"), Times.Once);
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }

        public RangeInputControl<TestPage> TestRange => new(this, "test-range");
    }
}
