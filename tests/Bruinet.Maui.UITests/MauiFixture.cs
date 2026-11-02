using Bruinet.Maui.Containers;
using Bruinet.Core.Utilities;
using Bruinet.Maui.Configuration;
using Bruinet.Maui.Enums;
using Bruinet.Maui.Testing;
using Bruinet.Maui.UITests.Containers;
using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests;

/// <summary>
/// Test fixture for Bruinet.Samples.Maui.App UI tests.
/// Inherits infrastructure from <see cref="MauiTestFixtureBase"/> and adds app-specific pages.
/// </summary>
[TestModuleScan(typeof(HubPage), NamespacePrefix = "Bruinet.Maui.UITests.Pages")]
public class MauiFixture : MauiTestFixtureBase
{
    private HubPage _hub;
    private AppRoot _appRoot;

    public MauiFixture()
    {
        _hub = new HubPage(Context);
        _appRoot = new AppRoot(Context);
        Composition = TestComposition.ForFixture(this, services =>
            services.AddSingleton<IMauiTestContext>(Context));

        // The app is up by now - the base constructor launched it - so this is the first moment
        // the other collection could truthfully be told there are two. See ParallelismProbe.
        ParallelismProbe.Enter(ParallelismProbe.Hub, Context.Driver);
    }

    public TestComposition Composition { get; }

