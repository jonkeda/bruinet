using Microsoft.Maui.Controls;
using MauiHeadless.Assertions;
using MauiHeadless.Dialogs;
using MauiHeadless.Inspection;
using MauiHeadless.Interaction;
using Xunit;

namespace MauiHeadless.Tests.Framework;

/// <summary>The app's alerts, prompts and action sheets: shown over the page, answered by clicking, the app's await resumed.</summary>
public sealed class DialogTests
{
    private static (ContentPage Page, Label Result) Asking(Func<ContentPage, Task<string>> ask)
    {
        var result = new Label { AutomationId = "Result", Text = "none" };
        var button = new Button { AutomationId = "Ask", Text = "Ask" };
        var page = new ContentPage { Content = new VerticalStackLayout { button, result } };
        button.Clicked += async (_, _) => result.Text = await ask(page);
        return (page, result);
    }

    [Fact]
    public void An_alert_is_shown_over_the_page_and_its_answer_reaches_the_app()
    {
        using var ctx = new MauiTestContext();
        var (made, _) = Asking(async page => (await page.DisplayAlertAsync("Delete?", "It cannot be undone.", "Delete", "Keep")).ToString());
        var page = ctx.Render(made);

        page.Find<Button>("Ask").Click();

        var dialog = page.Find<TestDialog>();
        Assert.Equal(DialogKind.Alert, dialog.Kind);
        dialog.Find<Label>("DialogMessage").Should().HaveText("It cannot be undone.");

        dialog.Choose("Delete");

        page.Find<Label>("Result").Should().HaveText("True");
        Assert.Empty(page.FindAll<TestDialog>());
    }

    [Fact]
    public void Dismissed_as_by_a_back_key_an_alert_answers_false_and_is_gone()
    {
        using var ctx = new MauiTestContext();
        var (made, _) = Asking(async page => (await page.DisplayAlertAsync("Delete?", "It cannot be undone.", "Delete", "Keep")).ToString());
        var page = ctx.Render(made);

        page.Find<Button>("Ask").Click();
        ctx.Dispatcher.Run(() => page.Find<TestDialog>().Dismiss());

        page.Find<Label>("Result").Should().HaveText("False");
        Assert.Empty(page.FindAll<TestDialog>());
    }

    [Fact]
    public void A_prompt_takes_what_is_typed()
    {
        using var ctx = new MauiTestContext();
        var (made, _) = Asking(async page => await page.DisplayPromptAsync("Name", "Who are you?", "OK", "Cancel") ?? "cancelled");
        var page = ctx.Render(made);

        page.Find<Button>("Ask").Click();
        page.Find<TestDialog>().Type("Ada").Choose("OK");

        page.Find<Label>("Result").Should().HaveText("Ada");
    }

    [Fact]
    public void An_action_sheet_answers_the_choice()
    {
        using var ctx = new MauiTestContext();
        var (made, _) = Asking(async page => await page.DisplayActionSheetAsync("Share", "Cancel", null, "Mail", "Radio") ?? "none");
        var page = ctx.Render(made);

        page.Find<Button>("Ask").Click();
        var sheet = page.Find<TestDialog>();
        Assert.Equal(["Mail", "Radio"], sheet.Choices);

        sheet.Choose("Radio");

        page.Find<Label>("Result").Should().HaveText("Radio");
    }
}
