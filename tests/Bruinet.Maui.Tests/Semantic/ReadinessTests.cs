using Bruinet.Maui.Controls.Base;

namespace Bruinet.Maui.Tests.Semantic;

/// <summary>
/// Covers the readiness ladder an action climbs before it acts: scope ready, element present,
/// visible, enabled.
/// </summary>
/// <remarks>
/// These exist because the ordering is easy to break and expensive to notice — a missing rung
/// shows up as a timeout against an element rather than a statement about what was not ready,
/// and only on the slower platform. Mocking the element makes each rung testable in
/// milliseconds, so the guarantee is pinned here rather than inferred from a device run.
/// </remarks>
public class ReadinessTests
{
    private readonly Mock<IMauiTestContext> _context = new();
    private readonly Mock<IMauiElement> _pageRoot = new();

    public ReadinessTests()
    {
        _context.Setup(c => c.Timeouts).Returns(new TimeoutSettings
        {
            DefaultWait = TimeSpan.FromMilliseconds(1000),
            PageLoad = TimeSpan.FromMilliseconds(1000),
            ElementFind = TimeSpan.FromMilliseconds(1000),
            PollingInterval = TimeSpan.FromMilliseconds(1)
        });
        _context.Setup(c => c.DefaultLocatorStrategy).Returns(LocatorStrategy.AutomationId);
        _pageRoot.Setup(e => e.Visible).Returns(true);
        _pageRoot.Setup(e => e.TagName).Returns("Page");
        _pageRoot.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 400, 800));
        _context.Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "GatedPage")))
            .Returns([_pageRoot.Object]);
    }

    /// <summary>
    /// A page whose loaded state is driven by the test.
    /// </summary>
    private sealed class GatedPage : PageObjectBase<GatedPage>
    {
        private readonly Func<bool> _isLoaded;

        public GatedPage(IMauiTestContext context, Func<bool> isLoaded)
            : base(context)
        {
            _isLoaded = isLoaded;
        }

        public override string Name => "GatedPage";

        public override bool IsLoaded() => _isLoaded();

        public Button<GatedPage> Target => new(this, "Target");
    }

    /// <summary>
    /// A page whose root is not in the tree at all — the app is somewhere else entirely.
    /// </summary>
    private sealed class AbsentPage(IMauiTestContext context) : PageObjectBase<AbsentPage>(context)
    {
        public override string Name => "AbsentPage";

        public Button<AbsentPage> Target => new(this, "Target");
    }

    private sealed class BusyGatedPage(IMauiTestContext context) : PageObjectBase<BusyGatedPage>(context)
    {
        public override string Name => "GatedPage";

        protected override BusySignalPolicy BusySignalPolicy => BusySignalPolicy.Required;

        public Button<BusyGatedPage> Target => new(this, "Target");
        public Label<BusyGatedPage> Status => new(this, "Target");
        public CompoundProbe Compound => new(this, "Target");
    }

    private sealed class CompoundProbe(IMauiScope<BusyGatedPage> scope, string locatorValue)
        : ViewBase<BusyGatedPage>(scope, locatorValue)
    {
        public string? ReadText()
            => Run<string?, string?>(
                "CompoundRead",
                null,
                () => RunGetWithElement(element => element.Text));
    }

    private Mock<IMauiElement> GivenElement()
    {
        var element = new Mock<IMauiElement>();
        element.Setup(e => e.Visible).Returns(true);
        element.Setup(e => e.Enabled).Returns(true);
        element.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 40, 20));

        _pageRoot.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "Target")))
            .Returns(element.Object);

        return element;
    }

    private Mock<IMauiElement> GivenBusySignal(Func<string> readValue)
    {
        var signal = new Mock<IMauiElement>();
        signal.Setup(e => e.Text).Returns(readValue);
        _pageRoot.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "Busy")))
            .Returns(signal.Object);
        return signal;
    }

    #region Scope readiness

    /// <summary>
    /// A page that becomes ready late is waited for, not failed against.
    /// </summary>
    /// <remarks>
    /// This is the rung the whole plan is about. The wait is not a sleep and not a retry the
    /// caller wrote: <c>FindElement</c> throws while the page is not loaded, and the action's
    /// own <c>RunPoll</c> treats that as the transient condition it is.
    /// </remarks>
    [Fact]
    public void Click_WaitsForThePage_WhenItBecomesLoadedLate()
    {
        var checks = 0;
        var page = new GatedPage(_context.Object, () => ++checks >= 3);
        var element = GivenElement();

        page.Target.Click();

        element.Verify(e => e.Invoke(), Times.Once);
        Assert.True(checks >= 3, "the page should have been re-checked until it was loaded");
    }

    /// <summary>
    /// A page that never loads fails naming the page, not the element.
    /// </summary>
    /// <remarks>
    /// The message is the point: "element not found" describes a symptom of being on the wrong
    /// page rather than the cause.
    /// </remarks>
    [Fact]
    public void Click_FailsNamingThePage_WhenItNeverLoads()
    {
        var page = new GatedPage(_context.Object, () => false);
        GivenElement();

        var ex = Assert.Throws<ScopeNotReadyException>(() => page.Target.Click());

        Assert.Contains("GatedPage", ex.Message);
        Assert.Contains(nameof(ScopeReadinessState.NotLoaded), ex.Message);
        Assert.Equal(ScopeReadinessState.NotLoaded, ex.Readiness.State);
    }

    #endregion

    #region Automatic page readiness gate

    /// <summary>
    /// A control resolves only within its own page: a page that is not on screen refuses,
    /// naming the missing root.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pinned because it was never covered and the gap cost a suite. Every page object in these
    /// tests holds controls that are genuinely inside it, so nothing exercised the arrangement
    /// where a control is scoped to a page it does not belong to. The sample-app fixture had
    /// exactly that — the hub's own back button, which is attached to whichever page the hub
    /// opened — and once <c>3784e70</c> began enforcing readiness, every UI test after the first
    /// died here.
    /// </para>
    /// <para>
    /// The behaviour asserted below is the correct one and the fix was to stop asking a page for
    /// a control it does not own. This test exists so that anyone tempted to relax the gate
    /// instead has to say so explicitly.
    /// </para>
    /// </remarks>
    [Fact]
    public void ControlScopedToAnAbsentPage_RefusesAndNamesTheMissingRoot()
    {
        _context.Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "AbsentPage")))
            .Returns([]);

        var page = new AbsentPage(_context.Object);

        var ex = Assert.Throws<ScopeNotReadyException>(() => page.Target.Click());

        Assert.Contains("AbsentPage", ex.Message);
        Assert.Contains(nameof(ScopeReadinessState.MissingRoot), ex.Message);
    }

    [Fact]
    public void Click_WaitsForPageIdle_ThenExecutesExactlyOnce()
    {
        var busyReads = 0;
        GivenBusySignal(() => ++busyReads < 3 ? "True" : "False");
        var element = GivenElement();
        element.Setup(e => e.Invoke()).Callback(() => Assert.True(busyReads >= 3));

        new BusyGatedPage(_context.Object).Target.Click();

        element.Verify(e => e.Invoke(), Times.Once);
    }

    [Fact]
    public void Getter_DoesNotReadControlWhilePageIsBusy()
    {
        var busyValues = new Queue<string>(["False", "True", "True", "False"]);
        var busy = true;
        GivenBusySignal(() =>
        {
            var value = busyValues.Count > 0 ? busyValues.Dequeue() : "False";
            busy = bool.Parse(value);
            return value;
        });
        var element = GivenElement();
        element.Setup(e => e.Text).Returns(() =>
        {
            Assert.False(busy);
            return "Ready";
        });

        var actual = new BusyGatedPage(_context.Object).Status.GetText();

        Assert.Equal("Ready", actual);
        element.VerifyGet(e => e.Text, Times.Once);
    }

    [Fact]
    public void Wait_DoesNotEvaluateControlConditionWhilePageIsBusy()
    {
        var busyValues = new Queue<string>(["False", "True", "True", "False"]);
        var busy = true;
        GivenBusySignal(() =>
        {
            var value = busyValues.Count > 0 ? busyValues.Dequeue() : "False";
            busy = bool.Parse(value);
            return value;
        });
        var element = GivenElement();
        element.Setup(e => e.Text).Returns(() =>
        {
            Assert.False(busy);
            return "Ready";
        });

        Assert.True(new BusyGatedPage(_context.Object).Status.WaitText("Ready"));
        element.VerifyGet(e => e.Text, Times.Once);
    }

    [Fact]
    public void Assert_DoesNotEvaluateControlConditionWhilePageIsBusy()
    {
        var busyValues = new Queue<string>(["False", "True", "False"]);
        var busy = true;
        GivenBusySignal(() =>
        {
            var value = busyValues.Count > 0 ? busyValues.Dequeue() : "False";
            busy = bool.Parse(value);
            return value;
        });
        var element = GivenElement();
        element.Setup(e => e.Text).Returns(() =>
        {
            Assert.False(busy);
            return "Ready";
        });

        new BusyGatedPage(_context.Object).Status.AssertText("Ready");
        element.VerifyGet(e => e.Text, Times.Once);
    }

    [Fact]
    public void NestedCompoundHelper_UsesTheOwningPageReadiness()
    {
        var busyReads = 0;
        GivenBusySignal(() =>
        {
            busyReads++;
            return "False";
        });
        var element = GivenElement();
        element.Setup(e => e.Text).Returns("Ready");

        var actual = new BusyGatedPage(_context.Object).Compound.ReadText();

        Assert.Equal("Ready", actual);
        Assert.True(busyReads >= 2);
    }

    [Fact]
    public void BusyTimeout_ReportsOperationAndLastPageState()
    {
        GivenBusySignal(() => "True");
        var element = GivenElement();

        var exception = Assert.Throws<ScopeNotReadyException>(
            () => new BusyGatedPage(_context.Object).Target.Click(timeoutMs: 10));

        Assert.Contains("Click", exception.Message);
        Assert.Contains("GatedPage", exception.Message);
        Assert.Contains("Busy", exception.Message);
        Assert.Contains("AutomationId:Target", exception.Message);
        Assert.Contains("10 ms", exception.Message);
        Assert.Contains("busy value: 'True'", exception.Message);
        element.Verify(e => e.Invoke(), Times.Never);
    }

    [Fact]
    public void Click_WaitsForThePage_WhenALoadingViewHidesItDuringTheAttempt()
    {
        // The root reads shown, then a loading view hides it, signal included, before the busy search.
        var visibleReads = 0;
        _pageRoot.Setup(e => e.Visible).Returns(() => ++visibleReads != 2);
        var signal = new Mock<IMauiElement>();
        signal.Setup(e => e.Text).Returns("False");
        var busySearches = 0;
        _pageRoot.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "Busy")))
            .Returns(() => ++busySearches == 1 ? null : signal.Object);
        var element = GivenElement();

        new BusyGatedPage(_context.Object).Target.Click();

        element.Verify(e => e.Invoke(), Times.Once);
    }

    [Fact]
    public void WaitLoadedFalse_BypassesBusyReadinessGate()
    {
        var signal = GivenBusySignal(() => "True");
        _pageRoot.SetupSequence(e => e.Visible)
            .Returns(true)
            .Returns(false);
        _context.SetupSequence(c => c.FindElements(It.Is<Locator>(l => l.Value == "GatedPage")))
            .Returns([_pageRoot.Object])
            .Returns([]);

        var unloaded = new BusyGatedPage(_context.Object).WaitLoaded(false);

        Assert.True(unloaded);
        signal.VerifyGet(e => e.Text, Times.Never);
    }

    #endregion

    #region Element readiness

    /// <summary>
    /// A control enabled late is waited for.
    /// </summary>
    /// <remarks>
    /// A button enabled by a binding that resolves a frame after the page appears is the
    /// ordinary case here, not an exotic one.
    /// </remarks>
    [Fact]
    public void Click_WaitsForTheControl_WhenItBecomesEnabledLate()
    {
        var page = new GatedPage(_context.Object, () => true);
        var element = GivenElement();

        var reads = 0;
        element.Setup(e => e.Enabled).Returns(() => ++reads >= 3);

        page.Target.Click();

        element.Verify(e => e.Invoke(), Times.Once);
    }

    /// <summary>
    /// A control that never becomes enabled fails naming the locator.
    /// </summary>
    [Fact]
    public void Click_FailsNamingTheLocator_WhenTheControlNeverEnables()
    {
        var page = new GatedPage(_context.Object, () => true);
        var element = GivenElement();
        element.Setup(e => e.Enabled).Returns(false);

        var ex = Assert.Throws<ElementNotReadyException>(() => page.Target.Click());

        Assert.Contains("Target", ex.Message);
        element.Verify(e => e.Invoke(), Times.Never);
    }

    #endregion

    #region Queries stay instantaneous

    /// <summary>
    /// <c>IsExists()</c> answers immediately when the page is loaded and the element is not.
    /// </summary>
    /// <remarks>
    /// The constraint that keeps this design from becoming "wait everywhere". An Is query
    /// observes once; callers that need eventual state use the corresponding Wait method.
    /// </remarks>
    [Fact]
    public void IsExists_ReturnsFalseImmediately_WhenThePageIsLoadedAndTheElementIsNot()
    {
        var page = new GatedPage(_context.Object, () => true);
        _context.Setup(c => c.TryFindElement(It.IsAny<Locator>())).Returns((IMauiElement?)null);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var exists = page.Target.IsExists();
        stopwatch.Stop();

        Assert.False(exists);
        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"IsExists must not wait for the element; took {stopwatch.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// <c>IsExists()</c> does not wait for a page that has not arrived yet.
    /// </summary>
    /// <remarks>
    /// A query issued straight after navigation is one observation, of the element only: a lookup
    /// does not consult the page (design 7.4). Use <c>WaitExists()</c> when arrival is the
    /// expected behavior; its attempts ask the page first.
    /// </remarks>
    [Fact]
    public void IsExists_DoesNotWaitForThePage_WhenItHasNotArrived()
    {
        var checks = 0;
        var page = new GatedPage(_context.Object, () => { checks++; return false; });
        _context.Setup(c => c.TryFindElement(It.IsAny<Locator>())).Returns((IMauiElement?)null);

        Assert.False(page.Target.IsExists());
        Assert.Equal(0, checks);
    }

    [Fact]
    public void IsVisible_ReadsTheElementOnce_AndNotThePage()
    {
        var pageChecks = 0;
        var visibleReads = 0;
        var page = new GatedPage(_context.Object, () => { pageChecks++; return true; });
        var element = GivenElement();
        element.Setup(e => e.Visible).Returns(() => { visibleReads++; return true; });

        Assert.True(page.Target.IsVisible());
        Assert.Equal(0, pageChecks);
        Assert.Equal(1, visibleReads);
    }

    #endregion

    #region Retry safety

    /// <summary>
    /// An action that throws after taking effect is performed exactly once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Poll to get ready, then act once.</b> Resolution — finding the element, checking
    /// visible and enabled — is safe to repeat, so the retry loop covers it. The action is not
    /// safe to repeat, so it runs after the loop.
    /// </para>
    /// <para>
    /// Before that split, <c>RunPoll</c> retried its whole body including the action, so a
    /// driver that acted and then threw had its action replayed. A click that navigates away
    /// leaves the element stale and some drivers raise on the response, which made this an
    /// ordinary case rather than an exotic one. The symptom was a silent double action: a
    /// counter incremented twice, an item added twice, a form submitted twice.
    /// </para>
    /// <para>
    /// The exception now propagates rather than being retried. That is the point: once an
    /// action has been attempted, repeating it can only compound the damage.
    /// </para>
    /// </remarks>
    [Fact]
    public void Click_IsPerformedOnce_WhenTheDriverThrowsAfterActing()
    {
        var page = new GatedPage(_context.Object, () => true);
        var element = GivenElement();

        var clicks = 0;
        element.Setup(e => e.Invoke()).Callback(() =>
        {
            clicks++;
            throw new InvalidOperationException("stale after navigation");
        });

        Assert.Throws<InvalidOperationException>(() => page.Target.Click());

        Assert.Equal(1, clicks);
    }

    #endregion
}
