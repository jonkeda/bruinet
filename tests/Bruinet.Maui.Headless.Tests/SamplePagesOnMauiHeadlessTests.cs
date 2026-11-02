using Bruinet.Maui.UITests.Pages;
using Bruinet.Samples.Maui.App;
using MauiHeadless;

namespace Bruinet.Maui.Headless.Tests;

/// <summary>
/// The sample app's Buttons and Toggle pages, driven by the device suite's own page objects through
/// the MauiHeadless head: the same members the Windows and Android runs call, headless.
/// </summary>
public sealed class SamplePagesOnMauiHeadlessTests : IDisposable
{
    private readonly MauiTestContext _ctx = new();

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void A_button_runs_its_command_and_the_status_says_so()
    {
        var page = new ButtonsTestPage(_ctx.Render<ButtonsPage>().AsBruinetContext());

        page.StatusLabel.AssertText("Ready. Click any button to test.");
        page.TestButton.Click();
        page.TestButton.Click();

        page.StatusLabel.AssertTextContains("Button tapped 2 times");
        page.ResetButton.Click();
        page.StatusLabel.AssertText("Ready. Click any button to test.");
    }

    [Fact]
    public void Toggles_reach_the_view_model_through_two_way_bindings()
    {
        var page = new ToggleTestPage(_ctx.Render<TogglePage>().AsBruinetContext());

        page.TestCheckBox.Check();
        page.TestSwitch.SetOn(true);
        page.TestRadioButton2.Select();

        page.CheckBoxStatusLabel.AssertTextContains("checked");
        page.SwitchStatusLabel.AssertTextContains("on");
        page.RadioButtonStatusLabel.AssertTextContains("2");
        Assert.True(page.TestSwitch.IsOn() == true);
        Assert.True(page.TestCheckBox.IsChecked() == true);
    }

    [Fact]
    public void The_page_object_finds_its_page_by_its_root_AutomationId()
    {
        var page = new ButtonsTestPage(_ctx.Render<ButtonsPage>().AsBruinetContext());

        Assert.True(page.IsLoaded());
        page.TestImageButton.AssertEnabled();
    }

    [Fact]
    public void A_control_type_and_name_locator_finds_the_button_and_not_a_label()
    {
        var app = _ctx.Render<ButtonsPage>().AsBruinetContext().AppElement;

        Assert.Single(app.FindElements(new Bruinet.Core.Locators.Locator("Button", "Reset")));
        Assert.Single(app.FindElements(new Bruinet.Core.Locators.Locator("text", "Basic Button")));
        Assert.Empty(app.FindElements(new Bruinet.Core.Locators.Locator("Button", "Basic Button")));
    }

    [Fact]
    public void The_context_says_it_runs_on_MauiHeadless()
    {
        var context = _ctx.Render<ButtonsPage>().AsBruinetContext();

        Assert.Equal(Bruinet.Maui.Enums.MauiPlatform.Headless, context.Platform);
    }
}
