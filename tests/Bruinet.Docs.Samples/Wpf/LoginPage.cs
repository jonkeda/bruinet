// docs/getting-started/quick-start-wpf.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:page
using Bruinet.Wpf.Controls.Buttons;
using Bruinet.Wpf.Controls.Display;
using Bruinet.Wpf.Controls.Text;
using Bruinet.Wpf.Interfaces;
using Bruinet.Wpf.Pages;

namespace MyWpfApp.UITests;

public sealed class LoginPage(IWpfTestContext context) : PageObjectBase<LoginPage>(context)
{
    public TextBox<LoginPage> Username => TextBox("UsernameTextBox");
    public PasswordBox<LoginPage> Password => PasswordBox("PasswordBox");
    public Button<LoginPage> SignIn => Button("LoginButton");
    public Label<LoginPage> Status => Label("StatusText");
}
#endregion
