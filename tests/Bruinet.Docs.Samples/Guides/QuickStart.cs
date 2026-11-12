// docs/getting-started/quick-start.md quotes these regions: change them here, then run tools/Scripts/update-doc-samples.ps1.
namespace Bruinet.Docs.Samples.Guides.QuickStart;

#region docs:page
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
#endregion
