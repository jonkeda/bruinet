// docs/getting-started/quick-start-native-ios.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:test
using Bruinet.NativeIos.Testing;
using Xunit;

namespace MyIosApp.UITests;

// The session from the APPIUM_* variables: APPIUM_APP_PATH, APPIUM_BUNDLE_ID, APPIUM_DEVICE_NAME.
public sealed class AppFixture : NativeIosTestFixtureBase, IAsyncLifetime
{
    Task IAsyncLifetime.InitializeAsync() => InitializeAsync();

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync();
}

public sealed class LoginTests(AppFixture app) : IClassFixture<AppFixture>
{
    [Fact]
    public void SigningIn_GreetsTheUser()
    {
        app.Context.NavigateTo("myapp://login");   // a deep link to the screen
        var page = new LoginPage(app.Context);

        page.UserName.SetText("daan");
        page.Password.SetText("s3cret");
        page.SignIn.Click();

        page.Status.AssertText("Welcome, daan");
    }
}
#endregion
