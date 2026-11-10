using Bruinet.Core.Calls;
using Bruinet.Html.Controls.Buttons;

namespace Bruinet.Html.Tests;

/// <summary>
/// A call that passed after using most of its budget is a near-miss even on its first attempt
/// (a Playwright action that waits inside itself), and the run's near-misses are one file.
/// The timeout scale is read once and only from a sensible factor.
/// </summary>
public sealed class NearMissTests : IDisposable
{
    private readonly Mock<IHtmlTestContext> _context = new();
    private readonly Mock<ITestLogger> _logger = new();
    private readonly Mock<IHtmlElement> _target = new();
    private readonly string _id = $"NearMissTarget{Guid.NewGuid():N}";
    private readonly string _summary = Path.Combine(Path.GetTempPath(), $"near-misses-{Guid.NewGuid():N}.md");

    public NearMissTests()
    {
        NearMisses.Path = _summary;
        _context.Setup(c => c.Timeouts).Returns(new TimeoutSettings { DefaultWait = TimeSpan.FromMilliseconds(400), PageLoad = TimeSpan.FromMilliseconds(400), PollingInterval = TimeSpan.FromMilliseconds(1) });
        _context.Setup(c => c.Logger).Returns(_logger.Object);
        _context.Setup(c => c.Context).Returns(() => _context.Object);
        _context.Setup(c => c.IsIdle()).Returns(true);
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == _id))).Returns(_target.Object);
        _target.Setup(e => e.Visible).Returns(true);
        _target.Setup(e => e.Enabled).Returns(true);
    }

    public void Dispose() => File.Delete(_summary);

    private sealed class Page(IHtmlTestContext context, string id) : PageObjectBase<Page>(context)
    {
        public override bool IsLoaded() => true;

        public ButtonControl<Page> Target => new(this, Locator.ByAutomationId(id));
    }

    // Other test classes run beside this one and add their own near-misses to the run's list.
    private List<NearMisses.NearMiss> Ours() => [.. NearMisses.All.Where(miss => miss.Control.Contains(_id, StringComparison.Ordinal))];

    private void VerifyExit(LogResult result) => _logger.Verify(l => l.LogExit(It.IsAny<string>(), It.IsAny<string>(),
        It.IsAny<string>(), "Click", result, It.IsAny<int>(), It.IsAny<string?>()), Times.Once);

    [Fact]
    public void AClickThatTakesMostOfItsBudgetInOneAttempt_IsANearMiss_InTheLogAndTheSummary()
    {
        _target.Setup(e => e.Click()).Callback(() => Thread.Sleep(300));

        new Page(_context.Object, _id).Target.Click();

        VerifyExit(LogResult.Warning);
        var miss = Assert.Single(Ours());
        Assert.Equal("Click", miss.Call);
        Assert.Equal(400, miss.BudgetMs);
        Assert.Contains(_id, File.ReadAllText(_summary), StringComparison.Ordinal);
    }

    [Fact]
    public void AQuickClick_IsNone()
    {
        new Page(_context.Object, _id).Target.Click();

        VerifyExit(LogResult.Success);
        Assert.Empty(Ours());
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData("2", 2)]
    [InlineData("1.5", 1.5)]
    [InlineData("0", 1)]
    [InlineData("50", 1)]
    [InlineData("slow", 1)]
    public void TheTimeoutScale_IsAFactorFromATenthToTwenty(string? value, double scale)
        => Assert.Equal(scale, TimeoutSettings.ReadScale(value));
}
