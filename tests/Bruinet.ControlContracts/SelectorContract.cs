using Bruinet.Core.Interfaces;
using Xunit;

namespace Bruinet.ControlContracts;

/// <summary>A selector's items, their values, and which is selected at the start; at least two items.</summary>
public sealed record SelectorValues(IReadOnlyList<string> Texts, IReadOnlyList<string> Values, int InitialIndex);

/// <summary><see cref="ISelectorControlObject{TScope}"/>: one item of a list selected by text, index or value.</summary>
public abstract class SelectorContract<TScope> : ControlContract<TScope> where TScope : class
{
    /// <summary>A drop-down or the like with <see cref="Items"/>.</summary>
    protected abstract ISelectorControlObject<TScope> List { get; }

    /// <summary>What <see cref="List"/> holds.</summary>
    protected abstract SelectorValues Items { get; }

    /// <summary>An item other than the one selected at the start.</summary>
    private int Other => Items.InitialIndex == Items.Texts.Count - 1 ? 0 : Items.Texts.Count - 1;

    [Fact]
    public virtual void GetItemTexts_ReadsThemInOrder() => Assert.Equal(Items.Texts, List.GetItemTexts());

    [Fact]
    public virtual void GetItemCount_CountsThem() => Assert.Equal(Items.Texts.Count, List.GetItemCount());

    [Fact]
    public virtual void WaitItemCount_TrueForTheCount_FalseForAnother()
    {
        Assert.True(List.WaitItemCount(Items.Texts.Count));
        Assert.False(List.WaitItemCount(Items.Texts.Count + 1, ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertItemCount_PassesForTheCount_FailsForAnother()
    {
        ReturnsScope(List.AssertItemCount(Items.Texts.Count));
        Fails(() => List.AssertItemCount(Items.Texts.Count + 1, timeoutMs: ShortTimeoutMs));
    }

    [Fact]
    public virtual void GetSelected_ReadsTheSelection()
    {
        Assert.Equal(Items.InitialIndex, List.GetSelectedIndex());
        Assert.Equal(Items.Texts[Items.InitialIndex], List.GetSelectedText());
    }

    [Fact]
    public virtual void SelectByText_SelectsThatItem()
    {
        ReturnsScope(List.SelectByText(Items.Texts[Other]));
        Assert.Equal(Other, List.GetSelectedIndex());
        Assert.Equal(Items.Texts[Other], List.GetSelectedText());
    }

    [Fact]
    public virtual void SelectByIndex_SelectsThatItem()
    {
        ReturnsScope(List.SelectByIndex(Other));
        Assert.Equal(Items.Texts[Other], List.GetSelectedText());
    }

    [Fact]
    public virtual void SelectByValue_SelectsThatItem()
    {
        ReturnsScope(List.SelectByValue(Items.Values[Other]));
        Assert.Equal(Other, List.GetSelectedIndex());
    }

    [Fact]
    public virtual void Selects_WithNull_Skip()
    {
        ReturnsScope(List.SelectByText(null));
        ReturnsScope(List.SelectByIndex(null));
        ReturnsScope(List.SelectByValue(null));
        Assert.Equal(Items.InitialIndex, List.GetSelectedIndex());
    }

    [Fact]
    public virtual void WaitSelected_TrueForTheSelection_FalseForAnother()
    {
        Assert.True(List.WaitSelectedText(Items.Texts[Items.InitialIndex]));
        Assert.True(List.WaitSelectedIndex(Items.InitialIndex));
        Assert.False(List.WaitSelectedText(Items.Texts[Other], ShortTimeoutMs));
        Assert.False(List.WaitSelectedIndex(Other, ShortTimeoutMs));
    }

    [Fact]
    public virtual void AssertSelected_PassesForTheSelection_FailsForAnother()
    {
        ReturnsScope(List.AssertSelectedText(Items.Texts[Items.InitialIndex]));
        ReturnsScope(List.AssertSelectedIndex(Items.InitialIndex));
        Fails(() => List.AssertSelectedText(Items.Texts[Other], timeoutMs: ShortTimeoutMs));
        Fails(() => List.AssertSelectedIndex(Other, timeoutMs: ShortTimeoutMs));
    }
}
