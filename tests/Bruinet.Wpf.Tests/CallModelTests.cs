namespace Bruinet.Wpf.Tests;

/// <summary>
/// The call model on WPF (upgrade plan U3): one budget, found again on every attempt, one log pair,
/// act once and confirm, over a mocked app. The same pins as native Android's <c>CallModelTests</c>.
/// </summary>
public class CallModelTests
{
    private readonly FakeApp _app = new();

    /// <summary>An element that never shows does not use up the call: the next attempt finds the new one.</summary>
    [Fact]
    public void Click_FindsTheElementAgain_WhenTheFirstOneNeverShows()
    {
        var hidden = FakeApp.Element(visible: false);
        var shown = FakeApp.Element();
        var lookups = 0;
        _app.Given("Target", () => ++lookups == 1 ? hidden.Object : shown.Object);

        _app.Page().Target.Click();

        shown.Verify(e => e.Click(), Times.Once);
        hidden.Verify(e => e.Click(), Times.Never);
    }

    /// <summary>The caller's timeout is the call's budget.</summary>
    [Fact]
    public void GetText_HonoursTheCallersTimeout_WhenTheElementNeverShows()
    {
        var hidden = FakeApp.Element(visible: false, text: "hidden");
        _app.Given("Target", () => hidden.Object);

        var elapsed = FakeApp.Timed(() => _app.Page().Status.GetText(timeoutMs: 300));

        Assert.True(elapsed < 800, $"GetText(timeoutMs: 300) took {elapsed} ms.");
    }

    /// <summary>One public call is one log entry/exit pair.</summary>
    [Fact]
    public void Click_WritesOneLogEntryAndExitPair()
    {
        _app.Given("Target", () => FakeApp.Element().Object);

        _app.Page().Target.Click();

        _app.VerifyLogPairs(1);
    }

    /// <summary>A disabled button is not clicked; the call asks again until it is enabled (R0).</summary>
    [Fact]
    public void Click_WaitsForADisabledButton_AndClicksItOnce()
    {
        var reads = 0;
        var button = FakeApp.Element();
        button.Setup(e => e.Enabled).Returns(() => ++reads > 2);
        _app.Given("Target", () => button.Object);

        _app.Page().Target.Click();

        button.Verify(e => e.Click(), Times.Once);
    }

    /// <summary>An app that has exited ends the call at once: it is gone, not "not ready yet".</summary>
    [Fact]
    public void A_Call_OnAnAppThatExited_ThrowsAppUnavailable_WithoutWaitingOutItsBudget()
    {
        _app.Given("Target", () => FakeApp.Element().Object);
        _app.Running = false;

        var elapsed = FakeApp.Timed(() => Assert.ThrowsAny<AppUnavailableException>(() => _app.Page().Target.Click(timeoutMs: 5000)));

        Assert.True(elapsed < 2000, $"The call took {elapsed} ms on an app that had exited.");
    }

    /// <summary>A page that is not shown is not ready, and a control on it is not found until it is.</summary>
    [Fact]
    public void A_Control_OnAPageThatIsNotShown_FailsAfterItsBudget_WithoutActing()
    {
        var button = FakeApp.Element();
        _app.Given("Target", () => button.Object);
        _app.Given(nameof(TestPage), () => null);

        Assert.ThrowsAny<Exception>(() => _app.Page().Target.Click(timeoutMs: 200));

        button.Verify(e => e.Click(), Times.Never);
    }
}
