using Bruinet.Maui.Testing;
using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Scroll;

/// <summary>
/// Tests scrolling on its own terms.
/// </summary>
/// <remarks>
/// <para>
/// These make the scroll itself the subject, rather than exercising it incidentally through
/// tests whose target happens to sit below the fold — where a scroll defect surfaces as an
/// unrelated assertion about something else.
/// </para>
/// <para>
/// The page is deliberately taller than any screen, with the status label first and the buttons
/// spread down it, so acting on one and reading the result crosses the full height in both
/// directions.
/// </para>
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "ScrollView")]
public class ScrollTests
{
    /// <summary>
    /// The budget of a call that crosses the page: its scroll to the control is part of the call
    /// and bounded by it (call-model R2), and on an Android emulator a swipe with its lookup costs
    /// about a second, so crossing this page down and back up takes 6 to 7 s, more than the
    /// default 5 s. A call that stays on screen keeps the default.
    /// </summary>
    private const int CrossPageMs = 10_000;

    private readonly MauiFixture _fixture;

    public ScrollTests(MauiFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>1. A control above the fold works with no scrolling involved — the control case.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task TopButton_Click_UpdatesStatus()
    {
        var page = _fixture.NavigateToScroll();

        page.TopButton.Click();

        page.StatusLabel.AssertTextContains("top pressed");
        return Task.CompletedTask;
    }

    /// <summary>2. Clicking a button that is below the fold requires scrolling down to it.</summary>
    [Fact(Timeout = TestConstants.LongTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task BottomButton_Click_UpdatesStatus()
    {
        var page = _fixture.NavigateToScroll();

        page.BottomButton.Click(CrossPageMs);

        // The label is at the top of the page, so this read also has to come back up.
        page.StatusLabel.AssertTextContains("bottom pressed", timeoutMs: CrossPageMs);
        return Task.CompletedTask;
    }

    /// <summary>3. The same, one screen down rather than at the very end.</summary>
    [Fact(Timeout = TestConstants.LongTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task MiddleButton_Click_UpdatesStatus()
    {
        var page = _fixture.NavigateToScroll();

        page.MiddleButton.Click(CrossPageMs);

        page.StatusLabel.AssertTextContains("middle pressed", timeoutMs: CrossPageMs);
        return Task.CompletedTask;
    }

    /// <summary>4. Scrolling down and then back up again — the direction the Reset tests failed on.</summary>
    [Fact(Timeout = TestConstants.LongTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task TopButton_Click_AfterScrollingToTheBottom_UpdatesStatus()
    {
        var page = _fixture.NavigateToScroll();

        page.BottomButton.Click(CrossPageMs);
        page.StatusLabel.AssertTextContains("bottom pressed", timeoutMs: CrossPageMs);

        page.TopButton.Click(CrossPageMs);

        page.StatusLabel.AssertTextContains("top pressed", timeoutMs: CrossPageMs);
        return Task.CompletedTask;
    }

    /// <summary>5. Reading text from a control below the fold.</summary>
    [Fact(Timeout = TestConstants.LongTestTimeoutMs)]
    [Trait("Method", "GetText")]
    public Task BottomLabel_Text_IsReadable()
    {
        var page = _fixture.NavigateToScroll();

        page.BottomLabel.AssertTextContains("bottom reached", timeoutMs: CrossPageMs);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 6. The two visibility questions, on one control, in one place.
    /// </summary>
    /// <remarks>
    /// <c>IsVisible</c> asks whether the control is on screen now and must answer false; the
    /// bottom label cannot be on screen while the page is scrolled to the top.
    /// <c>IsVisibleAfterScroll</c> asks whether it can be seen at all and must answer true.
    /// Both answers have to be the same on Windows and Android, which is the whole reason the
    /// two are separate methods.
    /// </remarks>
    [DeviceFact("it needs layout: what is below the fold", Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsVisible")]
    public Task BottomLabel_IsVisible_IsFalseUntilScrolledTo()
    {
        var page = _fixture.NavigateToScroll();

        Assert.False(page.BottomLabel.IsVisible());
        Assert.True(page.BottomLabel.IsVisibleAfterScroll());
        return Task.CompletedTask;
    }

    /// <summary>7. A control below the fold still exists, whether or not it is on screen.</summary>
    [Fact(Timeout = TestConstants.LongTestTimeoutMs)]
    [Trait("Method", "Exists")]
    public Task BottomButton_Exists_WithoutScrolling()
    {
        var page = _fixture.NavigateToScroll();

        page.BottomButton.AssertExists(timeoutMs: CrossPageMs);
        return Task.CompletedTask;
    }
}
