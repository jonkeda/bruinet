using System.Diagnostics;
using Bruinet.Core.Exceptions;
using Bruinet.Html.Context;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;
using Bruinet.Html.Controls.Text;
using Bruinet.Html.Playwright;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Bruinet.Html.Tests;

/// <summary>
/// Pins the behaviour the Html call-model work sets out to fix, before it is fixed.
/// </summary>
/// <remarks>
/// <para>
/// Written before the Html call-model work. Each test states what the design requires
/// and fails on the code as it was then, for the reason in its summary. The <c>Pin</c> trait
/// names the step expected to make it pass: <c>dotnet test --filter "Pin=step4"</c>.
/// </para>
/// <para>
/// <c>Pin=guard</c> tests are the R0 guards: they pass now and must keep passing. A later step
/// that makes one fail has turned an app bug into a pass.
/// </para>
/// <para>
/// Assertions are on behaviour (timings, call counts, log entries, messages), not on types later
/// steps introduce, so the file compiles against the code it pins. Html action members take no
/// <c>timeoutMs</c> today, so budget pins use the members that do (<c>GetText</c>, <c>Wait*</c>).
/// </para>
/// </remarks>
public class CallModelPinTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<IHtmlTestContext> _context = new();
    private readonly Mock<ITestLogger> _logger = new();

    public CallModelPinTests(ITestOutputHelper output)
    {
        _output = output;
        _context.Setup(c => c.Timeouts).Returns(new TimeoutSettings
        {
            DefaultWait = TimeSpan.FromMilliseconds(1000),
            PageLoad = TimeSpan.FromMilliseconds(1000),
            ElementFind = TimeSpan.FromMilliseconds(1000),
            PollingInterval = TimeSpan.FromMilliseconds(1),
        });
        _context.Setup(c => c.Logger).Returns(_logger.Object);
        _context.Setup(c => c.Context).Returns(() => _context.Object);
        _context.Setup(c => c.DefaultLocatorStrategy).Returns(LocatorStrategy.Css);
        _context.Setup(c => c.IsIdle()).Returns(true);
    }

    #region Pages and helpers

    private sealed class PinPage(IHtmlTestContext context) : PageObjectBase<PinPage>(context)
    {
        public override bool IsLoaded() => true;

        public ButtonControl<PinPage> Target => new(this, Locator.ByAutomationId("Target"));

        public LabelControl<PinPage> Status => new(this, Locator.ByAutomationId("Target"));

        public TextInputControl<PinPage> Input => new(this, Locator.ByAutomationId("Target"));
    }

    private PinPage NewPage() => new(_context.Object);

    /// <summary>Makes the context answer <c>Target</c> with whatever <paramref name="next"/> gives.</summary>
    private void GivenTarget(Func<IHtmlElement?> next)
    {
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Target"))).Returns(next);
        _context.Setup(c => c.FindElement(It.Is<Locator>(l => l.Value == "Target")))
            .Returns(() => next() ?? throw new ElementNotFoundException(Locator.ByAutomationId("Target")));
    }

    private static Mock<IHtmlElement> Element(bool visible = true, bool enabled = true, string? text = null)
    {
        var element = new Mock<IHtmlElement>();
        element.Setup(e => e.Visible).Returns(visible);
        element.Setup(e => e.Enabled).Returns(enabled);
        element.Setup(e => e.Text).Returns(text);
        return element;
    }

    private static long Timed(Action act)
    {
        var stopwatch = Stopwatch.StartNew();
        _ = Record.Exception(act);
        return stopwatch.ElapsedMilliseconds;
    }

    private void VerifyLogPairs(int expected)
    {
        _logger.Verify(l => l.LogEntry(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>()), Times.Exactly(expected));
        _logger.Verify(l => l.LogExit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<LogResult>(), It.IsAny<int>(), It.IsAny<string?>()), Times.Exactly(expected));
    }

    /// <summary>A Playwright page whose every lookup lands on <paramref name="child"/>.</summary>
    private static Mock<IPage> PageAnswering(Mock<ILocator> child)
    {
        var root = new Mock<ILocator>();
        root.Setup(l => l.Locator(It.IsAny<string>(), It.IsAny<LocatorLocatorOptions?>())).Returns(child.Object);
        var page = new Mock<IPage>();
        page.Setup(p => p.Locator(It.IsAny<string>(), It.IsAny<PageLocatorOptions?>())).Returns(root.Object);
        page.Setup(p => p.Url).Returns("http://localhost/pin");
        page.Setup(p => p.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>()))
            .ReturnsAsync("""{"ready":"complete","body":true,"busy":null}""");
        return page;
    }

    private static Mock<ILocator> PlaywrightElement(int count = 1)
    {
        var locator = new Mock<ILocator>();
        locator.Setup(l => l.CountAsync()).ReturnsAsync(count);
        locator.Setup(l => l.First).Returns(() => locator.Object);
        locator.Setup(l => l.Nth(It.IsAny<int>())).Returns(() => locator.Object);
        locator.Setup(l => l.IsVisibleAsync(It.IsAny<LocatorIsVisibleOptions?>())).ReturnsAsync(true);
        locator.Setup(l => l.IsEnabledAsync(It.IsAny<LocatorIsEnabledOptions?>())).ReturnsAsync(true);
        locator.Setup(l => l.InnerTextAsync(It.IsAny<LocatorInnerTextOptions?>())).ReturnsAsync("text");
        return locator;
    }

    private static PlaywrightTestContext PlaywrightContext(IPage page, int elementFind = 1000)
        => PlaywrightTestContext.ForPage(page, new HtmlTestContextOptions
        {
            Timeouts = new TimeoutSettings { DefaultWait = TimeSpan.FromMilliseconds(1000), ElementFind = TimeSpan.FromMilliseconds(elementFind), PollingInterval = TimeSpan.FromMilliseconds(10) },
        });

    #endregion

    #region (a)-(e): one budget, found again on every attempt, one log unit, act once

    /// <summary>
    /// (a) An element being replaced must not use up the call: the next attempt finds the new one.
    /// Fails today: the first element's nested visibility wait takes the whole budget.
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void A_Click_FindsTheElementAgain_WhenTheFirstOneNeverBecomesVisible()
    {
        var dead = Element(visible: false);
        var good = Element();
        var lookups = 0;
        GivenTarget(() => ++lookups == 1 ? dead.Object : good.Object);

        NewPage().Target.Click();

        good.Verify(e => e.Click(), Times.Once);
        dead.Verify(e => e.Click(), Times.Never);
    }

    /// <summary>
    /// (b) The caller's timeout is the call's budget. Fails today: the nested visibility wait uses
    /// DefaultWait (1000 ms here) whatever the caller asked for.
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void B_GetText_HonoursTheCallersTimeout_WhenTheElementNeverBecomesVisible()
    {
        var hidden = Element(visible: false, text: "hidden");
        GivenTarget(() => hidden.Object);

        var elapsed = Timed(() => NewPage().Status.GetText(timeoutMs: 300));

        Assert.True(elapsed < 800, $"GetText(timeoutMs: 300) took {elapsed} ms.");
    }

    /// <summary>
    /// (c) One public call is one log entry/exit pair. Fails today: the nested visibility wait
    /// logs a pair of its own.
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void C_Click_WritesOneLogEntryAndExitPair()
    {
        var reads = 0;
        var element = Element();
        element.Setup(e => e.Visible).Returns(() => ++reads > 1);
        GivenTarget(() => element.Object);

        NewPage().Target.Click();

        VerifyLogPairs(1);
    }

    /// <summary>(d) As (a), for <c>Get*</c>.</summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void D_GetText_FindsTheElementAgain_WhenTheFirstOneNeverBecomesVisible()
    {
        var dead = Element(visible: false, text: "dead");
        var good = Element(text: "good");
        var lookups = 0;
        GivenTarget(() => ++lookups == 1 ? dead.Object : good.Object);

        Assert.Equal("good", NewPage().Status.GetText(timeoutMs: 1000));
    }

    /// <summary>(d) As (a), for <c>Wait*</c>.</summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void D_WaitText_FindsTheElementAgain_WhenTheFirstOneNeverBecomesVisible()
    {
        var dead = Element(visible: false, text: "dead");
        var good = Element(text: "good");
        var lookups = 0;
        GivenTarget(() => ++lookups == 1 ? dead.Object : good.Object);

        Assert.True(NewPage().Status.WaitText("good", timeoutMs: 1000));
    }

    /// <summary>(d) As (a), for <c>Assert*</c>.</summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void D_AssertText_FindsTheElementAgain_WhenTheFirstOneNeverBecomesVisible()
    {
        var dead = Element(visible: false, text: "dead");
        var good = Element(text: "good");
        var lookups = 0;
        GivenTarget(() => ++lookups == 1 ? dead.Object : good.Object);

        NewPage().Status.AssertText("good", timeoutMs: 1000);
    }

    /// <summary>
    /// (e) An action that throws runs once, and its failure is in the call's log exit. Fails
    /// today: <c>RunDoWithElement</c> retries the click until the budget runs out (X2).
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void E_Click_WhenTheActionThrows_ClicksOnceAndLogsTheError()
    {
        var element = Element();
        element.Setup(e => e.Click()).Throws(new InvalidOperationException("boom"));
        GivenTarget(() => element.Object);

        Assert.Throws<InvalidOperationException>(() => NewPage().Target.Click());

        element.Verify(e => e.Click(), Times.Once);
        _logger.Verify(l => l.LogExit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), LogResult.Error, It.IsAny<int>(), It.Is<string?>(m => m != null && m.Contains("boom"))),
            Times.Once);
    }

    #endregion

    #region (f)-(i): nothing below the call outlasts it; non-Try members wait

    /// <summary>
    /// (f1) A missing control honours the caller's budget. Fails today:
    /// <c>PlaywrightTestContext.FindElement</c> waits ElementFind (3000 ms) inside every attempt.
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void F1_MissingControl_HonoursTheCallersTimeout_OnThePlaywrightContext()
    {
        var page = PageAnswering(PlaywrightElement(count: 0));
        using var context = PlaywrightContext(page.Object, elementFind: 3000);

        var elapsed = Timed(() => new PinPage(context).Status.GetText(timeoutMs: 300));

        Assert.True(elapsed < 1500, $"GetText(timeoutMs: 300) on a missing control took {elapsed} ms.");
    }

    /// <summary>
    /// (f2) A Playwright call below a control never waits on its own clock (?A5). Fails today:
    /// reads and actions go to Playwright without a timeout, so each may wait Playwright's default
    /// 30 s on an element that has gone.
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void F2_PlaywrightCalls_PassATimeout()
    {
        var locator = PlaywrightElement();
        var element = new PlaywrightHtmlElement(locator.Object);

        _ = element.GetAttribute("value");
        element.Click();

        locator.Verify(l => l.GetAttributeAsync("value",
            It.Is<LocatorGetAttributeOptions?>(o => o != null && o.Timeout != null)), Times.Once);
        locator.Verify(l => l.ClickAsync(
            It.Is<LocatorClickOptions?>(o => o != null && o.Timeout != null)), Times.Once);
    }

    /// <summary>
    /// (g) A lookup that fails with an error is not "absent". Fails today:
    /// <c>PlaywrightTestContext.TryFindElement</c> turns every exception into null, so a closed
    /// browser reads as "the control is not there".
    /// </summary>
    [Fact]
    [Trait("Pin", "step3")]
    public void G_IsExists_DoesNotAnswerFalse_WhenTheLookupThrows()
    {
        var broken = PlaywrightElement();
        broken.Setup(l => l.CountAsync()).ThrowsAsync(new PlaywrightException("Target page, context or browser has been closed"));
        using var context = PlaywrightContext(PageAnswering(broken).Object);

        var answered = false;
        var error = Record.Exception(() => answered = new PinPage(context).Status.IsExists());

        Assert.False(error == null && !answered, "IsExists() answered false although the lookup threw.");
    }

    /// <summary>
    /// (h) <c>WaitExists(false)</c> passes when the control is absent. Fails today: the wait needs
    /// the element found first, so on an absent element it throws "not found" instead of passing.
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void H_WaitExistsFalse_Passes_WhenTheControlIsAbsent()
    {
        GivenTarget(() => null);

        Assert.True(NewPage().Status.WaitExists(false, timeoutMs: 300));
    }

    /// <summary>
    /// (i) A non-Try member waits (R7). Fails today: <c>LabelControl.AssertTextContains</c> reads
    /// once and fails at once, which is why the counter tests in the baseline lose "one click".
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void I_AssertTextContains_WaitsForTheText()
    {
        var reads = 0;
        var element = Element();
        element.Setup(e => e.Text).Returns(() => ++reads < 3 ? "count: 0" : "count: 1");
        GivenTarget(() => element.Object);

        NewPage().Status.AssertTextContains("1");
    }

    #endregion

    #region Confirm and near-misses

    /// <summary>
    /// A value that does not take is not a pass: the call fails, and the fill is not repeated.
    /// Fails today: <c>SetText</c> fills once and reports success whatever the field shows.
    /// </summary>
    [Fact]
    [Trait("Pin", "step6")]
    public void SetText_WhenTheValueDoesNotTake_FailsAndFillsOnce()
    {
        var input = Element();
        input.Setup(e => e.InputValue).Returns(string.Empty);
        GivenTarget(() => input.Object);

        var error = Record.Exception(() => NewPage().Input.SetText("abc"));

        Assert.NotNull(error);
        input.Verify(e => e.Fill("abc"), Times.Once);
    }

    /// <summary>
    /// A call that got there only after its element was replaced passes, and says so. Fails today:
    /// the retries are silent, and the log says Success.
    /// </summary>
    [Fact]
    [Trait("Pin", "step4")]
    public void NearMiss_ReplacedElements_PassAndLogAWarning()
    {
        var lookups = 0;
        GivenTarget(() =>
        {
            var element = Element(text: "good");
            if (++lookups <= 3)
            {
                element.Setup(e => e.Text).Throws(new Bruinet.Core.Exceptions.StaleElementException());
            }

            return element.Object;
        });

        Assert.Equal("good", NewPage().Status.GetText(timeoutMs: 1000));

        _logger.Verify(l => l.LogExit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), LogResult.Warning, It.IsAny<int>(),
            It.Is<string?>(m => m != null && m.StartsWith("near-miss:"))), Times.Once);
    }

    #endregion

    #region R0 guards: pass now, keep passing

    /// <summary>A button that never enables fails the call within its budget, and is never clicked.</summary>
    [Fact]
    [Trait("Pin", "guard")]
    public void Guard_Click_OnAButtonThatNeverEnables_FailsWithinItsBudget_AndNeverClicks()
    {
        var disabled = Element(enabled: false);
        GivenTarget(() => disabled.Object);

        Exception? error = null;
        var elapsed = Timed(() => error = Record.Exception(() => NewPage().Target.Click()));

        Assert.NotNull(error);
        Assert.True(
            error!.Message.Contains("enabled", StringComparison.OrdinalIgnoreCase)
            || error.Message.Contains("disabled", StringComparison.OrdinalIgnoreCase),
            $"The failure does not say the button was disabled: {error.Message}");
        Assert.True(elapsed < 2500, $"Click on a disabled button took {elapsed} ms on a 1000 ms budget.");
        disabled.Verify(e => e.Click(), Times.Never);
    }

    /// <summary>
    /// The browser closing mid-call ends the call at once with <c>AppUnavailableException</c>; it
    /// does not wait out the budget (written in step 3, which introduces the type).
    /// </summary>
    [Fact]
    [Trait("Pin", "guard")]
    public void Guard_BrowserClosedMidCall_FailsAtOnce_AsAppUnavailable()
    {
        var element = PlaywrightElement();
        element.Setup(l => l.InnerTextAsync(It.IsAny<LocatorInnerTextOptions?>()))
            .ThrowsAsync(new PlaywrightException("Target page, context or browser has been closed"));
        using var context = PlaywrightContext(PageAnswering(element).Object);

        Exception? error = null;
        var elapsed = Timed(() => error = Record.Exception(() => new PinPage(context).Status.GetText(timeoutMs: 2000)));

        Assert.IsType<Bruinet.Core.Exceptions.AppUnavailableException>(error);
        Assert.True(elapsed < 500, $"The call took {elapsed} ms after the browser closed; it must end at once.");
    }

    /// <summary>
    /// A disabled button still fails as "disabled" when the budget runs out in the middle of the
    /// last attempt's read (a Playwright call given the last millisecond times out). Found in the
    /// step 4 UI run: the cut-off read was reported instead of what every earlier attempt saw.
    /// </summary>
    [Fact]
    [Trait("Pin", "guard")]
    public void Guard_Click_OnAButtonThatNeverEnables_SaysDisabled_WhenTheLastReadIsCutOff()
    {
        var disabled = Element();
        disabled.Setup(e => e.Enabled).Returns(() => Bruinet.Core.Calls.CallBudget.RemainingOr(1000) <= 1
            ? throw new TimeoutException("Timeout 1ms exceeded.")
            : false);
        GivenTarget(() => disabled.Object);

        var error = Record.Exception(() => NewPage().Target.Click(timeoutMs: 300));

        var notReady = Assert.IsType<Bruinet.Core.Exceptions.ElementNotReadyException>(error);
        Assert.Equal(Bruinet.Core.Exceptions.NotReadyReason.Disabled, notReady.Reason);
        disabled.Verify(e => e.Click(), Times.Never);
    }

    /// <summary>An element that never becomes visible is never clicked.</summary>
    [Fact]
    [Trait("Pin", "guard")]
    public void Guard_Click_OnAnElementThatNeverShows_FailsAndNeverClicks()
    {
        var hidden = Element(visible: false);
        GivenTarget(() => hidden.Object);

        Assert.NotNull(Record.Exception(() => NewPage().Target.Click()));

        hidden.Verify(e => e.Click(), Times.Never);
    }

    #endregion

    #region Measurement

    /// <summary>
    /// Not a pin: the plan's baseline number "Playwright calls per completed Click", measured on a
    /// present, visible, enabled button through the real Playwright context. Printed, not asserted.
    /// </summary>
    [Fact]
    [Trait("Pin", "measure")]
    public void Measure_PlaywrightCallsPerCompletedClick()
    {
        var button = PlaywrightElement();
        using var context = PlaywrightContext(PageAnswering(button).Object);

        new PinPage(context).Target.Click();

        var calls = button.Invocations
            .Where(i => i.Method.Name.EndsWith("Async", StringComparison.Ordinal))
            .GroupBy(i => i.Method.Name)
            .ToDictionary(g => g.Key, g => g.Count());
        _output.WriteLine($"Playwright calls for one Click: {calls.Values.Sum()} ({string.Join(", ", calls.Select(c => $"{c.Key} {c.Value}"))})");
        Assert.Equal(1, calls.GetValueOrDefault(nameof(ILocator.ClickAsync)));
    }

    #endregion
}
