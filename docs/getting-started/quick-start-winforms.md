# Quick start: a WinForms app

A first UI test of a WinForms app, through UI Automation, in five steps. The code below is compiled with Bruinet's
build (`tests/Bruinet.Docs.Samples`), so it is today's API.

## 1. The feed

Bruinet's packages are on the organisation's GitHub Packages feed, not nuget.org. Add it once, with a token that can
read packages (see [VERSIONING](../../VERSIONING.md)).

## 2. A test project

```bash
dotnet new xunit -n MyWinFormsApp.UITests
cd MyWinFormsApp.UITests
```

UI Automation is Windows only: set `<TargetFramework>net10.0-windows</TargetFramework>` in the project file.

## 3. The one package

```bash
dotnet add package Bruinet.WinForms
```

It brings `Bruinet.FlaUI` and `Bruinet.Core`.

## 4. A page object and a test

A page object names the form's controls; a control's AutomationId is its `Name`. A test uses only the page object.

<!-- sample: tests/Bruinet.Docs.Samples/WinForms/LoginPage.cs#page -->
```csharp
using Bruinet.Core.Locators;
using Bruinet.WinForms.Controls.Buttons;
using Bruinet.WinForms.Controls.Display;
using Bruinet.WinForms.Controls.Text;
using Bruinet.WinForms.Interfaces;
using Bruinet.WinForms.Pages;

namespace MyWinFormsApp.UITests;

public sealed class LoginPage(IWinFormsTestContext context) : PageObjectBase<LoginPage>(context)
{
    public TextBox<LoginPage> Username => TextBox("txtUsername");
    public PasswordBox<LoginPage> Password => PasswordBox("txtPassword");
    public Button<LoginPage> SignIn => Button("btnLogin");
    public Label<LoginPage> Status => Label("lblStatus");

    // The form is the lookup root: the page is known by a control it shows.
    protected override Locator ReadyLocator => Locator.ByAutomationId("txtUsername");
}
```

The fixture launches the app once for the test class and closes it after:

<!-- sample: tests/Bruinet.Docs.Samples/WinForms/LoginTests.cs#test -->
```csharp
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
```

## 5. Run it

Build the app, then:

```bash
dotnet test
```

The app runs behind your window, and behind a locked screen. A failing assert says what it expected and what it
read, and the control's locator. Next: the [WinForms guide](../platform-guides/winforms.md) and the
[WinForms run guide](../run/WinForms.md).
