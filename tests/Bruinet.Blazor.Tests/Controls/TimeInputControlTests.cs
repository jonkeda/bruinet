namespace Bruinet.Blazor.Tests.Controls;

public class TimeInputControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly TestPage _page;

    public TimeInputControlTests()
    {
        _mockContext = MockHtmlFactory.CreateMockContext();
        _mockElement = MockHtmlFactory.CreateMockElement();
        MockHtmlFactory.SetupFindElement(_mockContext, _mockElement);
        _page = new TestPage(_mockContext.Object);
    }

    [Fact]
    public void GetTime_ReturnsCurrentTime()
    {
        _mockElement.Setup(e => e.InputValue).Returns("14:30");

        Assert.Equal(new TimeOnly(14, 30), _page.TestTimeInput.GetTime());
    }

    [Fact]
    public void SetTime_FillsFormattedTime()
    {
        _page.TestTimeInput.SetTime(new TimeOnly(10, 15));

        _mockElement.Verify(e => e.Fill("10:15"), Times.Once);
    }

    [Fact]
    public void GetMinimum_ReturnsMinAttribute()
    {
        _mockElement.Setup(e => e.GetAttribute("min")).Returns("08:00");

        Assert.Equal(new TimeOnly(8, 0), _page.TestTimeInput.GetMinimum());
    }

    [Fact]
    public void GetMaximum_ReturnsMaxAttribute()
    {
        _mockElement.Setup(e => e.GetAttribute("max")).Returns("17:00");

        Assert.Equal(new TimeOnly(17, 0), _page.TestTimeInput.GetMaximum());
    }

    [Fact]
    public void IsExists_WhenExists_ReturnsTrue()
    {
        Assert.True(_page.TestTimeInput.IsExists());
    }

    [Fact]
    public void IsEnabled_WhenEnabled_ReturnsTrue()
    {
        Assert.Equal(true, _page.TestTimeInput.IsEnabled());
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }

        public TimeInputControl<TestPage> TestTimeInput => new(this, "test-time");
    }

    // Proposal 8, rule 1: Html's input implements Core's capability, converting at the boundary.
    [Fact]
    public void Through_the_core_time_capability_a_time_is_a_TimeSpan()
    {
        _mockElement.Setup(e => e.InputValue).Returns("14:30");
        ITimeControlObject<TestPage> time = _page.TestTimeInput;

        Assert.Equal(new TimeSpan(14, 30, 0), time.GetTime());
    }
}
