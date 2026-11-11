namespace Bruinet.Blazor.Tests.Controls;

public class SelectControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly TestPage _page;

    public SelectControlTests()
    {
        _mockContext = MockHtmlFactory.CreateMockContext();
        // The select starts on "option1"; selecting another option changes it (step 6 confirms that).
        _mockElement = MockHtmlFactory.CreateMockElement("option1");
        MockHtmlFactory.SetupFindElement(_mockContext, _mockElement);

        _page = new TestPage(_mockContext.Object);
    }

    [Fact]
    public void SelectByValue_CallsSelectOption()
    {
        // A value no option has is refused (review 04, finding 2), so the select has the option.
        var option = MockHtmlFactory.CreateMockElement("Option 1");
        option.Setup(e => e.GetAttribute("value")).Returns("opt1");
        _mockElement.Setup(e => e.FindElements(It.IsAny<Locator>())).Returns([option.Object]);

        _page.TestSelect.SelectByValue("opt1");

        _mockElement.Verify(e => e.SelectOption("opt1"), Times.Once);
    }

    [Fact]
    public void GetSelectedValue_ReturnsInputValue()
    {
        Assert.Equal("option1", _page.TestSelect.GetSelectedValue());
    }

    [Fact]
    public void SelectMultiple_CallsSelectOptionArray()
    {
        _page.TestSelect.SelectMultiple(["a", "b"]);

        _mockElement.Verify(e => e.SelectOption(It.Is<string[]>(v => v.Length == 2 && v[0] == "a" && v[1] == "b")), Times.Once);
    }

    [Fact]
    public void SelectByText_FindsOptionAndSelects()
    {
        var optionElement = new Mock<IHtmlElement>();
        optionElement.Setup(e => e.Text).Returns("Option One");
        optionElement.Setup(e => e.GetAttribute("value")).Returns("opt1");

        _mockElement.Setup(e => e.FindElements(It.IsAny<Locator>()))
            .Returns(new List<IHtmlElement> { optionElement.Object }.AsReadOnly());

        _page.TestSelect.SelectByText("Option One");

        _mockElement.Verify(e => e.SelectOption("opt1"), Times.Once);
    }

    [Fact]
    public void SelectByText_NotFound_Throws()
    {
        _mockElement.Setup(e => e.FindElements(It.IsAny<Locator>()))
            .Returns(new List<IHtmlElement>().AsReadOnly());

        Assert.Throws<InvalidOperationException>(() => _page.TestSelect.SelectByText("Nonexistent"));
    }

    [Fact]
    public void Click_CallsElementClick()
    {
        _page.TestSelect.Click();

        _mockElement.Verify(e => e.Click(), Times.Once);
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }

        public SelectControl<TestPage> TestSelect => new(this, "test-select");
    }
}
