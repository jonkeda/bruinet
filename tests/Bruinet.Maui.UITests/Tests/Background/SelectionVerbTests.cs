using Bruinet.Maui.Testing;
using Bruinet.Core.Locators;
using Bruinet.Maui.Interfaces;
using Bruinet.Maui.UITests.Pages;
using Xunit;

namespace Bruinet.Maui.UITests.Tests.Background;

/// <summary>
/// Step 24: selecting without opening anything, and reading without moving anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every question a test asked a picker was answered by opening its dropdown.</b> Selecting
/// meant expand, wait up to two seconds for the popup's items to reach the accessibility tree,
/// select one, collapse. Reading the item texts meant the same journey - so asking a picker what
/// it held changed what a user would see, and asking twice in a row was two different journeys
/// through the app. A read with a side effect is the one kind that cannot be repeated to check
/// itself.
/// </para>
/// <para>
/// <b>The flyout keeps its own members, because some tests really are about the flyout.</b>
/// <c>OpenFlyout</c> and <c>CloseFlyout</c> say so out loud; they stay on the ExpandCollapse
/// pattern rather than becoming bridge verbs, since MAUI has no public API to open a Picker's
/// dropdown and an app could only answer such a verb by reaching into WinUI.
/// </para>
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "GestureBridge")]
[Trait("Stage", "Background")]
public class SelectionVerbTests
{
    private readonly MauiFixture _fixture;

