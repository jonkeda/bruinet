using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Xunit;

namespace Bruinet.ControlContracts;

/// <summary><see cref="IScrollableControlObject{TScope}"/>: a vertical position from 0 to 100 percent, moved to the ends, by an amount and to an item.</summary>
public abstract class ScrollableContract<TScope> : ControlContract<TScope> where TScope : class
{
    /// <summary>At the top, with more content than it shows.</summary>
    protected abstract IScrollableControlObject<TScope> View { get; }

    /// <summary>An item at the end of <see cref="View"/>'s content.</summary>
    protected abstract Locator LastItem { get; }

    /// <summary>How far a position may be from the one asked for, in percent.</summary>
    protected virtual double Tolerance => 1;

    [Fact]
    public virtual void GetScrollPosition_ZeroAtTheTop() => Assert.Equal(0, View.GetScrollPosition() ?? double.NaN, Tolerance);

    [Fact]
    public virtual void CanScroll_DownFromTheTop_NotUp()
    {
        Assert.True(View.CanScrollDown());
        Assert.False(View.CanScrollUp());
    }

    [Fact]
    public virtual void ScrollToEnd_GoesToTheEnd_ScrollToTop_Back()
    {
        ReturnsScope(View.ScrollToEnd());
        Assert.Equal(100, View.GetScrollPosition() ?? double.NaN, Tolerance);
        Assert.False(View.CanScrollDown());
        Assert.True(View.CanScrollUp());
        ReturnsScope(View.ScrollToTop());
        Assert.Equal(0, View.GetScrollPosition() ?? double.NaN, Tolerance);
    }

    [Fact]
    public virtual void SetScrollPosition_GoesThere()
    {
        ReturnsScope(View.SetScrollPosition(50));
        Assert.Equal(50, View.GetScrollPosition() ?? double.NaN, Tolerance);
    }

    [Fact]
    public virtual void ScrollBy_MovesDown()
    {
        ReturnsScope(View.ScrollBy(0, 100));
        Assert.True(View.GetScrollPosition() > 0);
    }

    [Fact]
    public virtual void ScrollTo_MovesToTheItem()
    {
        ReturnsScope(View.ScrollTo(LastItem));
        Assert.True(View.GetScrollPosition() > 0);
    }

    [Fact]
    public virtual void WaitScrollPosition_TrueThere_FalseElsewhere()
    {
        Assert.True(View.WaitScrollPosition(0, Tolerance));
        Assert.False(View.WaitScrollPosition(100, Tolerance, ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertScrollPosition_PassesThere_FailsElsewhere()
    {
        ReturnsScope(View.AssertScrollPosition(0, Tolerance));
        Fails(() => View.AssertScrollPosition(100, Tolerance, timeoutMs: ShortTimeoutMs));
    }
}

/// <summary><see cref="ISwipeableControlObject{TScope}"/>: swipes in four directions and between two points.</summary>
public abstract class SwipeableContract<TScope> : ControlContract<TScope> where TScope : class
{
    /// <summary>The page records the direction of the last swipe on it.</summary>
    protected abstract ISwipeableControlObject<TScope> Card { get; }

    /// <summary>The last swipe's direction: "left", "right", "up" or "down"; null before any.</summary>
    protected abstract string? LastSwipe();

    /// <summary>A start and end point on <see cref="Card"/> for a swipe to the left.</summary>
    protected abstract (int StartX, int StartY, int EndX, int EndY) LeftwardPoints { get; }

    [Fact]
    public virtual void SwipeLeft_SwipesLeft()
    {
        ReturnsScope(Card.SwipeLeft());
        Shows("left", LastSwipe);
    }

    [Fact]
    public virtual void SwipeRight_SwipesRight()
    {
        ReturnsScope(Card.SwipeRight());
        Shows("right", LastSwipe);
    }

    [Fact]
    public virtual void SwipeUp_SwipesUp()
    {
        ReturnsScope(Card.SwipeUp());
        Shows("up", LastSwipe);
    }

    [Fact]
    public virtual void SwipeDown_SwipesDown()
    {
        ReturnsScope(Card.SwipeDown());
        Shows("down", LastSwipe);
    }

    [Fact]
    public virtual void Swipe_BetweenPoints_SwipesThatWay()
    {
        var (startX, startY, endX, endY) = LeftwardPoints;
        ReturnsScope(Card.Swipe(startX, startY, endX, endY));
        Shows("left", LastSwipe);
    }
}

/// <summary><see cref="IRefreshableControlObject{TScope}"/>: pull to refresh.</summary>
public abstract class RefreshableContract<TScope> : ControlContract<TScope> where TScope : class
{
    /// <summary>Not refreshing at the start; the page counts its refreshes.</summary>
    protected abstract IRefreshableControlObject<TScope> View { get; }

    /// <summary>How often <see cref="View"/> was refreshed.</summary>
    protected abstract int Refreshes();

    [Fact]
    public virtual void IsRefreshing_FalseAtTheStart() => Assert.False(View.IsRefreshing());

    [Fact]
    public virtual void PullToRefresh_RefreshesIt()
    {
        ReturnsScope(View.PullToRefresh());
        Shows(1, Refreshes);
    }

    [Fact]
    public virtual void WaitRefreshing_TrueWhenItHolds_FalseWhenItDoesNot()
    {
        Assert.True(View.WaitRefreshing(false));
        Assert.False(View.WaitRefreshing(true, ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertRefreshing_PassesWhenItHolds_FailsWhenItDoesNot()
    {
        ReturnsScope(View.AssertRefreshing(false));
        Fails(() => View.AssertRefreshing(true, timeoutMs: ShortTimeoutMs));
    }
}
