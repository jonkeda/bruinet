using Bruinet.Core.Scopes;
using Bruinet.Html.Controls.Collection;

namespace Bruinet.Html.Tests;

/// <summary>A table's rows often load after the table: a "has rows" wait waits for them (Bouw7 friction #25).</summary>
public sealed class TableHasRowsTests
{
    private readonly Mock<IHtmlTestContext> _context = new();
    private readonly Mock<IHtmlElement> _table = new();
    private int _reads;
    private int _rowsAfter = 3;

    public TableHasRowsTests()
    {
        _context.Setup(c => c.Timeouts).Returns(new TimeoutSettings { DefaultWait = TimeSpan.FromMilliseconds(300), PollingInterval = TimeSpan.FromMilliseconds(1) });
        _context.Setup(c => c.Logger).Returns(Mock.Of<ITestLogger>());
        _context.Setup(c => c.Context).Returns(() => _context.Object);
        _context.Setup(c => c.IsIdle()).Returns(true);
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "table"))).Returns(_table.Object);
        _table.Setup(t => t.Visible).Returns(true);
        _table.Setup(t => t.FindElements(It.Is<Locator>(l => l.Value == "tbody tr")))
            .Returns(() => ++_reads >= _rowsAfter ? [Mock.Of<IHtmlElement>()] : []);
    }

    private sealed class Page(IHtmlTestContext context) : PageObjectBase<Page>(context)
    {
        public TableControl<Page> Table => new(this, Locator.ByCss("table"));
    }

    [Fact]
    public void Rows_that_load_after_the_table_are_waited_for()
    {
        var page = new Page(_context.Object);

        Assert.True(page.Table.WaitHasRows());
        page.Table.AssertHasRows();
    }

    [Fact]
    public void A_table_that_stays_empty_fails_the_assert()
    {
        _rowsAfter = int.MaxValue;
        var page = new Page(_context.Object);

        Assert.False(page.Table.WaitHasRows(50));
        Assert.ThrowsAny<Exception>(() => page.Table.AssertHasRows(timeoutMs: 50));
    }
}
