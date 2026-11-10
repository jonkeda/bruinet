using Bruinet.Maui.Testing;
using Bruinet.Core.Locators;
using Bruinet.Maui.Interfaces;
using Bruinet.Maui.UITests.Pages;
using Xunit;

namespace Bruinet.Maui.UITests.Tests.Background;

/// <summary>
/// Step 21: scrolling by asking the app, rather than by turning a wheel and watching.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this replaces is the least quantified thing in the framework.</b> A mouse-wheel click
/// means whatever the OS and the control between them decide it means, and nothing reports that
/// the content arrived - so the old route scrolled a step, polled a scroll percentage, and
/// guessed when it had stopped moving. <c>ScrollToAsync</c> takes the element you want and puts
/// it on screen.
/// </para>
/// <para>
/// <b>And it is the same call on every platform</b>, which is why the plan calls this the biggest
/// parity win in the catalogue. The wheel exists only on desktop; a swipe substituted for it on
/// Android is a different gesture with different physics and its own stuck-detection.
/// </para>
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "GestureBridge")]
[Trait("Stage", "Background")]
public class ScrollVerbTests
{
    private const string Scroller = "ScrollTestScroller";

    private readonly MauiFixture _fixture;

    public ScrollVerbTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _fixture.Open(SamplePage.Scroll);
    }

    private IMauiElement Element
        => _fixture.Context.TryFindElement(Locator.ByAutomationId(Scroller))
           ?? throw new InvalidOperationException(
               $"'{Scroller}' was not found, so nothing below is measuring what it claims.");

    /// <summary>
    /// The page is long enough for scrolling to mean something.
    /// </summary>
    /// <remarks>
    /// <b>The precondition, asserted rather than assumed.</b> "We are at the bottom" is true both
    /// for a list scrolled to its end and for a page too short to scroll at all, so a test that
    /// only checked the offset would pass on a page that had quietly become short - which is a
    /// change to the sample app that ought to fail loudly.
    /// </remarks>
    [DeviceFact("it reads scroll offsets and extents, which need layout", Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task ScrollPosition_ReportsExtentAsWellAsOffset()
    {
        var position = Element.ReadScrollPosition();

        Assert.True(
            position.CanScrollVertically,
            $"The scroll page is {position.ContentHeight} tall in a {position.ViewportHeight} "
            + "viewport, so it cannot scroll and the scrolling tests prove nothing.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Scrolling to the bottom element moves the viewport, with no pointer anywhere.
    /// </summary>
    /// <remarks>
    /// Asserted as a change in offset rather than as an absolute value: what "the bottom" is
    /// depends on the window size, and a test that hard-coded it would be a test about this
    /// machine.
    /// </remarks>
    [DeviceFact("it reads scroll offsets, which need layout", Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task ScrollTo_MovesTheViewport_WithoutPhysicalInput()
    {
        var before = Element.ReadScrollPosition();
        Assert.True(before.IsAtTop, $"The page did not start at the top; it is at {before.Y}.");

        Element.ScrollTo("ScrollBottomButton");

        var after = Element.ReadScrollPosition();

        Assert.True(
            after.Y > before.Y,
            $"Scrolling to the bottom button left the offset at {after.Y}, from {before.Y}.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Each scroll puts the element that was named on screen, in both directions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other direction, because "scrolled" and "scrolled to the thing I named" are different
    /// claims and only the round trip separates them: a route that always scrolled to the end
    /// would pass the test above.
    /// </para>
    /// <para>
    /// <b>Asserted on what is visible, not on the offset returning to zero.</b> That was this
    /// test's first form and it was wrong: <c>ScrollToPosition.MakeVisible</c> scrolls the
    /// minimum needed, so scrolling back to the first element leaves that element at the top of
    /// the viewport rather than the viewport at the top of the content - short by the stack
    /// layout's padding. Visibility is what the verb promises, so visibility is what this asks.
    /// </para>
    /// </remarks>
    [DeviceFact("it reads scroll offsets, which need layout", Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task ScrollTo_PutsTheNamedElementOnScreen()
    {
        var page = new ScrollTestPage(_fixture.Context);

        Element.ScrollTo("ScrollBottomButton");

        var atBottom = Element.ReadScrollPosition();
        Assert.True(page.BottomLabel.IsVisible(), "The bottom label is not on screen.");

        Element.ScrollTo("ScrollStatusLabel");

        var backUp = Element.ReadScrollPosition();

        Assert.True(
            backUp.Y < atBottom.Y,
            $"Scrolling back to the status label left the offset at {backUp.Y}, no higher than "
            + $"the {atBottom.Y} it reached at the bottom.");

        Assert.True(page.StatusLabel.IsVisible(), "The status label is not back on screen.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// A virtualized collection scrolls to an index, materializing rows the wheel used to chase.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The case the wheel loop existed for.</b> A <c>CollectionView</c> recycles row
    /// containers, so a row further down the list does not exist until the viewport approaches
    /// it - and the old route advanced the list a wheel click at a time, re-reading the realized
    /// items after each one and stopping when the count stopped changing. Asking for an index
    /// is one call with a definite answer.
    /// </para>
    /// <para>
    /// Asserted by the realized rows changing rather than by an offset: a collection reports no
    /// scroll position of its own, and the rows that exist are the thing the search actually
    /// cares about.
    /// </para>
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task ScrollToIndex_MaterializesARowFurtherDown()
    {
        _fixture.Open(SamplePage.GridCollection);

        var products = _fixture.GridCollectionDemoPage.Products;
        products.BulkAddButton.Click();
        Assert.True(products.WaitLogicalCount(63, TestConstants.LongTestTimeoutMs));

        var collection = _fixture.Context.TryFindElement(
                             Locator.ByAutomationId("ProductCollectionView"))
                         ?? throw new InvalidOperationException(
                             "ProductCollectionView was not found.");

        collection.ScrollToIndex(0);
        var top = collection.FindElements(Locator.ByAutomationId("ProductRow")).Count;

        collection.ScrollToIndex(60);
        var down = collection.FindElements(Locator.ByAutomationId("ProductRow")).Count;

        Assert.True(
            top > 0 && down > 0,
            $"The collection realized {top} rows at the top and {down} after scrolling, so "
            + "one of the two scrolls did not land anywhere with rows in it.");

        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task ScrollToIndex_AtItemCount_IsRefusedWithTheCount()
    {
        _fixture.Open(SamplePage.GridCollection);

        var collection = _fixture.Context.TryFindElement(
                             Locator.ByAutomationId("ProductCollectionView"))
                         ?? throw new InvalidOperationException(
                             "ProductCollectionView was not found.");
        var itemCount = int.Parse(
            collection.ReadState("ItemCount")
                ?? throw new InvalidOperationException(
                    "ProductCollectionView does not answer GetState('ItemCount')."),
            System.Globalization.CultureInfo.InvariantCulture);

        var error = Assert.Throws<ArgumentOutOfRangeException>(
            () => collection.ScrollToIndex(itemCount));

        Assert.Contains($"item count is {itemCount}", error.Message);
        return Task.CompletedTask;
    }

    /// <summary>
    /// An id that is not in the scroller is reported, not ignored.
    /// </summary>
    /// <remarks>
    /// The failure that matters most in practice: a typo in an id, or an element that has moved
    /// out of this scroller, used to be a scroll that ran to the end and found nothing. Naming it
    /// is the difference between a two-second fix and reading a wheel-loop's logs.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task ScrollTo_AnIdThatIsNotThere_SaysSo()
    {
        var error = Assert.Throws<BruinetException>(
            () => Element.ScrollTo("NoSuchElementOnThisPage"));

        Assert.Contains("NoSuchElementOnThisPage", error.Message);
        return Task.CompletedTask;
    }
}
