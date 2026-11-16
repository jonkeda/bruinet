// docs/getting-started/quick-start-native-ios.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:page
using Bruinet.Core.Locators;
using Bruinet.NativeIos.Controls.Buttons;
using Bruinet.NativeIos.Controls.Text;
using Bruinet.NativeIos.Interfaces;
using Bruinet.NativeIos.Pages;

namespace MyIosApp.UITests;

public sealed class LoginPage(INativeIosTestContext context) : PageObjectBase<LoginPage>(context)
{
    protected override Locator? ReadyLocator => Locator.ById("login");

    public TextField<LoginPage> UserName => new(this, "username");
    public SecureTextField<LoginPage> Password => new(this, "password");
    public Button<LoginPage> SignIn => new(this, "login");
    public Label<LoginPage> Status => new(this, "status");
}
#endregion
