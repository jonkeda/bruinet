// docs/getting-started/quick-start-native-android.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:page
using Bruinet.Core.Locators;
using Bruinet.NativeAndroid.Controls.Buttons;
using Bruinet.NativeAndroid.Controls.Text;
using Bruinet.NativeAndroid.Interfaces;
using Bruinet.NativeAndroid.Pages;

namespace MyAndroidApp.UITests;

public sealed class LoginPage(INativeAndroidTestContext context) : PageObjectBase<LoginPage>(context)
{
    public override string? Activity => ".LoginActivity";
    protected override Locator? ReadyLocator => Locator.ById("login");

    public EditText<LoginPage> UserName => new(this, "username");
    public EditText<LoginPage> Password => new(this, "password");
    public Button<LoginPage> SignIn => new(this, "login");
    public TextView<LoginPage> Status => new(this, "status");
}
#endregion
