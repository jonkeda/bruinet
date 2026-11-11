using Bruinet.WinForms.UITests.Fixtures;
using Bruinet.WinForms.UITests.Pages;

namespace Bruinet.WinForms.UITests.Tests;

/// <summary>
/// Advanced tests demonstrating framework patterns: wait, assert, and complete workflows.
/// </summary>
[Collection("WinForms UITests")]
public class AdvancedLoginTests
{
    private readonly WinFormsSampleFixture _fixture;

    public AdvancedLoginTests(WinFormsSampleFixture fixture) => _fixture = fixture;

    private LoginPage GetPage()
    {
        var page = new LoginPage(_fixture.Context);
        page.ClickClear();
        page.WaitForFormCleared(timeoutMs: 2000);
        return page;
    }

    [Fact]
    public void AdvancedLogin_DemonstratesWaitPattern()
    {
        var page = GetPage();
        page.WaitForStatusContains("Ready", timeoutMs: 5000);
        Assert.True(page.IsLoaded());
    }

    [Fact]
    public void AdvancedLogin_DemonstratesAssertPattern()
    {
        var page = GetPage();
        page.SelectRole("Admin");
        Assert.Equal("Admin", page.GetSelectedRole());
    }

    [Fact]
    public void AdvancedLogin_CompleteWorkflow()
    {
        var page = GetPage();
        page.EnterUsername("john.smith");
        page.EnterPassword("SecurePass123!");
        page.SetRememberMe(true);
        page.SelectRole("User");

        Assert.Equal("john.smith", page.GetUsername());
        Assert.True(page.IsRememberMeChecked());
        Assert.Equal("User", page.GetSelectedRole());

        page.ClickLogin();
        page.WaitForLoginComplete();

        var status = page.GetStatusMessage();
        Assert.Contains("Logged in", status);
        Assert.Contains("john.smith", status);
        Assert.Contains("User", status);
    }

    [Fact]
    public void AdvancedLogin_MultipleLogins()
    {
        var page = GetPage();
        var testCases = new[]
        {
            ("alice", "Admin"),
            ("bob", "User"),
            ("charlie", "Guest")
        };

        foreach (var (username, role) in testCases)
        {
            page.EnterUsername(username);
            page.SelectRole(role);
            page.ClickLogin();
            page.WaitForLoginComplete();

            var status = page.GetStatusMessage();
            Assert.Contains(username, status);
            Assert.Contains(role, status);
            Assert.Contains("Logged in", status);

            page.ClickClear();
            page.WaitForFormCleared();
        }
    }

    [Fact]
    public void AdvancedLogin_ControlVisibility()
    {
        var page = GetPage();
        Assert.True(page.IsLoaded());
        page.UsernameField.AssertExists(true, "Username field should exist");
        page.LoginButton.AssertExists(true, "Login button should exist");
    }
}
