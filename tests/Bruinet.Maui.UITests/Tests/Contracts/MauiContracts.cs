using Bruinet.ControlContracts;
using Bruinet.Core.Interfaces;
using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Contracts;

// The control contracts on Bruinet.Maui's controls, on the sample app's pages: each test opens its page afresh, so
// the controls start as the page's view model sets them. Every binding runs on every head; a test a head cannot
// keep says so with [SkipOnFact] and why, and one no MAUI head can keep with [Fact(Skip)] (review 04, finding 5).

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

    private const string NotOnAndroid = "the Android head cannot tell a read-only Entry (R2-T116), so it neither reads it nor refuses a write to it.";
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void IsReadOnly_FalseEditable_TrueReadOnly() => base.IsReadOnly_FalseEditable_TrueReadOnly();
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void WaitReadOnly_TrueWhenItHolds_FalseWhenItDoesNot() => base.WaitReadOnly_TrueWhenItHolds_FalseWhenItDoesNot();
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void AssertReadOnly_PassesWhenItHolds_FailsWhenItDoesNot() => base.AssertReadOnly_PassesWhenItHolds_FailsWhenItDoesNot();
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void SetText_OnAReadOnlyField_IsRefused_AndItKeepsItsText() => base.SetText_OnAReadOnlyField_IsRefused_AndItKeepsItsText();
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

    private const string NotOnAndroid = "Appium cannot drop focus on Android; ClearFocus moves it on with Tab (R2-T116).";
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void Blur_TakesTheFocusAway() => base.Blur_TakesTheFocusAway();
}

[Collection("Maui")]
[Trait("Category", "Contract")]
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

    private const string NotOnAndroid = "the Android head reads no Picker's items or selection (R2-T116).";
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void GetItemCount_CountsThem() => base.GetItemCount_CountsThem();
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void GetItemTexts_ReadsThemInOrder() => base.GetItemTexts_ReadsThemInOrder();
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void GetSelected_ReadsTheSelection() => base.GetSelected_ReadsTheSelection();
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void SelectByText_SelectsThatItem() => base.SelectByText_SelectsThatItem();
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void SelectByValue_SelectsThatItem() => base.SelectByValue_SelectsThatItem();
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void Selects_WithNull_Skip() => base.Selects_WithNull_Skip();
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void WaitItemCount_TrueForTheCount_FalseForAnother() => base.WaitItemCount_TrueForTheCount_FalseForAnother();
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void AssertItemCount_PassesForTheCount_FailsForAnother() => base.AssertItemCount_PassesForTheCount_FailsForAnother();
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void WaitSelected_TrueForTheSelection_FalseForAnother() => base.WaitSelected_TrueForTheSelection_FalseForAnother();
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void AssertSelected_PassesForTheSelection_FailsForAnother() => base.AssertSelected_PassesForTheSelection_FailsForAnother();
}

[Collection("Maui")]
[Trait("Category", "Contract")]
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

    // A button's name is its text on every head (Windows answers a fixed list of names, R2-T50).
    protected override (string Name, string Value) Attribute => ("Name", "Contract");

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

    [Fact(Skip = "MAUI's Button raises no right click the sample can record, and MauiHeadless refuses one (DeviceOnlyException).")]
    public override void RightClick_RightClicksIt()
    {
    }

    [Fact(Skip = "MAUI's Button raises no hover the sample can record, and MauiHeadless refuses one (DeviceOnlyException).")]
    public override void Hover_HoversIt()
    {
    }

    [Fact(Skip = "MAUI has no long-press recognizer, and the sample app names no long-press sink.")]
    public override void LongPress_PressesIt()
    {
    }

    private const string NotOnAndroid = "a MAUI Button raises no double tap on Android without a recognizer, and a recognizer swallows its clicks there.";
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void DoubleClick_DoubleClicksIt() => base.DoubleClick_DoubleClicksIt();
}

