using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;

namespace Bruinet.Html.UITests.Uat.Pages;

[TestPage("Counter")]
public sealed class CounterPage : PageObjectBase<CounterPage>
{
    public CounterPage(IHtmlTestContext context)
        : base(context)
    {
    }

    public LabelControl<CounterPage> CountDisplay => new(this, "[data-testid='count-display']");

    public ButtonControl<CounterPage> IncrementButton => new(this, "[data-testid='increment-btn']");
}
