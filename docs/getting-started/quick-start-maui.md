# Quick start: a MAUI app on Windows

A first UI test of a .NET MAUI app, on its Windows head through UI Automation, in six steps. The code below is
compiled with Bruinet's build (`tests/Bruinet.Docs.Samples`), so it is today's API.

## 1. The feed

Bruinet's packages are on the organisation's GitHub Packages feed, not nuget.org. Add it once, with a token that can
read packages (see [VERSIONING](../../VERSIONING.md)).

## 2. The app's side

On Windows, MAUI's layouts do not show their AutomationId to UI Automation, and swipes, flyouts and pickers have no
pattern to drive them: the app hosts Bruinet's automation handlers and gesture bridge. Reference
`samples/Bruinet.Maui.AppSupport` from the app as a project, or copy its sources with its `Bruinet.Uia.Bridge.props`,
and register both in `MauiProgram`:

<!-- sample: tests/Bruinet.Docs.Samples/Maui/MauiProgram.cs#app -->
```csharp
using Bruinet.Maui.AppSupport;

namespace MyMauiApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            // Layouts and content views show their AutomationId to UI Automation on Windows.
            .ConfigureMauiHandlers(handlers => handlers.AddBruinetAutomationHandlers());

        // Before the first page loads; it does nothing unless a Debug build is started by a test.
        builder.UseBruinetGestureBridge();

        return builder.Build();
    }
}
```

The bridge is compiled into Debug builds only. Give each control a test names an `AutomationId` in XAML.

## 3. A test project

```bash
dotnet new xunit -n MyMauiApp.UITests
cd MyMauiApp.UITests
```

The Windows head is Windows only: set `<TargetFramework>net10.0-windows</TargetFramework>` in the project file.

## 4. The one package

```bash
dotnet add package Bruinet.Maui.FlaUI
```

It brings `Bruinet.Maui` and `Bruinet.Core`.

## 5. A page object and a test

A page object names the page's controls by AutomationId; a test uses only those.

<!-- sample: tests/Bruinet.Docs.Samples/Maui/LoginPage.cs#page -->
```csharp
using Bruinet.Maui.Controls.Buttons;
using Bruinet.Maui.Controls.Display;
using Bruinet.Maui.Controls.Text;
using Bruinet.Maui.Interfaces;
using Bruinet.Maui.Pages;

namespace MyMauiApp.UITests;

public sealed class LoginPage(IMauiTestContext context) : PageObjectBase<LoginPage>(context)
{
    public Entry<LoginPage> Username => Entry("UsernameEntry");
    public Entry<LoginPage> Password => Entry("PasswordEntry");
    public Button<LoginPage> SignIn => Button("SignInButton");
    public Label<LoginPage> Status => Label("StatusLabel");
}
```

The fixture launches the app's Windows build once for the test class and closes it after:

<!-- sample: tests/Bruinet.Docs.Samples/Maui/LoginTests.cs#test -->
```csharp
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
```

## 6. Run it

Build the app for Windows, then:

```bash
dotnet test
```

The app runs behind your window: nothing takes the pointer or the keyboard. The same page objects run on Android
and iOS through Appium, and without a device on [MauiHeadless](../guides/maui-headless.md). Next: the
[MAUI guide](../platform-guides/maui.md) and the [MAUI run guide](../run/MAUI.md).
