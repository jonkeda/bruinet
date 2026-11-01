using Bruinet.Core.Interfaces;
using Xunit;

namespace Bruinet.ControlContracts;

/// <summary><see cref="IToggleControlObject{TScope}"/>: checked and unchecked.</summary>
public abstract class ToggleContract<TScope> : ControlContract<TScope> where TScope : class
{
    /// <summary>Unchecked at the start.</summary>
    protected abstract IToggleControlObject<TScope> Box { get; }

    [Fact]
    public virtual void IsChecked_FalseAtTheStart() => Assert.False(Box.IsChecked());

    [Fact]
    public virtual void Check_ChecksIt_Uncheck_UnchecksIt()
    {
        ReturnsScope(Box.Check());
        Assert.True(Box.IsChecked());
        ReturnsScope(Box.Uncheck());
        Assert.False(Box.IsChecked());
    }

    [Fact]
    public virtual void Check_WhenChecked_KeepsItChecked()
    {
        Box.Check();
        ReturnsScope(Box.Check());
        Assert.True(Box.IsChecked());
    }

    [Fact]
    public virtual void Toggle_TurnsItOver()
    {
        ReturnsScope(Box.Toggle());
        Assert.True(Box.IsChecked());
        ReturnsScope(Box.Toggle());
        Assert.False(Box.IsChecked());
    }

    [Fact]
    public virtual void SetChecked_SetsIt_NullSkips()
    {
        ReturnsScope(Box.SetChecked(true));
        Assert.True(Box.IsChecked());
        ReturnsScope(Box.SetChecked(null));
        Assert.True(Box.IsChecked());
        ReturnsScope(Box.SetChecked(false));
        Assert.False(Box.IsChecked());
    }

    [Fact]
    public virtual void WaitChecked_TrueWhenItHolds_FalseWhenItDoesNot()
    {
        Assert.True(Box.WaitChecked(false));
        Assert.False(Box.WaitChecked(true, ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertChecked_PassesWhenItHolds_FailsWhenItDoesNot()
    {
        ReturnsScope(Box.AssertChecked(false));
        Fails(() => Box.AssertChecked(true, timeoutMs: ShortTimeoutMs));
        Box.Check();
        ReturnsScope(Box.AssertChecked(true));
    }
}
