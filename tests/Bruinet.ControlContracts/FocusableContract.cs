using Bruinet.Core.Interfaces;
using Xunit;

namespace Bruinet.ControlContracts;

/// <summary><see cref="IFocusableControlObject{TScope}"/>: focus comes and goes.</summary>
public abstract class FocusableContract<TScope> : ControlContract<TScope> where TScope : class
{
    /// <summary>Can take the focus; does not have it at the start.</summary>
    protected abstract IFocusableControlObject<TScope> Field { get; }

    [Fact]
    public virtual void IsFocused_FalseAtTheStart() => Assert.False(Field.IsFocused());

    [Fact]
    public virtual void Focus_GivesItTheFocus()
    {
        ReturnsScope(Field.Focus());
        Assert.True(Field.WaitFocused(true));
        Assert.True(Field.IsFocused());
    }

    [Fact]
    public virtual void Blur_TakesTheFocusAway()
    {
        Field.Focus();
        ReturnsScope(Field.Blur());
        Assert.True(Field.WaitFocused(false));
    }

    [Fact]
    public virtual void WaitFocused_FalseWhenItDoesNotHold() => Assert.False(Field.WaitFocused(true, ShortTimeoutMs));

    [Fact]
    public virtual void AssertFocused_PassesWhenItHolds_FailsWhenItDoesNot()
    {
        ReturnsScope(Field.AssertFocused(false));
        Fails(() => Field.AssertFocused(true, timeoutMs: ShortTimeoutMs));
        Field.Focus();
        ReturnsScope(Field.AssertFocused(true));
    }
}
