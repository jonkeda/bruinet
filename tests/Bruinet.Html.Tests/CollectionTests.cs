using Bruinet.Core.Collections;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Scopes;
using Bruinet.Html.Collections;
using Bruinet.Html.Controls.Buttons;

namespace Bruinet.Html.Tests;

/// <summary>Rows stand for their items (design 7.6, plan step 7).</summary>
public class CollectionTests
{
    private readonly Mock<IHtmlTestContext> _context = new();
    private readonly Mock<IHtmlElement> _list = new();
    private readonly List<IHtmlElement> _rows = [];

    public CollectionTests()
    {
        _context.Setup(c => c.Timeouts).Returns(new TimeoutSettings { DefaultWait = 1000, PollingInterval = 1 });
        _context.Setup(c => c.Logger).Returns(Mock.Of<ITestLogger>());
        _context.Setup(c => c.Context).Returns(() => _context.Object);
        _context.Setup(c => c.IsIdle()).Returns(true);
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "List"))).Returns(_list.Object);
        _list.Setup(l => l.FindElements(It.Is<Locator>(l => l.Value == "Row"))).Returns(() => _rows.ToArray());

        // A keyed row is found by its key (a CSS attribute selector), as a browser would.
        _list.Setup(l => l.TryFindElement(It.Is<Locator>(l => l.Value.StartsWith("[" + ItemKeys.Attribute))))
            .Returns((Locator selector) => _rows.FirstOrDefault(row => selector.Value == KeySelectorOf(row.GetAttribute(ItemKeys.Attribute))));
    }

    private static string KeySelectorOf(string? key) => $"[{ItemKeys.Attribute}=\"{key}\"]";

    private sealed class Page(IHtmlTestContext context) : PageObjectBase<Page>(context)
    {
        public Rows List => new(this);
    }

    private sealed class Rows(IHtmlScope<Page> page)
        : CollectionObjectBase<Page, Rows, Row>(page, Locator.ByAutomationId("List"), Locator.ByAutomationId("Row"))
    {
        public override Rows Self => this;

        protected override Row CreateItem(ItemKey key, int position) => new(this, key, position);
    }

    private sealed class Row(IHtmlCollection rows, ItemKey key, int position) : ItemObjectBase<Row>(rows, key, position)
    {
        public override Row Self => this;

        public ButtonControl<Row> Delete => new(this, Locator.ByAutomationId("Delete"));
    }

    private static (Mock<IHtmlElement> Row, Mock<IHtmlElement> Delete) RowFor(string? key)
    {
        var row = new Mock<IHtmlElement>();
        var delete = new Mock<IHtmlElement>();
        row.Setup(r => r.GetAttribute(ItemKeys.Attribute)).Returns(key);
        row.Setup(r => r.TryFindElement(It.Is<Locator>(l => l.Value == "Delete"))).Returns(delete.Object);
        delete.Setup(d => d.Visible).Returns(true);
        delete.Setup(d => d.Enabled).Returns(true);
        return (row, delete);
    }

    [Fact]
    public void ARow_ActsOnItsOwnItem_AfterTheListIsReordered()
    {
        var a = RowFor("a");
        var b = RowFor("b");
        _rows.AddRange([a.Row.Object, b.Row.Object]);
        var row = new Page(_context.Object).List.Item(0);

        _rows.Reverse();
        row.Delete.Click();

        a.Delete.Verify(d => d.Click(), Times.Once);
        b.Delete.Verify(d => d.Click(), Times.Never);
    }

    /// <summary>
    /// A keyed row is found by its key, never by its position: an element found by position is
    /// found again by position on every read, and after a re-render reads another item's row.
    /// </summary>
    [Fact]
    public void AKeyedRow_IsFoundByItsKey_NotByItsPosition()
    {
        var a = RowFor("a");
        _rows.Add(a.Row.Object);
        var row = new Page(_context.Object).List.Item(0);

        row.Delete.Click();

        _list.Verify(l => l.TryFindElement(It.Is<Locator>(selector => selector.Value == KeySelectorOf("a"))), Times.AtLeastOnce);
    }

    /// <summary>
    /// An item asked for by its key is one lookup by the key's selector, not a read of every row's
    /// key: a driver call per row outlasts the call's budget on a long list.
    /// </summary>
    [Fact]
    public void AnItemAskedForByItsKey_IsOneLookup_NotARowByRowScan()
    {
        _rows.AddRange([RowFor("a").Row.Object, RowFor("b").Row.Object]);

        var row = new Page(_context.Object).List.Item("b");

        Assert.Equal("b", row.Key.Value);
        _list.Verify(l => l.TryFindElement(It.Is<Locator>(selector => selector.Value == KeySelectorOf("b"))), Times.AtLeastOnce);
        _list.Verify(l => l.FindElements(It.IsAny<Locator>()), Times.Never);
    }

    [Fact]
    public void AnItemThatIsNotThere_IsNull_AndItemFails()
    {
        _rows.Add(RowFor("a").Row.Object);
        var list = new Page(_context.Object).List;

        Assert.Null(list.TryItem("b"));
        Assert.Throws<ElementNotFoundException>(() => list.Item("b", timeoutMs: 100));
    }

    [Fact]
    public void ARowWhoseItemIsGone_FailsAsItemChanged_AndActsOnNothing()
    {
        var a = RowFor("a");
        var b = RowFor("b");
        _rows.AddRange([a.Row.Object, b.Row.Object]);
        var row = new Page(_context.Object).List.Item(0);

        _rows.RemoveAt(0);
        var error = Record.Exception(() => row.Delete.Click(timeoutMs: 200));

        var notReady = Assert.IsType<ScopeNotReadyException>(error);
        Assert.Equal(ScopeReadinessState.ItemChanged, notReady.Readiness.State);
        a.Delete.Verify(d => d.Click(), Times.Never);
        b.Delete.Verify(d => d.Click(), Times.Never);
    }

    [Fact]
    public void RowsWithoutAKey_AreKeptByPosition()
    {
        _rows.Add(RowFor(null).Row.Object);

        Assert.Equal(ItemKeyKind.Position, new Page(_context.Object).List.Item(0).Key.Kind);
    }

    [Fact]
    public void Item_WaitsForTheRow_TryItem_AnswersNow()
    {
        var list = new Page(_context.Object).List;
        Assert.Null(list.TryItem(0));

        var lookups = 0;
        _list.Setup(l => l.FindElements(It.Is<Locator>(l => l.Value == "Row")))
            .Returns(() => ++lookups < 3 ? [] : [RowFor("late").Row.Object]);

        Assert.Equal("late", list.Item(0).Key.Value);
    }

    [Fact]
    public void Item_Fails_WhenTheRowNeverComes()
    {
        Assert.Throws<ElementNotFoundException>(() => new Page(_context.Object).List.Item(5, timeoutMs: 100));
    }
}