[Collection("Maui")]
[Trait("Category", "Contract")]
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

    private const string NotOnAndroid = "the Android head reads no ProgressBar's progress (R2-T116).";
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void GetProgress_ReadsTheFraction() => base.GetProgress_ReadsTheFraction();
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void WaitProgress_TrueForTheFraction_FalseForAnother() => base.WaitProgress_TrueForTheFraction_FalseForAnother();
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void AssertProgress_PassesForTheFraction_FailsForAnother() => base.AssertProgress_PassesForTheFraction_FailsForAnother();
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void WaitComplete_TrueAtOne_FalseBefore() => base.WaitComplete_TrueAtOne_FalseBefore();
    [SkipOnFact(ContractHeads.Mobile, NotOnAndroid)]
    public override void AssertComplete_PassesAtOne_FailsBefore() => base.AssertComplete_PassesAtOne_FailsBefore();
}

[Collection("Maui")]
[Trait("Category", "Contract")]
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

[Collection("Maui")]
[Trait("Category", "Contract")]
public sealed class MauiSwipeViewContract : SwipeableContract<SwipeView<ContainerTestPage>>
{
    private readonly ContainerTestPage _page;
    private readonly SwipeView<ContainerTestPage> _view;

    public MauiSwipeViewContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Container);
        _page = new ContainerTestPage(fixture.Context);
        _view = new SwipeView<ContainerTestPage>(_page, "ContractSwipeView");
    }

    // A container is its own scope: its actions return it.
    protected override SwipeView<ContainerTestPage> Scope => _view;

    protected override ISwipeableControlObject<SwipeView<ContainerTestPage>> Card => _view;

    protected override string? LastSwipe() => _page.ContractSwipeDirection.GetText() is { Length: > 0 } direction ? direction : null;

    protected override (int StartX, int StartY, int EndX, int EndY) LeftwardPoints => (200, 20, 20, 20);

    private const string SwipeNotOnWindows =
        "a SwipeView is not addressable on Windows (its WinUI peer cannot be overridden); drive it by id through IMauiDriver.PerformGesture.";

    [SkipOnFact(ContractHeads.Headless | ContractHeads.Windows, "a swipe between points is a device's: MauiHeadless refuses it, and Windows cannot reach a SwipeView.")]
    public override void Swipe_BetweenPoints_SwipesThatWay() => base.Swipe_BetweenPoints_SwipesThatWay();

    [SkipOnFact(ContractHeads.Windows, SwipeNotOnWindows)]
    public override void SwipeLeft_SwipesLeft() => base.SwipeLeft_SwipesLeft();

    [SkipOnFact(ContractHeads.Windows, SwipeNotOnWindows)]
    public override void SwipeRight_SwipesRight() => base.SwipeRight_SwipesRight();

    [SkipOnFact(ContractHeads.Windows | ContractHeads.Mobile, SwipeNotOnWindows + " On Android, " + NotOnAndroid)]
    public override void SwipeUp_SwipesUp() => base.SwipeUp_SwipesUp();

    [SkipOnFact(ContractHeads.Windows | ContractHeads.Mobile, SwipeNotOnWindows + " On Android, " + NotOnAndroid)]
    public override void SwipeDown_SwipesDown() => base.SwipeDown_SwipesDown();

    private const string NotOnAndroid = "a vertical swipe on Android does not open a SwipeView's top or bottom items (R2-T116).";
}

[Collection("Maui")]
[Trait("Category", "Contract")]
public sealed class MauiRefreshViewContract : RefreshableContract<RefreshView<ContainerTestPage>>
{
    private readonly ContainerTestPage _page;
    private readonly RefreshView<ContainerTestPage> _view;

