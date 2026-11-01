using System.Text;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using Bruinet.Maui.Enums;
using Bruinet.Maui.Interfaces;
using Microsoft.Maui.Controls;
using MauiHeadless;
using MauiHeadless.Inspection;
using MauiHeadless.Rendering;
using MauiHeadless.Snapshots;
using MauiElement = Microsoft.Maui.Controls.Element;

namespace Bruinet.Maui.Headless;

/// <summary>
/// Bruinet's MAUI driver over a page rendered by MauiHeadless: lookup in the window's tree, navigation
/// through MAUI's own NavigationPage and Shell, the tree as page source. The same page objects and
/// controls that drive the app on Windows, Android and iOS drive it here, headless.
/// </summary>
/// <remarks>
/// <para>
/// It reports <see cref="MauiPlatform.Headless"/>. Its answers are otherwise Windows' (the UI Automation
/// defaults, WinUI's ids for the title bar), since MauiHeadless's tree is like that one: off-screen
/// elements are in it, finding is not seeing.
/// </para>
/// <para>Every lookup first runs the work the context has queued, so a poll sees settled state.</para>
/// </remarks>
public sealed class MauiHeadlessDriver : IMauiDriver
{
    private static int s_nextHandle;

    private readonly IRenderedElement<MauiElement> _rendered;
    private readonly string _handle = "headless-" + Interlocked.Increment(ref s_nextHandle);
    private readonly bool _ownsContext;

    /// <param name="rendered">The rendered app.</param>
    /// <param name="ownsContext">Whether disposing the driver disposes the MauiHeadless context it was rendered in, and with it the app's services.</param>
    public MauiHeadlessDriver(IRenderedElement<MauiElement> rendered, bool ownsContext = false)
    {
        ArgumentNullException.ThrowIfNull(rendered);
        _rendered = rendered;
        _ownsContext = ownsContext;
    }

    /// <summary>The MauiHeadless context the page was rendered in.</summary>
    public MauiTestContext TestContext => _rendered.Context;

    /// <summary>The window the page was rendered into.</summary>
    public Window Window => _rendered.Window;

    internal Page CurrentPage => _rendered.CurrentPage;

    #region Lookup

    public IReadOnlyList<IMauiElement> FindElements(Locator locator) => Find(Window, locator);

    /// <summary>
    /// The elements under <paramref name="root"/> that <paramref name="locator"/> names. An element
    /// with <c>IsVisible</c> false (or under one) is left out, as Windows, Android and iOS leave it
    /// out of their trees; one merely off screen, like a carousel's other cards, stays, as on
    /// Windows. With <paramref name="includeHidden"/> it is found anyway, for <c>TryFindDeclared</c>.
    /// </summary>
    internal IReadOnlyList<IMauiElement> Find(MauiElement root, Locator locator, bool includeHidden = false)
    {
        ArgumentNullException.ThrowIfNull(locator);
        return MauiHeadlessLocators.Match(Read(root), locator)
            .Where(node => includeHidden || !IsHidden(node.Element))
            .Select(node => (IMauiElement)new MauiHeadlessElement(this, node.Element))
            .ToList();
    }

    private static bool IsHidden(MauiElement element)
    {
        for (var walker = element; walker is not null; walker = walker.Parent)
        {
            if (walker is VisualElement { IsVisible: false })
                return true;
        }

        return false;
    }

    internal VisualNode Read(MauiElement root) => ElementQueries.Read(root);

    #endregion

    #region Session, platform, the app

    public void Close()
    {
    }

    public void Quit()
    {
    }

    // No pixels: a failure's page source carries the tree instead.
    public byte[] GetScreenshot() => [];

    public byte[] TakeScreenshot() => [];

    public MauiPlatform Platform => MauiPlatform.Headless;

    /// <summary>Shell's tab bar and flyout, as MauiHeadless's tree shows them (<see cref="ShellTabBar"/>, <see cref="ShellFlyoutList"/>).</summary>
    public ShellChromeLocators ShellChrome { get; } = new(
        TabHost: Locator.ByClassName(nameof(ShellTabBar)),
        Tab: Locator.ByClassName(nameof(ShellSection)),
        FlyoutHost: Locator.ByClassName(nameof(ShellFlyoutList)),
        FlyoutItem: Locator.ByClassName(nameof(ShellItem)));

    public IMauiElement AppElement => new MauiHeadlessElement(this, Window, isApp: true);

    public string Context { get; set; } = "NATIVE_APP";

    public IReadOnlyCollection<string> Contexts => ["NATIVE_APP"];

    // One per rendered window, so two apps in one process are told apart, as two windows are.
    public string CurrentWindowHandle => _handle;

    public IReadOnlyCollection<string> WindowHandles => [_handle];

    public string GetPageSource() => TreeSnapshot.Format(Read(Window));

    public string GetAutomationTree() => GetPageSource();

    #endregion

    #region Navigation

