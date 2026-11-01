using Bruinet.Html;
using Bruinet.Html.UITests.PageObjects;
using Bruinet.Html.UITests.TestBase;

namespace Bruinet.Html.UITests.Tests.Pages;

public sealed class LoginPageTests : BlazorSampleTestBase
{
    [Fact]
    public void Login_ValidCredentials_ShowsSuccessMessage()
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
    public void Login_InvalidCredentials_ShowsErrorMessage()
    {
        NavigateToPage("/login");
        var page = new LoginPage(Context);

        page.EmailInput.SetText("invalid@example.com");
        page.PasswordInput.SetText("wrongpw");
        page.LoginButton.Click();

        page.ErrorMessage.WaitVisible(true);
        page.ErrorMessage.AssertTextContains("Invalid email or password");
    }
}
