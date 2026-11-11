using Bruinet.Html.Controls.Buttons;

namespace Bruinet.Html.Bunit.Tests;

public sealed class ProbePage(IHtmlTestContext context) : BlazorPageObjectBase<ProbePage>(context)
{
    public LabelControl<ProbePage> Title => new(this, "[data-testid='title']");

    public ButtonControl<ProbePage> Slow => new(this, "[data-testid='slow']");

    public LabelControl<ProbePage> Status => new(this, "[data-testid='status']");

    public ButtonControl<ProbePage> Increment => new(this, "[data-testid='inc']");

    public LabelControl<ProbePage> Count => new(this, "[data-testid='count']");

    public ButtonControl<ProbePage> Missing => new(this, "[data-testid='no-such-button']");
}
