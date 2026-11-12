# Quick start: a WPF app

A first UI test of a WPF app, through UI Automation, in five steps. The code below is compiled with Bruinet's build
(`tests/Bruinet.Docs.Samples`), so it is today's API.

## 1. The feed

Bruinet's packages are on the organisation's GitHub Packages feed, not nuget.org. Add it once, with a token that can
read packages (see [VERSIONING](../../VERSIONING.md)).

## 2. A test project

```bash
dotnet new xunit -n MyWpfApp.UITests
cd MyWpfApp.UITests
```

UI Automation is Windows only: set `<TargetFramework>net10.0-windows</TargetFramework>` in the project file.

## 3. The one package

```bash
dotnet add package Bruinet.Wpf
```

It brings `Bruinet.FlaUI` and `Bruinet.Core`.

## 4. A page object and a test

A page object names the page's controls by AutomationId; a test uses only those. The page is known by the element
whose AutomationId is its name, so the app's login view carries `AutomationProperties.AutomationId="LoginPage"`.

<!-- sample: tests/Bruinet.Docs.Samples/Wpf/LoginPage.cs#page -->
```csharp
using Bruinet.Wpf.Controls.Buttons;
using Bruinet.Wpf.Controls.Display;
using Bruinet.Wpf.Controls.Text;
using Bruinet.Wpf.Interfaces;
using Bruinet.Wpf.Pages;

namespace MyWpfApp.UITests;

public sealed class LoginPage(IWpfTestContext context) : PageObjectBase<LoginPage>(context)
{
    public TextBox<LoginPage> Username => TextBox("UsernameTextBox");
    public PasswordBox<LoginPage> Password => PasswordBox("PasswordBox");
    public Button<LoginPage> SignIn => Button("LoginButton");
    public Label<LoginPage> Status => Label("StatusText");
}
```

The fixture launches the app once for the test class and closes it after:

<!-- sample: tests/Bruinet.Docs.Samples/Wpf/LoginTests.cs#test -->
```csharp
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
```

## 5. Run it

Build the app, then:

```bash
dotnet test
```

The app runs behind your window: nothing takes the pointer or the keyboard. A failing assert says what it expected
and what it read, and the control's locator. Next: the [WPF guide](../platform-guides/wpf.md) and the
[WPF run guide](../run/WPF.md).
