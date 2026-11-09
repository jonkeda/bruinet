using Bruinet.Core.Logging;
using Bruinet.Maui.Containers;

namespace Bruinet.Maui.Tests;

/// <summary><c>WaitFirst</c> on a scope: one call that polls several answers about now and names the first that holds.</summary>
public sealed class ScopeWaitFirstTests
{
    private readonly Mock<IMauiTestContext> _context = new();
    private readonly Mock<ITestLogger> _logger = new();
    private readonly AppRoot _root;

    public ScopeWaitFirstTests()
    {
        _context.Setup(c => c.Timeouts).Returns(new TimeoutSettings { DefaultWait = 100, PollingInterval = 1 });
        _context.Setup(c => c.Logger).Returns(_logger.Object);
        _root = new AppRoot(_context.Object);
    }

    [Fact]
    public void WaitFirst_ReturnsTheIndexOfTheOutcomeThatComesTrue()
    {
        var reads = 0;

        var index = _root.WaitFirst(1000, () => false, () => ++reads >= 3);

        Assert.Equal(1, index);
        Assert.Equal(3, reads);
    }

    [Fact]
    public void WaitFirst_PrefersTheEarlierOutcome_WhenBothHold()
        => Assert.Equal(0, _root.WaitFirst(1000, () => true, () => true));

    [Fact]
    public void WaitFirst_AnswersMinusOne_WithOneErrorExit_WhenNoneHolds()
    {
        Assert.Equal(-1, _root.WaitFirst(50, () => false, () => false));

        _logger.Verify(l => l.LogExit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            "WaitFirst", LogResult.Error, It.IsAny<int>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public void WaitFirst_AsksAgain_WhenAnAnswerHitsAStaleElement()
    {
        var reads = 0;

        var index = _root.WaitFirst(1000, () => ++reads == 1 ? throw new StaleElementException(detail: "gone") : true);

        Assert.Equal(0, index);
        Assert.Equal(2, reads);
    }

    [Fact]
    public void WaitFirst_OnAContainer_HoldsWhenTheContainerItselfIsGone()
    {
        var sheet = new Sheet(_root, "Sheet");
        Assert.False(sheet.IsReady());

        Assert.Equal(1, sheet.WaitFirst(1000, () => false, () => !sheet.IsExists()));
    }

    private sealed class Sheet(IMauiScope<AppRoot> scope, string automationId)
        : ContainerObjectBase<AppRoot, Sheet>(scope, automationId);
}
