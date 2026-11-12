// docs/platform-guides/winforms.md quotes these regions: change them here, then run tools/Scripts/update-doc-samples.ps1.
using Bruinet.Core.Locators;
using Bruinet.WinForms.Context;
using Bruinet.WinForms.Controls.Range;
using Bruinet.WinForms.Controls.Selection;
using Bruinet.WinForms.Controls.Text;
using Bruinet.WinForms.Interfaces;
using Bruinet.WinForms.Pages;

namespace Bruinet.Docs.Samples.WinForms;

public static class WinFormsSession
{
    public static void Start(string appPath)
    {
        #region docs:session
        var context = new WinFormsTestContext(new WinFormsTestContextOptions { ExecutablePath = appPath });
        var login = new LoginPage(context);
        login.AssertLoaded(true);
        #endregion
    }
}

#region docs:page
public sealed class LoginPage(IWinFormsTestContext context) : PageObjectBase<LoginPage>(context)
{
    public TextBox<LoginPage> Username => TextBox("txtUsername");
    public NumericUpDown<LoginPage> Port => new(this, "nudPort", minimum: 1, maximum: 65535);
    public ComboBox<LoginPage> Role => ComboBox("cmbRole");

    protected override Locator ReadyLocator => Locator.ByAutomationId("txtUsername");
}
#endregion
