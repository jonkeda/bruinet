# Quick Start

This page gets a Bruinet developer from a clean checkout to a compiled framework
and a first page-object style test.

## Build The Framework

Working directory: Bruinet root.

```powershell
dotnet build Bruinet.sln -v:minimal /nr:false
```

`Bruinet.sln` holds every project in the repository, in folders by area. The two
Visual Studio extension projects are in it but not built by it: they need Visual Studio's
MSBuild, and build from `tools\Bruinet.Bdd.VisualStudio\Bruinet.Bdd.VisualStudio.slnx`.

## Add Packages

For package consumers:

```powershell
dotnet add package Bruinet.Core
dotnet add package Bruinet.Maui
dotnet add package Bruinet.Maui.FlaUI
dotnet add package Bruinet.Html.Playwright
dotnet add package Bruinet.Wpf
dotnet add package Bruinet.WinForms
dotnet add package Bruinet.Mocking
dotnet add package Bruinet.Uat
```

For repo-local tests, prefer project references to `src/*` projects.

## First Page Object

Page objects should expose user intent and hide locator plumbing.

```csharp
using Bruinet.Maui.Controls.Buttons;
using Bruinet.Maui.Controls.Text;
using Bruinet.Maui.Interfaces;
using Bruinet.Maui.Pages;

public sealed class LoginPage(IMauiTestContext context) : PageObjectBase<LoginPage>(context)
{
    public override string Name => "Login";

    public Entry<LoginPage> Username => Entry("UsernameEntry");
    public Entry<LoginPage> Password => Entry("PasswordEntry");
    public Button<LoginPage> SignIn => Button("SignInButton");

    public void SignInAs(string username, string password)
    {
        Username.SetText(username);
        Password.SetText(password);
        SignIn.Click();
    }
}
```

Use the concrete platform page-object base and controls that match the test
project you are writing. Keep repeated interaction behavior in Bruinet controls,
not in test methods.

## First Test Shape

```csharp
using Xunit;

public sealed class LoginTests
{
    private readonly AppFixture _fixture;

    public LoginTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void ValidCredentials_ShowHomePage()
    {
        var login = _fixture.OpenLoginPage();

        login.SignInAs("user@example.test", "password");

        var home = _fixture.CurrentHomePage();
        Assert.True(home.IsVisible(), "Home page should be visible after sign in.");
    }
}
```

Rules:

- Use xUnit `Assert`.
- Do not add FluentAssertions.
- Wait for concrete UI state.
- Do not add arbitrary sleeps to fix timing.
- Use semantic control operations such as `SetText`, `Click`, `SelectItem`, and
  `WaitReady`.

## Next Steps

- Read [Framework Overview](framework-overview.md).
- Read [Test Writing](../guides/test-writing.md).
- Use [Build And Test](../run/build-and-test.md) for common commands.
