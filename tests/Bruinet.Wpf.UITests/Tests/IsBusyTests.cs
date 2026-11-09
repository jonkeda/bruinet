using Bruinet.Wpf.UITests.Fixtures;
using Bruinet.Wpf.UITests.PageObjects;

namespace Bruinet.Wpf.UITests.Tests;

/// <summary>
/// Tests for busy indicator during async login operations.
/// Demonstrates the wait/poll pattern for asynchronous UI state changes.
/// </summary>
[Collection("WPF UITests")]
public class IsBusyTests
{
    private readonly WpfSampleFixture _fixture;

    public IsBusyTests(WpfSampleFixture fixture) => _fixture = fixture;

    [Fact]
    public void Login_DuringSubmit_BusyStateCyclesCorrectly()
    {
        var shell = new ShellPage(_fixture.Context);
        shell.WaitLoaded(true);

        var loginPage = shell.NavigateToLogin();
        loginPage.WaitPageReady();

        loginPage.EnterCredentials("demo", "password");
        loginPage.ClickLogin();

        var completed = loginPage.WaitNotBusy(timeoutMs: 5000);
        Assert.True(completed, "Login operation should complete and busy indicator should hide");

        var homePage = new HomePage(_fixture.Context);
        homePage.WaitLoaded(true);
        Assert.True(homePage.IsLoaded(), "Should navigate to home page after successful login");
    }

    [Fact]
    public void Login_AfterInvalidSubmit_HidesBusyIndicator()
    {
        var shell = new ShellPage(_fixture.Context);
        shell.WaitLoaded(true);

        var loginPage = shell.NavigateToLogin();
        loginPage.WaitPageReady();

        loginPage.EnterCredentials("wronguser", "wrongpass");
        loginPage.ClickLogin();

        var isNotBusy = loginPage.WaitNotBusy(timeoutMs: 5000);
        Assert.True(isNotBusy, "Busy indicator should hide after login completes");
        Assert.False(loginPage.IsBusy(), "Page should not be busy after operation completes");
    }

    [Fact]
    public void Login_InputsAreEnabled_BeforeSubmit()
    {
        var shell = new ShellPage(_fixture.Context);
        shell.WaitLoaded(true);

        var loginPage = shell.NavigateToLogin();
        loginPage.WaitPageReady();

        loginPage.UsernameTextBox.AssertEnabled(true, "Username should be enabled before submit");
        loginPage.PasswordField.AssertEnabled(true, "Password should be enabled before submit");
    }

    [Fact]
    public void Login_WaitPageReady_CombinesLoadedAndNotBusy()
    {
        var shell = new ShellPage(_fixture.Context);
        shell.WaitLoaded(true);

        var loginPage = shell.NavigateToLogin();
        var isReady = loginPage.WaitPageReady();

        Assert.True(isReady, "Page should be loaded and not busy");
        // WPF leaves a collapsed element out of the automation tree: a hidden indicator does not
        // exist, and an absent element has no visibility to assert (the same on every stack).
        loginPage.BusyIndicator.AssertExists(false, "Page should not show busy indicator when ready");
    }
}
