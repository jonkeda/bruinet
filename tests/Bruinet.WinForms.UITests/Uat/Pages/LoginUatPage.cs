namespace Bruinet.WinForms.UITests.Uat.Pages;

[TestPage("Login")]
[UatName("Login")]
public sealed class LoginUatPage : PageObjectBase<LoginUatPage>
{
    public LoginUatPage(IWinFormsTestContext context)
        : base(context)
    {
    }

    public TextBox<LoginUatPage> UsernameField => TextBox("txtUsername");

    public PasswordBox<LoginUatPage> PasswordField => PasswordBox("txtPassword");

    public CheckBox<LoginUatPage> RememberCheckBox => CheckBox("chkRemember");

    [UatName("Role")]
    public ComboBox<LoginUatPage> RoleCombo => ComboBox("cmbRole");

    public Button<LoginUatPage> LoginButton => Button("btnLogin");

    public Label<LoginUatPage> StatusLabel => Label("lblStatus");

    /// <summary>The form is the lookup root, so the page is known by its username field.</summary>
    protected override Locator ReadyLocator => Locator.ByAutomationId("txtUsername");
}
