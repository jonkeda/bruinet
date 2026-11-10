using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Playwright;
using Microsoft.Playwright;

namespace Bruinet.Html.Tests;

/// <summary>
/// A click at a point of a control's box (a canvas's spot): the control hands the point to its element once, and Playwright's element
/// clicks at that position with a timeout, not at the box's centre.
/// </summary>
public class ClickAtTests
{
    private readonly Mock<IHtmlTestContext> _context = new();
    private readonly Mock<IHtmlElement> _canvas = new();

    public ClickAtTests()
    {
        _context.Setup(c => c.Timeouts).Returns(new TimeoutSettings { DefaultWait = 1000, PollingInterval = 1 });
        _context.Setup(c => c.Logger).Returns(Mock.Of<ITestLogger>());
        _context.Setup(c => c.Context).Returns(() => _context.Object);
        _context.Setup(c => c.IsIdle()).Returns(true);
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Map"))).Returns(_canvas.Object);
        _canvas.Setup(e => e.Visible).Returns(true);
        _canvas.Setup(e => e.Enabled).Returns(true);
    }

    private sealed class Page(IHtmlTestContext context) : PageObjectBase<Page>(context)
    {
        public ButtonControl<Page> Map => new(this, Locator.ByAutomationId("Map"));
    }

    [Fact]
    public void ClickAt_HitsThePointGiven()
    {
        new Page(_context.Object).Map.ClickAt(12.5, 40);

        _canvas.Verify(e => e.ClickAt(12.5, 40), Times.Once);
        _canvas.Verify(e => e.Click(), Times.Never);
    }

    [Fact]
    public void ClickAt_Playwright_ClicksAtThePosition()
    {
        var locator = new Mock<ILocator>();
        locator.Setup(l => l.CountAsync()).ReturnsAsync(1);
        locator.Setup(l => l.First).Returns(() => locator.Object);

        new PlaywrightHtmlElement(locator.Object).ClickAt(12.5, 40);

        locator.Verify(l => l.ClickAsync(It.Is<LocatorClickOptions?>(o => o != null && o.Position != null && o.Position.X == 12.5f && o.Position.Y == 40f && o.Timeout != null)),
            Times.Once);
    }
}
