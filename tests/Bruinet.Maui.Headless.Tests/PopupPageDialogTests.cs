using Bruinet.Maui.Controls.Dialogs;
using Bruinet.Maui.Interfaces;
using MauiHeadless;

namespace Bruinet.Maui.Headless.Tests;

/// <summary>
/// A popup page as the active dialog: a modal page of a type the context's <c>DialogPages</c> names, as
/// Mopups' and MauiCore's popups are WinUI popups on Windows. Its buttons are tap-only views with a label,
/// its field an Entry, and <see cref="ContentDialog{TParent}"/> drives it as it drives an alert.
/// </summary>
public sealed class PopupPageDialogTests : IDisposable
{
    private readonly MauiTestContext _ctx;
    private readonly Label _result = new() { AutomationId = "Result", Text = "none" };
    private readonly ContentPage _first;
    private readonly IMauiTestContext _context;

    public PopupPageDialogTests()
    {
        var options = new MauiTestContextOptions();
        options.DialogPages.Add(typeof(PopupPage));
        _ctx = new MauiTestContext(options);

        var open = new Button { AutomationId = "Open", Text = "Open" };
        var plain = new Button { AutomationId = "Plain", Text = "Plain" };
        var error = new Button { AutomationId = "Error", Text = "Error" };
        _first = new ContentPage { AutomationId = "First", Content = new VerticalStackLayout { open, plain, error, _result } };
        open.Clicked += async (_, _) => await _first.Navigation.PushModalAsync(new PopupPage(_result));
        plain.Clicked += async (_, _) => await _first.Navigation.PushModalAsync(new ContentPage { AutomationId = "Plain page", Title = "Plain" });
        error.Clicked += async (_, _) =>
        {
            await _first.Navigation.PushModalAsync(new PopupPage(_result));
            await _first.DisplayAlertAsync("Error", "It failed.", "OK");
        };
        _context = _ctx.Render(new NavigationPage(_first)).AsBruinetContext();
    }

    public void Dispose() => _ctx.Dispose();

    private ContentDialog<AppRoot> Dialog => new(new AppRoot(_context));

    private void Click(string automationId) => new Bruinet.Maui.Controls.Buttons.Button<AppRoot>(new AppRoot(_context), automationId).Click();

    [Fact]
    public void A_registered_popup_page_is_the_active_dialog_and_its_tapped_label_answers()
    {
        Click("Open");

        Dialog.AssertAsks("Discard").Choose("Yes");

        Dialog.AssertNotShown();
        Assert.Equal("discarded", _result.Text);
    }

    [Fact]
    public void Its_entry_is_the_prompt_input()
    {
        Click("Open");

        Dialog.PromptInput.SetText("12");
        Dialog.Choose("No");

        Dialog.AssertNotShown();
        Assert.Equal("kept 12", _result.Text);
    }

    [Fact]
    public void A_popup_that_closes_itself_after_async_work_is_gone_when_the_work_is()
    {
        Click("Open");

        Dialog.Choose("Save");

        Dialog.AssertNotShown();
        Assert.Equal("saved", _result.Text);
    }

    [Fact]
    public void An_alert_raised_while_a_popup_is_open_is_on_top_of_it()
    {
        Click("Error");

        Dialog.AssertAsks("Error").Choose("OK");

        Dialog.AssertAsks("Discard");
    }

    [Fact]
    public void An_ordinary_modal_page_is_no_dialog()
    {
        Click("Plain");

        Dialog.AssertNotShown();
    }

    /// <summary>A popup as MauiCore draws one: a title, a field, and buttons that are a label in a tapped grid.</summary>
    private sealed class PopupPage : ContentPage
    {
        public PopupPage(Label result)
        {
            Title = "Discard";
            AutomationId = "DiscardPopup";
            var amount = new Entry { AutomationId = "Amount" };
            Content = new VerticalStackLayout
            {
                new Label { Text = "Discard your changes?" },
                amount,
                Tapped("Yes", () => { result.Text = "discarded"; return Task.CompletedTask; }),
                Tapped("No", () => { result.Text = $"kept {amount.Text}"; return Task.CompletedTask; }),
                Tapped("Save", async () => { await Task.Run(() => { }); result.Text = "saved"; }),
            };
        }

        private Grid Tapped(string text, Func<Task> work)
        {
            var grid = new Grid { AutomationId = $"Popup_{text}" };
            grid.Add(new Label { Text = text });
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                await work();
                await Navigation.PopModalAsync();
            };
            grid.GestureRecognizers.Add(tap);
            return grid;
        }
    }
}
