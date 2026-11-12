// docs/getting-started/quick-start-html.md and quick-start-uat.md quote this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:page
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;
using Bruinet.Html.Interfaces;
using Bruinet.Html.Pages;

namespace MyApp.UITests;

public sealed class CounterPage(IHtmlTestContext context) : PageObjectBase<CounterPage>(context)
{
    public LabelControl<CounterPage> Count => new(this, "[data-testid='count-display']");

    public ButtonControl<CounterPage> Increment => new(this, "[data-testid='increment-btn']");
}
#endregion
