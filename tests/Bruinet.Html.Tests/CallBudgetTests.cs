using Bruinet.Core.Calls;
using Bruinet.Html.Controls.Display;

namespace Bruinet.Html.Tests;

/// <summary>
/// A driver's own wait (a page to publish, a dropdown to open) keeps its window outside a call
/// and is cut to what the running call has left inside one (R2).
/// </summary>
public class CallBudgetTests
{
    private sealed class Page(IHtmlTestContext context) : PageObjectBase<Page>(context)
    {
        public override bool IsLoaded() => true;

        public LabelControl<Page> Status => new(this, Locator.ByAutomationId("BudgetStatus"));
    }

    [Fact]
    public void Within_OutsideACall_IsTheWindow()
        => Assert.Equal(1_000, CallBudget.Within(1_000));

    [Fact]
    public void Within_InsideACall_IsWhatTheCallHasLeft()
    {
        var context = new Mock<IHtmlTestContext>();
        context.Setup(c => c.Timeouts).Returns(new TimeoutSettings { DefaultWait = TimeSpan.FromMilliseconds(1000), PageLoad = TimeSpan.FromMilliseconds(1000), PollingInterval = TimeSpan.FromMilliseconds(1) });
        context.Setup(c => c.Logger).Returns(new Mock<ITestLogger>().Object);
        context.Setup(c => c.Context).Returns(() => context.Object);
        context.Setup(c => c.IsIdle()).Returns(true);
        var element = new Mock<IHtmlElement>();
        element.Setup(e => e.Visible).Returns(true);
        var seen = 0;
        element.Setup(e => e.Text).Returns(() =>
        {
            seen = CallBudget.Within(5_000);
            return "ready";
        });
        context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "BudgetStatus"))).Returns(element.Object);

        new Page(context.Object).Status.GetText(timeoutMs: 200);

        Assert.InRange(seen, 1, 200);
    }
}
