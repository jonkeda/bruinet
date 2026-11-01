using Bruinet.Maui.AppSupport.Uia;
using Bruinet.Uia;
using Bruinet.Samples.Maui.App.Navigation;

namespace Bruinet.Samples.Maui.App;

/// <summary>
/// The sample app's entry point: one button per registered page.
/// </summary>
public partial class HubPage : ContentPage
{
    public HubPage()
    {
        InitializeComponent();
        BuildPageList();

        // The hub answers questions about the app, and it is the only page that can answer them
        // from the root.
        //
        // Every page the hub opens declares NavigateBack (see AddBackToHub), so while one is open
        // something is on the bridge. At the root there is nothing but this page - and "are we at
        // the root" is precisely the question a caller asks there. Without this, the honest
        // answer to it was unobtainable at the one moment it mattered, which is what the
        // navigation RCA left open.
        GestureAutomation.SetVerbs(
            this,
            $"{nameof(BruinetVerb.GetState)},{nameof(BruinetVerb.CurrentRoute)},"
            + $"{nameof(BruinetVerb.IsIdle)},{nameof(BruinetVerb.CurrentAlert)}");
    }

    /// <summary>
    /// Creates one button per registry entry.
    /// </summary>
    /// <remarks>
    /// Built in code rather than bound to a CollectionView: a bound item template would
    /// give every button the same AutomationId, which is right for testing item scoping but
    /// wrong here — each button needs its own id so a test can open one page directly.
    /// </remarks>
    private void BuildPageList()
    {
        foreach (var entry in SamplePages.All)
        {
            PageList.Add(CreateButton(entry));
        }
    }

    private Button CreateButton(SamplePageEntry entry)
    {
        var button = new Button
        {
            // Derived from the enum member, never hand-written, so the app and the tests
            // cannot drift apart.
            AutomationId = entry.AutomationId,
            Text = entry.Title,
            HorizontalOptions = LayoutOptions.Fill,
        };

        button.Clicked += async (_, _) => await OpenAsync(entry);

        return button;
    }

    /// <summary>
    /// Opens a page by pushing it onto the navigation stack.
    /// </summary>
    /// <remarks>
    /// The page is constructed here, per open, so nothing carries over from a previous visit.
    /// Shell reused page instances, which is why the container tests needed an explicit reset
    /// to be order-independent (see rca-001).
    /// </remarks>
    private async Task OpenAsync(SamplePageEntry entry)
    {
        if (Navigation == null)
            return;

        var page = entry.Create();
        AddBackToHub(page);

        await Navigation.PushAsync(page);
    }

    /// <summary>
    /// Adds a "Back to hub" toolbar item to a page being opened.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Added here rather than in each page's XAML so a page stays unaware of how it was
    /// reached, and so a new page gets the affordance for free.
    /// </para>
    /// <para>
    /// It exists because returning to the hub must be a *click*. The driver's back navigation
    /// falls back to Alt+Left on Windows, which is global keyboard input and blocked by the
    /// interaction policy — and a synthesized shortcut is not what a user does anyway. A
    /// toolbar item carrying an AutomationId is one addressable element on every platform.
    /// </para>
    /// <para>
    /// <b>The click cannot be made semantic, so the page is given its own route instead.</b>
    /// A MAUI <c>ToolbarItem</c> renders into native chrome, and driving it through the Invoke
    /// pattern reports success without raising the command — measured four ways, including the
    /// activation ladder that works for every other control in the app. That one mouse click was
    /// the whole suite's physical-input footprint and the only thing keeping the app from being
    /// automated off-screen. The <c>NavigateBack</c> declaration below is what replaces it: the
    /// page answers for itself, because the affordance cannot.
    /// </para>
    /// </remarks>
    private void AddBackToHub(ContentPage page)
    {
        page.ToolbarItems.Add(new ToolbarItem
        {
            Text = "Back",
            AutomationId = BackToHubAutomationId,
            Command = new Command(async () =>
            {
                if (page.Navigation != null)
                {
                    await page.Navigation.PopAsync();
                }
            })
        });

        // Declared here rather than in each page's XAML for the same reason the toolbar item is:
        // a page stays unaware of how it was reached, and a page added later gets this for free.
        // An AutomationId is what joins an element to its bridge target, so pages that brought
        // their own keep it and the rest are named after the type.
        if (string.IsNullOrWhiteSpace(page.AutomationId))
        {
            page.AutomationId = page.GetType().Name;
        }

        // GetState and CurrentRoute alongside going back, because a caller needs to ask before it
        // acts. CurrentAlert is here for the same reason IsIdle is: it asks about the app rather
        // than about this page, so any live element can answer it and the client does not have to
        // know which one to address. NavigateBack answers four different things and only one of them is a failure;
        // "how deep is the stack" separates them in one round trip, where the client used to
        // separate them by waiting.
        GestureAutomation.SetVerbs(
            page,
            $"{nameof(BruinetVerb.NavigateBack)},{nameof(BruinetVerb.GetState)},"
            + $"{nameof(BruinetVerb.CurrentRoute)},{nameof(BruinetVerb.IsIdle)},"
            + $"{nameof(BruinetVerb.CurrentAlert)},{nameof(BruinetVerb.InvokeMenuItem)},"
            + $"{nameof(BruinetVerb.InvokeToolbarItem)}");
    }

    /// <summary>
    /// The AutomationId of the "back to hub" item present on every opened page.
    /// </summary>
    public const string BackToHubAutomationId = "BackToHub";
}
