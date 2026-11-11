using Bruinet.ControlContracts;
using Bruinet.Core.Interfaces;
using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Contracts;

// The control contracts on Bruinet.Maui's controls, on the sample app's pages: each test opens its page afresh, so
// the controls start as the page's view model sets them. Run on MauiHeadless only so far: a run on Windows and
// Android is still to say which tests those heads keep (a slider's step may differ); --filter "Head!=MauiHeadless"
// leaves them out of a device run until then.

[Collection("Maui")]
[Trait("Category", "Contract")]
[Trait("Head", "MauiHeadless")]
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
[Trait("Head", "MauiHeadless")]
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
[Trait("Head", "MauiHeadless")]
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
[Trait("Head", "MauiHeadless")]
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
[Trait("Head", "MauiHeadless")]
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
[Trait("Head", "MauiHeadless")]
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
[Trait("Head", "MauiHeadless")]
public sealed class MauiPickerContract : SelectorContract<SelectionTestPage>
{
    private readonly SelectionTestPage _page;

    // SelectionViewModel starts with the second item chosen.
    public MauiPickerContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Selection);
        _page = new SelectionTestPage(fixture.Context);
    }

    protected override SelectionTestPage Scope => _page;

    protected override ISelectorControlObject<SelectionTestPage> List => _page.TestPicker;

    // A picker's items are texts: each item's value is its text.
    protected override SelectorValues Items => new(Texts, Texts, InitialIndex: 1);

    private static string[] Texts => ["Option 1", "Option 2", "Option 3", "Option 4", "Option 5"];
}

[Collection("Maui")]
[Trait("Category", "Contract")]
[Trait("Head", "MauiHeadless")]
public sealed class MauiElementContract : ElementContract<ButtonsTestPage>
{
    private readonly ButtonsTestPage _page;

    public MauiElementContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Buttons);
        _page = new ButtonsTestPage(fixture.Context);
    }

    protected override ButtonsTestPage Scope => _page;

    protected override IControlObject<ButtonsTestPage> Shown => _page.ContractButton;

    protected override IControlObject<ButtonsTestPage> Hidden => _page.HiddenButton;

    protected override IControlObject<ButtonsTestPage> Disabled => _page.DisabledButton;

    protected override IControlObject<ButtonsTestPage> Missing => _page.MissingButton;

    // A bindable property by its name, as MAUI's elements answer GetAttribute.
    protected override (string Name, string Value) Attribute => ("Text", "Contract");

    private const string HiddenIsGone =
        "MAUI leaves a view with IsVisible false out of the tree on Windows, Android and iOS, and MauiHeadless does the same: "
        + "a hidden control is not on the page, so ask AssertExists(false) (D1: a wait or an assert on a gone control fails by name).";

    [Fact(Skip = HiddenIsGone)]
    public override void IsExists_TrueOnThePage_FalseOffIt()
    {
    }

    [Fact(Skip = HiddenIsGone)]
    public override void WaitVisible_TrueWhenItHolds_FalseWhenItDoesNot()
    {
    }

    [Fact(Skip = HiddenIsGone)]
    public override void AssertVisible_PassesWhenItHolds_FailsWhenItDoesNot()
    {
    }
}

[Collection("Maui")]
[Trait("Category", "Contract")]
[Trait("Head", "MauiHeadless")]
public sealed class MauiButtonContract : ClickableContract<ButtonsTestPage>
{
    private readonly ButtonsTestPage _page;

    public MauiButtonContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Buttons);
        _page = new ButtonsTestPage(fixture.Context);
    }

    protected override ButtonsTestPage Scope => _page;

    protected override IClickableControlObject<ButtonsTestPage> Button => _page.ContractButton;

    protected override IClickableControlObject<ButtonsTestPage> DisabledButton => _page.DisabledButton;

    protected override string? LastAction() => _page.ContractAction.GetText() is { Length: > 0 } action ? action : null;

    [Fact(Skip = "A pointer's right button is a device's: MauiHeadless refuses it (DeviceOnlyException).")]
    public override void RightClick_RightClicksIt()
    {
    }

    [Fact(Skip = "Hover is a device's: MauiHeadless refuses it (DeviceOnlyException).")]
    public override void Hover_HoversIt()
    {
    }

    [Fact(Skip = "MAUI has no long-press recognizer, and the sample app names no long-press sink.")]
    public override void LongPress_PressesIt()
    {
    }
}

