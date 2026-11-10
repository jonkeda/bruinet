using Bruinet.Core.Exceptions;
using Bruinet.Html.Context;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Playwright;
using Microsoft.Playwright;

namespace Bruinet.Html.Tests;

/// <summary>
/// On Playwright, an action's wait for visible, stable and receiving events is Playwright's own:
/// Bruinet does not check visibility first, a timeout before the action ran is
/// <see cref="ElementNotReadyException"/> with Playwright's reason, and a timeout after it ran (the
/// navigation it started) is the action done, never repeated.
/// </summary>
public class ActionabilityTests
{
    private const string Hidden = """
        Timeout 300ms exceeded.
        Call log:
          - waiting for Locator("[id='Target']").First
            - locator resolved to <button id="Target">Go</button>
          - attempting click action
            - waiting for element to be visible, enabled and stable
            - element is not visible
          - retrying click action
            - waiting for element to be visible, enabled and stable
            - element is not visible
        """;

    private const string Covered = """
        Timeout 300ms exceeded.
        Call log:
          - attempting click action
            - waiting for element to be visible, enabled and stable
            - element is visible, enabled and stable
            - scrolling into view if needed
            - <div class="modal-backdrop"></div> intercepts pointer events
          - retrying click action
            - <div class="modal-backdrop"></div> intercepts pointer events
        """;

    private const string Navigating = """
        Timeout 4964ms exceeded.
        Call log:
          - attempting click action
            - waiting for element to be visible, enabled and stable
            - element is visible, enabled and stable
            - scrolling into view if needed
            - done scrolling
            - performing click action
            - click action done
            - waiting for scheduled navigations to finish
        """;

    [Fact]
    public void NotReadyOf_ReadsPlaywrightsReason()
    {
        Assert.Equal((NotReadyReason.NotVisible, "element is not visible"), PlaywrightErrors.NotReadyOf(Hidden));
        Assert.Equal((NotReadyReason.Other, "<div class=\"modal-backdrop\"></div> intercepts pointer events"),
            PlaywrightErrors.NotReadyOf(Covered));
    }

    [Fact]
    public void ActionRan_OnlyWhenTheCallLogSaysTheActionWasDone()
    {
        Assert.True(PlaywrightErrors.ActionRan(Navigating));
        Assert.False(PlaywrightErrors.ActionRan(Hidden));
        Assert.False(PlaywrightErrors.ActionRan(Covered));
    }

    private sealed class Page(IHtmlTestContext context) : PageObjectBase<Page>(context)
    {
        public override bool IsLoaded() => true;

        public ButtonControl<Page> Target => new(this, Locator.ByAutomationId("Target"));
    }

    private static (PlaywrightTestContext Context, Mock<ILocator> Button) Browser(Func<Task> click)
    {
        var button = new Mock<ILocator>();
        button.Setup(l => l.CountAsync()).ReturnsAsync(1);
        button.Setup(l => l.First).Returns(() => button.Object);
        button.Setup(l => l.Nth(It.IsAny<int>())).Returns(() => button.Object);
        button.Setup(l => l.IsVisibleAsync(It.IsAny<LocatorIsVisibleOptions?>())).ReturnsAsync(true);
        button.Setup(l => l.IsEnabledAsync(It.IsAny<LocatorIsEnabledOptions?>())).ReturnsAsync(true);
        button.Setup(l => l.ClickAsync(It.IsAny<LocatorClickOptions?>())).Returns(click);
        var root = new Mock<ILocator>();
        root.Setup(l => l.Locator(It.IsAny<string>(), It.IsAny<LocatorLocatorOptions?>())).Returns(button.Object);
        var page = new Mock<IPage>();
        page.Setup(p => p.Locator(It.IsAny<string>(), It.IsAny<PageLocatorOptions?>())).Returns(root.Object);
        page.Setup(p => p.Url).Returns("http://localhost/actionability");
        page.Setup(p => p.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>()))
            .ReturnsAsync("""{"ready":"complete","body":true,"busy":null}""");
        var context = PlaywrightTestContext.ForPage(page.Object, new HtmlTestContextOptions
        {
            Timeouts = new TimeoutSettings { DefaultWait = 300, ElementFind = 300, PollingInterval = 10 },
        });
        return (context, button);
    }

    [Fact]
    public void Click_LeavesVisibilityToPlaywright()
    {
        var (context, button) = Browser(() => Task.CompletedTask);
        using var _ = context;

        new Page(context).Target.Click();

        button.Verify(l => l.IsVisibleAsync(It.IsAny<LocatorIsVisibleOptions?>()), Times.Never);
        button.Verify(l => l.ClickAsync(It.IsAny<LocatorClickOptions?>()), Times.Once);
    }

    [Fact]
    public void Click_OnACoveredButton_FailsAsNotReady_NamingWhatCoversIt()
    {
        var (context, _) = Browser(() => throw new TimeoutException(Covered));
        using var __ = context;

        var error = Assert.IsType<ElementNotReadyException>(Record.Exception(() => new Page(context).Target.Click(timeoutMs: 300)));

        Assert.Equal(Locator.ByAutomationId("Target"), error.Locator);
        Assert.Contains("intercepts pointer events", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Click_ThatRanAndThenWaitedForANavigation_IsDone_AndNotRepeated()
    {
        var (context, button) = Browser(() => throw new TimeoutException(Navigating));
        using var _ = context;

        new Page(context).Target.Click();

        button.Verify(l => l.ClickAsync(It.IsAny<LocatorClickOptions?>()), Times.Once);
    }
}
