using Bruinet.Core.Interfaces;
using Xunit;

namespace Bruinet.ControlContracts;

/// <summary>A range's bounds and the values the contract uses: <see cref="Initial"/> + <see cref="Step"/> and <see cref="Target"/> - <see cref="Step"/> stay within the bounds.</summary>
public sealed record RangeValues(double Minimum, double Maximum, double Step, double Initial, double Target);

/// <summary><see cref="IRangeControlObject{TScope}"/>: a value between bounds, set and stepped.</summary>
public abstract class RangeContract<TScope> : ControlContract<TScope> where TScope : class
{
    /// <summary>A slider or the like at <see cref="RangeValues.Initial"/>.</summary>
    protected abstract IRangeControlObject<TScope> Slider { get; }

    /// <summary>What <see cref="Slider"/> is.</summary>
    protected abstract RangeValues Values { get; }

    [Fact]
    public virtual void GetMinimum_GetMaximum_GetStep_ReadTheBounds()
    {
        Assert.Equal(Values.Minimum, Slider.GetMinimum());
        Assert.Equal(Values.Maximum, Slider.GetMaximum());
        Assert.Equal(Values.Step, Slider.GetStep());
    }

    [Fact]
    public virtual void GetValue_ReadsTheValue() => Assert.Equal(Values.Initial, Slider.GetValue());

    [Fact]
    public virtual void SetValue_SetsIt_NullSkips()
    {
        ReturnsScope(Slider.SetValue(Values.Target));
        Assert.Equal(Values.Target, Slider.GetValue());
        ReturnsScope(Slider.SetValue(null));
        Assert.Equal(Values.Target, Slider.GetValue());
    }

    [Fact]
    public virtual void Increment_RaisesItByAStep()
    {
        ReturnsScope(Slider.Increment());
        Assert.Equal(Values.Initial + Values.Step, Slider.GetValue());
    }

    [Fact]
    public virtual void Decrement_LowersItByAStep()
    {
        Slider.SetValue(Values.Target);
        ReturnsScope(Slider.Decrement());
        Assert.Equal(Values.Target - Values.Step, Slider.GetValue());
    }

    [Fact]
    public virtual void WaitValue_TrueForItsValue_FalseForAnother()
    {
        Assert.True(Slider.WaitValue(Values.Initial));
        Assert.False(Slider.WaitValue(Values.Target, ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertValue_PassesForItsValue_FailsForAnother()
    {
        ReturnsScope(Slider.AssertValue(Values.Initial));
        Fails(() => Slider.AssertValue(Values.Target, timeoutMs: ShortTimeoutMs));
    }

    [Fact]
    public virtual void WaitValueWithin_TrueNearIt_FalseFarFromIt()
    {
        Assert.True(Slider.WaitValueWithin(Values.Initial + (Values.Step / 2), Values.Step));
        Assert.False(Slider.WaitValueWithin(Values.Target, Values.Step / 2, ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertValueWithin_PassesNearIt_FailsFarFromIt()
    {
        ReturnsScope(Slider.AssertValueWithin(Values.Initial + (Values.Step / 2), Values.Step));
        Fails(() => Slider.AssertValueWithin(Values.Target, Values.Step / 2, timeoutMs: ShortTimeoutMs));
    }
}
