using System.Diagnostics;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Scopes;
using Bruinet.Html.Controls;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;
using Bruinet.Html.Scopes;

namespace Bruinet.Html.Tests;

/// <summary>
/// A call waits for its scope chain (R4, plan step 5): the page's busy signal, a container's
/// content, a misconfigured signal that fails at once.
/// </summary>
public class ScopeReadinessTests
{
    private readonly Mock<IHtmlTestContext> _context = new();
    private readonly Mock<ITestLogger> _logger = new();
    private readonly Mock<IHtmlElement> _body = new();
    private readonly Mock<IHtmlElement> _target = new();

    public ScopeReadinessTests()
    {
        _context.Setup(c => c.Timeouts).Returns(new TimeoutSettings
        {
            DefaultWait = TimeSpan.FromMilliseconds(1000),
            PageLoad = TimeSpan.FromMilliseconds(1000),
            PollingInterval = TimeSpan.FromMilliseconds(1),
        });
        _context.Setup(c => c.Logger).Returns(_logger.Object);
        _context.Setup(c => c.Context).Returns(() => _context.Object);
        _context.Setup(c => c.IsIdle()).Returns(true);
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "body"))).Returns(_body.Object);
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Target"))).Returns(_target.Object);
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Panel"))).Returns(_target.Object);
        _target.Setup(e => e.Visible).Returns(true);
        _target.Setup(e => e.Enabled).Returns(true);
        _target.Setup(e => e.Text).Returns("ready");
        _target.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "Inner"))).Returns(_target.Object);
    }

    private sealed class Page(IHtmlTestContext context, BusySignal busySignal = BusySignal.Optional) : PageObjectBase<Page>(context)
    {
        protected override BusySignal BusySignal => busySignal;

        public ButtonControl<Page> Target => new(this, Locator.ByAutomationId("Target"));

        public SlowPanel Panel => new(this, Locator.ByAutomationId("Panel"));
    }

    /// <summary>A container whose content is ready only from its third check.</summary>
    private sealed class SlowPanel(IHtmlScope<Page> parent, Locator locator) : ContainerBase<Page, SlowPanel>(parent, locator)
    {
        public override SlowPanel Self => this;

        public int ContentChecks { get; private set; }

        public LabelControl<SlowPanel> Inner => new(this, Locator.ByAutomationId("Inner"));

        protected override ScopeReadiness ProbeContentReadiness(IHtmlElement root)
            => ++ContentChecks >= 3 ? ContentReady() : ContentNotReady("still loading");
    }

    [Fact]
    public void ABusyPage_DefersTheCall_UntilItIsIdle_AndActsOnce()
    {
        var reads = 0;
        _body.Setup(b => b.GetAttribute("data-busy")).Returns(() => ++reads <= 3 ? "true" : "false");

        new Page(_context.Object).Target.Click();

        Assert.True(reads >= 4, $"The busy signal was read {reads} times.");
        _target.Verify(e => e.Click(), Times.Once);
    }

    [Fact]
    public void AnUnreadableBusySignal_FailsAtOnce_AndNeverActs()
    {
        _body.Setup(b => b.GetAttribute("data-busy")).Returns("maybe");

        var clock = Stopwatch.StartNew();
        var error = Record.Exception(() => new Page(_context.Object).Target.Click(timeoutMs: 1000));

        var notReady = Assert.IsType<ScopeNotReadyException>(error);
        Assert.Equal(ScopeReadinessState.InvalidBusySignal, notReady.Readiness.State);
        Assert.True(clock.ElapsedMilliseconds < 500, $"A misconfigured page took {clock.ElapsedMilliseconds} ms to fail.");
        _target.Verify(e => e.Click(), Times.Never);
    }

    [Fact]
    public void ARequiredBusySignal_ThatNeverAppears_FailsAsNotLoaded_WithinTheBudget()
    {
        var error = Record.Exception(() => new Page(_context.Object, BusySignal.Required).Target.Click(timeoutMs: 300));

        var notReady = Assert.IsType<ScopeNotReadyException>(error);
        Assert.Equal(ScopeReadinessState.NotLoaded, notReady.Readiness.State);
        _target.Verify(e => e.Click(), Times.Never);
    }

    [Fact]
    public void AContainersContentReadiness_IsOnItsChildsPath()
    {
        var panel = new Page(_context.Object).Panel;

        Assert.Equal("ready", panel.Inner.GetText(timeoutMs: 1000));
        Assert.True(panel.ContentChecks >= 3, $"The container's readiness was checked {panel.ContentChecks} times.");
    }

    [Fact]
    public void AssertLoaded_Fails_WithScopeNotReady_NotPageLoadException()
    {
        _context.Setup(c => c.IsIdle()).Returns(false);

        Assert.Throws<ScopeNotReadyException>(() => new Page(_context.Object).AssertLoaded(true, timeoutMs: 100));
    }

    [Fact]
    public void AppRootWaitReady_IsOneCall()
    {
        _context.Setup(c => c.ProbeReadiness()).Returns(ScopeReadiness.Ready("context"));

        Assert.True(new AppRoot(_context.Object).WaitReady(timeoutMs: 300));

        _logger.Verify(l => l.Write(It.Is<LogEvent>(e => e.Kind == LogEventKind.Entry)), Times.Once);
        _logger.Verify(l => l.Write(It.Is<LogEvent>(e => e.Kind == LogEventKind.Exit)), Times.Once);
    }
}
