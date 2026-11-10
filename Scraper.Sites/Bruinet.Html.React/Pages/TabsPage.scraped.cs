// <scraped page="/tabs" from="2026-10-01-explore-2/001-demosite.nodes.jsonl" hash="f67c5caf" />
using Bruinet.Core.Locators;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Collection;
using Bruinet.Html.Controls.Display;
using Bruinet.Html.Interfaces;
using Bruinet.Html.Pages;

namespace Bruinet.Html.React.Pages;

/// <summary>The page /tabs: drafted by the Scraper from a recording. Draft again to update; add to the other part, TabsPage.cs.</summary>
public sealed partial class TabsPage(IHtmlTestContext context) : PageObjectBase<TabsPage>(context)
{
    public ButtonControl<TabsPage> Button => new(this, Locator.ByCss("button.navbar-toggler"));

    public LinkControl<TabsPage> TextBox => new(this, Locator.ByLinkText("Text Box"));

    public LinkControl<TabsPage> CheckBox => new(this, Locator.ByLinkText("Check Box"));

    public LinkControl<TabsPage> RadioButton => new(this, Locator.ByLinkText("Radio Button"));

    public LinkControl<TabsPage> WebTables => new(this, Locator.ByLinkText("Web Tables"));

    public LinkControl<TabsPage> Buttons => new(this, Locator.ByLinkText("Buttons"));

    public LinkControl<TabsPage> BrokenLinksImages => new(this, Locator.ByLinkText("Broken Links - Images"));

    public LinkControl<TabsPage> UploadAndDownload => new(this, Locator.ByLinkText("Upload and Download"));

    public LinkControl<TabsPage> DynamicProperties => new(this, Locator.ByLinkText("Dynamic Properties"));

    public LinkControl<TabsPage> PracticeForm => new(this, Locator.ByLinkText("Practice Form"));

    public LinkControl<TabsPage> BrowserWindows => new(this, Locator.ByLinkText("Browser Windows"));

    public LinkControl<TabsPage> Alerts => new(this, Locator.ByLinkText("Alerts"));

    public LinkControl<TabsPage> NestedFrames => new(this, Locator.ByLinkText("Nested Frames"));

    public LinkControl<TabsPage> ModalDialogs => new(this, Locator.ByLinkText("Modal Dialogs"));

    public LinkControl<TabsPage> Accordian => new(this, Locator.ByLinkText("Accordian"));

    public LinkControl<TabsPage> AutoComplete => new(this, Locator.ByLinkText("Auto Complete"));

    public LinkControl<TabsPage> DatePicker => new(this, Locator.ByLinkText("Date Picker"));

    public LinkControl<TabsPage> Slider => new(this, Locator.ByLinkText("Slider"));

    public LinkControl<TabsPage> ProgressBar => new(this, Locator.ByLinkText("Progress Bar"));

    public LinkControl<TabsPage> Tabs => new(this, Locator.ByLinkText("Tabs"));

    public LinkControl<TabsPage> ToolTips => new(this, Locator.ByLinkText("Tool Tips"));

    public LinkControl<TabsPage> SelectMenu => new(this, Locator.ByLinkText("Select Menu"));

    public LinkControl<TabsPage> Sortable => new(this, Locator.ByLinkText("Sortable"));

    public LinkControl<TabsPage> Selectable => new(this, Locator.ByLinkText("Selectable"));

    public LinkControl<TabsPage> Resizable => new(this, Locator.ByLinkText("Resizable"));

    public LinkControl<TabsPage> Droppable => new(this, Locator.ByLinkText("Droppable"));

    public LinkControl<TabsPage> Dragabble => new(this, Locator.ByLinkText("Dragabble"));

    public LinkControl<TabsPage> Login => new(this, Locator.ByLinkText("Login"));

    public LinkControl<TabsPage> Profile => new(this, Locator.ByLinkText("Profile"));

    public LinkControl<TabsPage> BookStoreApi => new(this, Locator.ByLinkText("Book Store API"));

    public LabelControl<TabsPage> Tabs2 => new(this, Locator.ByCss("h1:has-text('Tabs')"));

    public ListControl<TabsPage> Demo => new(this, Locator.ById("demo"));

    public ButtonControl<TabsPage> What => new(this, Locator.ById("demo-tab-what"));

    public ButtonControl<TabsPage> Origin => new(this, Locator.ById("demo-tab-origin"));

    public ButtonControl<TabsPage> Use => new(this, Locator.ById("demo-tab-use"));

    public ButtonControl<TabsPage> More => new(this, Locator.ById("demo-tab-more"));

    // Not drafted: no stable locator. Give the element a test id, or pick a locator by hand.
    // LinkControl A: Locator.ByCss("#root > header > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Links: Locator.ByCss("#root > div > div > div > div:nth-of-type(1) > div > div > div:nth-of-type(1) > div > ul > li:nth-of-type(6) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Frames: Locator.ByCss("#root > div > div > div > div:nth-of-type(1) > div > div > div:nth-of-type(3) > div > ul > li:nth-of-type(3) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Menu: Locator.ByCss("#root > div > div > div > div:nth-of-type(1) > div > div > div:nth-of-type(4) > div > ul > li:nth-of-type(8) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl BookStore: Locator.ByCss("#root > div > div > div > div:nth-of-type(1) > div > div > div:nth-of-type(6) > div > ul > li:nth-of-type(2) > a") (nothing stable: its position; ask for a test id in the app)

    // Not drafted: shown only after a click (a state of the page, or a collection's other rows).
    // FrameScope Advertisement: Locator.ByCss("iframe[aria-label='Advertisement']"), in "demosite: Use"
    // LinkControl Conversion0Product0OchImage: Locator.ByCss("a.ns-j3p29-e-6"), in "demosite: Use"
    // LinkControl Conversion0Product1OchImage: Locator.ByCss("a.ns-j3p29-e-11"), in "demosite: Use"
    // LinkControl Conversion0Product2OchImage: Locator.ByCss("a.ns-j3p29-e-16"), in "demosite: Use"
    // LinkControl Conversion0Product3OchImage: Locator.ByCss("a.ns-j3p29-e-24"), in "demosite: Use"
    // LinkControl Conversion0Product0OchLogo: Locator.ByCss("a.ns-j3p29-e-28"), in "demosite: Use"
    // LinkControl Abgl: Locator.ById("abgl"), in "demosite: Use"
    // ButtonControl Cbb: Locator.ById("cbb"), in "demosite: Use"
    // FrameScope GoogleAdsTopFrame: Locator.ById("google_ads_top_frame"), in "demosite: Use"
}
