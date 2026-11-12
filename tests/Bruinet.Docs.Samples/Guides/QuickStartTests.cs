// docs/getting-started/quick-start.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
using Bruinet.Maui.Interfaces;
using Bruinet.Maui.Pages;

namespace Bruinet.Docs.Samples.Guides.QuickStart;

#region docs:test
using Xunit;

public sealed class LoginTests : IClassFixture<AppFixture>
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
#endregion

/// <summary>The suite's own fixture: the app launched, its pages opened.</summary>
public sealed class AppFixture
{
    public IMauiTestContext Context { get; } = null!;

    public LoginPage OpenLoginPage() => new(Context);

    public HomePage CurrentHomePage() => new(Context);
}

public sealed class HomePage(IMauiTestContext context) : PageObjectBase<HomePage>(context);
