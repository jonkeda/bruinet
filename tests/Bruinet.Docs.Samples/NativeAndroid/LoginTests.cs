// docs/getting-started/quick-start-native-android.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:test
using Bruinet.NativeAndroid.Testing;
using Xunit;

namespace MyAndroidApp.UITests;

// The session from the APPIUM_* variables: APPIUM_APP_PATH, APPIUM_APP_PACKAGE, APPIUM_DEVICE_NAME.
public sealed class AppFixture : NativeAndroidTestFixtureBase, IAsyncLifetime
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
