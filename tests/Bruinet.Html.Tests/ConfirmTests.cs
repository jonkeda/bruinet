using System.Diagnostics;
using Bruinet.Core.Calls;
using Bruinet.Core.Exceptions;
using Bruinet.Html.Controls.Text;

namespace Bruinet.Html.Tests;

/// <summary>
/// An action is confirmed, never repeated (R0, plan step 6): its effect is read in a phase with a
/// budget of its own (R3).
/// </summary>
public class ConfirmTests
{
    private readonly Mock<IHtmlTestContext> _context = new();
    private readonly Mock<IHtmlElement> _input = new();

    public ConfirmTests()
    {
        _context.Setup(c => c.Timeouts).Returns(new TimeoutSettings { DefaultWait = 1000, PollingInterval = 1 });
        _context.Setup(c => c.Logger).Returns(Mock.Of<ITestLogger>());
        _context.Setup(c => c.Context).Returns(() => _context.Object);
        _context.Setup(c => c.IsIdle()).Returns(true);
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Title"))).Returns(_input.Object);
        _input.Setup(e => e.Visible).Returns(true);
        _input.Setup(e => e.Enabled).Returns(true);
    }

    private sealed class Page(IHtmlTestContext context) : PageObjectBase<Page>(context)
    {
        public TextInputControl<Page> Title => new(this, Locator.ByAutomationId("Title"));
    }

    [Fact]
    public void AnElementReplacedAfterTheAction_FailsAsStale_AndTheActionRanOnce()
    {
        _input.Setup(e => e.InputValue).Throws(new StaleElementException());

        var error = Record.Exception(() => new Page(_context.Object).Title.SetText("abc", timeoutMs: 300));

        var stale = Assert.IsType<StaleElementException>(error);
        Assert.Contains("ran once", stale.Message);
        _input.Verify(e => e.Fill("abc"), Times.Once);
    }

    [Fact]
    public void TheConfirmation_HasABudgetOfItsOwn_NotWhatTheResolveLeft()
    {
        // Disabled for most of the 300 ms budget, so the resolve phase leaves little. The field
        // shows the value only to a read given a real budget, as a Playwright read with a timeout.
        var clock = Stopwatch.StartNew();
        _input.Setup(e => e.Enabled).Returns(() => clock.ElapsedMilliseconds > 250);
        var filled = string.Empty;
        _input.Setup(e => e.Fill(It.IsAny<string>())).Callback<string>(value => filled = value);
        _input.Setup(e => e.InputValue).Returns(() => CallBudget.RemainingOr(0) > 150 ? filled : string.Empty);

        new Page(_context.Object).Title.SetText("abc", timeoutMs: 300);

        _input.Verify(e => e.Fill("abc"), Times.Once);
    }
}
