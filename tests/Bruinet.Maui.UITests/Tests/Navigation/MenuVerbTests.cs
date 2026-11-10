using Bruinet.Maui.Testing;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Utilities;
using Bruinet.Maui.UITests.Pages;
using Xunit;

namespace Bruinet.Maui.UITests.Tests.Navigation;

/// <summary>
/// Step 26: reaching a menu item that the accessibility tree cannot see.
/// </summary>
/// <remarks>
/// <para>
/// <b>These items are not merely awkward to find - they are absent.</b>
/// <c>NavigationProbeTests</c> measures it and reports <c>PageMenuFile</c> and
/// <c>PageMenuFileNew</c> as findable by neither <c>AutomationId</c> nor name: MAUI does not
/// propagate <c>AutomationId</c> to menu chrome on Windows (dotnet/maui#3996). A context flyout
/// is further out of reach still, because it does not exist in the tree until someone
/// right-clicks it into being.
/// </para>
/// <para>
/// <b>So the pointer was the whole story, twice over.</b> A right-click at one coordinate to
/// open the flyout, a click at another to pick an entry, and the app holding the foreground for
/// both - which is what stage B exists to stop, and the reason
/// <c>FlaUIMauiElement.RightClick</c> has been recording itself as a physical-input use with
/// this step named as its replacement.
/// </para>
/// <para>
/// <b>The pointer is still right for one thing.</b> A test that means "right-clicking opens a
/// menu showing these entries" is a test about the menu, and no verb can stand in for it. What
/// the verb removes is the far commoner case: a test that only wanted the action to happen.
/// </para>
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "GestureBridge")]
[Trait("Stage", "Background")]
public class MenuVerbTests
{
    private readonly MauiFixture _fixture;

    public MenuVerbTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _fixture.NavigateToNavigationDemo();
    }

    private NavigationDemoPage Page => _fixture.NavigationDemoPage;

    /// <summary>
    /// The premise, measured here rather than taken from the probe.
    /// </summary>
    /// <remarks>
    /// <b>A test whose whole point is "no other route exists" has to check that.</b> If MAUI
    /// starts publishing menu chrome, this fails and says so - at which point the verb is a
    /// convenience rather than the only way in, and everything below should be re-argued.
    /// </remarks>
    [DeviceFact("it checks what Windows' accessibility tree leaves out", Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task MenuItems_AreInvisibleToTheAccessibilityTree()
    {
        Assert.Null(Page.TryFindByAutomationId("PageMenuFileNew"));
        Assert.Null(Page.TryFindByName("New"));
        Assert.Null(Page.TryFindByAutomationId("ContextMenuCopy"));

        return Task.CompletedTask;
    }

    /// <summary>
    /// A page-menu item runs, without the pointer.
    /// </summary>
    [WindowsOnlyFact("MAUI shows menu bars and context menus on Windows and Mac Catalyst only", Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task InvokeMenuItem_RunsAPageMenuItem_WithoutThePointer()
    {
        _fixture.Context.Driver.InvokeMenuItem("PageMenuFileNew");

        Page.LastAction.AssertText("Menu/File/New");

        return Task.CompletedTask;
    }

    /// <summary>
    /// A context-menu item runs without the flyout ever opening.
    /// </summary>
    /// <remarks>
    /// The entry belongs to a flyout that has never been shown, so there is nothing on screen to
    /// click even in principle. <c>IMenuItemController.Activate</c> is MAUI's own entry point for
    /// "the user picked this", so the app cannot tell this apart from the real thing.
    /// </remarks>
    [WindowsOnlyFact("MAUI shows menu bars and context menus on Windows and Mac Catalyst only", Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task InvokeMenuItem_RunsAContextMenuItem_WithoutOpeningTheFlyout()
    {
        _fixture.Context.Driver.InvokeMenuItem("ContextMenuCopy");

        Page.LastAction.AssertText("Menu/Context/Copy");

        return Task.CompletedTask;
    }

    /// <summary>
    /// A disabled item is refused, and nothing runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The assertion a disabled control exists to make false.</b> Nothing stops the app from
    /// executing the command behind a greyed-out entry, so a verb that did would let a test
    /// prove an action the app is refusing to offer. The refusal names the reason, because
    /// "disabled" and "no such item" call for opposite responses from whoever reads the failure.
    /// </para>
    /// <para>
    /// <b>This test found that <c>S_FALSE</c> does not survive the bridge.</b> The app answered
    /// "found it, did nothing" and the client read plain success - UI Automation reports every
    /// success HRESULT from a custom pattern as <c>S_OK</c>, measured from both ends at once
    /// through the bridge log. The outcome now travels as a value, which is why this asserts on
    /// the message rather than on an HRESULT.
    /// </para>
    /// </remarks>
    [WindowsOnlyFact("MAUI shows menu bars and context menus on Windows and Mac Catalyst only", Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task InvokeMenuItem_WhenTheItemIsDisabled_IsRefusedAndDoesNothing()
    {
        var refusal = Assert.Throws<ElementNotReadyException>(
            () => _fixture.Context.Driver.InvokeMenuItem("ContextMenuDelete"));

        Assert.Equal(NotReadyReason.Disabled, refusal.Reason);
        Assert.Contains("disabled", refusal.Message);

        // The action counter is the check that matters: a refusal that had already run the
        // command would be a worse failure than no refusal at all.
        Page.LastAction.AssertText("none");

        return Task.CompletedTask;
    }

    /// <summary>
    /// An unknown id is refused with a reason, rather than quietly doing nothing.
    /// </summary>
    [WindowsOnlyFact("MAUI shows menu bars and context menus on Windows and Mac Catalyst only", Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task InvokeMenuItem_WithNoSuchItem_IsRefusedWithTheReason()
    {
        var refusal = Assert.Throws<BruinetException>(
            () => _fixture.Context.Driver.InvokeMenuItem("NoSuchMenuItem"));

        Assert.Contains("carries that AutomationId", refusal.Message);

        return Task.CompletedTask;
    }
}
