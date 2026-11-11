using Xunit;
using Bruinet.ControlContracts.Html.Pages;
using Bruinet.Html.Controls.Range;
using Bruinet.Html.Controls.Selection;

namespace Bruinet.ControlContracts.Html;

// Html's selector and range controls at their edges, beyond what the contracts ask: what mutation testing of
// SelectorControlBase and RangeControlBase found no test for (an index just out of range, an option with no value,
// the first option, a value at the tolerance's edge). Bound on both heads, as the contracts are (review 04, finding 13).
public abstract class HtmlSelectorEdgeBinding<THead> : IAsyncLifetime where THead : IContractHead, new()
{
    private readonly ContractSession<THead> _session = new(typeof(SelectorControls));

    public Task InitializeAsync() => _session.OpenAsync();

    public Task DisposeAsync() => _session.DisposeAsync().AsTask();

    private SelectControl<ContractPage> List => new(_session.Page, ContractSession<THead>.Selector("list"));

    private SelectControl<ContractPage> Plain => new(_session.Page, ContractSession<THead>.Selector("plain"));

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void AnIndexJustOutOfRange_Throws(int index)
        => Assert.Throws<ArgumentOutOfRangeException>(() => List.SelectByIndex(index));

    [Fact]
    public void TheFirstOption_IsIndexZero()
    {
        List.SelectByIndex(0);

        Assert.Equal((0, "Apple"), (List.GetSelectedIndex(), List.GetSelectedText()));
    }

    [Fact]
    public void AnOptionWithNoValue_IsChosenByItsText()
    {
        Plain.SelectByText("Plum");
        Assert.Equal("Plum", Plain.GetSelectedText());

        Plain.SelectByIndex(0);
        Assert.Equal("Pear", Plain.GetSelectedText());
    }

    [Fact]
    public void AnUnknownText_SaysItWasNotFound()
        => Assert.Contains("was not found", Assert.Throws<InvalidOperationException>(() => List.SelectByText("Durian")).Message);
}

public abstract class HtmlRangeEdgeBinding<THead> : IAsyncLifetime where THead : IContractHead, new()
{
    private readonly ContractSession<THead> _session = new(typeof(RangeControls));

    public Task InitializeAsync() => _session.OpenAsync();

    public Task DisposeAsync() => _session.DisposeAsync().AsTask();

    private RangeInputControl<ContractPage> Slider => new(_session.Page, ContractSession<THead>.Selector("slider"));

    // The slider is at 2: 3 is within 1 of it, at the edge.
    [Fact]
    public void AValueAtTheToleranceEdge_IsWithin()
    {
        Assert.True(Slider.WaitValueWithin(3, 1));
        Slider.AssertValueWithin(3, 1);
    }
}
