using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Display;

/// <summary>
/// UI that changes by time, waited for as a user waits: a status after an awaited delay, one written by
/// delayed work, a debounced search, and a message that hides itself. No test moves a clock.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "Timed")]
public class TimedUiTests
{
    private readonly TimedTestPage _page;

    public TimedUiTests(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Timed);
        _page = new TimedTestPage(fixture.Context);
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task AwaitedDelay_TheStatusFollows()
    {
        _page.SaveButton.Click();

        _page.SaveStatus.AssertText("saved");

        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task DelayedWork_TheStatusFollows()
    {
        _page.SaveLaterButton.Click();

        _page.SaveLaterStatus.AssertText("saved");

        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task DebouncedSearch_RunsOnceTypingPauses()
    {
        _page.Search.SetText("bolt");

        _page.SearchStatus.AssertText("searched 1: bolt");

        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task Message_ShowsThenHidesItself()
    {
        _page.ShowMessageButton.Click();

        _page.Message.AssertVisible();
        // A hidden view leaves the tree, on every head.
        _page.Message.AssertExists(false);

        return Task.CompletedTask;
    }
}
