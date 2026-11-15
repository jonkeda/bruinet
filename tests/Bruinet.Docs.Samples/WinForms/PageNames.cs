// docs/guides/step-definitions.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
using Bruinet.Bdd;
using Bruinet.Core.Composition;
using Bruinet.Uat;
using Bruinet.WinForms.Controls.Selection;
using Bruinet.WinForms.Interfaces;
using Bruinet.WinForms.Pages;

namespace Bruinet.Docs.Samples.WinForms;

#region docs:names
[TestPage("Login")]
[BddPage("Login")]
public sealed class LoginUatPage(IWinFormsTestContext context) : PageObjectBase<LoginUatPage>(context)
{
    [BddControl("Role")]
    public ComboBox<LoginUatPage> RoleCombo => ComboBox("cmbRole");
}
#endregion
