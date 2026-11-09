using System.Globalization;

namespace Bruinet.WinForms.Tests;

/// <summary>
/// The WinForms controls whose value UI Automation shows as text: a track bar and a numeric up-down
/// (a number in a Value pattern, no RangeValue), and a date picker (a Value pattern that ignores
/// writes, set with the keyboard). Each acts once and confirms (R0, R3).
/// </summary>
public class WinFormsControlTests
{
    private readonly FakeApp _app = new();

    [Fact]
    public void A_TrackBar_IsSetByWritingItsValueText_AndConfirmed()
    {
        var text = "50";
        var bar = FakeApp.Element();
        bar.Setup(e => e.Value).Returns(() => text);
        bar.Setup(e => e.SetValue(It.IsAny<string>())).Callback<string>(value => text = value);
        _app.Given("Target", () => bar.Object);

        var page = _app.Page();
        page.Level.SetValue(75);

        Assert.Equal(75, page.Level.GetValue());
        Assert.Equal(100, page.Level.GetMaximum());
        bar.Verify(e => e.SetRangeValue(It.IsAny<double>()), Times.Never);
    }

    [Fact]
    public void A_NumericUpDown_WritesTheEditInsideIt()
    {
        var text = "8080";
        var edit = FakeApp.Element();
        edit.Setup(e => e.Value).Returns(() => text);
        edit.Setup(e => e.SetValue(It.IsAny<string>())).Callback<string>(value => text = value);
        var spinner = FakeApp.Element();
        spinner.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "Edit"))).Returns(edit.Object);
        _app.Given("Target", () => spinner.Object);

        var page = _app.Page();
        page.Port.SetValue(9090);

        Assert.Equal("9090", text);
        Assert.Equal(9090, page.Port.GetValue());
        spinner.Verify(e => e.SetValue(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void A_NumericUpDown_CommitsTheWrittenNumber_WithUpThenDown()
    {
        var text = "8080";
        var clicks = new List<string>();
        var edit = FakeApp.Element();
        edit.Setup(e => e.Value).Returns(() => text);
        edit.Setup(e => e.SetValue(It.IsAny<string>())).Callback<string>(value => text = value);
        var up = FakeApp.Element();
        up.Setup(e => e.Click()).Callback(() => clicks.Add("Up"));
        var down = FakeApp.Element();
        down.Setup(e => e.Click()).Callback(() => clicks.Add("Down"));
        var spinner = FakeApp.Element();
        spinner.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "Edit"))).Returns(edit.Object);
        spinner.Setup(e => e.FindElements(It.Is<Locator>(l => l.Value == "Button"))).Returns([up.Object, down.Object]);
        _app.Given("Target", () => spinner.Object);

        _app.Page().Port.SetValue(9090);

        Assert.Equal(["Up", "Down"], clicks);
    }

    [Fact]
    public void A_NumericUpDown_AtItsMaximum_CommitsWithDownThenUp()
    {
        var text = "1";
        var clicks = new List<string>();
        var edit = FakeApp.Element();
        edit.Setup(e => e.Value).Returns(() => text);
        edit.Setup(e => e.SetValue(It.IsAny<string>())).Callback<string>(value => text = value);
        var up = FakeApp.Element();
        up.Setup(e => e.Click()).Callback(() => clicks.Add("Up"));
        var down = FakeApp.Element();
        down.Setup(e => e.Click()).Callback(() => clicks.Add("Down"));
        var spinner = FakeApp.Element();
        spinner.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "Edit"))).Returns(edit.Object);
        spinner.Setup(e => e.FindElements(It.Is<Locator>(l => l.Value == "Button"))).Returns([up.Object, down.Object]);
        _app.Given("Target", () => spinner.Object);

        _app.Page().Port.SetValue(65535);

        Assert.Equal(["Down", "Up"], clicks);
    }

    [Fact]
    public void A_TrackBar_ThatDoesNotTakeTheValue_FailsWithoutWritingAgain()
    {
        var bar = FakeApp.Element();
        bar.Setup(e => e.Value).Returns("50");
        _app.Given("Target", () => bar.Object);

        Assert.ThrowsAny<Exception>(() => _app.Page().Level.SetValue(75, timeoutMs: 200));

        bar.Verify(e => e.SetValue(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void A_ComboBox_WithAWritableValue_SelectsByWritingIt_WithoutOpening()
    {
        var selected = "Admin";
        var combo = FakeApp.Element();
        combo.Setup(e => e.IsReadOnly).Returns(false);
        combo.Setup(e => e.Value).Returns(() => selected);
        combo.Setup(e => e.SetValue(It.IsAny<string>())).Callback<string>(text => selected = text);
        _app.Given("Target", () => combo.Object);

        new ComboBox<TestPage>(_app.Page(), "Target").SelectByText("User");

        combo.Verify(e => e.SetValue("User"), Times.Once);
        combo.Verify(e => e.Expand(), Times.Never);
    }

    [Fact]
    public void A_ComboBox_IgnoringAnUnknownText_FailsTheSelection()
    {
        var combo = FakeApp.Element();
        combo.Setup(e => e.IsReadOnly).Returns(false);
        combo.Setup(e => e.Value).Returns("Admin");
        _app.Given("Target", () => combo.Object);

        Assert.ThrowsAny<Exception>(() => new ComboBox<TestPage>(_app.Page(), "Target").SelectByText("Nobody", timeoutMs: 200));

        combo.Verify(e => e.SetValue("Nobody"), Times.Once);
    }

    [Fact]
    public void A_DatePicker_TypesEachFieldOfTheShortDate_AndConfirmsTheDateShown()
    {
        var target = new DateTime(2025, 6, 15);
        var shown = new DateTime(2026, 11, 10).ToString("d", CultureInfo.CurrentCulture);
        var keys = new List<string>();
        var picker = FakeApp.Element();
        picker.Setup(e => e.Value).Returns(() => shown);
        picker.Setup(e => e.PressKey(It.IsAny<string>())).Callback<string>(key =>
        {
            keys.Add(key);
            // The app's answer: once the second pass types, the picker shows the date.
            if (keys.Count(k => k == "HOME") == 2 && key != "HOME")
                shown = target.ToString("d", CultureInfo.CurrentCulture);
        });
        _app.Given("Target", () => picker.Object);

        _app.Page().Start.SetDate(target);

        Assert.Equal(2, keys.Count(k => k == "HOME"));
        Assert.Equal(target, _app.Page().Start.GetDate());
        picker.Verify(e => e.SetValue(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void A_DatePicker_ShowingTheDateAlready_TypesNothing()
    {
        var picker = FakeApp.Element();
        picker.Setup(e => e.Value).Returns(DateTime.Today.ToString("d", CultureInfo.CurrentCulture));
        _app.Given("Target", () => picker.Object);

        _app.Page().Start.SetDate(DateTime.Today);

        picker.Verify(e => e.PressKey(It.IsAny<string>()), Times.Never);
    }
}
