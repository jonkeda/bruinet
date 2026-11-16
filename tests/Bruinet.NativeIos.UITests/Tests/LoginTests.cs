namespace Bruinet.NativeIos.UITests.Tests;

/// <summary>Sign in: fields, a button that enables, a busy moment, and the app bug R0 must report.</summary>
[Collection(GalleryCollection.Name)]
public sealed class LoginTests(GalleryFixture gallery)
{
    private LoginPage Open(string link = "login")
    {
        gallery.Open(link);
        var page = new LoginPage(gallery.Context);
        page.AssertIdle();
        return page;
    }

    [Fact]
    public void SigningIn_WaitsOutTheBusyMoment_AndGreetsTheUser()
    {
        var page = Open();

        page.UserName.SetText("daan");
        page.Password.SetText("s3cret");
        page.SignIn.Click();

        page.Status.AssertText("Welcome, daan");
    }

    [Fact]
    public void TheButton_IsDisabled_UntilBothFieldsAreFilled()
    {
        var page = Open();

        page.SignIn.AssertEnabled(false);
        page.UserName.SetText("daan");
        page.SignIn.AssertEnabled(false);
        page.Password.SetText("s3cret");
        page.SignIn.AssertEnabled(true);
    }

    [Fact]
    public void AFieldsHint_IsItsPlaceholder_AndNotItsText()
    {
        var page = Open();

        page.UserName.AssertTextEmpty(true);
        page.UserName.AssertPlaceholder("User name");
    }

    [Fact]
    public void TheToolbar_ShowsTheScreensTitle()
    {
        var page = Open();

        page.Bar.Title.AssertText("Sign in");
    }

    /// <summary>
    /// The R0 proof (plan step 10a): a button the app never enables fails as "disabled" within its
    /// budget, naming what it saw, and is never tapped.
    /// </summary>
    [Fact]
    public void AButtonThatNeverEnables_FailsAsDisabled_WithinItsBudget()
    {
        var page = Open("login?stuck=1");
        page.UserName.SetText("daan");
        page.Password.SetText("s3cret");

        var error = Record.Exception(() => page.SignIn.Click(timeoutMs: 2000));

        var notReady = Assert.IsType<ElementNotReadyException>(error);
        Assert.Equal(NotReadyReason.Disabled, notReady.Reason);
        page.Status.AssertText("Signed out");
    }
}
