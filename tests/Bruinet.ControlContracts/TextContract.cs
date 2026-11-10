using Bruinet.Core.Interfaces;
using Xunit;

namespace Bruinet.ControlContracts;

/// <summary><see cref="ITextControlObject{TScope}"/>: a control's text, read and asserted.</summary>
public abstract class TextContract<TScope> : ControlContract<TScope> where TScope : class
{
    /// <summary>Shows <see cref="Text"/>.</summary>
    protected abstract ITextControlObject<TScope> Label { get; }

    /// <summary>Shows no text.</summary>
    protected abstract ITextControlObject<TScope> EmptyLabel { get; }

    /// <summary>What <see cref="Label"/> shows: at least six characters, so it has a start, a middle and an end.</summary>
    protected abstract string Text { get; }

    private string Start => Text[..3];

    private string Middle => Text[1..^1];

    private string End => Text[^3..];

    [Fact]
    public virtual void WaitText_TrueForItsText_FalseForAnother()
    {
        Assert.True(Label.WaitText(Text));
        Assert.False(Label.WaitText(Text + "x", ShortTimeoutMs));
    }

    [Fact]
    public virtual void WaitTextContains_TrueForAPart_FalseForAnother()
    {
        Assert.True(Label.WaitTextContains(Middle));
        Assert.False(Label.WaitTextContains("no such part", ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertText_PassesForItsText_FailsForAnother()
    {
        ReturnsScope(Label.AssertText(Text));
        Fails(() => Label.AssertText(Text + "x", timeoutMs: ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertTextContains_PassesForAPart_FailsForAnother()
    {
        ReturnsScope(Label.AssertTextContains(Middle));
        Fails(() => Label.AssertTextContains("no such part", timeoutMs: ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertTextStartsWith_PassesForItsStart_FailsForItsEnd()
    {
        ReturnsScope(Label.AssertTextStartsWith(Start));
        Fails(() => Label.AssertTextStartsWith(End, timeoutMs: ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertTextEndsWith_PassesForItsEnd_FailsForItsStart()
    {
        ReturnsScope(Label.AssertTextEndsWith(End));
        Fails(() => Label.AssertTextEndsWith(Start, timeoutMs: ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertTextEmpty_PassesWhenItHolds_FailsWhenItDoesNot()
    {
        ReturnsScope(EmptyLabel.AssertTextEmpty(true));
        ReturnsScope(Label.AssertTextEmpty(false));
        Fails(() => Label.AssertTextEmpty(true, timeoutMs: ShortTimeoutMs));
        ReturnsScope(Label.AssertTextEmpty(null));
    }
}
