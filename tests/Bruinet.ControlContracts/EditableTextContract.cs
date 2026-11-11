using Bruinet.Core.Interfaces;
using Xunit;

namespace Bruinet.ControlContracts;

/// <summary><see cref="IEditableTextControlObject{TScope}"/>: text written, appended and cleared; placeholder and read-only.</summary>
public abstract class EditableTextContract<TScope> : ControlContract<TScope> where TScope : class
{
    /// <summary>Empty at the start, editable, with <see cref="Placeholder"/>.</summary>
    protected abstract IEditableTextControlObject<TScope> Field { get; }

    /// <summary>Read-only.</summary>
    protected abstract IEditableTextControlObject<TScope> ReadOnlyField { get; }

    /// <summary><see cref="Field"/>'s placeholder.</summary>
    protected abstract string Placeholder { get; }

    [Fact]
    public virtual void Enter_WritesTheText()
    {
        ReturnsScope(Field.Enter("Apple pie"));
        ReturnsScope(Field.AssertText("Apple pie"));
    }

    [Fact]
    public virtual void SetText_ReplacesTheText()
    {
        Field.SetText("first");
        ReturnsScope(Field.SetText("second"));
        ReturnsScope(Field.AssertText("second"));
    }

    [Fact]
    public virtual void Append_WritesAfterTheText()
    {
        Field.SetText("Apple");
        ReturnsScope(Field.Append(" pie"));
        ReturnsScope(Field.AssertText("Apple pie"));
    }

    [Fact]
    public virtual void Clear_EmptiesIt()
    {
        Field.SetText("Apple pie");
        ReturnsScope(Field.Clear());
        ReturnsScope(Field.AssertTextEmpty(true));
    }

    [Fact]
    public virtual void Writes_WithNullText_Skip()
    {
        Field.SetText("kept");
        ReturnsScope(Field.SetText(null));
        ReturnsScope(Field.Enter(null));
        ReturnsScope(Field.Append(null));
        ReturnsScope(Field.AssertText("kept"));
    }

    [Fact]
    public virtual void GetPlaceholder_ReadsIt() => Assert.Equal(Placeholder, Field.GetPlaceholder());

    [Fact]
    public virtual void WaitPlaceholder_TrueForIt_FalseForAnother()
    {
        Assert.True(Field.WaitPlaceholder(Placeholder));
        Assert.False(Field.WaitPlaceholder(Placeholder + "x", ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertPlaceholder_PassesForIt_FailsForAnother()
    {
        ReturnsScope(Field.AssertPlaceholder(Placeholder));
        Fails(() => Field.AssertPlaceholder(Placeholder + "x", timeoutMs: ShortTimeoutMs));
    }

    [Fact]
    public virtual void IsReadOnly_FalseEditable_TrueReadOnly()
    {
        Assert.False(Field.IsReadOnly());
        Assert.True(ReadOnlyField.IsReadOnly());
    }

    /// <summary>A read-only field refuses text, and keeps its own (review 04, finding 3).</summary>
    [Fact]
    public virtual void SetText_OnAReadOnlyField_IsRefused_AndItKeepsItsText()
    {
        var before = ReadOnlyField.GetText();

        Assert.ThrowsAny<Bruinet.Core.Exceptions.BruinetException>(() => ReadOnlyField.SetText("Changed", ShortTimeoutMs));
        Assert.Equal(before, ReadOnlyField.GetText());
    }

    [Fact]
    public virtual void WaitReadOnly_TrueWhenItHolds_FalseWhenItDoesNot()
    {
        Assert.True(ReadOnlyField.WaitReadOnly(true));
        Assert.True(Field.WaitReadOnly(false));
        Assert.False(Field.WaitReadOnly(true, ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertReadOnly_PassesWhenItHolds_FailsWhenItDoesNot()
    {
        ReturnsScope(ReadOnlyField.AssertReadOnly(true));
        ReturnsScope(Field.AssertReadOnly(false));
        Fails(() => Field.AssertReadOnly(true, timeoutMs: ShortTimeoutMs));
    }
}
