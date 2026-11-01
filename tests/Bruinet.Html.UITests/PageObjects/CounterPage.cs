using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;

namespace Bruinet.Html.UITests.PageObjects;

public sealed class CounterPage : HtmlPageObjectBase<CounterPage>
{
    public CounterPage(IHtmlTestContext context)
        : base(context)
    {
    }

    public LabelControl<CounterPage> CountDisplay => new(this, "[data-testid='count-display']");

    public ButtonControl<CounterPage> IncrementButton => new(this, "[data-testid='increment-btn']");

    public ButtonControl<CounterPage> ResetButton => new(this, "[data-testid='reset-btn']");
}
