namespace Bruinet.NativeAndroid.Tests;

/// <summary>
/// The call model on native Android (plan step 3): one budget, found again on every attempt, one
/// log pair, act once and confirm. The same pins as Html's <c>CallModelPinTests</c>, over a mocked
/// app; the <c>Pin=guard</c> tests are the R0 guards.
/// </summary>
public class CallModelTests
{
    private readonly FakeApp _app = new();

    #region (a)-(e): one budget, found again on every attempt, one log unit, act once

    /// <summary>(a) A view being replaced does not use up the call: the next attempt finds the new one.</summary>
    [Fact]
    public void A_Click_FindsTheViewAgain_WhenTheFirstOneNeverShows()
    {
        var dead = FakeApp.View(visible: false);
        var good = FakeApp.View();
        var lookups = 0;
        _app.Given("Target", () => ++lookups == 1 ? dead.Object : good.Object);

        _app.Page().Target.Click();

        good.Verify(e => e.Click(), Times.Once);
        dead.Verify(e => e.Click(), Times.Never);
    }

    /// <summary>(b) The caller's timeout is the call's budget.</summary>
    [Fact]
    public void B_GetText_HonoursTheCallersTimeout_WhenTheViewNeverShows()
    {
        var hidden = FakeApp.View(visible: false, text: "hidden");
        _app.Given("Target", () => hidden.Object);

        var elapsed = FakeApp.Timed(() => _app.Page().Status.GetText(timeoutMs: 300));

        Assert.True(elapsed < 800, $"GetText(timeoutMs: 300) took {elapsed} ms.");
    }

    /// <summary>(c) One public call is one log entry/exit pair.</summary>
    [Fact]
    public void C_Click_WritesOneLogEntryAndExitPair()
    {
        var reads = 0;
        var element = FakeApp.View();
        element.Setup(e => e.Visible).Returns(() => ++reads > 1);
        _app.Given("Target", () => element.Object);

        _app.Page().Target.Click();

        _app.VerifyLogPairs(1);
    }

    /// <summary>(d) As (a), for Get, Wait and Assert.</summary>
    [Fact]
    public void D_GetWaitAndAssert_FindTheViewAgain_WhenTheFirstOneNeverShows()
    {
        var good = FakeApp.View(text: "good");
        var lookups = 0;
        _app.Given("Target", () => lookups++ % 2 == 0 ? FakeApp.View(visible: false, text: "dead").Object : good.Object);

        Assert.Equal("good", _app.Page().Status.GetText(timeoutMs: 1000));
        Assert.True(_app.Page().Status.WaitText("good", timeoutMs: 1000));
        _app.Page().Status.AssertText("good", timeoutMs: 1000);
    }

    /// <summary>(e) An action that throws runs once, and its failure is in the call's log exit.</summary>
    [Fact]
    public void E_Click_WhenTheActionThrows_TapsOnceAndLogsTheError()
    {
        var element = FakeApp.View();
        element.Setup(e => e.Click()).Throws(new InvalidOperationException("boom"));
        _app.Given("Target", () => element.Object);

        Assert.Throws<InvalidOperationException>(() => _app.Page().Target.Click());

        element.Verify(e => e.Click(), Times.Once);
        _app.Logger.Verify(l => l.Write(It.Is<LogEvent>(e => e.Kind == LogEventKind.Exit && e.Result == LogResult.Error && (e.Message != null && e.Message.Contains("boom")))),
            Times.Once);
    }

    #endregion

    #region Waits and absence

    /// <summary><c>WaitExists(false)</c> passes when the control is absent.</summary>
    [Fact]
    public void WaitExistsFalse_Passes_WhenTheControlIsAbsent()
    {
        _app.Given("Target", () => null);

        Assert.True(_app.Page().Status.WaitExists(false, timeoutMs: 300));
    }

    /// <summary>A non-Try member waits for its state (R7).</summary>
    [Fact]
    public void AssertTextContains_WaitsForTheText()
    {
        var reads = 0;
        var element = FakeApp.View();
        element.Setup(e => e.Text).Returns(() => ++reads < 3 ? "count: 0" : "count: 1");
        _app.Given("Target", () => element.Object);

        _app.Page().Status.AssertTextContains("1");
    }

