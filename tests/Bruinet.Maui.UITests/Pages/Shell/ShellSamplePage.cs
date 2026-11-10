using Bruinet.Maui.Controls.Navigation;

namespace Bruinet.Maui.UITests.Pages.Shell;

/// <summary>
/// Page object for the Shell sample app.
/// </summary>
/// <remarks>
/// One page object rather than one per tab: the Shell chrome is on screen throughout, and
/// each page announces itself with a layout carrying its own AutomationId, so "which page am
/// I on" is a plain lookup rather than a separate object per page.
/// </remarks>
public class ShellSamplePage : PageObjectBase<ShellSamplePage>
{
    public ShellSamplePage(IMauiTestContext context)
        : base(context)
    {
        Shell = new Shell<ShellSamplePage>(this);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The Shell itself, not a page: its tab strip, its flyout and whichever page is showing all
    /// sit under it. This used to name <c>ShellSamplePage</c>, an id nothing in the app carried, so
    /// every lookup scoped under it failed before any Shell test began - stage G step 32. The app
    /// now copies the Shell's own id onto the view that draws it.
    /// </remarks>
    public override string Name => "AppShell";

    /// <summary>
    /// The shell is up once it shows either its tab strip or a flyout item's page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plain lookup, never a scrolling one: a readiness check that scrolls turns "not ready
    /// yet" into a sweep of the whole page. The rule from the Android performance work.
    /// </para>
    /// <para>
    /// <b>Not the tab strip alone.</b> A flyout item without tabs shows none on Android (Windows
    /// keeps an empty host), so on those pages the Shell never counted as loaded, every lookup
    /// scoped under it was refused, and the fixture could not open the flyout to leave.
    /// </para>
    /// </remarks>
    public override bool IsLoaded()
        => Shell.Tabs.Exists()
           || Context.TryFindElement(Locator.ByAutomationId("ShellFlyoutPageTitle")) != null;

    /// <inheritdoc />
    /// <remarks>
    /// Not cached. On Android the Shell's view element goes stale when its content is replaced -
    /// a tab reselected, a page pushed - while still passing the cache's validity check, and
    /// lookups under it then find nothing: <c>ShellDetailSubBackButton</c> "not found within
    /// container" while the button was on screen. Finding the root each time is one lookup by id.
    /// </remarks>
    protected override bool CacheContainerRoot => false;

    /// <summary>The shell's tabs and flyout.</summary>
    public Shell<ShellSamplePage> Shell { get; }

    #region Page markers

    /// <summary>The Home tab's page.</summary>
    public Label<ShellSamplePage> HomePage => new(this, "ShellHomePage");

    /// <summary>The Controls tab's page.</summary>
    public Label<ShellSamplePage> ControlsPage => new(this, "ShellControlsPage");

    /// <summary>The Detail tab's page.</summary>
    public Label<ShellSamplePage> DetailPage => new(this, "ShellDetailPage");

    /// <summary>The page the Detail tab pushes onto its stack.</summary>
    public Label<ShellSamplePage> DetailSubPage => new(this, "ShellDetailSubPage");

    /// <summary>The Status tab's page.</summary>
    public Label<ShellSamplePage> StatusPage => new(this, "ShellStatusPage");

    /// <summary>Any flyout item's page: one label naming the item that led here.</summary>
    public Label<ShellSamplePage> FlyoutPageTitle => new(this, "ShellFlyoutPageTitle");

    /// <summary>
    /// Whether the Detail tab currently has its sub-page pushed.
    /// </summary>
    /// <remarks>
    /// A plain lookup rather than <c>DetailSubPage.Exists()</c>, which scrolls to be sure of
    /// an absence. The fixture asks this before every test, and the usual answer is "no", so
    /// the scrolling form would spend a sweep per test confirming nothing is there.
    /// </remarks>
    public bool IsSubPagePushed()
        => Context.TryFindElement(Locator.ByAutomationId("ShellDetailSubPage")) != null;

    #endregion

    #region Controls

    /// <summary>Pushes the Detail tab's sub-page.</summary>
    public Button<ShellSamplePage> PushSubPageButton => new(this, "ShellDetailPushButton");

    /// <summary>The sub-page's own way back, which pops the stack.</summary>
    public Button<ShellSamplePage> SubPageBackButton => new(this, "ShellDetailSubBackButton");

    /// <summary>A button on the Controls tab, so a tab test can prove the page is live.</summary>
    public Button<ShellSamplePage> ControlsButton => new(this, "ShellControlsButton");

    /// <summary>What <see cref="ControlsButton"/> writes.</summary>
    public Label<ShellSamplePage> ControlsResult => new(this, "ShellControlsResult");

    #endregion
}
