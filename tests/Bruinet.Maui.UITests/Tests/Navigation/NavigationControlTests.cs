using Bruinet.Core.Collections;
using Bruinet.Maui.Controls.Navigation;
using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Navigation;

/// <summary>
/// UI tests for the navigation control objects: Toolbar, Menu, and TabMenu.
/// </summary>
/// <remarks>
/// <para>
/// Every assertion is on view-model state observed through a label — never on a fixed
/// delay. A click either changed <c>LastActionLabel</c> or it did not.
/// </para>
/// <para>
/// Shell is a different app and a different suite; see Tests/Shell.
/// </para>
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "Navigation")]
public class NavigationControlTests
{
    /// <summary>
    /// How long to wait when the point is to prove something is absent.
    /// </summary>
    /// <remarks>
    /// Absence costs the whole timeout by construction, so the timeout is chosen for what the
    /// assertion is about - which exception - rather than for how long a present element might
    /// take to appear. These two tests were 20 s of this class's 24 s.
    /// </remarks>
    private const int NegativeAssertionTimeoutMs = 500;

    private readonly MauiFixture _fixture;

    public NavigationControlTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _fixture.NavigateToNavigationDemo();
    }

    private NavigationDemoPage Page => _fixture.NavigationDemoPage;

    #region Toolbar

    /// <summary>1. The toolbar resolves, and both toolbars are distinct elements.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Control", "Toolbar")]
    public Task Toolbar_Exists()
    {
        Page.PrimaryToolbar.AssertExists();
        Page.SecondaryToolbar.AssertExists();

        return Task.CompletedTask;
    }

    /// <summary>
    /// 1b. GetTitle returns without throwing on a container that carries no caption.
    /// </summary>
    /// <remarks>
    /// <c>GetTitleCore</c> falls back through the Title attribute, the text attribute,
    /// then <c>Text</c>. A layout container has none of those, so the meaningful contract
    /// is that the call completes rather than that it yields a particular value — asserting
    /// a value here would only pin whichever fallback the platform happens to answer with.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "GetTitle")]
    public Task Toolbar_GetTitle_DoesNotThrowWithoutACaption()
    {
        var exception = Record.Exception(() => Page.PrimaryToolbar.GetTitle());

        Assert.Null(exception);

        return Task.CompletedTask;
    }

    /// <summary>2. An item clicks itself.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task Toolbar_ClickItem_FiresTheItem()
    {
        Page.PrimaryToolbar["Save"].Click();

        Page.LastAction.AssertText("Primary/Save");
        Page.ActionCount.AssertText("1");

        return Task.CompletedTask;
    }

    /// <summary>
    /// 3. Items are found within their own toolbar.
    /// </summary>
    /// <remarks>
    /// Both toolbars hold a Save item. If the toolbar searched page-wide, both clicks would
    /// hit whichever came first in the tree and the two recorded actions would be identical.
    /// This is the test that makes the repeated items in the demo markup worth having.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "Scoping")]
    public Task Toolbar_ItemSearch_IsScopedToItsOwnToolbar()
    {
        Page.PrimaryToolbar["Save"].Click();
        Page.LastAction.AssertText("Primary/Save");

        Page.SecondaryToolbar["Save"].Click();
        Page.LastAction.AssertText("Secondary/Save");

        return Task.CompletedTask;
    }

    /// <summary>4. An item present in one toolbar but absent from the other is not found.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "NoParentFallback")]
    public Task Toolbar_DoesNotReachItemsOutsideItself()
    {
        // Only the primary toolbar has a Back item.
        Page.PrimaryToolbar["Back"].Click();
        Page.LastAction.AssertText("Primary/Back");

        // TryItem answers now; Item waits, so it is given a short timeout rather than the
        // full one it would otherwise spend confirming an absence the line above proved.
        Assert.Null(Page.SecondaryToolbar.TryItem("Back"));
        // Stage G step 33B. The assertion is about which exception, not about how long Item
        // waits before throwing it - and it waits the whole timeout, correctly, because an
        // absence can only be proven by running out of time. Ten seconds proved nothing half a
        // second does not.
        Assert.Throws<ElementNotFoundException>(
            () => Page.SecondaryToolbar.Item("Back", NegativeAssertionTimeoutMs));

        return Task.CompletedTask;
    }

    /// <summary>5. A toolbar knows how many items it has.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "AssertItemCount")]
    public Task Toolbar_ReportsItsItems()
    {
        Page.PrimaryToolbar.AssertItemCount(3);
        Page.SecondaryToolbar.AssertItemCount(2);

        Assert.Equal(
            new[] { "Save", "Delete", "Back" },
            Page.PrimaryToolbar.Items.Select(item => item.GetText()));

        return Task.CompletedTask;
    }

    /// <summary>
    /// 5b. An item can be addressed by its automation id or by an explicit selector.
    /// </summary>
    /// <remarks>
    /// The plain string key prefers the automation id and falls back to the caption, which is
    /// why both spellings of the Save item reach it. The demo markup gives every item both, so
    /// this test would still pass if only one route worked — hence the explicit
    /// <c>ByText</c> selector on the second line, which cannot resolve by id.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "ItemKey")]
    public Task Toolbar_ItemsAreAddressableByIdAndByCaption()
    {
        Page.PrimaryToolbar["ToolbarSaveButton"].Click();
        Page.LastAction.AssertText("Primary/Save");

        Page.PrimaryToolbar.ItemByText("Delete").Click();
        Page.LastAction.AssertText("Primary/Delete");

        Page.PrimaryToolbar.ItemByAutomationId("ToolbarBackButton").Click();
        Page.LastAction.AssertText("Primary/Back");

        return Task.CompletedTask;
    }

    /// <summary>6. An item answers for its own state.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "ItemState")]
    public Task Toolbar_Item_ReportsItsOwnState()
    {
        var save = Page.PrimaryToolbar["Save"];

        save.AssertText("Save")
            .AssertEnabled()
            .AssertVisible();

        return Task.CompletedTask;
    }

    /// <summary>
    /// 7. An item's members return the item; the toolbar's return the toolbar.
    /// </summary>
    /// <remarks>
    /// A chain stays where it is until <c>Parent</c> walks back out, which is what makes
    /// <c>Toolbar["Save"].AssertEnabled().Click()</c> read as one thought.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "FluentReturn")]
    public Task Toolbar_Members_ReturnWhereTheyAre()
    {
        var page = Page;
        var save = page.PrimaryToolbar["Save"];

        ToolbarItem<NavigationDemoPage> afterClick = save.Click();
        Assert.Same(save, afterClick);

        Toolbar<NavigationDemoPage> afterCount = page.PrimaryToolbar.AssertItemCount(3);
        Assert.Same(page.PrimaryToolbar, afterCount);

        Assert.Same(page, afterCount.Parent);

        return Task.CompletedTask;
    }

    #endregion

    #region Menu

    /// <summary>7. The menu resolves and starts closed.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Control", "Menu")]
    public Task Menu_Exists_AndStartsClosed()
    {
        Page.ActionsMenu.AssertExists();
        Page.ActionsMenu.AssertOpen(false);

        // A closed menu has no items to offer.
        Page.ActionsMenu.AssertItemCount(0);

        return Task.CompletedTask;
    }

    /// <summary>8. Open expands the menu; its items become reachable.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Open")]
    public Task Menu_Open_RevealsItems()
    {
        Page.ActionsMenu.Open()
            .AssertOpen()
            .AssertItemCount(3);

        Assert.Equal(
            new[] { "New", "Open", "Close" },
            Page.ActionsMenu.Items.Select(item => item.GetText()));

        return Task.CompletedTask;
    }

    /// <summary>
    /// 9. An item clicks itself, and selecting one dismisses the menu.
    /// </summary>
    /// <remarks>
    /// The trigger is a button inside the menu too, so this also covers the item host doing
    /// its job: if items were searched menu-wide, the trigger would be one of them and the
    /// count above would be four.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task Menu_ClickItem_FiresItAndCloses()
    {
        Page.ActionsMenu.Open()["Open"].Click();

        Page.LastAction.AssertText("Menu/Open");
        Page.ActionsMenu.AssertOpen(false);

        return Task.CompletedTask;
    }

    /// <summary>10. The trigger toggles: opening an open menu closes it.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Control", "Menu")]
    public Task Menu_OpenTwice_Toggles()
    {
        Page.ActionsMenu.Open().AssertOpen();
        Page.ActionsMenu.Open().AssertOpen(false);

        return Task.CompletedTask;
    }

    /// <summary>11. Menu items are addressable by id as well as by caption.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "ItemKey")]
    public Task Menu_ItemsAreAddressableById()
    {
        Page.ActionsMenu.Open().ItemByAutomationId("MenuItemNew").Click();

        Page.LastAction.AssertText("Menu/New");

        return Task.CompletedTask;
    }

    #endregion

    #region TabMenu

    /// <summary>12. The tab menu resolves and reports its tabs.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Control", "TabMenu")]
    public Task TabMenu_Exists_AndReportsItsTabs()
    {
        Page.Tabs.AssertExists()
            .AssertItemCount(3);

        // A tab's own element carries no text: the caption comes from the label inside it.
        Assert.Equal(
            new[] { "Home", "Search", "Profile" },
            Page.Tabs.Items.Select(tab => tab.GetText()));

        return Task.CompletedTask;
    }

    /// <summary>13. A tab activates itself.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task TabMenu_ClickTab_ActivatesIt()
    {
        Page.Tabs["Search"].Click();

        Page.SelectedTab.AssertText("Search");
        Page.LastAction.AssertText("Tab/Search");

        return Task.CompletedTask;
    }

    /// <summary>14. Clicking a different tab moves the selection.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task TabMenu_ClickAnother_MovesSelection()
    {
        Page.Tabs["Home"].Click();
        Page.SelectedTab.AssertText("Home");

        Page.Tabs["Profile"].Click();
        Page.SelectedTab.AssertText("Profile");

        return Task.CompletedTask;
    }

    /// <summary>15. An unknown caption is absent rather than an error.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "TryItem")]
    public Task TabMenu_UnknownCaption_IsAbsent()
    {
        Assert.Null(Page.Tabs.TryItem("Nonexistent"));

        // Nothing was selected, so the recorded state is untouched.
        Page.SelectedTab.AssertText("none");

        return Task.CompletedTask;
    }

    /// <summary>16. Naming an unknown tab throws.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Item")]
    public Task TabMenu_UnknownCaption_Throws()
    {
        // Step 33B, as above: a short timeout proves the same exception.
        Assert.Throws<ElementNotFoundException>(
            () => Page.Tabs.Item("Nonexistent", NegativeAssertionTimeoutMs));

        return Task.CompletedTask;
    }

    /// <summary>
    /// 17. A tab bar built from plain buttons reports no selection.
    /// </summary>
    /// <remarks>
    /// The demo's tabs are buttons, and a button exposes neither the selection pattern nor a
    /// checked state - so <c>IsSelected</c> answers false even for the tab just clicked, and
    /// the app's own record of the current tab is what the tests above assert. Pinned
    /// deliberately: reading "current" from app state or from styling is exactly the guess this
    /// control must not make. Real selection is covered where tabs are really selectable:
    /// the Shell sample app, under <c>ShellFixture</c>.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsSelected")]
    public Task TabMenu_PlainButtonBar_ReportsNoSelection()
    {
        Page.Tabs["Home"].Click();

        Page.SelectedTab.AssertText("Home");
        Page.Tabs["Home"].AssertSelected(false);

        return Task.CompletedTask;
    }

    #endregion

    #region Fixture contract

    /// <summary>16. Reset restores the initial state, so tests are order-independent.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "Reset")]
    public Task Reset_RestoresInitialState()
    {
        Page.PrimaryToolbar["Save"].Click();
        Page.Tabs["Home"].Click();

        Page.ResetButton.Click();

        Page.LastAction.AssertText("none");
        Page.ActionCount.AssertText("0");
        Page.SelectedTab.AssertText("none");

        return Task.CompletedTask;
    }

    #endregion
}
