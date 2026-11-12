// docs/platform-guides/native-android.md quotes these regions: change them here, then run tools/Scripts/update-doc-samples.ps1.
using Bruinet.Core.Locators;
using Bruinet.Core.Composition;
using Bruinet.NativeAndroid;
using Bruinet.NativeAndroid.Appium;
using Bruinet.NativeAndroid.Controls.Buttons;
using Bruinet.NativeAndroid.Controls.Text;
using Bruinet.NativeAndroid.Interfaces;
using Bruinet.NativeAndroid.Pages;

namespace Bruinet.Docs.Samples.NativeAndroid;

public static class NativeAndroidSession
{
    public static void Start()
    {
        #region docs:session
        var context = NativeAndroidTestContext.Create(NativeAndroidTestContextOptions.FromEnvironment());
        context.NavigateTo("myapp://orders");   // a deep link
        var page = new OrdersPage(context);
        page.AssertIdle();
        #endregion
    }
}

public sealed class OrdersPage(INativeAndroidTestContext context) : PageObjectBase<OrdersPage>(context)
{
}

#region docs:page
[TestPage("Sign In")]
public sealed class LoginPage(INativeAndroidTestContext context) : PageObjectBase<LoginPage>(context)
{
    public override string? Activity => ".LoginActivity";
    protected override Locator? ReadyLocator => Locator.ById("login");

    public EditText<LoginPage> UserName => new(this, "username");
    public Button<LoginPage> SignIn => new(this, "login");
    public TextView<LoginPage> Status => new(this, "status");
}
#endregion
