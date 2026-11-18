using Bruinet.Cli.Suite;

namespace Bruinet.Cli.Tests.Suite;

/// <summary>A suite's page checklist: what its source shows of each page, and the column a person keeps.</summary>
public sealed class PagesChecklistTests
{
    private const string Shared = """
        namespace Shop.Components;

        [BddKind("field")]
        public class TextRow<TScope>(TScope scope, string label) { }

        [BddKind("button")]
        public partial class Btn<TScope> { }

        public partial class Btn<TScope>(TScope scope) { }

        public abstract class ListPage<TSelf>(IHtmlTestContext context, string address) : PageObjectBase<TSelf>(context, address)
        {
            public ShopTable<TSelf> Table => new(this);
        }

        public sealed class Rows<TParent, TRow>(TParent page) : CollectionObjectBase<TParent, Rows<TParent, TRow>, TRow>(page) { }

        public abstract class Row<TSelf> : ItemObjectBase<TSelf> { }
        """;

    private const string Orders = """
        namespace Shop.Modules.Orders.Pages;

        [BddPage("Orders")]
        public sealed class OrdersPage(IHtmlTestContext context) : ListPage<OrdersPage>(context, "/order")
        {
            public Btn<OrdersPage> AddOrder => new(this);

            public Rows<OrdersPage, OrderRow> Rows => new(this);

            public OrderDialog NewOrder => new(this);

            [BddControl("the link {text}")]
            public ButtonControl<OrdersPage> Link(string text) => new(this, text);

            [BddIgnore]
            public Btn<OrdersPage> Hidden => new(this);
        }

        public sealed class OrderRow : Row<OrderRow>
        {
            public LabelControl<OrderRow> Number => Cell("Number");

            public LabelControl<OrderRow> Client => Cell("Client");
        }

        public sealed class OrderDialog(OrdersPage page)
        {
            public TextRow<OrderDialog> Client => new(this, "Client");

            public ButtonControl<OrderDialog> Create => new(this);
        }

        public sealed class ReturnsPage(IHtmlTestContext context) : ListPage<ReturnsPage>(context, "/return") { }

        public sealed class OrderViewPage(IHtmlTestContext context) : PageObjectBase<OrderViewPage>(context) { }
        """;

    private const string Map = """
        | Module | Item | Address | Lands on |
        | --- | --- | --- | --- |
        | Orders | Overview | `/order#/` |  |
        | Orders | Returns | `/returns` | `/return` |
        | Orders | Invoices | `/invoice` |  |
        """;

    private static SuitePages Pages()
    {
        var source = new SuiteSource();
        source.Add("Shop/Components/Shared.cs", Shared);
        source.Add("Shop/Modules/Orders/Pages/Orders/OrdersPages.cs", Orders);
        return SuitePages.From(
            source,
            new Dictionary<string, string> { ["SH.ORD.01"] = "Given I am on the Orders page", ["SH.ORD.02"] = "the Order page" },
            new Dictionary<string, string> { ["OrdersTests"] = "var page = new OrdersPage(context);", ["OtherTests"] = "new MyOrdersPage()" },
            Map);
    }

    [Fact]
    public void A_page_s_members_are_counted_by_their_kind_with_its_base_s_and_its_part_s()
    {
        var orders = Pages().Pages.Single(page => page.Page == "OrdersPage");

        Assert.Equal(("Orders", "Orders", "Orders", "/order"), (orders.Module, orders.Subject, orders.PageName, orders.Address));
        Assert.Equal(["list", "form"], orders.Kinds);

        // The dialog's field and button count for the page; the ignored button and the method do not.
        Assert.Equal((1, 2, 0), (orders.Fields, orders.Buttons, orders.Tabs));
        Assert.Equal(["OrderRow (2)"], orders.Rows);
        Assert.Equal(["SH.ORD.01"], orders.Specs);
        Assert.Equal(["OrdersTests"], orders.Tests);
        Assert.True(orders.InMap);
        Assert.Equal(["by text (1)"], orders.Gaps);
    }

    [Fact]
    public void A_table_without_a_row_type_and_a_page_nothing_visits_are_gaps()
    {
        var pages = Pages();

        var returns = pages.Pages.Single(page => page.Page == "ReturnsPage");
        Assert.Equal(["list"], returns.Kinds);
        Assert.True(returns.InMap);
        Assert.Equal(["no typed row", "nothing named"], returns.Gaps);

        var view = pages.Pages.Single(page => page.Page == "OrderViewPage");
        Assert.Equal(["view"], view.Kinds);
        Assert.Null(view.Address);
        Assert.Equal(["nothing named", "no spec, no test"], view.Gaps);

        // A base class is not a page; the map's address nobody opens at is a page still to make.
        Assert.Equal(3, pages.Pages.Count);
        Assert.Equal([new MapAddress("/invoice", "Orders / Invoices")], pages.Unmade);
        Assert.Equal(3, pages.MapRows);
    }

    [Fact]
    public void Writing_again_keeps_what_a_person_wrote_and_says_what_it_dropped()
    {
        var pages = Pages();
        var first = PagesChecklist.Write(pages, new Dictionary<string, string>(), out _);
        var noted = first.Replace("| by text (1) |  |", "| by text (1) | Not tried as Admin \\| yet |", StringComparison.Ordinal)
            .Replace("\n", "\r\n", StringComparison.Ordinal);

        var owed = PagesChecklist.Owed(noted + "| Orders |  | `GonePage` | view |  |  |  |  |  |  |  |  |  | Was here |\r\n");
        var second = PagesChecklist.Write(pages, owed, out var dropped);

        Assert.Equal(noted.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("something owed | 0 |", "something owed | 1 |", StringComparison.Ordinal), second);
        Assert.Equal(["GonePage"], dropped);
        Assert.Contains("| Orders | Orders | `OrdersPage` \"Orders\" | list + form | `/order` | 1 | 2 |  | OrderRow (2) | SH.ORD.01 | OrdersTests | yes | by text (1) | Not tried as Admin \\| yet |", second, StringComparison.Ordinal);
        Assert.Contains("| `/invoice` | Orders / Invoices |", second, StringComparison.Ordinal);
        Assert.Contains("| Pages with something owed | 1 |", second, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_command_writes_the_document_and_checks_it_against_the_source()
    {
        var suite = Directory.CreateTempSubdirectory("bruinet-pages").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(suite, ".testing"));
            Directory.CreateDirectory(Path.Combine(suite, "Shop", "Modules", "Orders", "Pages"));
            File.WriteAllText(Path.Combine(suite, "Shop", "Shared.cs"), Shared);
            File.WriteAllText(Path.Combine(suite, "Shop", "Modules", "Orders", "Pages", "OrdersPages.cs"), Orders);

            Assert.Equal(1, await Run(suite, "suite", "pages", "--check"));
            Assert.Equal(0, await Run(suite, "suite", "pages", "--write"));
            Assert.Equal(0, await Run(suite, "suite", "pages", "--check"));

            File.AppendAllText(Path.Combine(suite, "Shop", "Modules", "Orders", "Pages", "OrdersPages.cs"),
                "\npublic sealed class NewPage(IHtmlTestContext context) : PageObjectBase<NewPage>(context, \"/new\") { }\n");
            Assert.Equal(1, await Run(suite, "suite", "pages", "--check"));
        }
        finally
        {
            Directory.Delete(suite, recursive: true);
        }
    }

    private static Task<int> Run(string folder, params string[] args)
        => BruinetCommand.RunAsync(args, new CliIo(TextReader.Null, TextWriter.Null, TextWriter.Null, folder, Interactive: false));
}