    public void NavigateTo(string destination)
    {
        if (Window.Page is not Shell)
            throw new DeviceOnlyException($"{nameof(NavigateTo)} without a Shell");
        _rendered.Navigation.GoTo(destination);
    }

    public void NavigateBack()
    {
        try
        {
            _rendered.Navigation.GoBack();
        }
        catch (InvalidOperationException nothingBehind)
        {
            throw new BruinetException(nothingBehind.Message, nothingBehind);
        }
    }

    public int NavigationDepth() => _rendered.Navigation.Stack.Count + _rendered.Navigation.ModalStack.Count;

    /// <summary>
    /// Whether the app has finished its work: its queued work run until nothing new has come for a
    /// moment (an awaited request answered and continued), within <paramref name="timeoutMs"/>. On a
    /// device the app works on by itself between a test's calls; headless it works only when asked,
    /// so a test's last work would otherwise finish during the next test.
    /// </summary>
    public bool IsIdle(int timeoutMs = 2000)
        => TestContext.Dispatcher.WaitForQuiet(QuietMoment, TimeSpan.FromMilliseconds(Math.Max(0, timeoutMs)));

    // Long enough for a local backend's answer to come back and be continued; short, as it is paid
    // on every page opened.
    private static readonly TimeSpan QuietMoment = TimeSpan.FromMilliseconds(25);

    // The top page's identity is its AutomationId, the id a caller then looks it up by
    // (PageNavigation.WaitArrived); its type name only where it has none.
    public string CurrentRoute()
        => _rendered.Navigation.Location
           ?? (string.IsNullOrEmpty(CurrentPage.AutomationId) ? CurrentPage.GetType().Name : CurrentPage.AutomationId);

    public void Refresh() => TestContext.Dispatcher.RunPending();

    public void ResetAppState() => throw new DeviceOnlyException(nameof(ResetAppState));

    public object? ExecuteScript(string script, params object[] args) => throw new DeviceOnlyException(nameof(ExecuteScript));

    /// <summary>
    /// A menu bar or context menu item by its <c>AutomationId</c>, raised without opening a menu, as
    /// the Windows bridge raises it: MAUI shows neither menu in any tree a test could walk.
    /// </summary>
    public void InvokeMenuItem(string automationId)
    {
        var page = CurrentPage;
        var menus = page.MenuBarItems.SelectMany(Flatten)
            .Concat(Read(Window).DescendantsAndSelf()
                .Select(node => node.Element)
                .OfType<Microsoft.Maui.Controls.BindableObject>()
                .Select(FlyoutBase.GetContextFlyout)
                .OfType<MenuFlyout>()
                .SelectMany(Flatten));
        Raise(menus.OfType<MenuItem>().FirstOrDefault(item => item.AutomationId == automationId), automationId, "menu item");
    }

    /// <summary>A toolbar item of the current page or a page that contains it, by its <c>AutomationId</c>.</summary>
    internal void InvokeToolbarItem(string automationId)
    {
        var items = new List<ToolbarItem>();
        for (Element? walker = CurrentPage; walker is not null; walker = walker.Parent)
        {
            if (walker is Page level)
                items.AddRange(level.ToolbarItems);
        }

        Raise(items.FirstOrDefault(item => item.AutomationId == automationId), automationId, "toolbar item");
    }

    private void Raise(MenuItem? item, string automationId, string kind)
    {
        if (item is null)
            throw new BruinetException($"No {kind} on the page carries that AutomationId: '{automationId}'.");
        if (!item.IsEnabled)
            throw new ElementNotReadyException(Locator.ByAutomationId(automationId), NotReadyReason.Disabled, $"the {kind} is disabled");

        TestContext.Dispatcher.Run(((IMenuItemController)item).Activate);
    }

    private static IEnumerable<IMenuElement> Flatten(IEnumerable<IMenuElement> entries)
    {
        foreach (var entry in entries)
        {
            yield return entry;
            if (entry is MenuFlyoutSubItem submenu)
            {
                foreach (var deeper in Flatten(submenu))
                    yield return deeper;
            }
        }
    }

    #endregion

    #region Gestures

    public bool SupportsGesture(string automationId, MauiGesture gesture)
        => Declared(automationId) is { } element && element.SupportsGesture(gesture);

    public void PerformGesture(string automationId, MauiGesture gesture, int arg1, int arg2)
        => (Declared(automationId) ?? throw new BruinetException($"No element carries the AutomationId '{automationId}'."))
            .PerformGesture(gesture, arg1, arg2);

    private MauiHeadlessElement? Declared(string automationId)
        => Find(Window, Locator.ByAutomationId(automationId), includeHidden: true).FirstOrDefault() as MauiHeadlessElement;

    #endregion

    public void Dispose()
    {
        if (_ownsContext)
            TestContext.Dispose();
    }

    public override string ToString() => new StringBuilder("MauiHeadless: ").Append(CurrentPage.GetType().Name).ToString();
}
