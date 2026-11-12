# Quick start: a native Android app

A first UI test of an Android app that is not a MAUI app (Views, AndroidX, Material), through Appium and
UiAutomator2, in five steps. The code below is compiled with Bruinet's build (`tests/Bruinet.Docs.Samples`), so it
is today's API.

## 1. The feed

Bruinet's packages are on the organisation's GitHub Packages feed, not nuget.org. Add it once, with a token that can
read packages (see [VERSIONING](../../VERSIONING.md)).

## 2. A test project

```bash
dotnet new xunit -n MyAndroidApp.UITests
cd MyAndroidApp.UITests
```

## 3. The one package

```bash
dotnet add package Bruinet.NativeAndroid
```

It brings `Bruinet.Appium` and `Bruinet.Core`. The tests need an emulator or a phone and an Appium 2 server with its
UiAutomator2 driver: the [native Android run guide](../run/native-android.md) sets them up.

## 4. A page object and a test

A page object names the screen's views by resource id (the package prefix is added); a test uses only those.

<!-- sample: tests/Bruinet.Docs.Samples/NativeAndroid/LoginPage.cs#page -->
```csharp
using Bruinet.Core.Locators;
using Bruinet.NativeAndroid.Controls.Buttons;
using Bruinet.NativeAndroid.Controls.Text;
using Bruinet.NativeAndroid.Interfaces;
using Bruinet.NativeAndroid.Pages;

namespace MyAndroidApp.UITests;

public sealed class LoginPage(INativeAndroidTestContext context) : PageObjectBase<LoginPage>(context)
{
    public override string? Activity => ".LoginActivity";
    protected override Locator? ReadyLocator => Locator.ById("login");

    public EditText<LoginPage> UserName => new(this, "username");
    public EditText<LoginPage> Password => new(this, "password");
    public Button<LoginPage> SignIn => new(this, "login");
    public TextView<LoginPage> Status => new(this, "status");
}
```

The fixture opens one Appium session for the test class and installs the app it is told of:

<!-- sample: tests/Bruinet.Docs.Samples/NativeAndroid/LoginTests.cs#test -->
```csharp
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
```

## 5. Run it

Start the emulator and Appium, then:

```powershell
$env:APPIUM_APP_PATH = "..\MyAndroidApp\bin\Debug\net10.0-android\com.example.myapp-Signed.apk"
$env:APPIUM_APP_PACKAGE = "com.example.myapp"
dotnet test
```

A failing assert says what it expected and what it read, and the view's locator. Next: the
[native Android guide](../platform-guides/native-android.md).
