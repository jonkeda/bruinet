using Bruinet.Html.Context;
using Bruinet.Html.Controls.Display;
using System.Globalization;

namespace Bruinet.Html.Tests;

/// <summary>
/// <c>WaitText(until)</c> and <c>WaitAttribute(name, until)</c>: a predicate polled within the
/// call's budget, the last value in the log when it never held.
/// </summary>
public class PredicateWaitTests
{
    private readonly Mock<IHtmlTestContext> _context = new();
    private readonly Mock<ITestLogger> _logger = new();

    public PredicateWaitTests()
    {
        _context.Setup(c => c.Timeouts).Returns(new TimeoutSettings
        {
            DefaultWait = TimeSpan.FromMilliseconds(1000),
            PageLoad = TimeSpan.FromMilliseconds(1000),
            ElementFind = TimeSpan.FromMilliseconds(1000),
            PollingInterval = TimeSpan.FromMilliseconds(1),
        });
        _context.Setup(c => c.Logger).Returns(_logger.Object);
        _context.Setup(c => c.Context).Returns(() => _context.Object);
        _context.Setup(c => c.DefaultLocatorStrategy).Returns(LocatorStrategy.Css);
        _context.Setup(c => c.IsIdle()).Returns(true);
    }

    private sealed class WaitPage(IHtmlTestContext context) : PageObjectBase<WaitPage>(context)
    {
        public override bool IsLoaded() => true;

        public LabelControl<WaitPage> Status => new(this, Locator.ByAutomationId("Target"));
    }

    private LabelControl<WaitPage> Status() => new WaitPage(_context.Object).Status;

    private Mock<IHtmlElement> GivenTarget()
    {
        var element = new Mock<IHtmlElement>();
        element.Setup(e => e.Visible).Returns(true);
        element.Setup(e => e.Enabled).Returns(true);
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Target"))).Returns(element.Object);
        _context.Setup(c => c.FindElement(It.Is<Locator>(l => l.Value == "Target"))).Returns(element.Object);
        return element;
    }

    [Fact]
    public void WaitText_Passes_WhenThePredicateHoldsAtOnce()
    {
        GivenTarget().Setup(e => e.Text).Returns("3 targets");

        Assert.True(Status().WaitText(text => text?.StartsWith('3') == true, timeoutMs: 500));
    }

    [Fact]
    public void WaitAttribute_Passes_WhenTheValueChangesWithinTheBudget()
    {
        var reads = 0;
        GivenTarget().Setup(e => e.GetAttribute("data-count")).Returns(() => ++reads < 4 ? "0" : "5");

        Assert.True(Status().WaitAttribute("data-count", count => int.Parse(count!, CultureInfo.InvariantCulture) > 0, timeoutMs: 1000));
        Assert.True(reads >= 4);
    }

    [Fact]
    public void WaitAttribute_AnswersFalse_WithTheLastValueInTheLog_WhenThePredicateNeverHolds()
    {
        GivenTarget().Setup(e => e.GetAttribute("data-count")).Returns("0");

        Assert.False(Status().WaitAttribute("data-count", count => count != "0", timeoutMs: 200));

        _logger.Verify(l => l.Write(It.Is<LogEvent>(e => e.Kind == LogEventKind.Exit && e.Action == "WaitAttribute" && e.Result == LogResult.Error && (e.Message != null && e.Message.Contains("value '0'")))),
            Times.Once);
    }
}
