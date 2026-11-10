// <scraped page="#/en-US/component/tabs" from="2026-10-01-explore-7/001-component-element.nodes.jsonl" hash="7e8705f5" />
using Bruinet.Core.Locators;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;
using Bruinet.Html.Controls.Text;
using Bruinet.Html.Interfaces;
using Bruinet.Html.Pages;

namespace Bruinet.Html.Vue2.Pages;

/// <summary>The page #/en-US/component/tabs: drafted by the Scraper from a recording. Draft again to update; add to the other part, EnUsComponentTabsPage.cs.</summary>
public sealed partial class EnUsComponentTabsPage(IHtmlTestContext context) : PageObjectBase<EnUsComponentTabsPage>(context)
{
    public TextInputControl<EnUsComponentTabsPage> Search => new(this, Locator.ByCss("input[placeholder='Search']"));

    public LinkControl<EnUsComponentTabsPage> Guide => new(this, Locator.ByLinkText("Guide"));

    public LinkControl<EnUsComponentTabsPage> Resource => new(this, Locator.ByLinkText("Resource"));

    public LinkControl<EnUsComponentTabsPage> Changelog => new(this, Locator.ByLinkText("Changelog"));

    public LinkControl<EnUsComponentTabsPage> ElementReact => new(this, Locator.ByLinkText("Element React"));

    public LinkControl<EnUsComponentTabsPage> ElementAngular => new(this, Locator.ByLinkText("Element Angular"));

    public LabelControl<EnUsComponentTabsPage> Development => new(this, Locator.ByLinkText("Development"));

    public LinkControl<EnUsComponentTabsPage> Installation => new(this, Locator.ByLinkText("Installation"));

    public LinkControl<EnUsComponentTabsPage> QuickStart => new(this, Locator.ByLinkText("Quick Start"));

    public LinkControl<EnUsComponentTabsPage> Internationalization => new(this, Locator.ByLinkText("Internationalization"));

    public LinkControl<EnUsComponentTabsPage> CustomTheme => new(this, Locator.ByLinkText("Custom Theme"));

    public LinkControl<EnUsComponentTabsPage> BuiltInTransition => new(this, Locator.ByLinkText("Built-in transition"));

    public LabelControl<EnUsComponentTabsPage> Components => new(this, Locator.ByLinkText("Components"));

    public LinkControl<EnUsComponentTabsPage> LayoutContainer => new(this, Locator.ByLinkText("Layout Container"));

    public LinkControl<EnUsComponentTabsPage> Typography => new(this, Locator.ByLinkText("Typography"));

    public LinkControl<EnUsComponentTabsPage> Border => new(this, Locator.ByLinkText("Border"));

    public LinkControl<EnUsComponentTabsPage> Icon => new(this, Locator.ByLinkText("Icon"));

    public LinkControl<EnUsComponentTabsPage> Button => new(this, Locator.ByLinkText("Button"));

    public LinkControl<EnUsComponentTabsPage> Link => new(this, Locator.ByLinkText("Link"));

    public LinkControl<EnUsComponentTabsPage> Radio => new(this, Locator.ByLinkText("Radio"));

    public LinkControl<EnUsComponentTabsPage> Checkbox => new(this, Locator.ByLinkText("Checkbox"));

    public LinkControl<EnUsComponentTabsPage> InputNumber => new(this, Locator.ByLinkText("InputNumber"));

    public LinkControl<EnUsComponentTabsPage> Select => new(this, Locator.ByLinkText("Select"));

    public LinkControl<EnUsComponentTabsPage> Cascader => new(this, Locator.ByLinkText("Cascader"));

    public LinkControl<EnUsComponentTabsPage> Switch => new(this, Locator.ByLinkText("Switch"));

    public LinkControl<EnUsComponentTabsPage> Slider => new(this, Locator.ByLinkText("Slider"));

    public LinkControl<EnUsComponentTabsPage> DatePicker => new(this, Locator.ByLinkText("DatePicker"));

    public LinkControl<EnUsComponentTabsPage> DateTimePicker => new(this, Locator.ByLinkText("DateTimePicker"));

    public LinkControl<EnUsComponentTabsPage> Upload => new(this, Locator.ByLinkText("Upload"));

    public LinkControl<EnUsComponentTabsPage> Rate => new(this, Locator.ByLinkText("Rate"));

    public LinkControl<EnUsComponentTabsPage> ColorPicker => new(this, Locator.ByLinkText("ColorPicker"));

    public LinkControl<EnUsComponentTabsPage> Transfer => new(this, Locator.ByLinkText("Transfer"));

    public LinkControl<EnUsComponentTabsPage> Form => new(this, Locator.ByLinkText("Form"));

    public LinkControl<EnUsComponentTabsPage> Table => new(this, Locator.ByLinkText("Table"));

    public LinkControl<EnUsComponentTabsPage> Tag => new(this, Locator.ByLinkText("Tag"));

    public LinkControl<EnUsComponentTabsPage> Progress => new(this, Locator.ByLinkText("Progress"));

