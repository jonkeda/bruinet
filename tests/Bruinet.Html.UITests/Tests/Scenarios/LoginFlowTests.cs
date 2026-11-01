using Bruinet.Html;
using Bruinet.Html.UITests.PageObjects;
using Bruinet.Html.UITests.TestBase;

namespace Bruinet.Html.UITests.Tests.Scenarios;

public sealed class LoginFlowTests : BlazorSampleTestBase
{
    [Fact]
    public void LoginFlow_ValidCredentials_ShowsSuccessMessage()
    {
        NavigateToPage("/login");
        var page = new LoginPage(Context);

        page.EmailInput.SetText("test@example.com");
        page.PasswordInput.SetText("password123");
        page.LoginButton.Click();

        page.SuccessMessage.WaitVisible(true);
        page.SuccessMessage.AssertTextContains("Login successful");
    }

    [Fact]
    public void LoginFlow_EmptyCredentials_ShowsError()
    {
        NavigateToPage("/login");
        var page = new LoginPage(Context);

        page.LoginButton.Click();

        page.ValidationMessage.AssertVisible(true);
    }

    /// <summary>
    /// R0 guard at the UI level: with the "never re-enables" bug switched on, the second Login
    /// fails and says the button is disabled. It must never pass, and never click a disabled button.
    /// </summary>
    [Fact]
    public void LoginFlow_WhenLoginNeverReenables_TheSecondLoginFailsSayingDisabled()
    {
        NavigateToPage("/login?login-bug=stuck-disabled");
        var page = new LoginPage(Context);
        page.EmailInput.SetText("test@example.com");
        page.PasswordInput.SetText("wrong-password");
        page.LoginButton.Click();
        page.ErrorMessage.WaitVisible(true);

        var error = Record.Exception(() => page.LoginButton.Click());

        Assert.NotNull(error);
        Assert.Contains("enabled", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
