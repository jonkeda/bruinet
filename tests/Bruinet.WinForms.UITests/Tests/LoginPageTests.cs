using Bruinet.WinForms.UITests.Fixtures;
using Bruinet.WinForms.UITests.Pages;

namespace Bruinet.WinForms.UITests.Tests;

/// <summary>
/// Basic login page tests demonstrating page object usage.
/// </summary>
[Collection("WinForms UITests")]
public class LoginPageTests
{
    private readonly WinFormsSampleFixture _fixture;

    public LoginPageTests(WinFormsSampleFixture fixture) => _fixture = fixture;

    private LoginPage GetPage()
    {
        var page = new LoginPage(_fixture.Context);
        page.ClickClear();
        page.WaitForFormCleared(timeoutMs: 2000);
        return page;
    }

    [Fact]
    public void LoginPage_ShouldDisplayLoginForm()
    {
        var page = GetPage();
        Assert.True(page.IsLoaded(), "Login form should be displayed");
    }

    [Fact]
    public void LoginPage_CanEnterUsername()
    {
        var page = GetPage();
        page.EnterUsername("testuser");
        Assert.Equal("testuser", page.GetUsername());
    }

    [Fact]
    public void LoginPage_CanToggleRememberMe()
    {
        var page = GetPage();

        page.SetRememberMe(true);
        Assert.True(page.IsRememberMeChecked());

        page.SetRememberMe(false);
        Assert.False(page.IsRememberMeChecked());
    }

    [Fact]
    public void LoginPage_CanSelectRole()
    {
        var page = GetPage();
        page.SelectRole("Admin");
        Assert.Equal("Admin", page.GetSelectedRole());
    }

    [Fact]
    public void LoginPage_CanLogin()
    {
        var page = GetPage();
        page.EnterUsername("john.doe");
        page.SelectRole("User");
        page.ClickLogin();
        page.WaitForLoginComplete();

        var status = page.GetStatusMessage();
        Assert.Contains("john.doe", status);
        Assert.Contains("User", status);
        Assert.Contains("Logged in", status);
    }

    [Fact]
    public void LoginPage_CanClearForm()
    {
        var page = GetPage();
        page.EnterUsername("testuser");
        page.SetRememberMe(true);

        page.ClickClear();
        page.WaitForFormCleared();

        Assert.Equal(string.Empty, page.GetUsername());
        Assert.False(page.IsRememberMeChecked());
    }

    [Fact]
    public void LoginPage_StatusLabelShowsReadyInitially()
    {
        var page = GetPage();
        Assert.Contains("Ready", page.GetStatusMessage());
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("User")]
    [InlineData("Guest")]
    public void LoginPage_CanSelectMultipleRoles(string role)
    {
        var page = GetPage();
        page.SelectRole(role);
        Assert.Equal(role, page.GetSelectedRole());
    }

    [Fact]
    public void LoginPage_CanLoginWithAllRoles()
    {
        var page = GetPage();
        var roles = new[] { "Admin", "User", "Guest" };

        foreach (var role in roles)
        {
            page.EnterUsername("testuser");
            page.SelectRole(role);
            page.ClickLogin();
            page.WaitForLoginComplete();

            var status = page.GetStatusMessage();
            Assert.Contains("Logged in", status);
            Assert.Contains(role, status);

            page.ClickClear();
            page.WaitForFormCleared();
        }
    }
}