    public LinkControl<EnUsComponentTabsPage> Tree => new(this, Locator.ByLinkText("Tree"));

    public LinkControl<EnUsComponentTabsPage> Pagination => new(this, Locator.ByLinkText("Pagination"));

    public LinkControl<EnUsComponentTabsPage> Badge => new(this, Locator.ByLinkText("Badge"));

    public LinkControl<EnUsComponentTabsPage> Skeleton => new(this, Locator.ByLinkText("Skeleton"));

    public LinkControl<EnUsComponentTabsPage> Empty => new(this, Locator.ByLinkText("Empty"));

    public LinkControl<EnUsComponentTabsPage> Descriptions => new(this, Locator.ByLinkText("Descriptions"));

    public LinkControl<EnUsComponentTabsPage> Result => new(this, Locator.ByLinkText("Result"));

    public LinkControl<EnUsComponentTabsPage> Statistic => new(this, Locator.ByLinkText("Statistic"));

    public LinkControl<EnUsComponentTabsPage> Alert => new(this, Locator.ByLinkText("Alert"));

    public LinkControl<EnUsComponentTabsPage> Loading => new(this, Locator.ByLinkText("Loading"));

    public LinkControl<EnUsComponentTabsPage> MessageBox => new(this, Locator.ByLinkText("MessageBox"));

    public LinkControl<EnUsComponentTabsPage> Notification => new(this, Locator.ByLinkText("Notification"));

    public LinkControl<EnUsComponentTabsPage> NavMenu => new(this, Locator.ByLinkText("NavMenu"));

    public LinkControl<EnUsComponentTabsPage> Tabs => new(this, Locator.ByLinkText("Tabs"));

    public LinkControl<EnUsComponentTabsPage> Breadcrumb => new(this, Locator.ByLinkText("Breadcrumb"));

    public LinkControl<EnUsComponentTabsPage> PageHeader => new(this, Locator.ByLinkText("PageHeader"));

    public LinkControl<EnUsComponentTabsPage> Dropdown => new(this, Locator.ByLinkText("Dropdown"));

    public LinkControl<EnUsComponentTabsPage> Steps => new(this, Locator.ByLinkText("Steps"));

    public LinkControl<EnUsComponentTabsPage> Dialog => new(this, Locator.ByLinkText("Dialog"));

    public LinkControl<EnUsComponentTabsPage> Tooltip => new(this, Locator.ByLinkText("Tooltip"));

    public LinkControl<EnUsComponentTabsPage> Popover => new(this, Locator.ByLinkText("Popover"));

    public LinkControl<EnUsComponentTabsPage> Popconfirm => new(this, Locator.ByLinkText("Popconfirm"));

    public LinkControl<EnUsComponentTabsPage> Card => new(this, Locator.ByLinkText("Card"));

    public LinkControl<EnUsComponentTabsPage> Carousel => new(this, Locator.ByLinkText("Carousel"));

    public LinkControl<EnUsComponentTabsPage> Collapse => new(this, Locator.ByLinkText("Collapse"));

    public LinkControl<EnUsComponentTabsPage> Timeline => new(this, Locator.ByLinkText("Timeline"));

    public LinkControl<EnUsComponentTabsPage> Divider => new(this, Locator.ByLinkText("Divider"));

    public LinkControl<EnUsComponentTabsPage> Calendar => new(this, Locator.ByLinkText("Calendar"));

    public LinkControl<EnUsComponentTabsPage> Image => new(this, Locator.ByLinkText("Image"));

    public LinkControl<EnUsComponentTabsPage> Backtop => new(this, Locator.ByLinkText("Backtop"));

    public LinkControl<EnUsComponentTabsPage> InfiniteScroll => new(this, Locator.ByLinkText("InfiniteScroll"));

    public LinkControl<EnUsComponentTabsPage> Avatar => new(this, Locator.ByLinkText("Avatar"));

    public LinkControl<EnUsComponentTabsPage> Drawer => new(this, Locator.ByLinkText("Drawer"));

    public LabelControl<EnUsComponentTabsPage> Tabs2 => new(this, Locator.ById("tabs"));

    public LabelControl<EnUsComponentTabsPage> BasicUsage => new(this, Locator.ById("basic-usage"));

    public LabelControl<EnUsComponentTabsPage> CardStyle => new(this, Locator.ById("card-style"));

    public LabelControl<EnUsComponentTabsPage> BorderCard => new(this, Locator.ById("border-card"));

    public LabelControl<EnUsComponentTabsPage> TabPosition => new(this, Locator.ById("tab-position"));

    public LabelControl<EnUsComponentTabsPage> CustomTab => new(this, Locator.ById("custom-tab"));

    public LabelControl<EnUsComponentTabsPage> AddCloseTab => new(this, Locator.ById("add-close-tab"));

    public LabelControl<EnUsComponentTabsPage> CustomizedTriggerButtonOf => new(this, Locator.ById("customized-trigger-button-of-new-tab"));

    public ButtonControl<EnUsComponentTabsPage> AddTab => new(this, Locator.ByCss("button:has-text('add tab')"));

