using Bruinet.Core.Interfaces;
using Xunit;

namespace Bruinet.ControlContracts;

/// <summary><see cref="IControlObject{TScope}"/> and <see cref="IElementObject{TScope}"/>: whether a control is there, shown and enabled.</summary>
public abstract class ElementContract<TScope> : ControlContract<TScope> where TScope : class
{
    /// <summary>On the page, visible and enabled.</summary>
    protected abstract IControlObject<TScope> Shown { get; }

    /// <summary>On the page, not visible.</summary>
    protected abstract IControlObject<TScope> Hidden { get; }

    /// <summary>Visible, not enabled.</summary>
    protected abstract IControlObject<TScope> Disabled { get; }

    /// <summary>Not on the page.</summary>
    protected abstract IControlObject<TScope> Missing { get; }

    /// <summary>An attribute <see cref="Shown"/> has, and its value.</summary>
    protected abstract (string Name, string Value) Attribute { get; }

    [Fact]
    public virtual void IsExists_TrueOnThePage_FalseOffIt()
    {
        Assert.True(Shown.Exists());
        Assert.True(Hidden.Exists());
        Assert.False(Missing.Exists());
    }

    [Fact]
    public virtual void IsVisible_TrueShown_FalseHidden_NullMissing()
    {
        Assert.True(Shown.IsVisible());
        Assert.False(Hidden.IsVisible());
        Assert.Null(Missing.IsVisible());
    }

    [Fact]
    public virtual void IsEnabled_TrueEnabled_FalseDisabled_NullMissing()
    {
        Assert.True(Shown.IsEnabled());
        Assert.False(Disabled.IsEnabled());
        Assert.Null(Missing.IsEnabled());
    }

    [Fact]
    public virtual void WaitExists_TrueWhenItHolds_FalseWhenItDoesNot()
    {
        Assert.True(Shown.WaitExists(true));
        Assert.True(Missing.WaitExists(false));
        Assert.False(Missing.WaitExists(true, ShortTimeoutMs));
    }

    [Fact]
    public virtual void WaitVisible_TrueWhenItHolds_FalseWhenItDoesNot()
    {
        Assert.True(Shown.WaitVisible(true));
        Assert.True(Hidden.WaitVisible(false));
        Assert.False(Hidden.WaitVisible(true, ShortTimeoutMs));
    }

    [Fact]
    public virtual void WaitEnabled_TrueWhenItHolds_FalseWhenItDoesNot()
    {
        Assert.True(Shown.WaitEnabled(true));
        Assert.True(Disabled.WaitEnabled(false));
        Assert.False(Disabled.WaitEnabled(true, ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertExists_PassesWhenItHolds_FailsWhenItDoesNot()
    {
        ReturnsScope(Shown.AssertExists(true));
        ReturnsScope(Missing.AssertExists(false));
        Fails(() => Missing.AssertExists(true, timeoutMs: ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertVisible_PassesWhenItHolds_FailsWhenItDoesNot()
    {
        ReturnsScope(Shown.AssertVisible(true));
        ReturnsScope(Hidden.AssertVisible(false));
        Fails(() => Hidden.AssertVisible(true, timeoutMs: ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertEnabled_PassesWhenItHolds_FailsWhenItDoesNot()
    {
        ReturnsScope(Shown.AssertEnabled(true));
        ReturnsScope(Disabled.AssertEnabled(false));
        Fails(() => Disabled.AssertEnabled(true, timeoutMs: ShortTimeoutMs));
    }

    [Fact]
    public virtual void Asserts_WithNullExpected_Skip()
    {
        ReturnsScope(Missing.AssertExists(null));
        ReturnsScope(Missing.AssertVisible(null));
        ReturnsScope(Missing.AssertEnabled(null));
    }

    [Fact]
    public virtual void GetAttribute_ReadsIt_NullWhenAbsent()
    {
        Assert.Equal(Attribute.Value, Shown.GetAttribute(Attribute.Name, null));
        Assert.Null(Shown.GetAttribute("data-no-such-attribute", ShortTimeoutMs));
        Assert.Null(Missing.GetAttribute(Attribute.Name, ShortTimeoutMs));
    }
}
