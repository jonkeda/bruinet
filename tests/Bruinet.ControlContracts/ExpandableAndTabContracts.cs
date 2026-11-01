using Bruinet.Core.Interfaces;
using Xunit;

namespace Bruinet.ControlContracts;

/// <summary><see cref="IExpandableControlObject{TScope}"/>: expanded and collapsed.</summary>
public abstract class ExpandableContract<TScope> : ControlContract<TScope> where TScope : class
{
    /// <summary>Collapsed at the start.</summary>
    protected abstract IExpandableControlObject<TScope> Expander { get; }

    [Fact]
    public virtual void IsExpanded_FalseAtTheStart() => Assert.False(Expander.IsExpanded());

    [Fact]
    public virtual void Expand_ExpandsIt_Collapse_CollapsesIt()
    {
        ReturnsScope(Expander.Expand());
        Assert.True(Expander.IsExpanded());
        ReturnsScope(Expander.Collapse());
        Assert.False(Expander.IsExpanded());
    }

    [Fact]
    public virtual void Expand_WhenExpanded_KeepsItExpanded()
    {
        Expander.Expand();
        ReturnsScope(Expander.Expand());
        Assert.True(Expander.IsExpanded());
    }

    [Fact]
    public virtual void ToggleExpanded_TurnsItOver()
    {
        ReturnsScope(Expander.ToggleExpanded());
        Assert.True(Expander.IsExpanded());
        ReturnsScope(Expander.ToggleExpanded());
        Assert.False(Expander.IsExpanded());
    }

    [Fact]
    public virtual void WaitExpanded_TrueWhenItHolds_FalseWhenItDoesNot()
    {
        Assert.True(Expander.WaitExpanded(false));
        Assert.False(Expander.WaitExpanded(true, ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertExpanded_PassesWhenItHolds_FailsWhenItDoesNot()
    {
        ReturnsScope(Expander.AssertExpanded(false));
        Fails(() => Expander.AssertExpanded(true, timeoutMs: ShortTimeoutMs));
        Expander.Expand();
        ReturnsScope(Expander.AssertExpanded(true));
    }
}

/// <summary><see cref="ITabControlObject{TScope}"/>: one tab of several selected.</summary>
public abstract class TabContract<TScope> : ControlContract<TScope> where TScope : class
{
    /// <summary>Selected at the start.</summary>
    protected abstract ITabControlObject<TScope> First { get; }

    /// <summary>Not selected at the start.</summary>
    protected abstract ITabControlObject<TScope> Second { get; }

    /// <summary>The two tabs' titles.</summary>
    protected abstract (string First, string Second) Titles { get; }

    [Fact]
    public virtual void Title_ReadsIt()
    {
        Assert.Equal(Titles.First, First.Title);
        Assert.Equal(Titles.Second, Second.Title);
    }

    [Fact]
    public virtual void IsSelected_TrueOnlyForTheSelectedTab()
    {
        Assert.True(First.IsSelected());
        Assert.False(Second.IsSelected());
    }

    [Fact]
    public virtual void Click_SelectsTheTab()
    {
        ReturnsScope(Second.Click());
        Assert.True(Second.WaitSelected(true));
        Assert.False(First.IsSelected());
    }

    [Fact]
    public virtual void WaitSelected_TrueWhenItHolds_FalseWhenItDoesNot()
    {
        Assert.True(First.WaitSelected(true));
        Assert.False(Second.WaitSelected(true, ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertSelected_PassesWhenItHolds_FailsWhenItDoesNot()
    {
        ReturnsScope(First.AssertSelected(true));
        ReturnsScope(Second.AssertSelected(false));
        Fails(() => Second.AssertSelected(true, timeoutMs: ShortTimeoutMs));
    }
}
