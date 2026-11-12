// docs/getting-started/quick-start-maui.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:test
using Bruinet.Maui.Enums;
using Bruinet.Maui.Testing;
using Xunit;

namespace MyMauiApp.UITests;

public sealed class AppFixture : MauiTestFixtureBase
{
    protected override string GetDefaultAppPath(MauiPlatform platform)
        => Path.GetFullPath(@"..\..\..\..\MyMauiApp\bin\Debug\net10.0-windows10.0.19041.0\win-x64\MyMauiApp.exe");
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
