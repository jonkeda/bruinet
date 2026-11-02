namespace Bruinet.Samples.Maui.App.Navigation;

/// <summary>
/// One entry in the sample app's page registry.
/// </summary>
/// <param name="Page">The page's stable identity.</param>
/// <param name="Title">The text shown on the hub button.</param>
/// <param name="Description">A short line describing what the page covers.</param>
/// <param name="Create">Builds the page. A factory, not an instance, so a page is
/// constructed fresh each time it is opened and cannot carry state between tests.</param>
public sealed record SamplePageEntry(
    SamplePage Page,
    string Title,
    string Description,
    Func<ContentPage> Create)
{
    /// <summary>
    /// The AutomationId of this entry's button on the hub.
    /// </summary>
    public string AutomationId => SamplePages.AutomationIdFor(Page);
}

/// <summary>
/// The sample app's page registry: the single source of truth for what pages exist and how
/// they are reached.
/// </summary>
/// <remarks>
/// <para>
/// Adding a page is one entry here. The hub builds its list from <see cref="All"/>, so no
/// XAML, route registration, or navigation wiring changes.
/// </para>
/// <para>
/// This replaces Shell-based navigation. Shell tabs did not survive the move to Android: its
/// <c>BottomNavigationView</c> shows five tabs and hides the rest behind an overflow menu, so
/// a page was not merely addressed differently per platform but was not on screen at all. A
/// flat list of buttons is uniform everywhere, and has no navigation stack to leak state
/// between tests.
/// </para>
/// </remarks>
public static class SamplePages
{
    /// <summary>
    /// The AutomationId convention for a hub button.
    /// </summary>
    /// <remarks>
    /// Shared by the app and its UI tests so the id is derived in both places rather than
    /// written twice. On Android this surfaces as <c>resource-id</c>, on Windows as
    /// <c>AutomationId</c>; the same string works on both.
    /// </remarks>
    public static string AutomationIdFor(SamplePage page) => $"Open_{page}";

    /// <summary>
    /// Every page the hub offers, in display order.
    /// </summary>
    public static IReadOnlyList<SamplePageEntry> All { get; } =
    [
        new(SamplePage.Main, "Main", "The greeting demo the UAT sample scenarios drive",
            () => new GreetingPage()),

        new(SamplePage.UserForm, "User Form", "The profile form the UAT sample scenarios fill in",
            () => new UserFormPage()),

        new(SamplePage.Buttons, "Buttons", "Button and ImageButton",
            () => new ButtonsPage()),

        new(SamplePage.DateTime, "DateTime", "DatePicker and TimePicker",
            () => new DateTimePage()),

        new(SamplePage.Display, "Display", "Label, Image, ActivityIndicator, ProgressBar",
            () => new DisplayPage()),

        new(SamplePage.Range, "Range", "Slider and Stepper",
            () => new RangePage()),

        new(SamplePage.Selection, "Selection", "Picker and selection controls",
            () => new SelectionPage()),

        new(SamplePage.Text, "Text", "Entry, Editor and SearchBar",
            () => new TextPage()),

        new(SamplePage.Scroll, "Scroll", "A long page for scrolling tests",
            () => new ScrollPage()),

        new(SamplePage.Gestures, "Gestures", "Tap, swipe, and verbs only a sink can answer",
            () => new GesturesPage()),

        new(SamplePage.Toggle, "Toggle", "CheckBox, RadioButton and Switch",
            () => new TogglePage()),

        new(SamplePage.Container, "Containers", "Grid, Border, ContentView, ScrollView",
            () => new ContainerPage()),

        new(SamplePage.Collection, "Collections", "CollectionView and ListView",
            () => new CollectionModulePage()),

        new(SamplePage.GridCollection, "Grid + Collection", "Grid and CollectionView demo",
            () => new GridCollectionPage()),

        new(SamplePage.Shapes, "Shapes", "Shape controls",
            () => new ShapesPage()),

        new(SamplePage.Dialogs, "Dialogs", "DisplayAlert and DisplayPrompt",
            () => new DialogsPage()),

        new(SamplePage.Navigation, "Navigation", "Toolbar, Menu and TabMenu",
            () => new NavigationDemoPage()),

        new(SamplePage.AutomationProbe, "Probe", "Layout AutomationId addressability",
            () => new AutomationProbePage()),

        new(SamplePage.CommunityToolkit, "Community Toolkit", "Expander, AvatarView, RatingView, DrawingView, StateContainer, DockLayout",
            () => new CommunityToolkitPage()),

        new(SamplePage.CommunityToolkitAlerts, "Toolkit Alerts", "Popup, Snackbar and Toast",
            () => new CommunityToolkitAlertsPage()),

        new(SamplePage.CommunityToolkitMedia, "Toolkit Media", "MediaElement",
            () => new CommunityToolkitMediaPage()),

        new(SamplePage.PagedList, "Paged list", "A CollectionView that loads pages and searches",
            () => new PagedListPage()),

        new(SamplePage.Covered, "Covered pages", "A busy page under a pushed page and a modal",
            () => new CoveredPage()),

        new(SamplePage.Timed, "Timed", "Delays, delayed work, a debounce, a message that hides",
            () => new TimedPage()),
    ];

    /// <summary>
    /// Finds an entry by its page identity.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// When the page has no registry entry — which means the enum gained a member and the
    /// registry did not. Failing here names the missing page; returning null would surface
    /// later as an unexplained navigation failure.
    /// </exception>
    public static SamplePageEntry Find(SamplePage page)
        => All.FirstOrDefault(entry => entry.Page == page)
           ?? throw new ArgumentOutOfRangeException(
               nameof(page), page, $"No registry entry for '{page}'. Add one to SamplePages.All.");
}
