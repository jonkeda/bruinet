using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Dialogs;

[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "ContentDialog")]
public class ContentDialogTests
{
    private readonly DialogsTestPage _page;

    public ContentDialogTests(MauiFixture fixture)
    {
        _page = fixture.NavigateToDialogs();
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task Alert_DismissesThroughScopedButton()
    {
        var dialog = _page.Dialog;

        _page.ShowAlertButton.Click();
        Assert.True(dialog.WaitExists(true, TestConstants.DefaultTestTimeoutMs));
        Assert.True(dialog.DialogButton("OK").Click().WaitExists(
            false, TestConstants.DefaultTestTimeoutMs));
        _page.Result.AssertText("alert dismissed");

        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task Confirmation_AcceptsThroughScopedButton()
    {
        var dialog = _page.Dialog;

        _page.ShowConfirmButton.Click();
        Assert.True(dialog.WaitExists(true, TestConstants.DefaultTestTimeoutMs));
        Assert.True(dialog.DialogButton("Yes").Click().WaitExists(
            false, TestConstants.DefaultTestTimeoutMs));
        _page.Result.AssertText("confirmed");

        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task Prompt_AcceptsTextThroughScopedControls()
    {
        var dialog = _page.Dialog;

        _page.ShowPromptButton.Click();
        Assert.True(dialog.WaitExists(true, TestConstants.DefaultTestTimeoutMs));
        dialog.PromptInput.SetText("Bruinet");
        Assert.True(dialog.DialogButton("OK").Click().WaitExists(
            false, TestConstants.DefaultTestTimeoutMs));
        _page.Result.AssertText("prompt: Bruinet");

        return Task.CompletedTask;
    }
}
