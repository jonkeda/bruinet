using Bruinet.Html.Controls.Container;

namespace Bruinet.Html.Tests;

/// <summary>A tab page opens its tab once, and is returned only when the tab is selected and its content shown.</summary>
public sealed class TabPageTests
{
    private readonly Mock<IHtmlTestContext> _context = new();
    private readonly Mock<IHtmlElement> _tab = new();
    private readonly Mock<IHtmlElement> _panel = new();
    private string _selected = "false";
    private int _clicks;

    public TabPageTests()
    {
        _context.Setup(c => c.Timeouts).Returns(new TimeoutSettings { DefaultWait = TimeSpan.FromMilliseconds(300), PageLoad = TimeSpan.FromMilliseconds(300), PollingInterval = TimeSpan.FromMilliseconds(1) });
        _context.Setup(c => c.Context).Returns(() => _context.Object);
        _context.Setup(c => c.IsIdle()).Returns(true);
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Expired"))).Returns(_tab.Object);
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Panel"))).Returns(_panel.Object);
        _tab.Setup(e => e.Visible).Returns(true);
        _tab.Setup(e => e.Enabled).Returns(true);
        _tab.Setup(e => e.GetAttribute("aria-selected")).Returns(() => _selected);
        _panel.Setup(e => e.Visible).Returns(() => _selected == "true");
    }

    private sealed class Page(IHtmlTestContext context) : PageObjectBase<Page>(context)
    {
        public override bool IsLoaded() => true;

        public ExpiredTab Expired => new(this);
    }

    private sealed class ExpiredTab(Page page)
        : TabPageBase<Page, ExpiredTab>(page, Locator.ByAutomationId("Expired"), Locator.ByAutomationId("Panel"));

    [Fact]
    public void Open_ClicksTheTab_AndReturnsOnceItIsSelected()
    {
        _tab.Setup(e => e.Click()).Callback(() => { _clicks++; _selected = "true"; });

        var tab = new Page(_context.Object).Expired.Open();

        Assert.Equal(1, _clicks);
        Assert.True(tab.IsSelected());
    }

    [Fact]
    public void Open_ATabAlreadySelected_IsNotClickedAgain()
    {
        _selected = "true";

        new Page(_context.Object).Expired.Open();

        Assert.Equal(0, _clicks);
    }

    [Fact]
    public void Open_ATabThatDoesNotSelect_FailsSayingSo()
    {
        _tab.Setup(e => e.Click()).Callback(() => _clicks++);

        var error = Assert.ThrowsAny<Exception>(() => new Page(_context.Object).Expired.Open());

        Assert.Equal(1, _clicks);
        Assert.Contains("did not become selected", error.Message, StringComparison.Ordinal);
    }
}
