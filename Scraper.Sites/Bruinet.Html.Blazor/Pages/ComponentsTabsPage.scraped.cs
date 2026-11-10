// <scraped page="/components/tabs" from="2026-10-01-explore-2/001-tabs-mudblazor.nodes.jsonl" hash="68c85e04" />
using Bruinet.Core.Locators;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;
using Bruinet.Html.Controls.Text;
using Bruinet.Html.Interfaces;
using Bruinet.Html.Pages;

namespace Bruinet.Html.Blazor.Pages;

/// <summary>The page /components/tabs: drafted by the Scraper from a recording. Draft again to update; add to the other part, ComponentsTabsPage.cs.</summary>
public sealed partial class ComponentsTabsPage(IHtmlTestContext context) : PageObjectBase<ComponentsTabsPage>(context)
{
    public ButtonControl<ComponentsTabsPage> OpenSearchDialog => new(this, Locator.ByCss("button[aria-label='Open search dialog']"));

    public LinkControl<ComponentsTabsPage> MudBlazor3 => new(this, Locator.ByCss("a.me-4"));

    public LinkControl<ComponentsTabsPage> GetStarted => new(this, Locator.ByLinkText("Get Started"));

    public LinkControl<ComponentsTabsPage> Docs => new(this, Locator.ByLinkText("Docs"));

    public ButtonControl<ComponentsTabsPage> Projects => new(this, Locator.ByCss("button:has-text('Projects')"));

    public ButtonControl<ComponentsTabsPage> SearchAdornment => new(this, Locator.ByCss("button[aria-label='Search adornment']"));

    public TextInputControl<ComponentsTabsPage> SearchCtrlK => new(this, Locator.ByCss("input[placeholder='Search (Ctrl+K)']"));

    public LinkControl<ComponentsTabsPage> OpenGitHubRepository => new(this, Locator.ByCss("a[aria-label='Open GitHub repository']"));

    public ButtonControl<ComponentsTabsPage> ToggleGettingStarted => new(this, Locator.ByCss("button[aria-label='Toggle Getting Started']"));

    public LinkControl<ComponentsTabsPage> Installation => new(this, Locator.ByLinkText("Installation"));

    public LinkControl<ComponentsTabsPage> Layouts => new(this, Locator.ByLinkText("Layouts"));

    public LinkControl<ComponentsTabsPage> Usage => new(this, Locator.ByLinkText("Usage"));

    public LinkControl<ComponentsTabsPage> Wireframes => new(this, Locator.ByLinkText("Wireframes"));

    public LinkControl<ComponentsTabsPage> Explore => new(this, Locator.ByLinkText("Explore"));

    public ButtonControl<ComponentsTabsPage> ToggleComponents => new(this, Locator.ByCss("button[aria-label='Toggle Components']"));

    public ButtonControl<ComponentsTabsPage> ToggleButtons => new(this, Locator.ByCss("button[aria-label='Toggle Buttons']"));

    public ButtonControl<ComponentsTabsPage> ToggleCharts => new(this, Locator.ByCss("button[aria-label='Toggle Charts']"));

    public ButtonControl<ComponentsTabsPage> ToggleFormInputs => new(this, Locator.ByCss("button[aria-label='Toggle Form & Inputs']"));

    public ButtonControl<ComponentsTabsPage> TogglePickers => new(this, Locator.ByCss("button[aria-label='Toggle Pickers']"));

    public ButtonControl<ComponentsTabsPage> ToggleComponentApi => new(this, Locator.ByCss("button[aria-label='Toggle Component API']"));

    public LinkControl<ComponentsTabsPage> ApiIndex => new(this, Locator.ByLinkText("API Index"));

    public ButtonControl<ComponentsTabsPage> ToggleFeatures => new(this, Locator.ByCss("button[aria-label='Toggle Features']"));

    public LinkControl<ComponentsTabsPage> Analyzers => new(this, Locator.ByLinkText("Analyzers"));

    public LinkControl<ComponentsTabsPage> Breakpoints => new(this, Locator.ByLinkText("Breakpoints"));

    public LinkControl<ComponentsTabsPage> Colors => new(this, Locator.ByLinkText("Colors"));

    public LinkControl<ComponentsTabsPage> Converters => new(this, Locator.ByLinkText("Converters"));

    public LinkControl<ComponentsTabsPage> Elevation => new(this, Locator.ByLinkText("Elevation"));

    public LinkControl<ComponentsTabsPage> IconReference => new(this, Locator.ByLinkText("Icon Reference"));

    public LinkControl<ComponentsTabsPage> Localization => new(this, Locator.ByLinkText("Localization"));

    public LinkControl<ComponentsTabsPage> Masking => new(this, Locator.ByLinkText("Masking"));

    public LinkControl<ComponentsTabsPage> ParameterState => new(this, Locator.ByLinkText("Parameter State"));

    public LinkControl<ComponentsTabsPage> RtlLanguages => new(this, Locator.ByLinkText("RTL Languages"));

    public LinkControl<ComponentsTabsPage> Services => new(this, Locator.ByLinkText("Services"));

    public ButtonControl<ComponentsTabsPage> ToggleCustomization => new(this, Locator.ByCss("button[aria-label='Toggle Customization']"));

    public LinkControl<ComponentsTabsPage> DefaultTheme => new(this, Locator.ByLinkText("Default theme"));