    public LabelControl<EnUsComponentTabsPage> TabsAttributes => new(this, Locator.ById("tabs-attributes"));

    public LabelControl<EnUsComponentTabsPage> TabsEvents => new(this, Locator.ById("tabs-events"));

    public LabelControl<EnUsComponentTabsPage> TabPaneAttributes => new(this, Locator.ById("tab-pane-attributes"));

    // Not drafted: no stable locator. Give the element a test id, or pick a locator by hand.
    // LabelControl H1: Locator.ByCss("#app > div:nth-of-type(1) > header > div > h1") (nothing stable: its position; ask for a test id in the app)
    // LinkControl A: Locator.ByCss("#app > div:nth-of-type(1) > header > div > h1 > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Component: Locator.ByCss("#app > div:nth-of-type(1) > header > div > ul > li:nth-of-type(3) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Theme: Locator.ByCss("#app > div:nth-of-type(1) > header > div > ul > li:nth-of-type(4) > a") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Element21514: Locator.ByCss("#app > div:nth-of-type(1) > header > div > ul > li:nth-of-type(7) > div > span") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl English: Locator.ByCss("#app > div:nth-of-type(1) > header > div > ul > li:nth-of-type(8) > div > span") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Layout: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(1) > div:nth-of-type(1) > div > div > ul > li:nth-of-type(5) > div:nth-of-type(1) > ul > li:nth-of-type(1) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Color: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(1) > div:nth-of-type(1) > div > div > ul > li:nth-of-type(5) > div:nth-of-type(1) > ul > li:nth-of-type(3) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Input: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(1) > div:nth-of-type(1) > div > div > ul > li:nth-of-type(5) > div:nth-of-type(2) > ul > li:nth-of-type(3) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl TimePicker: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(1) > div:nth-of-type(1) > div > div > ul > li:nth-of-type(5) > div:nth-of-type(2) > ul > li:nth-of-type(9) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Message: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(1) > div:nth-of-type(1) > div > div > ul > li:nth-of-type(5) > div:nth-of-type(4) > ul > li:nth-of-type(3) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl A2: Locator.ByCss("#tabs > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl A3: Locator.ByCss("#basic-usage > a") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(1) > div:nth-of-type(1) > div > div > div:nth-of-type(1) > div > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl TryIt: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(1) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LinkControl A4: Locator.ByCss("#card-style > a") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div2: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(2) > div:nth-of-type(1) > div > div > div:nth-of-type(1) > div > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl TryIt2: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(2) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LinkControl A5: Locator.ByCss("#border-card > a") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div3: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(3) > div:nth-of-type(1) > div > div > div:nth-of-type(1) > div > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl TryIt3: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(3) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LinkControl A6: Locator.ByCss("#tab-position > a") (nothing stable: its position; ask for a test id in the app)
    // RadioButtonControl Input2: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(4) > div:nth-of-type(1) > div > div:nth-of-type(1) > label:nth-of-type(1) > input") (nothing stable: its position; ask for a test id in the app)
    // RadioButtonControl Input3: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(4) > div:nth-of-type(1) > div > div:nth-of-type(1) > label:nth-of-type(2) > input") (nothing stable: its position; ask for a test id in the app)
    // RadioButtonControl Input4: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(4) > div:nth-of-type(1) > div > div:nth-of-type(1) > label:nth-of-type(3) > input") (nothing stable: its position; ask for a test id in the app)
    // RadioButtonControl Input5: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(4) > div:nth-of-type(1) > div > div:nth-of-type(1) > label:nth-of-type(4) > input") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div4: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(4) > div:nth-of-type(1) > div > div:nth-of-type(2) > div:nth-of-type(1) > div > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl TryIt4: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(4) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LinkControl A7: Locator.ByCss("#custom-tab > a") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div5: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(5) > div:nth-of-type(1) > div > div > div:nth-of-type(1) > div > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl TryIt5: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(5) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LinkControl A8: Locator.ByCss("#add-close-tab > a") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div6: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(6) > div:nth-of-type(1) > div > div > div:nth-of-type(1) > div > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl TryIt6: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(6) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LinkControl A9: Locator.ByCss("#customized-trigger-button-of-new-tab > a") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div7: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(7) > div:nth-of-type(1) > div > div:nth-of-type(2) > div:nth-of-type(1) > div > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl TryIt7: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > div:nth-of-type(7) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LinkControl A10: Locator.ByCss("#tabs-attributes > a") (nothing stable: its position; ask for a test id in the app)
    // TableControl Table2: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > table:nth-of-type(1)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl A11: Locator.ByCss("#tabs-events > a") (nothing stable: its position; ask for a test id in the app)
    // TableControl Table3: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > table:nth-of-type(2)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl A12: Locator.ByCss("#tab-pane-attributes > a") (nothing stable: its position; ask for a test id in the app)
    // TableControl Table4: Locator.ByCss("#app > div:nth-of-type(2) > div > div:nth-of-type(1) > div > div > div:nth-of-type(2) > section > table:nth-of-type(3)") (nothing stable: its position; ask for a test id in the app)
}
