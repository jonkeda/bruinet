namespace Bruinet.NativeIos.UITests.Tests;

/// <summary>An alert, an alert with a text field, an action sheet, the system's permission prompt, and a busy page.</summary>
[Collection(GalleryCollection.Name)]
public sealed class DialogsTests(GalleryFixture gallery)
{
    private DialogsPage Open()
    {
        gallery.Open("dialogs");
        var page = new DialogsPage(gallery.Context);
        page.AssertIdle();
        return page;
    }

    [Fact]
    public void AnAlert_ShowsItsTitleAndMessage_AndItsButtonsAnswer()
    {
        var page = Open();

        page.ShowAlert.Click();
        page.Alert.Title.AssertText("Delete everything?");
        page.Alert.Message.AssertText("This cannot be undone.");
        page.Alert.Button("Cancel").Click();

        page.Status.AssertText("Kept");
        page.Alert.AssertExists(false);
    }

    [Fact]
    public void AnAlertsTextField_TakesText()
    {
        var page = Open();

        page.Rename.Click();
        page.Alert.TextField().SetText("Bob");
        page.Alert.Button("Rename").Click();

        page.Status.AssertText("Renamed to Bob");
    }

    [Fact]
    public void AnActionSheet_ShowsItsTitle_AndAnActionAnswers()
    {
        var page = Open();

        page.ShowSheet.Click();
        page.Sheet.Title.AssertText("Share");
        page.Sheet.Action("Copy").Click();

        page.Status.AssertText("Copy chosen");
    }

    /// <summary>
    /// The privacy prompt is Springboard's, not in the app's tree; the prompt is found there, and
    /// answering it gives the app back. The session installs the app fresh, so the prompt always
    /// comes; the tap waits for it.
    /// </summary>
    [Fact]
    public void ThePermissionPrompt_IsAnswered_AndTheAppCarriesOn()
    {
        var page = Open();

        page.AskCamera.Click();
        page.Permission.Allow.Click();

        page.Status.AssertText("Camera allowed");
    }

    /// <summary>A page that is busy for two seconds: the next call waits it out, then reads the result.</summary>
    [Fact]
    public void ABusyPage_IsWaitedOut()
    {
        var page = Open();

        page.Work.Click();
        page.WaitBusy(true, timeoutMs: 2000);

        page.Status.AssertText("Work done");
        Assert.False(page.IsBusy());
    }
}
