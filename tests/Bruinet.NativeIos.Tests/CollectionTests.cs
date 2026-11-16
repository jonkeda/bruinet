using Bruinet.Core.Collections;
using Bruinet.NativeIos.Collections;
using Bruinet.NativeIos.Controls.Collection;

namespace Bruinet.NativeIos.Tests;

/// <summary>Collections on native iOS (plan step 9): rows keyed by their item, recycled rows, scrolling to find.</summary>
public class CollectionTests
{
    private static readonly Locator NameView = Locator.ById("name");

    private readonly FakeApp _app = new();
    private readonly Mock<INativeIosElement> _list = FakeApp.View();
    private List<INativeIosElement> _rows = [];

    public CollectionTests()
    {
        _list.Setup(l => l.FindElements(It.IsAny<Locator>())).Returns(() => _rows);

        // XCUITest's answer to "a row holding a text with the key": the rows whose name is the key.
        _list.Setup(l => l.FindByClassChain(It.IsAny<string>())).Returns((string chain) =>
            _rows.Where(row => chain.Contains($"label == '{row.TryFindElement(NameView)?.Text}'", StringComparison.Ordinal)).ToList());
        _app.Given("invoices", () => _list.Object);
    }

    private TableView<TestPage> Invoices => new(_app.Page(), "invoices", "name");

    private static INativeIosElement Row(string name) => RowMock(name).Object;

    private static Mock<INativeIosElement> RowMock(string name, bool visible = true)
    {
        var label = FakeApp.View(text: name);
        var row = FakeApp.View(visible);
        row.Setup(r => r.TryFindElement(It.Is<Locator>(l => l.Value == NameView.Value))).Returns(label.Object);
        row.Setup(r => r.FindElements(It.Is<Locator>(l => l.Value == "."))).Returns([row.Object]);
        row.Setup(r => r.TryFindElement(It.Is<Locator>(l => l.Value == "."))).Returns(row.Object);
        return row;
    }

    /// <summary>A row's key is the text of the row view the list names.</summary>
    [Fact]
    public void ARow_IsKeyedByItsNameView()
    {
        _rows = [Row("Invoice 1"), Row("Invoice 2")];

        var item = Invoices.Item("Invoice 2");

        Assert.Equal(new ItemKey(ItemKeyKind.Logical, "Invoice 2"), item.Key);
        Assert.Equal(-1, item.Position);
    }

    /// <summary>
    /// A row is found by its key with one query for the rows holding it: a table keeps all its rows in
    /// the tree, and reading each costs a tree snapshot.
    /// </summary>
    [Fact]
    public void ARow_IsFoundByItsKey_WithoutReadingEveryRow()
    {
        _rows = [Row("Invoice 1"), Row("Invoice 2")];

        Invoices.Item("Invoice 2");

        _list.Verify(l => l.FindByClassChain("**/XCUIElementTypeCell[$(name == 'name') AND (label == 'Invoice 2' OR value == 'Invoice 2')$]"), Times.Once);
        _list.Verify(l => l.FindElements(It.IsAny<Locator>()), Times.Never);
    }

    /// <summary>
    /// A table keeps a row far down in the tree, off the screen: the row found is scrolled into view
    /// within the call's budget.
    /// </summary>
    [Fact]
    public void ARowFoundOffTheScreen_IsScrolledIntoView()
    {
        var far = RowMock("Invoice 150", visible: false);
        _rows = [Row("Invoice 1"), far.Object];

        Invoices.Item("Invoice 150", timeoutMs: 5000);

        far.Verify(r => r.ScrollIntoView(It.IsInRange(1, 5000, Moq.Range.Inclusive)), Times.Once);
    }

    /// <summary>
    /// When the list rebinds the view at a position to another item, the row object follows its
    /// item to where it went, and never taps the item now in its old place (R0).
    /// </summary>
    [Fact]
    public void ARecycledRow_IsFoundByItsKey_NotByWhereItWas()
    {
        var first = Row("Invoice 1");
        var second = Row("Invoice 2");
        _rows = [first, second];
        var item = Invoices.Item("Invoice 2");

        var other = Row("Invoice 9");
        _rows = [second, other];

        Assert.Same(second, Invoices.TryGetItemRoot(item.Key));
        item.Row.Click();
        Mock.Get(second).Verify(r => r.Click(), Times.Once);
        Mock.Get(other).Verify(r => r.Click(), Times.Never);
    }

    /// <summary>A row whose item is gone fails, naming the item; it never taps another row.</summary>
    [Fact]
    public void ARowWhoseItemIsGone_FailsNamingTheItem()
    {
        var gone = Row("Invoice 2");
        _rows = [Row("Invoice 1"), gone];
        var item = Invoices.Item("Invoice 2");

        _rows = [Row("Invoice 1")];

        var error = Record.Exception(() => item.Row.Click(timeoutMs: 300));
        Assert.NotNull(error);
        Assert.Contains("Invoice 2", error!.Message);
        Mock.Get(gone).Verify(r => r.Click(), Times.Never);
    }

    /// <summary>An item that is not shown is found by scrolling the list, and the scrolling stops at its end.</summary>
    [Fact]
    public void AnItemBelowTheFold_IsFoundByScrolling()
    {
        var pages = new[] { new[] { "Invoice 1", "Invoice 2" }, new[] { "Invoice 3", "Invoice 4" }, new[] { "Invoice 5" } };
        var shown = 0;
        _rows = [.. pages[0].Select(Row)];
        _list.Setup(l => l.Scroll(GestureDirection.Up, It.IsAny<double>())).Returns(() =>
        {
            shown = Math.Min(shown + 1, pages.Length - 1);
            _rows = [.. pages[shown].Select(Row)];
            return shown < pages.Length - 1;
        });

        var item = Invoices.Item("Invoice 5");

        Assert.Equal("Invoice 5", item.Key.Value);
    }

    /// <summary>An item that is nowhere fails within its budget, after scrolling both ways once.</summary>
    [Fact]
    public void AnItemThatIsNowhere_FailsWithinItsBudget()
    {
        _rows = [Row("Invoice 1")];
        _list.Setup(l => l.Scroll(It.IsAny<GestureDirection>(), It.IsAny<double>())).Returns(false);

        var elapsed = FakeApp.Timed(() => Assert.Throws<ElementNotFoundException>(() => Invoices.Item("Invoice 99", timeoutMs: 300)));

        Assert.True(elapsed < 1500, $"Item took {elapsed} ms on a 300 ms budget.");
        _list.Verify(l => l.Scroll(It.IsAny<GestureDirection>(), It.IsAny<double>()), Times.Exactly(2));
    }

    /// <summary>A row with no name view is keyed by its description, then its first text, then its position.</summary>
    [Fact]
    public void ItemKey_FallsBackToTheDescription_ThenTheText_ThenThePosition()
    {
        var described = FakeApp.View();
        described.Setup(r => r.Label).Returns("Invoice A");
        var texted = FakeApp.View();
        texted.Setup(r => r.TryFindElement(It.Is<Locator>(l => l.Value == "XCUIElementTypeStaticText"))).Returns(FakeApp.View(text: "Invoice B").Object);
        var blank = FakeApp.View();

        Assert.Equal(new ItemKey(ItemKeyKind.Logical, "Invoice A"), ItemKeys.Of(described.Object, 0));
        Assert.Equal(new ItemKey(ItemKeyKind.Logical, "Invoice B"), ItemKeys.Of(texted.Object, 1));
        Assert.Equal(new ItemKey(ItemKeyKind.Position, "2"), ItemKeys.Of(blank.Object, 2));
    }
}
