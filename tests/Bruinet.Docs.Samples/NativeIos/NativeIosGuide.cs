// docs/platform-guides/native-ios.md quotes these regions: change them here, then run tools/Scripts/update-doc-samples.ps1.
using Bruinet.Core.Locators;
using Bruinet.Core.Composition;
using Bruinet.NativeIos;
using Bruinet.NativeIos.Appium;
using Bruinet.NativeIos.Controls.Buttons;
using Bruinet.NativeIos.Controls.Text;
using Bruinet.NativeIos.Interfaces;
using Bruinet.NativeIos.Pages;

namespace Bruinet.Docs.Samples.NativeIos;

public static class NativeIosSession
{
    public static void Start()
    {
        #region docs:session
        var context = NativeIosTestContext.Create(NativeIosTestContextOptions.FromEnvironment());
        context.NavigateTo("myapp://orders");   // a deep link
        var page = new OrdersPage(context);
        page.AssertIdle();
        #endregion
    }
}

public sealed class OrdersPage(INativeIosTestContext context) : PageObjectBase<OrdersPage>(context)
{
}

#region docs:page
[TestPage("Sign In")]
public sealed class LoginPage(INativeIosTestContext context) : PageObjectBase<LoginPage>(context)
{
    protected override Locator? ReadyLocator => Locator.ById("login");

    public TextField<LoginPage> UserName => new(this, "username");
    public Button<LoginPage> SignIn => new(this, "login");
    public Label<LoginPage> Status => new(this, "status");
}
#endregion
