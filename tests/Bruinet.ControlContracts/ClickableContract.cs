using Bruinet.Core.Interfaces;
using Xunit;

namespace Bruinet.ControlContracts;

/// <summary><see cref="IClickableControlObject{TScope}"/>: the pointer's actions reach the control.</summary>
public abstract class ClickableContract<TScope> : ControlContract<TScope> where TScope : class
{
    /// <summary>Visible and enabled; the page records what happened to it.</summary>
    protected abstract IClickableControlObject<TScope> Button { get; }

    /// <summary>Visible, not enabled.</summary>
    protected abstract IClickableControlObject<TScope> DisabledButton { get; }

    /// <summary>
    /// What the page saw happen to <see cref="Button"/> last: "click", "double-click", "right-click",
    /// "hover" or "long-press", null before anything; the head maps its own events to these.
    /// </summary>
    protected abstract string? LastAction();

    [Fact]
    public virtual void Click_ClicksIt()
    {
        ReturnsScope(Button.Click());
        Shows("click", LastAction);
    }

    [Fact]
    public virtual void DoubleClick_DoubleClicksIt()
    {
        ReturnsScope(Button.DoubleClick());
        Shows("double-click", LastAction);
    }

    [Fact]
    public virtual void RightClick_RightClicksIt()
    {
        ReturnsScope(Button.RightClick());
        Shows("right-click", LastAction);
    }

    [Fact]
    public virtual void Hover_HoversIt()
    {
        ReturnsScope(Button.Hover());
        Shows("hover", LastAction);
    }

    [Fact]
    public virtual void LongPress_PressesIt()
    {
        ReturnsScope(Button.LongPress());
        Shows("long-press", LastAction);
    }

    [Fact]
    public virtual void IsClickable_TrueEnabled_FalseDisabled()
    {
        Assert.True(Button.IsClickable());
        Assert.False(DisabledButton.IsClickable());
    }

    [Fact]
    public virtual void WaitClickable_TrueWhenItHolds_FalseWhenItDoesNot()
    {
        Assert.True(Button.WaitClickable(true));
        Assert.True(DisabledButton.WaitClickable(false));
        Assert.False(DisabledButton.WaitClickable(true, ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertClickable_PassesWhenItHolds_FailsWhenItDoesNot()
    {
        ReturnsScope(Button.AssertClickable(true));
        ReturnsScope(DisabledButton.AssertClickable(false));
        Fails(() => DisabledButton.AssertClickable(true, timeoutMs: ShortTimeoutMs));
    }
}
