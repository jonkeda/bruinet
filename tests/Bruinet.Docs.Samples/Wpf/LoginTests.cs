// docs/getting-started/quick-start-wpf.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:test
using Bruinet.Wpf.Testing;
using Xunit;

namespace MyWpfApp.UITests;

public sealed class AppFixture : WpfTestFixtureBase
{
    protected override string GetDefaultAppPath()
        => Path.GetFullPath(@"..\..\..\..\MyWpfApp\bin\Debug\net10.0-windows\MyWpfApp.exe");
}

public sealed class LoginTests(AppFixture app) : IClassFixture<AppFixture>
{
    [Fact]
    public void SigningIn_GreetsTheUser()
    {
        var page = new LoginPage(app.Context);

        page.Username.SetText("demo");
        page.Password.SetText("password");
        page.SignIn.Click();

        page.Status.AssertText("Welcome, demo");
    }
}
#endregion