    public MauiRefreshViewContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Container);
        _page = new ContainerTestPage(fixture.Context);
        _view = new RefreshView<ContainerTestPage>(_page, "TestRefreshView");
    }

    protected override RefreshView<ContainerTestPage> Scope => _view;

    protected override IRefreshableControlObject<RefreshView<ContainerTestPage>> View => _view;

    // ContainerViewModel says "refreshed N" after N refreshes, "not refreshed" before.
    protected override int Refreshes()
        => _page.RefreshText.GetText() is { } text && text.StartsWith("refreshed ", StringComparison.Ordinal)
            ? int.Parse(text["refreshed ".Length..], System.Globalization.CultureInfo.InvariantCulture)
            : 0;

    private const string RefreshNotOnAndroid = "a pull does not reach the RefreshView's command and its state is not read (R2-T116).";

    private const string RefreshNotOnWindows =
        "a RefreshView is not addressable on Windows (its WinUI peer cannot be overridden), so its state cannot be read; pull it by id through IMauiDriver.PerformGesture.";

    [SkipOnFact(ContractHeads.Windows, RefreshNotOnWindows)]
    public override void IsRefreshing_FalseAtTheStart() => base.IsRefreshing_FalseAtTheStart();

    [SkipOnFact(ContractHeads.Windows | ContractHeads.Mobile, RefreshNotOnWindows + " On Android, " + RefreshNotOnAndroid)]
    public override void PullToRefresh_RefreshesIt() => base.PullToRefresh_RefreshesIt();

    [SkipOnFact(ContractHeads.Windows | ContractHeads.Mobile, RefreshNotOnWindows + " On Android, " + RefreshNotOnAndroid)]
    public override void WaitRefreshing_TrueWhenItHolds_FalseWhenItDoesNot() => base.WaitRefreshing_TrueWhenItHolds_FalseWhenItDoesNot();

    [SkipOnFact(ContractHeads.Windows | ContractHeads.Mobile, RefreshNotOnWindows + " On Android, " + RefreshNotOnAndroid)]
    public override void AssertRefreshing_PassesWhenItHolds_FailsWhenItDoesNot() => base.AssertRefreshing_PassesWhenItHolds_FailsWhenItDoesNot();
}

[Collection("Maui")]
[Trait("Category", "Contract")]
public sealed class MauiScrollViewContract : ScrollableContract<ScrollView<ContainerTestPage>>
{
    private readonly ScrollView<ContainerTestPage> _view;

    public MauiScrollViewContract(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Container);
        _view = new ContainerTestPage(fixture.Context).TestScrollView;

        // The fixture keeps the app between tests: each starts at the top, where a head can scroll.
        if ((SkipOnFactAttribute.Current & NoRead) == 0)
        {
            _view.ScrollToTop();
        }
    }

    protected override ScrollView<ContainerTestPage> Scope => _view;

    protected override IScrollableControlObject<ScrollView<ContainerTestPage>> View => _view;

    protected override Locator LastItem => Locator.ByAutomationId("ScrollLastLabel");

    private const ContractHeads NoRead = ContractHeads.Headless | ContractHeads.Mobile;

    private const string NoReadWhy =
        "MauiHeadless lays nothing out, so a scroller has no position; Android reads no scroll position yet (R2-T116).";

    [SkipOnFact(NoRead, NoReadWhy)]
    public override void GetScrollPosition_ZeroAtTheTop() => base.GetScrollPosition_ZeroAtTheTop();

    [SkipOnFact(NoRead, NoReadWhy)]
    public override void CanScroll_DownFromTheTop_NotUp() => base.CanScroll_DownFromTheTop_NotUp();

    [SkipOnFact(NoRead, NoReadWhy)]
    public override void ScrollToEnd_GoesToTheEnd_ScrollToTop_Back() => base.ScrollToEnd_GoesToTheEnd_ScrollToTop_Back();

    [SkipOnFact(NoRead, NoReadWhy)]
    public override void SetScrollPosition_GoesThere() => base.SetScrollPosition_GoesThere();

    [SkipOnFact(NoRead, NoReadWhy)]
    public override void ScrollBy_MovesDown() => base.ScrollBy_MovesDown();

    [SkipOnFact(NoRead, NoReadWhy)]
    public override void ScrollTo_MovesToTheItem() => base.ScrollTo_MovesToTheItem();

    [SkipOnFact(NoRead, NoReadWhy)]
    public override void WaitScrollPosition_TrueThere_FalseElsewhere() => base.WaitScrollPosition_TrueThere_FalseElsewhere();

    [SkipOnFact(NoRead, NoReadWhy)]
    public override void AssertScrollPosition_PassesThere_FailsElsewhere() => base.AssertScrollPosition_PassesThere_FailsElsewhere();
}
