# Quick start: a native iOS app

A first UI test of an iOS app that is not a MAUI app (UIKit), through Appium and XCUITest, in five
steps. The code below is compiled with Bruinet's build (`tests/Bruinet.Docs.Samples`), so it is
today's API. The tests run on a Mac.

## 1. The feed

Bruinet's packages are on the organisation's GitHub Packages feed, not nuget.org. Add it once, with a token that can
read packages (see [VERSIONING](../../VERSIONING.md)).

## 2. A test project

```bash
dotnet new xunit -n MyIosApp.UITests
cd MyIosApp.UITests
```

## 3. The one package

```bash
dotnet add package Bruinet.NativeIos
```

It brings `Bruinet.Appium` and `Bruinet.Core`. The tests need a simulator or an iPhone and an Appium server with its
XCUITest driver: the [native iOS run guide](../run/native-ios.md) sets them up.

## 4. A page object and a test

A page object names the screen's views by accessibility identifier; a test uses only those.

<!-- sample: tests/Bruinet.Docs.Samples/NativeIos/LoginPage.cs#page -->
```csharp
using Bruinet.Core.Locators;
using Bruinet.NativeIos.Controls.Buttons;
using Bruinet.NativeIos.Controls.Text;
using Bruinet.NativeIos.Interfaces;
using Bruinet.NativeIos.Pages;

namespace MyIosApp.UITests;

public sealed class LoginPage(INativeIosTestContext context) : PageObjectBase<LoginPage>(context)
{
    protected override Locator? ReadyLocator => Locator.ById("login");

    public TextField<LoginPage> UserName => new(this, "username");
    public SecureTextField<LoginPage> Password => new(this, "password");
    public Button<LoginPage> SignIn => new(this, "login");
    public Label<LoginPage> Status => new(this, "status");
}
```

The fixture opens one Appium session for the test class and installs the app it is told of:

<!-- sample: tests/Bruinet.Docs.Samples/NativeIos/LoginTests.cs#test -->
```csharp
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
```

## 5. Run it

Boot a simulator and start Appium, then:

```bash
export APPIUM_APP_PATH=../MyIosApp/bin/Debug/net10.0-ios/iossimulator-arm64/MyIosApp.app
export APPIUM_BUNDLE_ID=com.example.myapp
dotnet test
```

A failing assert says what it expected and what it read, and the element's locator. Next: the
[native iOS guide](../platform-guides/native-ios.md).
