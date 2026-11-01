using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;
using Bruinet.Html.Controls.Range;
using Bruinet.Html.Controls.Selection;
using Bruinet.Html.Controls.Text;
using Bruinet.Html.Controls.Toggle;

namespace Bruinet.Html.UITests.PageObjects;

public sealed class FormControlsPage : HtmlPageObjectBase<FormControlsPage>
{
    public FormControlsPage(IHtmlTestContext context)
        : base(context)
    {
    }

    public CheckBoxControl<FormControlsPage> TermsCheckBox => new(this, "#terms-checkbox");

    public CheckBoxControl<FormControlsPage> NewsletterCheckBox => new(this, "#newsletter-checkbox");

    public SelectControl<FormControlsPage> CountrySelect => new(this, "#country-select");

    public SelectControl<FormControlsPage> ColorsSelect => new(this, "#colors-select");

    public TextAreaControl<FormControlsPage> CommentsTextArea => new(this, "#comments-textarea");

    public LinkControl<FormControlsPage> ExternalLink => new(this, "#external-link");

    public ProgressControl<FormControlsPage> UploadProgress => new(this, "#upload-progress");

    public RangeInputControl<FormControlsPage> VolumeRange => new(this, "#volume-range");

    public LabelControl<FormControlsPage> SelectStatus => new(this, "#select-status");
}