[Collection("Maui")]
[Trait("Category", "Contract")]
[Trait("Head", "MauiHeadless")]
public sealed class MauiLabelContract : TextContract<ButtonsTestPage>
{
    private readonly ButtonsTestPage _page;

    public MauiLabelContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Buttons);
        _page = new ButtonsTestPage(fixture.Context);
    }

    protected override ButtonsTestPage Scope => _page;

    protected override ITextControlObject<ButtonsTestPage> Label => _page.ContractLabel;

    protected override ITextControlObject<ButtonsTestPage> EmptyLabel => _page.EmptyLabel;

    protected override string Text => "Contract label";
}

[Collection("Maui")]
[Trait("Category", "Contract")]
[Trait("Head", "MauiHeadless")]
public sealed class MauiDatePickerContract : DateContract<DateTimeTestPage>
{
    private readonly DateTimeTestPage _page;

    // The sample allows today to 30 days on; the contract starts the picker two days on, as it cannot know today.
    public MauiDatePickerContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.DateTime);
        _page = new DateTimeTestPage(fixture.Context);
        _page.TestDatePicker.SetDate(Initial);
    }

    protected override DateTimeTestPage Scope => _page;

    protected override IDateControlObject<DateTimeTestPage> Picker => _page.TestDatePicker;

    protected override DateTime Initial => DateTime.Today.AddDays(2);

    protected override DateTime Target => DateTime.Today.AddDays(9);
}

[Collection("Maui")]
[Trait("Category", "Contract")]
[Trait("Head", "MauiHeadless")]
public sealed class MauiTimePickerContract : TimeContract<DateTimeTestPage>
{
    private readonly DateTimeTestPage _page;

    // The sample starts at the time it opened; the contract starts the picker at a time of its own.
    public MauiTimePickerContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.DateTime);
        _page = new DateTimeTestPage(fixture.Context);
        _page.TestTimePicker.SetTime(Initial);
    }

    protected override DateTimeTestPage Scope => _page;

    protected override ITimeControlObject<DateTimeTestPage> Picker => _page.TestTimePicker;

    protected override TimeSpan Initial => new(9, 15, 0);
}

[Collection("Maui")]
[Trait("Category", "Contract")]
[Trait("Head", "MauiHeadless")]
public sealed class MauiProgressBarContract : ProgressContract<DisplayTestPage>
{
    private readonly DisplayTestPage _page;

    public MauiProgressBarContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Display);
        _page = new DisplayTestPage(fixture.Context);
    }

    protected override DisplayTestPage Scope => _page;

    protected override IProgressControlObject<DisplayTestPage> Partial => _page.TestProgressBar;

    protected override IProgressControlObject<DisplayTestPage> Complete => _page.CompleteProgressBar;

    // MAUI's ProgressBar is never indeterminate; its test skips.
    protected override IProgressControlObject<DisplayTestPage> Indeterminate => _page.TestProgressBar;

    // DisplayViewModel starts at 50 of 100.
    protected override double Fraction => 0.5;

    [Fact(Skip = "MAUI's ProgressBar always shows a value; its indeterminate progress is an ActivityIndicator, which is not a progress control.")]
    public override void IsIndeterminate_TrueOnlyWhenIndeterminate()
    {
    }
}

[Collection("Maui")]
[Trait("Category", "Contract")]
[Trait("Head", "MauiHeadless")]
public sealed class MauiExpanderContract : ExpandableContract<CommunityToolkitTestPage>
{
    private readonly CommunityToolkitTestPage _page;

    public MauiExpanderContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.CommunityToolkit);
        _page = new CommunityToolkitTestPage(fixture.Context);
    }

    protected override CommunityToolkitTestPage Scope => _page;

    protected override IExpandableControlObject<CommunityToolkitTestPage> Expander => _page.TestExpander;
}
