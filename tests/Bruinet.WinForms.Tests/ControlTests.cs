namespace Bruinet.WinForms.Tests;

/// <summary>
/// WinForms's controls act through UI Automation's patterns, once, and confirm the effect (R0, R3).
/// </summary>
public class ControlTests
{
    private readonly FakeApp _app = new();

    [Fact]
    public void SetText_WritesTheValuePatternOnce_AndConfirmsIt()
    {
        string? value = "old";
        var field = FakeApp.Element();
        field.Setup(e => e.Value).Returns(() => value);
        field.Setup(e => e.SetValue(It.IsAny<string>())).Callback<string>(text => value = text);
        _app.Given("Target", () => field.Object);

        _app.Page().Input.SetText("demo");

        field.Verify(e => e.SetValue("demo"), Times.Once);
        field.Verify(e => e.SendKeys(It.IsAny<string>(), It.IsAny<TextInputMethod>()), Times.Never);
    }

    [Fact]
    public void SetText_ThatDoesNotTake_FailsWithoutWritingAgain()
    {
        var field = FakeApp.Element();
        field.Setup(e => e.Value).Returns("unchanged");
        _app.Given("Target", () => field.Object);

        Assert.ThrowsAny<Exception>(() => _app.Page().Input.SetText("demo", timeoutMs: 200));

        field.Verify(e => e.SetValue("demo"), Times.Once);
    }

    [Fact]
    public void A_PasswordBox_WritesThePasswordOnce_WithoutReadingItBack()
    {
        var field = FakeApp.Element();
        _app.Given("Target", () => field.Object);

        _app.Page().Secret.SetText("secret");

        field.Verify(e => e.SetValue("secret"), Times.Once);
        field.Verify(e => e.Value, Times.Never);
    }

    [Fact]
    public void Check_TogglesOnce_AndConfirmsTheState()
    {
        bool? state = false;
        var box = FakeApp.Element();
        box.Setup(e => e.Checked).Returns(() => state);
        box.Setup(e => e.Click()).Callback(() => state = !state);
        _app.Given("Target", () => box.Object);

        _app.Page().Agree.Check();

        box.Verify(e => e.Click(), Times.Once);
        Assert.True(state);
    }

    [Fact]
    public void Check_OnACheckedBox_DoesNothing()
    {
        var box = FakeApp.Element();
        box.Setup(e => e.Checked).Returns(true);
        _app.Given("Target", () => box.Object);

        _app.Page().Agree.Check();

        box.Verify(e => e.Click(), Times.Never);
    }

    [Fact]
    public void SelectByText_OnAComboBox_Expands_SelectsTheItem_AndCollapses()
    {
        string? selected = "User";
        var expanded = false;
        var admin = FakeApp.Element();
        admin.Setup(e => e.Name).Returns("Admin");
        admin.Setup(e => e.Select()).Callback(() => selected = "Admin");
        var user = FakeApp.Element();
        user.Setup(e => e.Name).Returns("User");
        var combo = FakeApp.Element();
        combo.Setup(e => e.Value).Returns(() => selected);
        combo.Setup(e => e.IsExpanded).Returns(() => expanded);
        combo.Setup(e => e.Expand()).Callback(() => expanded = true);
        combo.Setup(e => e.Collapse()).Callback(() => expanded = false);
        combo.Setup(e => e.FindElements(It.IsAny<Locator>())).Returns(() => expanded ? [user.Object, admin.Object] : []);
        _app.Given("Target", () => combo.Object);

        _app.Page().Role.SelectByText("Admin");

        admin.Verify(e => e.Select(), Times.Once);
        combo.Verify(e => e.Expand(), Times.Once);
        combo.Verify(e => e.Collapse(), Times.Once);
        Assert.False(expanded);
    }

    [Fact]
    public void SelectByText_ForAnItemThatIsNotThere_FailsNamingTheItems_AndCollapses()
    {
        var expanded = false;
        var user = FakeApp.Element();
        user.Setup(e => e.Name).Returns("User");
        var combo = FakeApp.Element();
        combo.Setup(e => e.Value).Returns("User");
        combo.Setup(e => e.IsExpanded).Returns(() => expanded);
        combo.Setup(e => e.Expand()).Callback(() => expanded = true);
        combo.Setup(e => e.Collapse()).Callback(() => expanded = false);
        combo.Setup(e => e.FindElements(It.IsAny<Locator>())).Returns(() => expanded ? [user.Object] : []);
        _app.Given("Target", () => combo.Object);

        var error = Assert.ThrowsAny<Exception>(() => _app.Page().Role.SelectByText("Guest", timeoutMs: 300));

        Assert.Contains("'User'", error.ToString(), StringComparison.Ordinal);
        Assert.False(expanded);
    }

    [Fact]
    public void SetValue_OnAProgressBar_WritesTheRangeValueOnce_AndReadsItsBoundsFromThePattern()
    {
        double? value = 50;
        var slider = FakeApp.Element();
        slider.Setup(e => e.RangeValue).Returns(() => value);
        slider.Setup(e => e.RangeMinimum).Returns(0);
        slider.Setup(e => e.RangeMaximum).Returns(100);
        slider.Setup(e => e.RangeSmallChange).Returns(5);
        slider.Setup(e => e.SetRangeValue(It.IsAny<double>())).Callback<double>(v => value = v);
        _app.Given("Target", () => slider.Object);

        var page = _app.Page();
        page.Volume.SetValue(75);
        page.Volume.Increment();

        Assert.Equal(80, value);
        Assert.Equal(100, page.Volume.GetMaximum());
    }

    [Fact]
    public void Expand_ExpandsOnce_AndConfirms()
    {
        bool? expanded = false;
        var expander = FakeApp.Element();
        expander.Setup(e => e.IsExpanded).Returns(() => expanded);
        expander.Setup(e => e.Expand()).Callback(() => expanded = true);
        _app.Given("Target", () => expander.Object);

        _app.Page().Details.Expand();

        expander.Verify(e => e.Expand(), Times.Once);
    }

    [Fact]
    public void A_Page_ShowingTheBruinetBusyElement_IsBusy()
    {
        _app.Given(PageObjectBase<TestPage>.BusyAutomationId, () => FakeApp.Element().Object);

        var readiness = _app.Page().ProbeReadiness();

        Assert.True(readiness.IsBusy);
    }
}