    /// <summary>A call that got there only after its view was replaced passes, and says so.</summary>
    [Fact]
    public void NearMiss_ReplacedViews_PassAndLogAWarning()
    {
        var lookups = 0;
        _app.Given("Target", () =>
        {
            var element = FakeApp.View(text: "good");
            if (++lookups <= 3)
            {
                element.Setup(e => e.Text).Throws(new StaleElementException());
            }

            return element.Object;
        });

        Assert.Equal("good", _app.Page().Status.GetText(timeoutMs: 1000));

        _app.Logger.Verify(l => l.Write(It.Is<LogEvent>(e => e.Kind == LogEventKind.Exit && e.Result == LogResult.Warning && (e.Message != null && e.Message.StartsWith("near-miss:")))), Times.Once);
    }

    #endregion

    #region Scroll to find

    /// <summary>
    /// A control below the fold is not in UiAutomator2's tree: the call scrolls it in and acts on
    /// it, once.
    /// </summary>
    [Fact]
    public void Click_ScrollsAControlBelowTheFoldIntoTheTree()
    {
        var below = FakeApp.View();
        var scrolled = false;
        _app.Given("Target", () => scrolled ? below.Object : null);
        _app.Driver.Setup(d => d.TryScrollIntoView(It.Is<Locator>(l => l.Value == "Target"))).Returns(() => scrolled = true);

        _app.Page().Target.Click();

        below.Verify(e => e.Click(), Times.Once);
        _app.Driver.Verify(d => d.TryScrollIntoView(It.IsAny<Locator>()), Times.Once);
    }

    /// <summary>A control that is not there is not scrolled for on every poll: the scroll is throttled.</summary>
    [Fact]
    public void AMissingControl_IsScrolledForAFewTimes_NotOnEveryPoll()
    {
        _app.Given("Target", () => null);

        Assert.NotNull(Record.Exception(() => _app.Page().Target.Click(timeoutMs: 300)));

        _app.Driver.Verify(d => d.TryScrollIntoView(It.IsAny<Locator>()), Times.AtMost(5));
    }

    #endregion

    #region Confirm

    /// <summary>Text that does not take is not a pass: the call fails, and the text is typed once.</summary>
    [Fact]
    public void SetText_WhenTheTextDoesNotTake_FailsAndTypesOnce()
    {
        var input = FakeApp.View(text: string.Empty);
        _app.Given("Target", () => input.Object);

        var error = Record.Exception(() => _app.Page().Input.SetText("abc", timeoutMs: 300));

        Assert.NotNull(error);
        Assert.Contains("ran once", error!.Message);
        input.Verify(e => e.SendKeys("abc", It.IsAny<TextInputMethod>()), Times.Once);
    }

    /// <summary>Text that takes is confirmed by reading it back.</summary>
    [Fact]
    public void SetText_ConfirmsTheText()
    {
        var text = string.Empty;
        var input = FakeApp.View();
        input.Setup(e => e.Text).Returns(() => text);
        input.Setup(e => e.SendKeys(It.IsAny<string>(), It.IsAny<TextInputMethod>())).Callback<string, TextInputMethod>((typed, _) => text = typed);
        _app.Given("Target", () => input.Object);

        _app.Page().Input.SetText("abc");

        input.Verify(e => e.Clear(), Times.Once);
        input.Verify(e => e.SendKeys("abc", It.IsAny<TextInputMethod>()), Times.Once);
    }

    /// <summary>A password field shows dots: its text is confirmed by length, and the message never shows it.</summary>
    [Fact]
    public void SetText_OnAPasswordField_ConfirmsByLength()
    {
        var shown = string.Empty;
        var password = FakeApp.View();
        password.Setup(e => e.GetAttribute("password")).Returns("true");
        password.Setup(e => e.Text).Returns(() => shown);
        password.Setup(e => e.SendKeys(It.IsAny<string>(), It.IsAny<TextInputMethod>())).Callback<string, TextInputMethod>((typed, _) => shown = new string('•', typed.Length));
        _app.Given("Target", () => password.Object);

        _app.Page().Input.SetText("s3cret");
    }

    /// <summary>A check box that ignores the tap fails the call, tapped once.</summary>
    [Fact]
    public void Check_WhenTheTapHasNoEffect_FailsAndTapsOnce()
    {
        var box = FakeApp.View();
        box.Setup(e => e.Checked).Returns(false);
        _app.Given("Target", () => box.Object);

        Assert.NotNull(Record.Exception(() => _app.Page().Agree.Check(timeoutMs: 300)));

        box.Verify(e => e.Click(), Times.Once);
    }