    public LinkControl<ComponentsTabsPage> Overview => new(this, Locator.ByLinkText("Overview"));

    public LinkControl<ComponentsTabsPage> Palette => new(this, Locator.ByLinkText("Palette"));

    public LinkControl<ComponentsTabsPage> PseudoCss => new(this, Locator.ByLinkText("Pseudo CSS"));

    public LinkControl<ComponentsTabsPage> Globals => new(this, Locator.ByLinkText("Globals"));

    public ButtonControl<ComponentsTabsPage> ToggleCssUtilities => new(this, Locator.ByCss("button[aria-label='Toggle CSS Utilities']"));

    public LinkControl<ComponentsTabsPage> Display => new(this, Locator.ByLinkText("Display"));

    public LinkControl<ComponentsTabsPage> Overflow => new(this, Locator.ByLinkText("Overflow"));

    public LinkControl<ComponentsTabsPage> Visibility => new(this, Locator.ByLinkText("Visibility"));

    public LinkControl<ComponentsTabsPage> ObjectFit => new(this, Locator.ByLinkText("Object Fit"));

    public LinkControl<ComponentsTabsPage> ObjectPosition => new(this, Locator.ByLinkText("Object Position"));

    public LinkControl<ComponentsTabsPage> EnableFlexbox => new(this, Locator.ByLinkText("Enable Flexbox"));

    public LinkControl<ComponentsTabsPage> FlexDirection => new(this, Locator.ByLinkText("Flex Direction"));

    public LinkControl<ComponentsTabsPage> FlexWrap => new(this, Locator.ByLinkText("Flex Wrap"));

    public LinkControl<ComponentsTabsPage> FlexGrow => new(this, Locator.ByLinkText("Flex Grow"));

    public LinkControl<ComponentsTabsPage> FlexShrink => new(this, Locator.ByLinkText("Flex Shrink"));

    public LinkControl<ComponentsTabsPage> Gap => new(this, Locator.ByLinkText("Gap"));

    public LinkControl<ComponentsTabsPage> JustifyContent => new(this, Locator.ByLinkText("Justify Content"));

    public LinkControl<ComponentsTabsPage> AlignContent => new(this, Locator.ByLinkText("Align Content"));

    public LinkControl<ComponentsTabsPage> AlignItems => new(this, Locator.ByLinkText("Align Items"));

    public LinkControl<ComponentsTabsPage> AlignSelf => new(this, Locator.ByLinkText("Align Self"));

    public LinkControl<ComponentsTabsPage> Spacing => new(this, Locator.ByLinkText("Spacing"));

    public LinkControl<ComponentsTabsPage> BorderRadius => new(this, Locator.ByLinkText("Border Radius"));

    public LinkControl<ComponentsTabsPage> BorderStyle => new(this, Locator.ByLinkText("Border Style"));

    public LinkControl<ComponentsTabsPage> BorderWidth => new(this, Locator.ByLinkText("Border Width"));

    public LinkControl<ComponentsTabsPage> Cursor => new(this, Locator.ByLinkText("Cursor"));

    public LinkControl<ComponentsTabsPage> PointerEvents => new(this, Locator.ByLinkText("Pointer Events"));

    public ButtonControl<ComponentsTabsPage> ToggleLearnMore => new(this, Locator.ByCss("button[aria-label='Toggle Learn More']"));

    public LinkControl<ComponentsTabsPage> WhatIsMudBlazor => new(this, Locator.ByLinkText("What is MudBlazor?"));

    public LinkControl<ComponentsTabsPage> Announcements => new(this, Locator.ByLinkText("Announcements"));

    public LinkControl<ComponentsTabsPage> GettingHelp => new(this, Locator.ByLinkText("Getting Help"));

    public LinkControl<ComponentsTabsPage> ReportingBugs => new(this, Locator.ByLinkText("Reporting Bugs"));

    public LinkControl<ComponentsTabsPage> Contribution => new(this, Locator.ByLinkText("Contribution"));

    public LinkControl<ComponentsTabsPage> CommunityExtensions => new(this, Locator.ByLinkText("Community Extensions"));

    public LinkControl<ComponentsTabsPage> Releases => new(this, Locator.ByLinkText("Releases"));

    public LinkControl<ComponentsTabsPage> Roadmap => new(this, Locator.ByLinkText("Roadmap"));

    public LinkControl<ComponentsTabsPage> SponsorsBackers => new(this, Locator.ByLinkText("Sponsors & Backers"));

    public LinkControl<ComponentsTabsPage> TeamContributors => new(this, Locator.ByLinkText("Team & Contributors"));

    public LinkControl<ComponentsTabsPage> HowItStarted => new(this, Locator.ByLinkText("How it Started"));

    public LabelControl<ComponentsTabsPage> Tabs3 => new(this, Locator.ByCss("h4:has-text('Tabs')"));

    public LinkControl<ComponentsTabsPage> MudTabs => new(this, Locator.ByLinkText("<MudTabs>"));

    public ButtonControl<ComponentsTabsPage> HideCode => new(this, Locator.ByCss("button:has-text('Hide code')"));

    public LabelControl<ComponentsTabsPage> TabOne => new(this, Locator.ByCss("h6:has-text('Tab One')"));

    public LabelControl<ComponentsTabsPage> ItemTwo => new(this, Locator.ByCss("h6:has-text('Item Two')"));

