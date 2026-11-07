using Bruinet.Maui.Testing;

namespace Bruinet.Maui.Tests;

// Recovery over a fake app: a stack of screens, Back pops it, a page is loaded while its screen is on top.
public sealed class NavigationRecoveryTests
{
    private const string Root = "root";

    private readonly Stack<string> _screens = new([Root]);
    private readonly Mock<IMauiTestContext> _context = new();
    private readonly Mock<IScreenshotService> _screenshots = new();
    private int _backs;

    // Probes left in a page's exit transition: the page still answers and the screen below is not shown yet.
    private int _transition;

    public NavigationRecoveryTests()
    {
        _context.Setup(context => context.NavigateBack()).Callback(() =>
        {
            _backs++;
            if (_screens.Count > 1)
            {
                _screens.Pop();
            }
        });
    }

    private NavigationRecovery Recovery()
    {
        var recovery = new NavigationRecovery(_context.Object, _screenshots.Object, settleTimeoutMs: 150, postNavigationDelayMs: 100);
        recovery.RegisterRootCheck(() => _transition == 0 && _screens.Peek() == Root);
        return recovery;
    }

    [Fact]
    public void AtTheRoot_ItReturnsAtOnce_WithoutGoingBack()
    {
        Assert.True(Recovery().TryReturnToRoot());

        Assert.Equal(0, _backs);
    }

    [Fact]
    public void APageThatCanLeave_IsLeftItsOwnWay_NotWithBack()
    {
        _screens.Push("edit");
        var edit = new Page(this, "edit", leave: true);
        var recovery = Recovery();
        recovery.RegisterPages(edit);

        Assert.True(recovery.TryReturnToRoot());

        Assert.Equal((1, 0), (edit.Leaves, _backs));
    }

    [Fact]
    public void APageThatCannotLeave_AndAScreenNoPageKnows_AreLeftWithBack()
    {
        _screens.Push("unknown");
        _screens.Push("detail");
        var recovery = Recovery();
        recovery.RegisterPages(new Page(this, "detail", leave: false));

        Assert.True(recovery.TryReturnToRoot());

        Assert.Equal(2, _backs);
    }

    [Fact]
    public void APageStillAnsweringWhileItLeaves_IsNotLeftTwice()
    {
        _screens.Push("edit");
        var edit = new Page(this, "edit", leave: true) { AnswersAfterLeaving = 3 };
        var recovery = Recovery();
        recovery.RegisterPages(edit);

        Assert.True(recovery.TryReturnToRoot());

        Assert.Equal(1, edit.Leaves);
    }

    [Fact]
    public void AnAppThatNeverReachesTheRoot_FailsAfterItsAttempts_WithAScreenshot()
    {
        var recovery = new NavigationRecovery(_context.Object, _screenshots.Object, settleTimeoutMs: 50, postNavigationDelayMs: 50);
        recovery.RegisterRootCheck(() => false);

        Assert.False(recovery.TryReturnToRoot(maxAttempts: 3));

        Assert.Equal(3, _backs);
        _screenshots.Verify(service => service.Capture("NavigationRecovery", nameof(NavigationRecovery.TryReturnToRoot), "recovery_failed_after_max_attempts"), Times.Once);
    }

    // A page whose screen is on top; leaving pops it, and it can keep answering for a few probes (an exit transition).
    private sealed class Page(NavigationRecoveryTests app, string screen, bool leave) : INavigablePage
    {
        private bool _leaving;

        public int AnswersAfterLeaving { get; init; }

        public int Leaves { get; private set; }

        public bool IsLoaded(int? timeoutMs = null)
        {
            if (_leaving && app._transition > 0)
            {
                app._transition--;
                return true;
            }

            return app._screens.Peek() == screen;
        }

        public bool TryLeave()
        {
            if (!leave)
            {
                return false;
            }

            Leaves++;
            if (app._screens.Peek() == screen)
            {
                app._screens.Pop();
            }

            _leaving = true;
            app._transition = AnswersAfterLeaving;
            return true;
        }
    }
}