    public SelectionVerbTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _fixture.Open(SamplePage.Selection);
    }

    private SelectionTestPage Page => new(_fixture.Context);

    private IMauiElement Element(string automationId)
        => _fixture.Context.TryFindElement(Locator.ByAutomationId(automationId))
           ?? throw new InvalidOperationException($"'{automationId}' was not found.");

    /// <summary>
    /// The control group: the sample's pickers answer state reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without this, the reads below would quietly fall back to the dropdown and still pass -
    /// which is how a capability comes to be believed in without ever having been exercised.
    /// </para>
    /// <para>
    /// <b>The selection verbs have no flag to assert any more</b>: <c>SupportsSelectIndex</c> and
    /// <c>SupportsSelectByText</c> went when the element took over the route choice. What catches a
    /// quiet fallback to the dropdown now is the refusal tests below - the app refuses with a
    /// <see cref="BruinetException"/> naming its reason, and the dropdown route throws something
    /// else entirely.
    /// </para>
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task Pickers_OfferTheSemanticRoute()
    {
        var picker = Element("TestPicker");

        // A non-null answer is the declaration: ReadState returns null where the app does not
        // answer state reads at all, which is what the removed SupportsStateReads flag said.
        Assert.NotNull(picker.ReadState("ItemCount"));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Selecting by index works without physical input.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task SelectIndex_WorksWithoutPhysicalInput()
    {
        var page = Page;

        page.TestPicker.SelectByIndex(2);

        page.StatusLabel.AssertTextContains("Option 3");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Selecting leaves the dropdown shut.
    /// </summary>
    /// <remarks>
    /// <b>The point of the step, as an assertion.</b> The old route's popup opened and closed
    /// again, so the app ended where it started and nothing failed - but a test that meant to
    /// change a value had performed a flyout journey on the way, and a test that meant to check
    /// the flyout could not be distinguished from it. Now only one of them opens anything.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task SelectIndex_DoesNotOpenTheDropdown()
    {
        var page = Page;

        page.TestPicker.SelectByIndex(1);

        Assert.False(
            page.TestPicker.IsFlyoutOpen(),
            "The picker's dropdown is open after a selection that never asked for it.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reading the items does not move the app, and can be repeated.
    /// </summary>
    /// <remarks>
    /// Asserted twice deliberately: a read whose answer depends on what the previous read left
    /// behind is the failure mode being removed, and one call cannot show that it is gone.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task ItemTexts_AreReadWithoutOpeningTheDropdown()
    {
        var page = Page;

        var first = page.TestPicker.GetItemTexts();
        var second = page.TestPicker.GetItemTexts();

        Assert.Equal(new[] { "Option 1", "Option 2", "Option 3", "Option 4", "Option 5" }, first);
        Assert.Equal(first, second);
        Assert.Equal(5, page.TestPicker.GetItemCount());

        Assert.False(
            page.TestPicker.IsFlyoutOpen(),
            "Reading the picker's items left its dropdown open, so the read changed what a user "
            + "would see.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// A picker with more items than its dropdown renders reports all of them, and its own
    /// index.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The case the old reads got wrong.</b> Both the item list and the count came from the
    /// open popup, which realizes only the items it is showing - so a picker of two hundred
    /// reported the visible handful, and the index, derived by finding the selected text in that
    /// short list, was wrong or null for anything below the fold. Neither read failed; they
    /// answered confidently about a list that was not the picker's.
    /// </para>
    /// <para>
    /// Why the popup is not asked is <see cref="LongPicker_Dropdown_RealizesOnlySomeItems"/>, on a device.
    /// </para>
    /// </remarks>
    [Fact(Timeout = TestConstants.LongTestTimeoutMs)]
    public Task LongPicker_ReportsEveryItemAndItsOwnIndex()
    {
        var page = Page;

        page.LongPicker.SelectByIndex(150);

        Assert.Equal(200, page.LongPicker.GetItemCount());
        Assert.Equal(150, page.LongPicker.GetSelectedIndex());
        Assert.Equal("Item 151", page.LongPicker.GetSelectedText());
        return Task.CompletedTask;
    }

    /// <summary>A long picker's open dropdown realizes only some of its items.</summary>
    /// <remarks>
    /// The contrast is asserted rather than described: the popup is opened and its realized
    /// items counted, so if a platform ever does render all two hundred, this says so instead of
    /// quietly ceasing to demonstrate anything.
    /// </remarks>
    [DeviceFact("it shows that a platform's picker popup realizes only some items", Timeout = TestConstants.LongTestTimeoutMs)]
    public Task LongPicker_Dropdown_RealizesOnlySomeItems()
    {
        var page = Page;

        page.LongPicker.SelectByIndex(150);

        var realized = RealizedDropdownItems(page);

        Assert.True(
            realized < 200,
            $"The dropdown realized all {realized} items, so reading them out of the popup would "
            + "have been right after all and this test no longer demonstrates why the app is "
            + "asked instead.");

        return Task.CompletedTask;
    }

    /// <summary>How many items the open dropdown has put into the accessibility tree.</summary>
    /// <remarks>
    /// The old route's view of the picker, obtained the way the old route obtained it. Left open
    /// for no longer than the count takes.
    /// </remarks>
    private static int RealizedDropdownItems(SelectionTestPage page)
    {
        page.LongPicker.OpenFlyout();

        try
        {
            return page.LongPicker.GetDropdownItemTexts()?.Count ?? 0;
        }
        finally
        {
            page.LongPicker.CloseFlyout();
        }
    }

    /// <summary>
    /// A selection MAUI cannot hold is refused, rather than attempted and hung.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured, not theorised.</b> Setting a <c>Picker</c> to the second of two
    /// identically-displayed items makes MAUI resolve <c>SelectedItem</c> back to the first
    /// equal entry and set <c>SelectedIndex</c> from it, forever: one core at 100%, the window
    /// stops responding, and every later verb in the run times out. The app's own code doing the
    /// same assignment freezes it identically with no automation running, so it is MAUI's
    /// behaviour and not the bridge's.
    /// </para>
    /// <para>
    /// A verb that reproduced it would take down the app under test and every test after it in
    /// the collection. Refusing costs a comparison the app can make before it assigns, and turns
    /// a wedged machine into an answer.
    /// </para>
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task SelectIndex_WhereMauiCannotHoldTheSelection_IsRefusedRatherThanAttempted()
    {
        var picker = Element("DuplicatePicker");

        var refusal = Assert.Throws<BruinetException>(() => picker.SelectIndex(1));

        Assert.Contains("an earlier item is equal to this one", refusal.Message);

        // Still answering, which is the whole point of the refusal.
        Assert.Equal("3", picker.ReadState("ItemCount"));

        return Task.CompletedTask;
    }

    /// <summary>
    /// An index past the end is refused with a reason.
    /// </summary>
    /// <remarks>
    /// The app range-checks against its own list, which is the only place the real count lives:
    /// the dropdown route counted the items the popup had rendered, and that is a different
    /// number while a virtualized list is still filling.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task SelectIndex_PastTheEnd_IsRefusedWithTheReason()
    {
        var picker = Element("TestPicker");

        var refusal = Assert.Throws<BruinetException>(() => picker.SelectIndex(99));

        Assert.Contains("past the end", refusal.Message);

        return Task.CompletedTask;
    }

    /// <summary>
    /// A text no item shows is refused rather than silently doing nothing.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task SelectByText_WithNoSuchItem_IsRefusedWithTheReason()
    {
        var picker = Element("TestPicker");

        Assert.Throws<BruinetException>(() => picker.SelectByText("Option 99"));

        return Task.CompletedTask;
    }

    /// <summary>
    /// A test that means "the dropdown opens and shows these items" can still say so.
    /// </summary>
    /// <remarks>
    /// The other half of the split. Selection no longer opens the flyout, so the flyout needs a
    /// way of being asked for - otherwise the capability disappears along with the side effect
    /// that used to stand in for it.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task OpenFlyout_ShowsTheDropdownAndCloseFlyoutPutsItBack()
    {
        var page = Page;

        page.TestPicker.OpenFlyout();
        Assert.True(page.TestPicker.IsFlyoutOpen(), "The picker's dropdown did not open.");

        page.TestPicker.CloseFlyout();
        Assert.False(page.TestPicker.IsFlyoutOpen(), "The picker's dropdown did not close.");

        return Task.CompletedTask;
    }
}
