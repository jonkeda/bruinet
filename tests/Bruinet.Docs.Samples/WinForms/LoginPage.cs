// docs/getting-started/quick-start-winforms.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:page
using Bruinet.Core.Locators;
using Bruinet.WinForms.Controls.Buttons;
using Bruinet.WinForms.Controls.Display;
using Bruinet.WinForms.Controls.Text;
using Bruinet.WinForms.Interfaces;
using Bruinet.WinForms.Pages;

namespace MyWinFormsApp.UITests;

public sealed class LoginPage(IWinFormsTestContext context) : PageObjectBase<LoginPage>(context)
{
    public TextBox<LoginPage> Username => TextBox("txtUsername");
    public PasswordBox<LoginPage> Password => PasswordBox("txtPassword");
    public Button<LoginPage> SignIn => Button("btnLogin");
    public Label<LoginPage> Status => Label("lblStatus");

    // The form is the lookup root: the page is known by a control it shows.
    protected override Locator ReadyLocator => Locator.ByAutomationId("txtUsername");
}
#endregion
