namespace Bruinet.Blazor.Tests.Controls;

public class DateInputControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly TestPage _page;

    public DateInputControlTests()
    {
        _mockContext = MockHtmlFactory.CreateMockContext();
        _mockElement = MockHtmlFactory.CreateMockElement();
        MockHtmlFactory.SetupFindElement(_mockContext, _mockElement);
        _page = new TestPage(_mockContext.Object);
    }

    [Fact]
    public void GetDate_ReturnsCurrentDate()
    {
        _mockElement.Setup(e => e.InputValue).Returns("2025-01-15");

        Assert.Equal(new DateOnly(2025, 1, 15), _page.TestDateInput.GetDate());
    }

    [Fact]
    public void SetDate_FillsFormattedDate()
    {
        _page.TestDateInput.SetDate(new DateOnly(2025, 6, 20));

        _mockElement.Verify(e => e.Fill("2025-06-20"), Times.Once);
    }

    [Fact]
    public void GetMinimum_ReturnsMinAttribute()
    {
        _mockElement.Setup(e => e.GetAttribute("min")).Returns("2020-01-01");

        Assert.Equal(new DateOnly(2020, 1, 1), _page.TestDateInput.GetMinimum());
    }

    [Fact]
    public void GetMaximum_ReturnsMaxAttribute()
    {
        _mockElement.Setup(e => e.GetAttribute("max")).Returns("2030-12-31");

        Assert.Equal(new DateOnly(2030, 12, 31), _page.TestDateInput.GetMaximum());
    }

    [Fact]
    public void IsExists_WhenExists_ReturnsTrue()
    {
        Assert.True(_page.TestDateInput.Exists());
    }

    [Fact]
    public void IsEnabled_WhenEnabled_ReturnsTrue()
    {
        Assert.True(_page.TestDateInput.IsEnabled());
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }

        public DateInputControl<TestPage> TestDateInput => new(this, "test-date");
    }

    // Proposal 8, rule 1: Html's input implements Core's capability, converting at the boundary.
    [Fact]
    public void Through_the_core_date_capability_a_date_is_a_DateTime_at_midnight()
    {
        _mockElement.Setup(e => e.InputValue).Returns("2025-01-15");
        IDateControlObject<TestPage> date = _page.TestDateInput;

        Assert.Equal(new System.DateTime(2025, 1, 15), date.GetDate());
    }

    [Fact]
    public void Through_the_core_date_capability_the_time_of_day_is_dropped()
    {
        IDateControlObject<TestPage> date = _page.TestDateInput;

        date.SetDate(new System.DateTime(2025, 6, 20, 14, 30, 0));

        _mockElement.Verify(e => e.Fill("2025-06-20"), Times.Once);
    }
}
