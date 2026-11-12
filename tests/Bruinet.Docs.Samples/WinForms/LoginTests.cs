// docs/getting-started/quick-start-winforms.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:test
using Bruinet.WinForms.Testing;
using Xunit;

namespace MyWinFormsApp.UITests;

public sealed class AppFixture : WinFormsTestFixtureBase
{
    protected override string GetDefaultAppPath()
        => Path.GetFullPath(@"..\..\..\..\MyWinFormsApp\bin\Debug\net10.0-windows\MyWinFormsApp.exe");
}

public sealed class LoginTests(AppFixture app) : IClassFixture<AppFixture>
{
    [Fact]
    public void SigningIn_ShowsTheUser()
    {
        var page = new LoginPage(app.Context);

        page.Username.SetText("Alice");
        page.Password.SetText("secret");
        page.SignIn.Click();

        page.Status.AssertTextContains("Logged in as Alice");
    }
}
#endregion
