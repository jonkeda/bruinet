using Bruinet.Core.Interfaces;
using Xunit;

namespace Bruinet.ControlContracts;

/// <summary><see cref="IDateControlObject{TScope}"/>: a date read and set, compared by day.</summary>
public abstract class DateContract<TScope> : ControlContract<TScope> where TScope : class
{
    /// <summary>A date picker at <see cref="Initial"/>.</summary>
    protected abstract IDateControlObject<TScope> Picker { get; }

    /// <summary>The date <see cref="Picker"/> shows at the start.</summary>
    protected abstract DateTime Initial { get; }

    /// <summary>Another date, set by the contract.</summary>
    protected virtual DateTime Target => new(2027, 1, 3, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public virtual void GetDate_ReadsTheDate() => Assert.Equal(Initial.Date, Picker.GetDate()?.Date);

    [Fact]
    public virtual void SetDate_SetsIt_NullSkips()
    {
        ReturnsScope(Picker.SetDate(Target));
        Assert.Equal(Target.Date, Picker.GetDate()?.Date);
        ReturnsScope(Picker.SetDate(null));
        Assert.Equal(Target.Date, Picker.GetDate()?.Date);
    }

    [Fact]
    public virtual void WaitDate_TrueForTheDate_FalseForAnother()
    {
        Assert.True(Picker.WaitDate(Initial));
        Assert.False(Picker.WaitDate(Target, ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertDate_PassesForTheDate_FailsForAnother()
    {
        ReturnsScope(Picker.AssertDate(Initial));
        Fails(() => Picker.AssertDate(Target, timeoutMs: ShortTimeoutMs));
    }
}

/// <summary><see cref="ITimeControlObject{TScope}"/>: a time of day read and set, to the minute.</summary>
public abstract class TimeContract<TScope> : ControlContract<TScope> where TScope : class
{
    /// <summary>A time picker at <see cref="Initial"/>.</summary>
    protected abstract ITimeControlObject<TScope> Picker { get; }

    /// <summary>The time <see cref="Picker"/> shows at the start, to the minute.</summary>
    protected abstract TimeSpan Initial { get; }

    /// <summary>Another time, set by the contract.</summary>
    protected virtual TimeSpan Target => new(14, 30, 0);

    [Fact]
    public virtual void GetTime_ReadsTheTime() => Assert.Equal(Initial, Picker.GetTime());

    [Fact]
    public virtual void SetTime_SetsIt_NullSkips()
    {
        ReturnsScope(Picker.SetTime(Target));
        Assert.Equal(Target, Picker.GetTime());
        ReturnsScope(Picker.SetTime(null));
        Assert.Equal(Target, Picker.GetTime());
    }

    [Fact]
    public virtual void WaitTime_TrueForTheTime_FalseForAnother()
    {
        Assert.True(Picker.WaitTime(Initial));
        Assert.False(Picker.WaitTime(Target, ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertTime_PassesForTheTime_FailsForAnother()
    {
        ReturnsScope(Picker.AssertTime(Initial));
        Fails(() => Picker.AssertTime(Target, timeoutMs: ShortTimeoutMs));
    }
}