    /// <summary>A check box that is already checked is not tapped.</summary>
    [Fact]
    public void Check_WhenAlreadyChecked_DoesNotTap()
    {
        var box = FakeApp.View();
        box.Setup(e => e.Checked).Returns(true);
        _app.Given("Target", () => box.Object);

        _app.Page().Agree.Check();

        box.Verify(e => e.Click(), Times.Never);
    }

    /// <summary>A radio group selects by tapping the option once, and confirms it is the checked one.</summary>
    [Fact]
    public void RadioGroup_SelectByText_TapsTheOptionOnce_AndConfirmsIt()
    {
        var small = FakeApp.View(text: "Small");
        var large = FakeApp.View(text: "Large");
        var chosen = small;
        small.Setup(e => e.Checked).Returns(() => chosen == small);
        large.Setup(e => e.Checked).Returns(() => chosen == large);
        large.Setup(e => e.Click()).Callback(() => chosen = large);
        var group = FakeApp.View();
        group.Setup(e => e.FindElements(It.IsAny<Locator>())).Returns([small.Object, large.Object]);
        _app.Given("Target", () => group.Object);

        _app.Page().Size.SelectByText("Large");

        large.Verify(e => e.Click(), Times.Once);
        Assert.Equal("Large", _app.Page().Size.GetSelectedText());
    }

    #endregion

    #region R0 guards

    /// <summary>A button that never enables fails the call within its budget, and is never tapped.</summary>
    [Fact]
    [Trait("Pin", "guard")]
    public void Guard_Click_OnAButtonThatNeverEnables_FailsWithinItsBudget_AndNeverTaps()
    {
        var disabled = FakeApp.View(enabled: false);
        _app.Given("Target", () => disabled.Object);

        Exception? error = null;
        var elapsed = FakeApp.Timed(() => error = Record.Exception(() => _app.Page().Target.Click()));

        var notReady = Assert.IsType<ElementNotReadyException>(error);
        Assert.Equal(NotReadyReason.Disabled, notReady.Reason);
        Assert.True(elapsed < 2500, $"Click on a disabled button took {elapsed} ms on a 1000 ms budget.");
        disabled.Verify(e => e.Click(), Times.Never);
    }

    /// <summary>A view that never shows is never tapped.</summary>
    [Fact]
    [Trait("Pin", "guard")]
    public void Guard_Click_OnAViewThatNeverShows_FailsAndNeverTaps()
    {
        var hidden = FakeApp.View(visible: false);
        _app.Given("Target", () => hidden.Object);

        Assert.NotNull(Record.Exception(() => _app.Page().Target.Click()));

        hidden.Verify(e => e.Click(), Times.Never);
    }

    /// <summary>The session ending mid-call ends the call at once; it does not wait out the budget.</summary>
    [Fact]
    [Trait("Pin", "guard")]
    public void Guard_SessionEndedMidCall_FailsAtOnce_AsAppUnavailable()
    {
        var element = FakeApp.View();
        element.Setup(e => e.Text).Throws(new AppUnavailableException("the Appium session has ended."));
        _app.Given("Target", () => element.Object);

        Exception? error = null;
        var elapsed = FakeApp.Timed(() => error = Record.Exception(() => _app.Page().Status.GetText(timeoutMs: 2000)));

        Assert.IsType<AppUnavailableException>(error);
        Assert.True(elapsed < 500, $"The call took {elapsed} ms after the session ended; it must end at once.");
    }

    /// <summary>An action Android has no route for ends the call at once, naming it.</summary>
    [Fact]
    [Trait("Pin", "guard")]
    public void Guard_Hover_HasNoRoute_AndEndsTheCallAtOnce()
    {
        _app.Given("Target", () => FakeApp.View().Object);

        Exception? error = null;
        var elapsed = FakeApp.Timed(() => error = Record.Exception(() => _app.Page().Target.Hover(timeoutMs: 2000)));

        Assert.IsType<RouteUnavailableException>(error);
        Assert.True(elapsed < 500, $"Hover took {elapsed} ms; a missing route must end the call at once.");
    }

    #endregion
}