    public ButtonControl<ComponentsTabsPage> Index0 => new(this, Locator.ByCss("button:has-text('Index 0')"));

    public ButtonControl<ComponentsTabsPage> Index1 => new(this, Locator.ByCss("button:has-text('Index 1')"));

    public ButtonControl<ComponentsTabsPage> Index2 => new(this, Locator.ByCss("button:has-text('Index 2')"));

    public ButtonControl<ComponentsTabsPage> AddTab => new(this, Locator.ByCss("button[aria-label='Add tab']"));

    public ButtonControl<ComponentsTabsPage> Restore => new(this, Locator.ByCss("button:has-text('Restore')"));

    public ButtonControl<ComponentsTabsPage> Panel1Counter0 => new(this, Locator.ByCss("button:has-text('Panel 1 Counter: 0')"));

    public LinkControl<ComponentsTabsPage> A3 => new(this, Locator.ByCss("a.carbon-img"));

    public LinkControl<ComponentsTabsPage> KiesEenGameserverMet => new(this, Locator.ByCss("a.carbon-text"));

    public LinkControl<ComponentsTabsPage> AdsViaCarbon => new(this, Locator.ByLinkText("ads via Carbon"));

    public ButtonControl<ComponentsTabsPage> Button5 => new(this, Locator.ByCss("button.mud-fab"));

    public LabelControl<ComponentsTabsPage> WeUseCookies => new(this, Locator.ByCss("h6:has-text('We use cookies')"));

    public ButtonControl<ComponentsTabsPage> Accept => new(this, Locator.ByCss("button:has-text('Accept')"));

    public ButtonControl<ComponentsTabsPage> Close => new(this, Locator.ByCss("button:has-text('Close')"));

    public LinkControl<ComponentsTabsPage> Reload => new(this, Locator.ByLinkText("Reload"));

    public LabelControl<ComponentsTabsPage> A4 => new(this, Locator.ByLinkText("🗙"));

