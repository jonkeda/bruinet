namespace Bruinet.Maui.UITests.Pages;

/// <summary>
/// The pages the sample app's hub can open.
/// </summary>
/// <remarks>
/// Mirrors <c>Bruinet.Samples.Maui.App.Navigation.SamplePage</c>. Duplicated rather than
/// shared because the UI tests drive the app through its UI and must not reference its
/// assembly — a test that could call into the app would stop being a UI test. The member
/// names are the contract, and <see cref="HubPage.AutomationIdFor"/> derives the same id the
/// app does, so a rename on either side fails visibly rather than drifting.
/// </remarks>
public enum SamplePage
{
    Main,
    UserForm,
    Buttons,
    DateTime,
    Display,
    Range,
    Selection,
    Text,
    Toggle,
    Container,
    Collection,
    GridCollection,
    Shapes,
    Dialogs,
    Navigation,
    Scroll,
    Gestures,
    AutomationProbe,
    CommunityToolkit,
    CommunityToolkitAlerts,
    CommunityToolkitMedia,
    PagedList,
    Covered
}

/// <summary>
/// Page object for the sample app's hub: the flat page list that replaced Shell navigation.
/// </summary>
/// <remarks>
/// Opening a page is one click on all three platforms, and each open pushes a fresh page, so
/// no navigation state leaks between tests.
/// </remarks>
public class HubPage : PageObjectBase<HubPage>
{
    public HubPage(IMauiTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public override string Name => "PageHub";

    /// <inheritdoc />
    /// <remarks>
    /// No override, deliberately: the base checks the page root (<c>PageHub</c>) with a plain
    /// lookup. A readiness check must never scroll — a page marker that is not present now means
    /// you are on another page, and scrolling cannot change that.
    /// </remarks>

    /// <remarks>
    /// Every control here is genuinely on the hub, so all of them resolve only while the hub is
    /// loaded. The back button that returns to the hub used to live here too, and did not belong:
    /// it is attached to whichever page is open, so the hub reports not-loaded at exactly the
    /// moment it was wanted. It now resolves from the app root in <c>MauiFixture</c>.
    /// </remarks>
    /// <summary>The hub's title label.</summary>
    public Label<HubPage> Title => new(this, "PageHubTitle");

    /// <summary>
    /// The AutomationId of the button that opens a page.
    /// </summary>
    /// <remarks>
    /// The same convention the app applies in <c>SamplePages.AutomationIdFor</c>. Derived on
    /// both sides rather than written twice, so the two cannot fall out of step silently.
    /// </remarks>
    public static string AutomationIdFor(SamplePage page) => $"Open_{page}";

    /// <summary>
    /// The button that opens the given page.
    /// </summary>
    public Button<HubPage> OpenButton(SamplePage page) => new(this, AutomationIdFor(page));
}
