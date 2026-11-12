// docs/platform-guides/wpf.md quotes these regions: change them here, then run tools/Scripts/update-doc-samples.ps1.
using Bruinet.Core.Locators;
using Bruinet.Wpf.Context;
using Bruinet.Wpf.Controls.Buttons;
using Bruinet.Wpf.Controls.Text;
using Bruinet.Wpf.Interfaces;
using Bruinet.Wpf.Pages;

namespace Bruinet.Docs.Samples.Wpf;

public static class WpfSession
{
    public static void Start(string appPath)
    {
        #region docs:session
        var context = new WpfTestContext(new WpfTestContextOptions { ExecutablePath = appPath });
        var shell = new ShellPage(context);
        shell.AssertLoaded(true);
        #endregion
    }
}

public sealed class ShellPage(IWpfTestContext context) : PageObjectBase<ShellPage>(context)
{
    protected override Locator ReadyLocator => Locator.ByAutomationId("AppTitleText");
}

#region docs:page
public sealed class LoginPage(IWpfTestContext context) : PageObjectBase<LoginPage>(context)
{
    public TextBox<LoginPage> Username => TextBox("UsernameTextBox");
    public PasswordBox<LoginPage> Password => PasswordBox("PasswordBox");
    public Button<LoginPage> SignIn => Button("LoginButton");

    protected override IReadOnlyList<Locator> BusyIndicators => [Locator.ByAutomationId("BusyIndicator")];
}
#endregion
