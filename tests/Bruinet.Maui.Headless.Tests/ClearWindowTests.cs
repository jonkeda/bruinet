using MauiHeadless;
using MauiHeadless.Dialogs;
using MauiHeadless.Interaction;

namespace Bruinet.Maui.Headless.Tests;

/// <summary>
/// What the head promises between tests: a back closes an open dialog before it pops a page, and
/// <see cref="MauiHeadlessDriver.ClearWindow"/> leaves the window as a fresh launch shows it, saying what it cleared.
/// </summary>
public sealed class ClearWindowTests : IDisposable
{
    private readonly MauiTestContext _ctx = new();
    private readonly ContentPage _second = new() { AutomationId = "Second", Content = new Entry { AutomationId = "Name" } };
    private readonly Label _result = new() { AutomationId = "Result", Text = "none" };
    private readonly Button _ask = new() { AutomationId = "Ask", Text = "Ask" };
    private readonly Button _push = new() { AutomationId = "Push", Text = "Push" };
    private readonly MauiHeadlessDriver _driver;

    public ClearWindowTests()
    {
        var first = new ContentPage { AutomationId = "First", Content = new VerticalStackLayout { _ask, _push, _result } };
        _ask.Clicked += async (_, _) => _result.Text = (await first.DisplayAlertAsync("Discard?", "Your changes are lost.", "Yes", "No")).ToString();
        _push.Clicked += async (_, _) => await first.Navigation.PushAsync(_second);
        _driver = new MauiHeadlessDriver(_ctx.Render(new NavigationPage(first)));
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void A_back_with_an_alert_open_closes_the_alert_and_keeps_the_page()
    {
        _push.Click();
        _driver.TestContext.Dispatcher.Run(() => { _ = _second.DisplayAlertAsync("Leave?", null, "Yes", "No"); });
        Assert.NotNull(DialogRequests.Top(_driver.Window));

        _driver.NavigateBack();

        Assert.Null(DialogRequests.Top(_driver.Window));
        Assert.Equal("Second", _driver.CurrentRoute());
    }

    [Fact]
    public void A_dismissed_alert_answers_the_app_as_a_back_key_does()
    {
        _ask.Click();

        _driver.NavigateBack();

        Assert.Equal("False", _result.Text);
    }

    [Fact]
    public void Clearing_the_window_dismisses_dialogs_pops_pages_and_takes_focus_and_says_so()
    {
        _push.Click();
        ((Entry)_second.Content).SetFocus();
        _driver.TestContext.Dispatcher.Run(() => { _ = _second.DisplayAlertAsync("Leave?", null, "Yes", "No"); });
        _driver.TestContext.Dispatcher.Run(() => { _ = _second.Navigation.PushModalAsync(new ContentPage { AutomationId = "Popup" }); });

        var cleared = _driver.ClearWindow();

        Assert.Equal(["focus on Name", "dialog 'Leave?'", "modal page Popup", "pushed page Second"], cleared);
        Assert.Null(DialogRequests.Top(_driver.Window));
        Assert.Equal("First", _driver.CurrentRoute());
        Assert.False(((Entry)_second.Content).IsFocused);
        Assert.Empty(_driver.ClearWindow());
    }
}
