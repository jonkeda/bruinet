// <scraped page="/tables" from="2026-10-01-explore-2/001-the-internet.nodes.jsonl" hash="0610f8ff" />
using Bruinet.Core.Locators;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Collection;
using Bruinet.Html.Controls.Display;
using Bruinet.Html.Interfaces;
using Bruinet.Html.Pages;

namespace Bruinet.Html.Plain.Pages;

/// <summary>The page /tables: drafted by the Scraper from a recording. Draft again to update; add to the other part, TablesPage.cs.</summary>
public sealed partial class TablesPage(IHtmlTestContext context) : PageObjectBase<TablesPage>(context)
{
    public LabelControl<TablesPage> DataTables => new(this, Locator.ByCss("h3:has-text('Data Tables')"));

    public LabelControl<TablesPage> Example1 => new(this, Locator.ByCss("h4:has-text('Example 1')"));

    public TableControl<TablesPage> Table1 => new(this, Locator.ById("table1"));

    public LabelControl<TablesPage> Example2 => new(this, Locator.ByCss("h4:has-text('Example 2')"));

    public TableControl<TablesPage> Table2 => new(this, Locator.ById("table2"));

    public LinkControl<TablesPage> ElementalSelenium => new(this, Locator.ByLinkText("Elemental Selenium"));

    // Not drafted: no stable locator. Give the element a test id, or pick a locator by hand.
    // LinkControl A: Locator.ByCss("div:nth-of-type(2) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Edit: Locator.ByCss("#table1 > tbody > tr:nth-of-type(1) > td:nth-of-type(6) > a:nth-of-type(1)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Delete: Locator.ByCss("#table1 > tbody > tr:nth-of-type(1) > td:nth-of-type(6) > a:nth-of-type(2)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Edit2: Locator.ByCss("#table1 > tbody > tr:nth-of-type(2) > td:nth-of-type(6) > a:nth-of-type(1)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Delete2: Locator.ByCss("#table1 > tbody > tr:nth-of-type(2) > td:nth-of-type(6) > a:nth-of-type(2)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Edit3: Locator.ByCss("#table1 > tbody > tr:nth-of-type(3) > td:nth-of-type(6) > a:nth-of-type(1)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Delete3: Locator.ByCss("#table1 > tbody > tr:nth-of-type(3) > td:nth-of-type(6) > a:nth-of-type(2)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Edit4: Locator.ByCss("#table1 > tbody > tr:nth-of-type(4) > td:nth-of-type(6) > a:nth-of-type(1)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Delete4: Locator.ByCss("#table1 > tbody > tr:nth-of-type(4) > td:nth-of-type(6) > a:nth-of-type(2)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Edit5: Locator.ByCss("#table2 > tbody > tr:nth-of-type(1) > td:nth-of-type(6) > a:nth-of-type(1)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Delete5: Locator.ByCss("#table2 > tbody > tr:nth-of-type(1) > td:nth-of-type(6) > a:nth-of-type(2)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Edit6: Locator.ByCss("#table2 > tbody > tr:nth-of-type(2) > td:nth-of-type(6) > a:nth-of-type(1)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Delete6: Locator.ByCss("#table2 > tbody > tr:nth-of-type(2) > td:nth-of-type(6) > a:nth-of-type(2)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Edit7: Locator.ByCss("#table2 > tbody > tr:nth-of-type(3) > td:nth-of-type(6) > a:nth-of-type(1)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Delete7: Locator.ByCss("#table2 > tbody > tr:nth-of-type(3) > td:nth-of-type(6) > a:nth-of-type(2)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Edit8: Locator.ByCss("#table2 > tbody > tr:nth-of-type(4) > td:nth-of-type(6) > a:nth-of-type(1)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Delete8: Locator.ByCss("#table2 > tbody > tr:nth-of-type(4) > td:nth-of-type(6) > a:nth-of-type(2)") (nothing stable: its position; ask for a test id in the app)
}
