using Bruinet.Core.Interfaces;
using Xunit;

namespace Bruinet.ControlContracts;

/// <summary><see cref="IProgressControlObject{TScope}"/>: progress as a fraction from 0 to 1, complete at 1, or indeterminate.</summary>
public abstract class ProgressContract<TScope> : ControlContract<TScope> where TScope : class
{
    /// <summary>Determinate, at <see cref="Fraction"/>.</summary>
    protected abstract IProgressControlObject<TScope> Partial { get; }

    /// <summary>Determinate, at 1.</summary>
    protected abstract IProgressControlObject<TScope> Complete { get; }

    /// <summary>Indeterminate.</summary>
    protected abstract IProgressControlObject<TScope> Indeterminate { get; }

    /// <summary>Where <see cref="Partial"/> stands, strictly between 0 and 1.</summary>
    protected abstract double Fraction { get; }

    [Fact]
    public virtual void GetProgress_ReadsTheFraction()
    {
        Assert.Equal(Fraction, Partial.GetProgress() ?? double.NaN, 3);
        Assert.Equal(1, Complete.GetProgress() ?? double.NaN, 3);
    }

    [Fact]
    public virtual void IsIndeterminate_TrueOnlyWhenIndeterminate()
    {
        Assert.False(Partial.IsIndeterminate());
        Assert.True(Indeterminate.IsIndeterminate());
    }

    [Fact]
    public virtual void WaitProgress_TrueForTheFraction_FalseForAnother()
    {
        Assert.True(Partial.WaitProgress(Fraction));
        Assert.False(Partial.WaitProgress(1, ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertProgress_PassesForTheFraction_FailsForAnother()
    {
        ReturnsScope(Partial.AssertProgress(Fraction));
        Fails(() => Partial.AssertProgress(1, timeoutMs: ShortTimeoutMs));
    }

    [Fact]
    public virtual void WaitComplete_TrueAtOne_FalseBefore()
    {
        Assert.True(Complete.WaitComplete());
        Assert.False(Partial.WaitComplete(ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertComplete_PassesAtOne_FailsBefore()
    {
        ReturnsScope(Complete.AssertComplete());
        Fails(() => Partial.AssertComplete(timeoutMs: ShortTimeoutMs));
    }
}
