using Bruinet.Html.Controls.Display;
using Bruinet.Html.Controls.Selection;
using Bruinet.Html.Controls.Text;
using Bruinet.Html.Controls.Toggle;

namespace Bruinet.Html.UITests.Uat.Pages;

[TestPage("Form Controls")]
public sealed class FormControlsPage : PageObjectBase<FormControlsPage>
{
    public FormControlsPage(IHtmlTestContext context)
        : base(context)
    {
    }

    public CheckBoxControl<FormControlsPage> TermsCheckBox => new(this, "#terms-checkbox");

    public CheckBoxControl<FormControlsPage> NewsletterCheckBox => new(this, "#newsletter-checkbox");

    public SelectControl<FormControlsPage> CountrySelect => new(this, "#country-select");

    public LabelControl<FormControlsPage> SelectStatus => new(this, "#select-status");

    public TextAreaControl<FormControlsPage> Comments => new(this, "#comments-textarea");
}
