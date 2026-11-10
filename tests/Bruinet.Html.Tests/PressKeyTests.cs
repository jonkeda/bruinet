using Bruinet.Core;
using Bruinet.Html.Controls.Display;

namespace Bruinet.Html.Tests;

/// <summary>
/// A key pressed by its name ("ArrowRight") reaches the element as one key press, not as the text "ArrowRight" typed
/// letter by letter, which is what SendKeys does.
/// </summary>
public class PressKeyTests
{
    private readonly Mock<IHtmlTestContext> _context = new();
    private readonly Mock<IHtmlElement> _canvas = new();

    public PressKeyTests()
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
        public LabelControl<Page> Map => new(this, Locator.ByAutomationId("Map"));
    }

    [Fact]
    public void PressKey_PressesTheKeyOnce_TypesNothing()
    {
        new Page(_context.Object).Map.PressKey("ArrowRight");

        _canvas.Verify(e => e.PressKey("ArrowRight"), Times.Once);
        _canvas.Verify(e => e.SendKeys(It.IsAny<string>(), It.IsAny<TextInputMethod>()), Times.Never);
    }
}