    // Not drafted: no stable locator. Give the element a test id, or pick a locator by hand.
    // ButtonControl ToggleDrawer: Locator.ByCss("div:nth-of-type(1) > div:nth-of-type(3) > header:nth-of-type(2) > div > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // LinkControl MudBlazor: Locator.ByCss("div:nth-of-type(1) > div:nth-of-type(3) > header:nth-of-type(2) > div > div:nth-of-type(1) > a") (nothing stable: its position; ask for a test id in the app)
    // LabelControl MudBlazor2: Locator.ByCss("div:nth-of-type(1) > div:nth-of-type(3) > header:nth-of-type(2) > div > div:nth-of-type(1) > a > h5") (nothing stable: its position; ask for a test id in the app)
    // LabelControl MudBlazor4: Locator.ByCss("div:nth-of-type(1) > div:nth-of-type(3) > header:nth-of-type(2) > div > div:nth-of-type(2) > a:nth-of-type(1) > h5") (nothing stable: its position; ask for a test id in the app)
    // LinkControl LearnMore: Locator.ByCss("div:nth-of-type(1) > div:nth-of-type(3) > header:nth-of-type(2) > div > div:nth-of-type(2) > a:nth-of-type(4)") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Notifications: Locator.ByCss("div:nth-of-type(1) > div:nth-of-type(3) > header:nth-of-type(2) > div > div:nth-of-type(2) > div:nth-of-type(4) > span > div > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl RightToLeft: Locator.ByCss("div:nth-of-type(1) > div:nth-of-type(3) > header:nth-of-type(2) > div > div:nth-of-type(2) > div:nth-of-type(5) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl LightMode: Locator.ByCss("div:nth-of-type(1) > div:nth-of-type(3) > header:nth-of-type(2) > div > div:nth-of-type(2) > div:nth-of-type(6) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ToggleDrawer2: Locator.ByCss("div:nth-of-type(1) > div:nth-of-type(3) > aside > div > div > div > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Notifications2: Locator.ByCss("div:nth-of-type(1) > div:nth-of-type(3) > aside > div > div > div > div:nth-of-type(2) > span > div > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl RightToLeft2: Locator.ByCss("div:nth-of-type(1) > div:nth-of-type(3) > aside > div > div > div > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl LightMode2: Locator.ByCss("div:nth-of-type(1) > div:nth-of-type(3) > aside > div > div > div > div:nth-of-type(4) > button") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Alert: Locator.ByCss("#s3qkibcso > div:nth-of-type(1) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl AppBar: Locator.ByCss("#s3qkibcso > div:nth-of-type(2) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Avatar: Locator.ByCss("#s3qkibcso > div:nth-of-type(3) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Badge: Locator.ByCss("#s3qkibcso > div:nth-of-type(4) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Breadcrumbs: Locator.ByCss("#s3qkibcso > div:nth-of-type(5) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl BreakpointProvider: Locator.ByCss("#s3qkibcso > div:nth-of-type(6) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Button: Locator.ByCss("#afybhin9q > div:nth-of-type(1) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ButtonFab: Locator.ByCss("#afybhin9q > div:nth-of-type(2) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ButtonFabMenu: Locator.ByCss("#afybhin9q > div:nth-of-type(3) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ButtonGroup: Locator.ByCss("#afybhin9q > div:nth-of-type(4) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl IconButton: Locator.ByCss("#afybhin9q > div:nth-of-type(5) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ToggleIconButton: Locator.ByCss("#afybhin9q > div:nth-of-type(6) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Card: Locator.ByCss("#s3qkibcso > div:nth-of-type(7) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Carousel: Locator.ByCss("#s3qkibcso > div:nth-of-type(8) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl BarChart: Locator.ByCss("#jf79lmggf > div:nth-of-type(1) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl DonutChart: Locator.ByCss("#jf79lmggf > div:nth-of-type(2) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl HeatMapChart: Locator.ByCss("#jf79lmggf > div:nth-of-type(3) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl LineChart: Locator.ByCss("#jf79lmggf > div:nth-of-type(4) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl PieChart: Locator.ByCss("#jf79lmggf > div:nth-of-type(5) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl RadarChart: Locator.ByCss("#jf79lmggf > div:nth-of-type(6) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl RoseChart: Locator.ByCss("#jf79lmggf > div:nth-of-type(7) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl SankeyChart: Locator.ByCss("#jf79lmggf > div:nth-of-type(8) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ScatterPlotChart: Locator.ByCss("#jf79lmggf > div:nth-of-type(9) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl StackedBarChart: Locator.ByCss("#jf79lmggf > div:nth-of-type(10) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl TimeSeriesChart: Locator.ByCss("#jf79lmggf > div:nth-of-type(11) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl UniversalChart: Locator.ByCss("#jf79lmggf > div:nth-of-type(12) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ChipSet: Locator.ByCss("#s3qkibcso > div:nth-of-type(9) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Chips: Locator.ByCss("#s3qkibcso > div:nth-of-type(10) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Collapse: Locator.ByCss("#s3qkibcso > div:nth-of-type(11) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Container: Locator.ByCss("#s3qkibcso > div:nth-of-type(12) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl DataGrid: Locator.ByCss("#s3qkibcso > div:nth-of-type(13) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Dialog: Locator.ByCss("#s3qkibcso > div:nth-of-type(14) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Divider: Locator.ByCss("#s3qkibcso > div:nth-of-type(15) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Drawer: Locator.ByCss("#s3qkibcso > div:nth-of-type(16) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl DropZone: Locator.ByCss("#s3qkibcso > div:nth-of-type(17) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Element: Locator.ByCss("#s3qkibcso > div:nth-of-type(18) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ExitPrompt: Locator.ByCss("#s3qkibcso > div:nth-of-type(19) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ExpansionPanels: Locator.ByCss("#s3qkibcso > div:nth-of-type(20) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl FocusTrap: Locator.ByCss("#s3qkibcso > div:nth-of-type(21) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Autocomplete: Locator.ByCss("#ej2hve4qe > div:nth-of-type(1) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl CheckBox: Locator.ByCss("#ej2hve4qe > div:nth-of-type(2) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Field: Locator.ByCss("#ej2hve4qe > div:nth-of-type(3) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl FileUpload: Locator.ByCss("#ej2hve4qe > div:nth-of-type(4) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Form: Locator.ByCss("#ej2hve4qe > div:nth-of-type(5) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl NumericField: Locator.ByCss("#ej2hve4qe > div:nth-of-type(6) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Radio: Locator.ByCss("#ej2hve4qe > div:nth-of-type(7) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Select: Locator.ByCss("#ej2hve4qe > div:nth-of-type(8) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Slider: Locator.ByCss("#ej2hve4qe > div:nth-of-type(9) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Switch: Locator.ByCss("#ej2hve4qe > div:nth-of-type(10) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl TextField: Locator.ByCss("#ej2hve4qe > div:nth-of-type(11) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ToggleGroup: Locator.ByCss("#ej2hve4qe > div:nth-of-type(12) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Grid: Locator.ByCss("#s3qkibcso > div:nth-of-type(22) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Hidden: Locator.ByCss("#s3qkibcso > div:nth-of-type(23) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Highlighter: Locator.ByCss("#s3qkibcso > div:nth-of-type(24) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Hotkey: Locator.ByCss("#s3qkibcso > div:nth-of-type(25) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Icons: Locator.ByCss("#s3qkibcso > div:nth-of-type(26) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Image: Locator.ByCss("#s3qkibcso > div:nth-of-type(27) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Link: Locator.ByCss("#s3qkibcso > div:nth-of-type(28) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl List: Locator.ByCss("#s3qkibcso > div:nth-of-type(29) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Menu: Locator.ByCss("#s3qkibcso > div:nth-of-type(30) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl MessageBox: Locator.ByCss("#s3qkibcso > div:nth-of-type(31) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl NavMenu: Locator.ByCss("#s3qkibcso > div:nth-of-type(32) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Overlay: Locator.ByCss("#s3qkibcso > div:nth-of-type(33) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Pagination: Locator.ByCss("#s3qkibcso > div:nth-of-type(34) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Paper: Locator.ByCss("#s3qkibcso > div:nth-of-type(35) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ColorPicker: Locator.ByCss("#ckwzjmgm8 > div:nth-of-type(1) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl DatePicker: Locator.ByCss("#ckwzjmgm8 > div:nth-of-type(2) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl DateRangePicker: Locator.ByCss("#ckwzjmgm8 > div:nth-of-type(3) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl TimePicker: Locator.ByCss("#ckwzjmgm8 > div:nth-of-type(4) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Popover: Locator.ByCss("#s3qkibcso > div:nth-of-type(36) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Progress: Locator.ByCss("#s3qkibcso > div:nth-of-type(37) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Rating: Locator.ByCss("#s3qkibcso > div:nth-of-type(38) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ScrollToTop: Locator.ByCss("#s3qkibcso > div:nth-of-type(39) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl SimpleTable: Locator.ByCss("#s3qkibcso > div:nth-of-type(40) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Skeleton: Locator.ByCss("#s3qkibcso > div:nth-of-type(41) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Snackbar: Locator.ByCss("#s3qkibcso > div:nth-of-type(42) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Spacer: Locator.ByCss("#s3qkibcso > div:nth-of-type(43) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl SplitPanel: Locator.ByCss("#s3qkibcso > div:nth-of-type(44) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Stack: Locator.ByCss("#s3qkibcso > div:nth-of-type(45) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Stepper: Locator.ByCss("#s3qkibcso > div:nth-of-type(46) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl SwipeArea: Locator.ByCss("#s3qkibcso > div:nth-of-type(47) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Table: Locator.ByCss("#s3qkibcso > div:nth-of-type(48) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Tabs: Locator.ByCss("#s3qkibcso > div:nth-of-type(49) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Timeline: Locator.ByCss("#s3qkibcso > div:nth-of-type(50) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ToolBar: Locator.ByCss("#s3qkibcso > div:nth-of-type(51) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Tooltip: Locator.ByCss("#s3qkibcso > div:nth-of-type(52) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl TreeView: Locator.ByCss("#s3qkibcso > div:nth-of-type(53) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Typography: Locator.ByCss("#s3qkibcso > div:nth-of-type(54) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Alert2: Locator.ByCss("#bf1e45usr > div:nth-of-type(2) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl AppBar2: Locator.ByCss("#bf1e45usr > div:nth-of-type(3) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Autocomplete2: Locator.ByCss("#bf1e45usr > div:nth-of-type(4) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Avatar2: Locator.ByCss("#bf1e45usr > div:nth-of-type(5) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Badge2: Locator.ByCss("#bf1e45usr > div:nth-of-type(6) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl BarChart2: Locator.ByCss("#bf1e45usr > div:nth-of-type(7) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Breadcrumbs2: Locator.ByCss("#bf1e45usr > div:nth-of-type(8) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl BreakpointProvider2: Locator.ByCss("#bf1e45usr > div:nth-of-type(9) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Button2: Locator.ByCss("#bf1e45usr > div:nth-of-type(10) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ButtonFab2: Locator.ByCss("#bf1e45usr > div:nth-of-type(11) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ButtonFabMenu2: Locator.ByCss("#bf1e45usr > div:nth-of-type(12) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ButtonGroup2: Locator.ByCss("#bf1e45usr > div:nth-of-type(13) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Card2: Locator.ByCss("#bf1e45usr > div:nth-of-type(14) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Carousel2: Locator.ByCss("#bf1e45usr > div:nth-of-type(15) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl CheckBox2: Locator.ByCss("#bf1e45usr > div:nth-of-type(16) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ChipSet2: Locator.ByCss("#bf1e45usr > div:nth-of-type(17) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Chips2: Locator.ByCss("#bf1e45usr > div:nth-of-type(18) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Collapse2: Locator.ByCss("#bf1e45usr > div:nth-of-type(19) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ColorPicker2: Locator.ByCss("#bf1e45usr > div:nth-of-type(20) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Container2: Locator.ByCss("#bf1e45usr > div:nth-of-type(21) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl DataGrid2: Locator.ByCss("#bf1e45usr > div:nth-of-type(22) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl DatePicker2: Locator.ByCss("#bf1e45usr > div:nth-of-type(23) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl DateRangePicker2: Locator.ByCss("#bf1e45usr > div:nth-of-type(24) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Dialog2: Locator.ByCss("#bf1e45usr > div:nth-of-type(25) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Divider2: Locator.ByCss("#bf1e45usr > div:nth-of-type(26) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl DonutChart2: Locator.ByCss("#bf1e45usr > div:nth-of-type(27) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Drawer2: Locator.ByCss("#bf1e45usr > div:nth-of-type(28) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl DropZone2: Locator.ByCss("#bf1e45usr > div:nth-of-type(29) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Element2: Locator.ByCss("#bf1e45usr > div:nth-of-type(30) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ExitPrompt2: Locator.ByCss("#bf1e45usr > div:nth-of-type(31) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ExpansionPanels2: Locator.ByCss("#bf1e45usr > div:nth-of-type(32) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Field2: Locator.ByCss("#bf1e45usr > div:nth-of-type(33) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl FileUpload2: Locator.ByCss("#bf1e45usr > div:nth-of-type(34) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl FocusTrap2: Locator.ByCss("#bf1e45usr > div:nth-of-type(35) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Form2: Locator.ByCss("#bf1e45usr > div:nth-of-type(36) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Grid2: Locator.ByCss("#bf1e45usr > div:nth-of-type(37) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl HeatMapChart2: Locator.ByCss("#bf1e45usr > div:nth-of-type(38) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Hidden2: Locator.ByCss("#bf1e45usr > div:nth-of-type(39) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Highlighter2: Locator.ByCss("#bf1e45usr > div:nth-of-type(40) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Hotkey2: Locator.ByCss("#bf1e45usr > div:nth-of-type(41) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl IconButton2: Locator.ByCss("#bf1e45usr > div:nth-of-type(42) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Icons2: Locator.ByCss("#bf1e45usr > div:nth-of-type(43) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Image2: Locator.ByCss("#bf1e45usr > div:nth-of-type(44) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl LineChart2: Locator.ByCss("#bf1e45usr > div:nth-of-type(45) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Link2: Locator.ByCss("#bf1e45usr > div:nth-of-type(46) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl List2: Locator.ByCss("#bf1e45usr > div:nth-of-type(47) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Menu2: Locator.ByCss("#bf1e45usr > div:nth-of-type(48) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl MessageBox2: Locator.ByCss("#bf1e45usr > div:nth-of-type(49) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl NavMenu2: Locator.ByCss("#bf1e45usr > div:nth-of-type(50) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl NumericField2: Locator.ByCss("#bf1e45usr > div:nth-of-type(51) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Overlay2: Locator.ByCss("#bf1e45usr > div:nth-of-type(52) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Pagination2: Locator.ByCss("#bf1e45usr > div:nth-of-type(53) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Paper2: Locator.ByCss("#bf1e45usr > div:nth-of-type(54) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl PieChart2: Locator.ByCss("#bf1e45usr > div:nth-of-type(55) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Popover2: Locator.ByCss("#bf1e45usr > div:nth-of-type(56) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Progress2: Locator.ByCss("#bf1e45usr > div:nth-of-type(57) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl RadarChart2: Locator.ByCss("#bf1e45usr > div:nth-of-type(58) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Radio2: Locator.ByCss("#bf1e45usr > div:nth-of-type(59) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Rating2: Locator.ByCss("#bf1e45usr > div:nth-of-type(60) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl RoseChart2: Locator.ByCss("#bf1e45usr > div:nth-of-type(61) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl SankeyChart2: Locator.ByCss("#bf1e45usr > div:nth-of-type(62) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ScatterPlotChart2: Locator.ByCss("#bf1e45usr > div:nth-of-type(63) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ScrollToTop2: Locator.ByCss("#bf1e45usr > div:nth-of-type(64) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Select2: Locator.ByCss("#bf1e45usr > div:nth-of-type(65) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl SimpleTable2: Locator.ByCss("#bf1e45usr > div:nth-of-type(66) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Skeleton2: Locator.ByCss("#bf1e45usr > div:nth-of-type(67) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Slider2: Locator.ByCss("#bf1e45usr > div:nth-of-type(68) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Snackbar2: Locator.ByCss("#bf1e45usr > div:nth-of-type(69) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Spacer2: Locator.ByCss("#bf1e45usr > div:nth-of-type(70) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl SplitPanel2: Locator.ByCss("#bf1e45usr > div:nth-of-type(71) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Stack2: Locator.ByCss("#bf1e45usr > div:nth-of-type(72) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl StackedBarChart2: Locator.ByCss("#bf1e45usr > div:nth-of-type(73) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Stepper2: Locator.ByCss("#bf1e45usr > div:nth-of-type(74) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl SwipeArea2: Locator.ByCss("#bf1e45usr > div:nth-of-type(75) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Switch2: Locator.ByCss("#bf1e45usr > div:nth-of-type(76) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Table2: Locator.ByCss("#bf1e45usr > div:nth-of-type(77) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Tabs2: Locator.ByCss("#bf1e45usr > div:nth-of-type(78) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl TextField2: Locator.ByCss("#bf1e45usr > div:nth-of-type(79) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl TimePicker2: Locator.ByCss("#bf1e45usr > div:nth-of-type(80) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl TimeSeriesChart2: Locator.ByCss("#bf1e45usr > div:nth-of-type(81) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Timeline2: Locator.ByCss("#bf1e45usr > div:nth-of-type(82) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ToggleGroup2: Locator.ByCss("#bf1e45usr > div:nth-of-type(83) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ToggleIconButton2: Locator.ByCss("#bf1e45usr > div:nth-of-type(84) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ToolBar2: Locator.ByCss("#bf1e45usr > div:nth-of-type(85) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Tooltip2: Locator.ByCss("#bf1e45usr > div:nth-of-type(86) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl TreeView2: Locator.ByCss("#bf1e45usr > div:nth-of-type(87) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Typography2: Locator.ByCss("#bf1e45usr > div:nth-of-type(88) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl UniversalChart2: Locator.ByCss("#bf1e45usr > div:nth-of-type(89) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Typography3: Locator.ByCss("#c10xu0svc > div:nth-of-type(5) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ZIndex: Locator.ByCss("#c10xu0svc > div:nth-of-type(6) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl ZIndex2: Locator.ByCss("#fak1kwi9v > div:nth-of-type(3) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Position: Locator.ByCss("#fak1kwi9v > div:nth-of-type(8) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Flex: Locator.ByCss("#fak1kwi9v > div:nth-of-type(13) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Order: Locator.ByCss("#fak1kwi9v > div:nth-of-type(16) > a") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H5: Locator.ByCss("#api > h5") (nothing stable: its position; ask for a test id in the app)
    // LinkControl A: Locator.ByCss("#docspage > div:nth-of-type(2) > div:nth-of-type(2) > ul > li:nth-of-type(2) > a") (nothing stable: its position; ask for a test id in the app)
    // LinkControl A2: Locator.ByCss("#docspage > div:nth-of-type(2) > div:nth-of-type(2) > ul > li:nth-of-type(3) > a") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H52: Locator.ByCss("#simple-tabs > h5") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(1) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div: Locator.ByCss("#tabac8jbgvh > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(1) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H53: Locator.ByCss("#icon-tabs > h5") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run2: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(2) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(2) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div2: Locator.ByCss("#tabappwr4xe > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard2: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(2) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H54: Locator.ByCss("#tabs-position > h5") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run3: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(3) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode2: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(3) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div3: Locator.ByCss("#tabh4ucz4qe > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard3: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(3) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H6: Locator.ByCss("#tabs-position-centered > h6") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run4: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(3) > div:nth-of-type(4) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode3: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(3) > div:nth-of-type(4) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div4: Locator.ByCss("#tabhszvxalb > div > div") (nothing stable: its position; ask for a test id in the app)
    // CheckBoxControl Input: Locator.ByCss("#switchwsck8la7 > span:nth-of-type(1) > span:nth-of-type(1) > span > input") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard4: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(3) > div:nth-of-type(4) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H55: Locator.ByCss("#tab-sorting > h5") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run5: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(4) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode4: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(4) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div5: Locator.ByCss("#tab39s4cykz > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard5: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(4) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H62: Locator.ByCss("#tab-sorting-sort-keys > h6") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run6: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(4) > div:nth-of-type(4) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode5: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(4) > div:nth-of-type(4) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div6: Locator.ByCss("#tabwi0o4id3 > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard6: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(4) > div:nth-of-type(4) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H63: Locator.ByCss("#tab-sorting-custom-sorting > h6") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run7: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(4) > div:nth-of-type(5) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode6: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(4) > div:nth-of-type(5) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div7: Locator.ByCss("#tabnmgckdcs > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard7: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(4) > div:nth-of-type(5) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H56: Locator.ByCss("#tab-width > h5") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run8: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(5) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode7: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(5) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div8: Locator.ByCss("#tab6zfnrx26 > div > div") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div9: Locator.ByCss("#tabcrnxb3dh > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard8: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(5) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H57: Locator.ByCss("#tooltips > h5") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run9: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(6) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode8: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(6) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div10: Locator.ByCss("#tabhlsnc7lb > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard9: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(6) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H58: Locator.ByCss("#badges > h5") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run10: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(7) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode9: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(7) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div11: Locator.ByCss("#tab25dlki2d > div > div") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div12: Locator.ByCss("#tabmvtjphk0 > div > div") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div13: Locator.ByCss("#tabvv4zax4c > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard10: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(7) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H59: Locator.ByCss("#drag-and-drop-tabs > h5") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run11: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(8) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode10: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(8) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard11: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(8) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H510: Locator.ByCss("#scrolling-tabs > h5") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run12: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(9) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode11: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(9) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ScrollTabsLeft: Locator.ByCss("#tab0semc7qm > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div15: Locator.ByCss("#tab0semc7qm > div:nth-of-type(2) > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ScrollTabsRight: Locator.ByCss("#tab0semc7qm > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ScrollTabsLeft2: Locator.ByCss("#tab35evnpt1 > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div16: Locator.ByCss("#tab35evnpt1 > div:nth-of-type(2) > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ScrollTabsRight2: Locator.ByCss("#tab35evnpt1 > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard12: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(9) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H64: Locator.ByCss("#scrolling-tabs-scroll-icons > h6") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run13: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(9) > div:nth-of-type(4) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode12: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(9) > div:nth-of-type(4) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ScrollTabsLeft3: Locator.ByCss("#tab6npmd8fp > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div17: Locator.ByCss("#tab6npmd8fp > div:nth-of-type(2) > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ScrollTabsRight3: Locator.ByCss("#tab6npmd8fp > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard13: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(9) > div:nth-of-type(4) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H511: Locator.ByCss("#render-fragments > h5") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run14: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(10) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode13: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(10) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div18: Locator.ByCss("#tabugnef8wk > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard14: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(10) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H512: Locator.ByCss("#visibility > h5") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run15: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(11) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode14: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(11) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div19: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(11) > div:nth-of-type(2) > div:nth-of-type(2) > div > div:nth-of-type(1) > div:nth-of-type(1) > div > div > div") (nothing stable: its position; ask for a test id in the app)
    // CheckBoxControl Input2: Locator.ByCss("#switchp8au8a5o > span:nth-of-type(1) > span:nth-of-type(1) > span > input") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard15: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(11) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H513: Locator.ByCss("#set-active-panel > h5") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run16: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(12) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode15: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(12) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div20: Locator.ByCss("#tab38hnc2vu > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ItemOne: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(12) > div:nth-of-type(2) > div:nth-of-type(2) > div:nth-of-type(3) > button:nth-of-type(1)") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ItemTwo2: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(12) > div:nth-of-type(2) > div:nth-of-type(2) > div:nth-of-type(3) > button:nth-of-type(2)") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ItemThree: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(12) > div:nth-of-type(2) > div:nth-of-type(2) > div:nth-of-type(3) > button:nth-of-type(3)") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ItemOne2: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(12) > div:nth-of-type(2) > div:nth-of-type(2) > div:nth-of-type(4) > button:nth-of-type(1)") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ItemTwo3: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(12) > div:nth-of-type(2) > div:nth-of-type(2) > div:nth-of-type(4) > button:nth-of-type(2)") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ItemThree2: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(12) > div:nth-of-type(2) > div:nth-of-type(2) > div:nth-of-type(4) > button:nth-of-type(3)") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard16: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(12) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H65: Locator.ByCss("#set-active-panel-binding-active-panel > h6") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run17: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(12) > div:nth-of-type(4) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode16: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(12) > div:nth-of-type(4) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div21: Locator.ByCss("#tabyjwqrdsz > div > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard17: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(12) > div:nth-of-type(4) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H514: Locator.ByCss("#dynamic-tabs > h5") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run18: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(13) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode17: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(13) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div22: Locator.ByCss("#tabnya89jd3 > div:nth-of-type(1) > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CloseTab: Locator.ByCss("#tabnya89jd3 > div:nth-of-type(1) > div > div:nth-of-type(1) > div > div > div > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CloseTab2: Locator.ByCss("#tablist-hr2ldu56x-tab-mudinputt5isdteg > div > div > button") (nothing stable: its position; ask for a test id in the app)
    // CheckBoxControl Input3: Locator.ByCss("#switchgavep7ve > span:nth-of-type(1) > span:nth-of-type(1) > span > input") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard18: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(13) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H66: Locator.ByCss("#dynamic-tabs-advanced-dynamic-tabs > h6") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run19: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(13) > div:nth-of-type(4) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode18: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(13) > div:nth-of-type(4) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div23: Locator.ByCss("#tab8hzgjr0e > div:nth-of-type(1) > div") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Button3: Locator.ByCss("#tab8hzgjr0e > div:nth-of-type(2) > div > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Button4: Locator.ByCss("#tab8hzgjr0e > div:nth-of-type(2) > div > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard19: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(13) > div:nth-of-type(4) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LabelControl H515: Locator.ByCss("#keep-panels-alive > h5") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl Run20: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(14) > div:nth-of-type(2) > div:nth-of-type(1) > div:nth-of-type(2) > button") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl ShowCode19: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(14) > div:nth-of-type(2) > div:nth-of-type(1) > button") (nothing stable: its position; ask for a test id in the app)
    // TabContainerControl Div24: Locator.ByCss("#tabhxvpvy31 > div > div") (nothing stable: its position; ask for a test id in the app)
    // CheckBoxControl Input4: Locator.ByCss("#switchl17zbziu > span:nth-of-type(1) > span:nth-of-type(1) > span > input") (nothing stable: its position; ask for a test id in the app)
    // CheckBoxControl Input5: Locator.ByCss("#switchggpdun7a > span:nth-of-type(1) > span:nth-of-type(1) > span > input") (nothing stable: its position; ask for a test id in the app)
    // ButtonControl CopyCodeToClipboard20: Locator.ByCss("#docspage > div:nth-of-type(3) > div:nth-of-type(14) > div:nth-of-type(3) > button") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Table3: Locator.ByCss("div:nth-of-type(1) > div:nth-of-type(3) > div > div:nth-of-type(1) > div:nth-of-type(2) > div > a:nth-of-type(1)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl Timeline3: Locator.ByCss("div:nth-of-type(1) > div:nth-of-type(3) > div > div:nth-of-type(1) > div:nth-of-type(2) > div > a:nth-of-type(2)") (nothing stable: its position; ask for a test id in the app)
    // LinkControl LearnMore2: Locator.ByCss("div:nth-of-type(2) > div > div:nth-of-type(1) > p > a") (nothing stable: its position; ask for a test id in the app)

