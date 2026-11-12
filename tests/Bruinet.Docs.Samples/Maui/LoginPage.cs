// docs/getting-started/quick-start-maui.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:page
using Bruinet.Maui.Controls.Buttons;
using Bruinet.Maui.Controls.Display;
using Bruinet.Maui.Controls.Text;
using Bruinet.Maui.Interfaces;
using Bruinet.Maui.Pages;

namespace MyMauiApp.UITests;

public sealed class LoginPage(IMauiTestContext context) : PageObjectBase<LoginPage>(context)
{
    public Entry<LoginPage> Username => Entry("UsernameEntry");
    public Entry<LoginPage> Password => Entry("PasswordEntry");
    public Button<LoginPage> SignIn => Button("SignInButton");
    public Label<LoginPage> Status => Label("StatusLabel");
}
#endregion
