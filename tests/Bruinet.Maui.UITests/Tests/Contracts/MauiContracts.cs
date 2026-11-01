using Bruinet.ControlContracts;
using Bruinet.Core.Interfaces;
using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Contracts;

// The control contracts on Bruinet.Maui's controls (.my/coverage/plan.md, C5), on the sample app's pages: each
// test opens its page afresh, so the controls start as the page's view model sets them. Run on any MAUI head;
// MauiHeadless is the one CI runs. A test a head cannot keep is overridden with Skip and why.

[Collection("Maui")]
[Trait("Category", "Contract")]
public sealed class MauiSliderContract : RangeContract<RangeTestPage>
{
    private readonly RangeTestPage _page;

    public MauiSliderContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Range);
        _page = new RangeTestPage(fixture.Context);
    }

    protected override RangeTestPage Scope => _page;

    protected override IRangeControlObject<RangeTestPage> Slider => _page.TestSlider;

    // RangeViewModel: 0 to 100, at 50; a slider's step is 1.
    protected override RangeValues Values => new(Minimum: 0, Maximum: 100, Step: 1, Initial: 50, Target: 70);
}

[Collection("Maui")]
[Trait("Category", "Contract")]
public sealed class MauiStepperContract : RangeContract<RangeTestPage>
{
    private readonly RangeTestPage _page;

    public MauiStepperContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Range);
        _page = new RangeTestPage(fixture.Context);
    }

    protected override RangeTestPage Scope => _page;

    protected override IRangeControlObject<RangeTestPage> Slider => _page.TestStepper;

    // RangeViewModel: 0 to 10 by 1, at 5.
    protected override RangeValues Values => new(Minimum: 0, Maximum: 10, Step: 1, Initial: 5, Target: 7);
}

[Collection("Maui")]
[Trait("Category", "Contract")]
public sealed class MauiCheckBoxContract : ToggleContract<ToggleTestPage>
{
    private readonly ToggleTestPage _page;

    public MauiCheckBoxContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Toggle);
        _page = new ToggleTestPage(fixture.Context);
    }

    protected override ToggleTestPage Scope => _page;

    protected override IToggleControlObject<ToggleTestPage> Box => _page.TestCheckBox;
}

[Collection("Maui")]
[Trait("Category", "Contract")]
public sealed class MauiSwitchContract : ToggleContract<ToggleTestPage>
{
    private readonly ToggleTestPage _page;

    public MauiSwitchContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Toggle);
        _page = new ToggleTestPage(fixture.Context);
    }

    protected override ToggleTestPage Scope => _page;

    protected override IToggleControlObject<ToggleTestPage> Box => _page.TestSwitch;
}

[Collection("Maui")]
[Trait("Category", "Contract")]
public sealed class MauiEntryContract : EditableTextContract<TextTestPage>
{
    private readonly TextTestPage _page;

    public MauiEntryContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Text);
        _page = new TextTestPage(fixture.Context);
    }

    protected override TextTestPage Scope => _page;

    protected override IEditableTextControlObject<TextTestPage> Field => _page.TestEntry;

    protected override IEditableTextControlObject<TextTestPage> ReadOnlyField => _page.ReadOnlyEntry;

    protected override string Placeholder => "Enter text here";
}

[Collection("Maui")]
[Trait("Category", "Contract")]
public sealed class MauiEntryFocusContract : FocusableContract<TextTestPage>
{
    private readonly TextTestPage _page;

    public MauiEntryFocusContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Text);
        _page = new TextTestPage(fixture.Context);
    }

    protected override TextTestPage Scope => _page;

    protected override IFocusableControlObject<TextTestPage> Field => _page.TestEntry;
}

[Collection("Maui")]
[Trait("Category", "Contract")]
public sealed class MauiPickerContract : SelectorContract<SelectionTestPage>
{
    private readonly SelectionTestPage _page;

    // The picker starts with nothing chosen; the contract starts from a choice, so the second item is chosen first.
    public MauiPickerContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Selection);
        _page = new SelectionTestPage(fixture.Context);
        _page.TestPicker.SelectByIndex(1);
    }

    protected override SelectionTestPage Scope => _page;

    protected override ISelectorControlObject<SelectionTestPage> List => _page.TestPicker;

    // A picker's items are texts: each item's value is its text.
    protected override SelectorValues Items => new(Texts, Texts, InitialIndex: 1);

    private static string[] Texts => ["Option 1", "Option 2", "Option 3", "Option 4", "Option 5"];
}