    // Not drafted: a container is a control of its own. Make one deriving from it (the scrape-pages skill), then add the member.
    // TabContainerControl<TParent, TScope> Div14: Locator.ByCss("div.mud-drop-container")

    // Not drafted: shown only after a click (a state of the page, or a collection's other rows).
    // ButtonControl MarkAsRead: Locator.ByCss("button:has-text('Mark as read')"), in "Tabs - MudBlazor: Notifications"
    // LinkControl MudBlazorV90: Locator.ByLinkText("MudBlazor v9.0.0 Released"), in "Tabs - MudBlazor: Notifications"
    // LinkControl MudBlazorV80: Locator.ByLinkText("MudBlazor v8.0.0 Released"), in "Tabs - MudBlazor: Notifications"
    // LinkControl MudBlazorV70: Locator.ByLinkText("MudBlazor v7.0.0 Released"), in "Tabs - MudBlazor: Notifications"
    // LinkControl MudBlazorIsHere: Locator.ByLinkText("MudBlazor is here to stay"), in "Tabs - MudBlazor: Notifications"
    // LinkControl ListItemgbcrws3e: Locator.ById("list-itemgbcrws3e"), in "Tabs - MudBlazor: Projects"
    // LinkControl ListItemkomriew9: Locator.ById("list-itemkomriew9"), in "Tabs - MudBlazor: Projects"
    // LinkControl ListItem7eufjx41: Locator.ById("list-item7eufjx41"), in "Tabs - MudBlazor: Projects"
    // LinkControl ListItemqofjwjol: Locator.ById("list-itemqofjwjol"), in "Tabs - MudBlazor: Projects"
    // LinkControl ListItemxoarovzq: Locator.ById("list-itemxoarovzq"), in "Tabs - MudBlazor: Projects"
    // LinkControl ListItemzmftyijl: Locator.ById("list-itemzmftyijl"), in "Tabs - MudBlazor: Projects"
    // LinkControl ListItem0p8znkz5: Locator.ById("list-item0p8znkz5"), in "Tabs - MudBlazor: Projects"
}