    /// <summary>
    /// The "back to hub" toolbar item, which the hub attaches to every page it opens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Scoped to the app, not to a page</b>, because that is where it is: MAUI draws a
    /// <c>ToolbarItem</c> into the window's chrome. It was once a <c>Button&lt;HubPage&gt;</c>,
    /// which is a contradiction - the hub is not loaded precisely when the way back to it is
    /// needed - and the readiness gates correctly refused, taking the whole suite down with them.
    /// The fix at the time replaced the control object with a hand-rolled lookup, which cost the
    /// activation ladder, the logging and the failure messages that come with one.
    /// <see cref="AppRoot"/> is the
    /// scope that was missing.
    /// </para>
    /// <para>
    /// Located by accessibility id rather than automation id: MAUI surfaces a toolbar item's
    /// <c>AutomationId</c> as the platform accessibility label, and that one locator is the same
    /// string on Windows, Android and iOS.
    /// </para>
    /// </remarks>
    public ToolbarButton<AppRoot> BackToHub
        => new(_appRoot, Locator.ByAccessibilityId("BackToHub"));

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            // After the base, so the app is gone before anyone is told this one is still running.
            ParallelismProbe.Leave(ParallelismProbe.Hub);
        }
    }

    /// <summary>
    /// Gets the hub page object: the app's flat page list.
    /// </summary>
    public HubPage Hub => _hub;


    /// <summary>
    /// Opens a page from the hub, returning to the hub first if another page is open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The single navigation primitive: one click, identical on Windows, Android and iOS.
    /// It replaced Shell tab navigation, which was neither - Android hid tabs past the fifth
    /// behind an overflow menu, and Windows exposed only nine.
    /// </para>
    /// <para>
    /// Because the hub pushes a fresh page instance each time and this pops back to the hub
    /// first, a test cannot inherit the previous test's page or its position in a navigation
    /// stack — the absence of state to recover from, rather than a recovery routine.
    /// </para>
    /// <para>
    /// A session the platform has lost - on Android the UiAutomator2 server crashing - is replaced
    /// once, here, before the page is opened: otherwise one crash failed every later test of the
    /// run in a millisecond each.
    /// </para>
    /// </remarks>
    public void Open(SamplePage page)
    {
        try
        {
            OpenOnce(page);
        }
        catch (Exception error) when (IsSessionLost(error))
        {
            RestartApp();
            OpenOnce(page);
        }
    }

    private void OpenOnce(SamplePage page)
    {
        ReturnToHub();
        _hub.OpenButton(page).Click();
        WaitForThePageToArrive();
    }

    /// <inheritdoc />
    protected override void OnRestarted()
    {
        _hub = new HubPage(Context);
        _appRoot = new AppRoot(Context);
    }

    /// <summary>
    /// Blocks until the pushed page is on top, in the automation tree, and settled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Clicking the hub's button starts a navigation; it does not finish one.</b> A test whose
    /// first line looked a control up directly raced the page arriving and lost occasionally -
    /// "'X' was not found", against a control that is unquestionably on that page.
    /// </para>
    /// <para>
    /// <b>"The stack is deeper than the root" was tried first and is too weak.</b> It becomes true
    /// the moment the push starts, before the page is in the tree. So the app is asked which page
    /// is on top - it can name it, by <c>AutomationId</c> - and the wait is for that page's own
    /// root to be findable, which is the thing a test's first lookup actually needs.
    /// </para>
    /// <para>
    /// <b>Neither step is asserted.</b> This is a wait, not a check: whatever the test does next
    /// has its own idea of what should be there and a better error message for it.
    /// </para>
    /// </remarks>
    private void WaitForThePageToArrive()
        => PageNavigation.WaitArrived(Context, _hub.Name, TestConstants.ShortTestTimeoutMs);

    /// <summary>
    /// Returns to the hub if a page is open.
    /// </summary>
    /// <remarks>
    /// <b>Asks before acting, which is what stopped this costing two seconds a time.</b> Being at
    /// the root already is the commonest case here - most tests open a page from the hub, having
    /// arrived there a moment earlier - and it used to be indistinguishable from three real
    /// failures, all of them reported as one <c>false</c>. The fixture then waited out a grace
    /// period on every single reset to be told what the first call had already established.
    /// </remarks>
    /// <summary>How many backs past the first a covered hub gets: a dialog, its page, one spare.</summary>
    private const int MaxExtraBacks = 3;


    private void ReturnToHub()
    {
        // The hub page object lives for the whole run and caches the root element it resolved;
        // navigation replaces that element, so each back drops it. On Windows a refused back
        // falls to the toolbar's BackToHub item.
        var (atHub, steps) = PageNavigation.ReturnToRoot(
            Context,
            _hub,
            TestConstants.ShortTestTimeoutMs,
            forget: _hub.InvalidateCache,
            whenBackRefused: () =>
            {
                if (!BackToHub.WaitExists(true, TestConstants.ShortTestTimeoutMs))
                {
                    return false;
                }

                BackToHub.Click();
                return true;
            },
            maxExtraBacks: MaxExtraBacks);

        if (!atHub)
        {
            throw new InvalidOperationException(
                "Could not get back to the hub, so the next page cannot be opened. This is a "
                + "navigation failure, not a fault in whatever is opened next." + Environment.NewLine + "  "
                + string.Join(Environment.NewLine + "  ", steps));
        }
    }


    /// <summary>
    /// Opens the greeting demo, which the UAT runtime calls for <c>I am on the Main page</c>.
    /// </summary>
    /// <remarks>
    /// The name is the convention the UAT engine navigates by: <c>NavigateTo</c> plus the page
    /// name with its spaces removed. It used to open the Buttons page, which left every Main
    /// scenario looking at the wrong page.
    /// </remarks>
    public void NavigateToMain()
    {
        Open(SamplePage.Main);
    }

    /// <summary>
    /// Opens the profile form, which the UAT runtime calls for <c>I am on the User Form page</c>.
    /// </summary>
    public void NavigateToUserForm()
    {
        Open(SamplePage.UserForm);
    }

    /// <summary>
    /// Opens the Buttons page and returns its page object.
    /// </summary>
    /// <remarks>
    /// Clicks only; the caller waits for what it needs. <see cref="NavigateToMain"/> used to
    /// open this page, which was a name that told its callers nothing about where they landed.
    /// </remarks>
    public ButtonsTestPage NavigateToButtons()
    {
        Open(SamplePage.Buttons);
        return new ButtonsTestPage(Context);
    }

    /// <summary>
    /// Opens the gestures page and returns its page object.
    /// </summary>
    /// <remarks>
    /// Created per access for the same reason as the others: the hub pushes a fresh page on
    /// every open, so a cached page object would hold the root of an instance that is gone.
    /// </remarks>
    public GesturesTestPage OpenGestures()
    {
        Open(SamplePage.Gestures);
        return new GesturesTestPage(Context);
    }

    /// <summary>
    /// Gets the Grid + CollectionView demo page object.
    /// </summary>
    /// <remarks>
    /// Created per access, not cached. A container object caches the element it resolved as
    /// its root, and the hub builds a fresh page on every open — so a cached page object
    /// would hold roots belonging to a page instance that no longer exists. Shell used to
    /// retain page instances, which is what made caching viable before.
    /// </remarks>
    public GridCollectionDemoPage GridCollectionDemoPage => new(Context);

    /// <summary>
    /// Navigates to the container demo and restores its seeded state.
    /// </summary>
    /// <remarks>
    /// The hub builds a fresh page on every open, but this fixture is shared across test
    /// classes and the page object caches container roots, so the demo's data is still reset
    /// here to keep tests order-independent. Every wait is on observed UI state, never a
    /// fixed delay.
    /// </remarks>
    public GridCollectionDemoPage NavigateToGridCollectionDemo()
    {
        Open(SamplePage.GridCollection);

        var page = GridCollectionDemoPage;
        page.WaitLoaded(true, TestConstants.DefaultTestTimeoutMs);

        page.Products.Reset(TestConstants.DefaultTestTimeoutMs);
        page.Products.AssertLogicalCount(
            ProductCollection.SeedCount,
            "Container demo did not return to its seeded state after reset.",
            TestConstants.DefaultTestTimeoutMs);

        return page;
    }

    /// <summary>
    /// Opens the scroll test page and returns it in its reset state.
    /// </summary>
    public ScrollTestPage NavigateToScroll()
    {
        Open(SamplePage.Scroll);

        var page = new ScrollTestPage(Context);
        page.WaitLoaded(true, TestConstants.DefaultTestTimeoutMs);
        page.ResetButton.Click();
        page.StatusLabel.WaitText("none", TestConstants.DefaultTestTimeoutMs);
        return page;
    }

    /// <summary>
    /// Gets the Phase 0 automation probe page object.
    /// </summary>
    public AutomationProbePage AutomationProbePage => new(Context);

    /// <summary>
    /// Navigates to the automation probe page.
    /// </summary>
    /// <remarks>
    /// The probe page is stateless — it has no data to seed and nothing to mutate — so
    /// unlike <see cref="NavigateToGridCollectionDemo"/> there is nothing to reset.
    /// </remarks>
    public AutomationProbePage NavigateToAutomationProbe()
    {
        Open(SamplePage.AutomationProbe);

        var page = AutomationProbePage;
        page.WaitLoaded(true, TestConstants.DefaultTestTimeoutMs);

        return page;
    }

    /// <summary>
    /// Gets the navigation controls demo page object.
    /// </summary>
    /// <remarks>
    /// Created per access — see <see cref="GridCollectionDemoPage"/> for why caching a page
    /// object is no longer safe.
    /// </remarks>
    public NavigationDemoPage NavigationDemoPage => new(Context);

    /// <summary>
    /// Navigates to the navigation demo and restores its initial state.
    /// </summary>
    /// <remarks>
    /// The page object is cached across test classes, so its state is reset explicitly to
    /// keep tests order-independent; the wait is on observed UI state, never a fixed delay.
    /// </remarks>
    public NavigationDemoPage NavigateToNavigationDemo()
    {
        Open(SamplePage.Navigation);

        var page = NavigationDemoPage;
        page.WaitLoaded(true, TestConstants.DefaultTestTimeoutMs);

        page.ResetButton.Click();
        page.LastAction.WaitText("none", TestConstants.DefaultTestTimeoutMs);

        return page;
    }

    /// <summary>
    /// Gets the container module test page object.
    /// </summary>
    public ContainerTestPage ContainerTestPage => new(Context);

    /// <summary>
    /// Navigates to the container module page and restores its initial state.
    /// </summary>
    /// <remarks>
    /// Opened directly from the hub, so no other page's state can leak into it.
    /// </remarks>
    public ContainerTestPage NavigateToContainerModule()
    {
        Open(SamplePage.Container);

        var page = ContainerTestPage;
        page.WaitLoaded(true, TestConstants.DefaultTestTimeoutMs);

        page.ResetButton.Click();
        page.Status.WaitText("none", TestConstants.DefaultTestTimeoutMs);

        return page;
    }

    /// <summary>
    /// Opens the paged list. Nothing to reset: the hub builds a fresh page, which loads its first page.
    /// </summary>
    public PagedListTestPage NavigateToPagedList()
    {
        Open(SamplePage.PagedList);

        var page = new PagedListTestPage(Context);
        page.WaitLoaded(true, TestConstants.DefaultTestTimeoutMs);
        return page;
    }

    /// <summary>
    /// Opens the dialogs sample page and restores its initial result.
    /// </summary>
    public DialogsTestPage NavigateToDialogs()
    {
        Open(SamplePage.Dialogs);

        var page = new DialogsTestPage(Context);
        page.WaitLoaded(true, TestConstants.DefaultTestTimeoutMs);
        page.ResetButton.Click();
        page.Result.WaitText("none", TestConstants.DefaultTestTimeoutMs);
        return page;
    }

    #region MauiTestFixtureBase Overrides

    /// <summary>
    /// The MauiHeadless head (<c>BRUINET_HEAD=headless</c>): the app as its <c>App</c> starts it, a
    /// <c>NavigationPage</c> on the hub, rendered headless from the app's own <c>net10.0</c> build.
    /// </summary>
    protected override MauiTestContextOptions CreateHeadlessOptions()
    {
        // What UseMauiCommunityToolkitMediaElement() does first on a device: the assembly loaded, as
        // Debug parses XAML at run time and knows only the XAML namespaces of loaded assemblies.
        _ = typeof(global::CommunityToolkit.Maui.Views.MediaElement).Assembly;
        return Bruinet.Maui.Headless.MauiHeadlessHead.Options(
            _ => new Microsoft.Maui.Controls.NavigationPage(new Bruinet.Samples.Maui.App.HubPage()),
            options => options.Application = _ => new Bruinet.Samples.Maui.App.App());
    }

    /// <inheritdoc />
    protected override string GetDefaultAppPath(MauiPlatform platform)
    {
        var solutionDir = FindSolutionDirectory();

        return platform switch
        {
            MauiPlatform.Windows => Path.Combine(solutionDir,
                "samples", "Bruinet.Samples.Maui.App", "bin", "Debug",
                "net10.0-windows10.0.19041.0", "win-x64", "Bruinet.Samples.Maui.App.exe"),
            MauiPlatform.Android => Path.Combine(solutionDir,
                "samples", "Bruinet.Samples.Maui.App", "bin", "Debug",
                "net10.0-android", "com.bruinet.samples.maui-Signed.apk"),
            MauiPlatform.iOS => Path.Combine(solutionDir,
                "samples", "Bruinet.Samples.Maui.App", "bin", "Debug",
                "net10.0-ios",
                System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64
                    ? "iossimulator-arm64"
                    : "iossimulator-x64",
                "Bruinet.Samples.Maui.App.app"),
            _ => ""
        };
    }

    /// <inheritdoc />
    protected override void ConfigureAndroidOptions(MauiDriverOptions options)
    {
        base.ConfigureAndroidOptions(options);
        // MAUI generates hashed activity names (e.g., crc643b83d6491f48953d.MainActivity)
        // Don't specify appPackage/appActivity - let Maui extract from the APK
        options.AdditionalCapabilities["autoGrantPermissions"] = true;
        options.AdditionalCapabilities["newCommandTimeout"] = 300; // 5 minutes
        // MAUI apps take longer to initialize - increase wait times
        options.AdditionalCapabilities["appWaitDuration"] = 60000; // 60 seconds to wait for app
        options.AdditionalCapabilities["adbExecTimeout"] = 60000; // 60 seconds for ADB commands

        // MAUI hashes the activity name (crc64<hash>.MainActivity) and the hash changes with
        // the namespace, so it cannot be written literally here. Waiting on the package alone
        // lets UiAutomator2 accept whatever activity that package launches.
        options.AdditionalCapabilities["appWaitPackage"] = "com.bruinet.samples.maui";

        // The idle wait and window animations are the factory's (MauiDriverFactory.SessionDefaults).

        // Reinstall even when the installed APK reports the same version: MAUI does not bump
        // versionCode between builds, so without this a run can silently test the previous one.
        options.AdditionalCapabilities["enforceAppInstall"] = !TestConstants.AppPreinstalled;
    }

    /// <inheritdoc />
    protected override void ConfigureiOSOptions(MauiDriverOptions options)
    {
        base.ConfigureiOSOptions(options);
        options.AdditionalCapabilities["bundleId"] = "com.bruinet.samples.maui";
    }

    #endregion
}
