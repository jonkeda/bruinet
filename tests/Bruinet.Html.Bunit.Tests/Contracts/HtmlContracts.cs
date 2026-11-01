using Bruinet.ControlContracts;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Html.Bunit.Tests.Components.Contracts;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Range;
using Bruinet.Html.Controls.Selection;
using Bruinet.Html.Controls.Text;
using Bruinet.Html.Controls.Toggle;

namespace Bruinet.Html.Bunit.Tests.Contracts;

// The control contracts (.my/coverage/plan.md, C2) on the bUnit head: each class renders its
// component, and the contract's tests run against Bruinet.Html's controls on it.

/// <summary>The page the contracts' controls are on.</summary>
public sealed class ContractPage(IHtmlTestContext context) : BlazorPageObjectBase<ContractPage>(context);

/// <summary>A component rendered on the bUnit head, with the page over it.</summary>
internal sealed class ContractHost<TComponent> : IDisposable
{
    private readonly BunitTestContext _context = new(new BunitTestContextOptions { RootComponent = typeof(TComponent), BaseUrl = "http://localhost/" });

    public ContractHost() => Page = new ContractPage(_context);

    public ContractPage Page { get; }

    public string Selector(string testId) => $"[data-testid='{testId}']";

    /// <summary>An element's text, null when it shows none.</summary>
    public string? Text(string testId) => _context.FindElement(Locator.ByDataTestId(testId)).Text is { Length: > 0 } text ? text : null;

    public void Dispose() => _context.Dispose();
}

public sealed class HtmlElementContract : ElementContract<ContractPage>, IDisposable
{
    private readonly ContractHost<ElementControls> _host = new();

    protected override ContractPage Scope => _host.Page;

    protected override IControlObject<ContractPage> Shown => new ButtonControl<ContractPage>(_host.Page, _host.Selector("shown"));

    protected override IControlObject<ContractPage> Hidden => new ButtonControl<ContractPage>(_host.Page, _host.Selector("hidden"));

    protected override IControlObject<ContractPage> Disabled => new ButtonControl<ContractPage>(_host.Page, _host.Selector("disabled"));

    protected override IControlObject<ContractPage> Missing => new ButtonControl<ContractPage>(_host.Page, _host.Selector("missing"));

    protected override (string Name, string Value) Attribute => ("title", "Apple pie");

    [Fact(Skip = MissingDiffers)]
    public override void IsVisible_TrueShown_FalseHidden_NullMissing() => base.IsVisible_TrueShown_FalseHidden_NullMissing();

    [Fact(Skip = MissingDiffers)]
    public override void IsEnabled_TrueEnabled_FalseDisabled_NullMissing() => base.IsEnabled_TrueEnabled_FalseDisabled_NullMissing();

    [Fact(Skip = MissingDiffers)]
    public override void GetAttribute_ReadsIt_NullWhenAbsent() => base.GetAttribute_ReadsIt_NullWhenAbsent();

    /// <summary>Open in .my/coverage/plan.md: the docs or the generated controls change.</summary>
    private const string MissingDiffers = "A missing element: Core's docs say null; Html's generated controls answer false "
        + "(IsVisible, IsEnabled) and throw (GetAttribute), as Maui's do; Wpf and WinForms answer null.";

    public void Dispose() => _host.Dispose();
}

public sealed class HtmlClickableContract : ClickableContract<ContractPage>, IDisposable
{
    private const string NoPointer = "bUnit has no pointer: BrowserOnlyException; Chromium runs it (C3).";

    private readonly ContractHost<ClickControls> _host = new();

    protected override ContractPage Scope => _host.Page;

    protected override IClickableControlObject<ContractPage> Button => new ButtonControl<ContractPage>(_host.Page, _host.Selector("button"));

    protected override IClickableControlObject<ContractPage> DisabledButton => new ButtonControl<ContractPage>(_host.Page, _host.Selector("disabled"));

    protected override string? LastAction() => _host.Text("last");

    [Fact(Skip = NoPointer)]
    public override void Hover_HoversIt() => base.Hover_HoversIt();

    [Fact(Skip = NoPointer)]
    public override void LongPress_PressesIt() => base.LongPress_PressesIt();

    public void Dispose() => _host.Dispose();
}

public sealed class HtmlFocusableContract : FocusableContract<ContractPage>, IDisposable
{
    private readonly ContractHost<TextControls> _host = new();

    protected override ContractPage Scope => _host.Page;

    protected override IFocusableControlObject<ContractPage> Field => new TextInputControl<ContractPage>(_host.Page, _host.Selector("field"));

    private const string NoScript = "bUnit has no script to read the focus with: BrowserOnlyException; Chromium runs it (C3).";

    [Fact(Skip = NoScript)]
    public override void IsFocused_FalseAtTheStart() => base.IsFocused_FalseAtTheStart();

    [Fact(Skip = NoScript)]
    public override void Focus_GivesItTheFocus() => base.Focus_GivesItTheFocus();

    [Fact(Skip = NoScript)]
    public override void Blur_TakesTheFocusAway() => base.Blur_TakesTheFocusAway();

    [Fact(Skip = NoScript)]
    public override void WaitFocused_FalseWhenItDoesNotHold() => base.WaitFocused_FalseWhenItDoesNotHold();

    [Fact(Skip = NoScript)]
    public override void AssertFocused_PassesWhenItHolds_FailsWhenItDoesNot() => base.AssertFocused_PassesWhenItHolds_FailsWhenItDoesNot();

    public void Dispose() => _host.Dispose();
}

public sealed class HtmlTextContract : TextContract<ContractPage>, IDisposable
{
    private readonly ContractHost<TextControls> _host = new();

    protected override ContractPage Scope => _host.Page;

    protected override ITextControlObject<ContractPage> Label => new LabelControl<ContractPage>(_host.Page, _host.Selector("label"));

    protected override ITextControlObject<ContractPage> EmptyLabel => new LabelControl<ContractPage>(_host.Page, _host.Selector("empty"));

    protected override string Text => "Apple pie";

    public void Dispose() => _host.Dispose();
}

public sealed class HtmlTextInputContract : EditableTextContract<ContractPage>, IDisposable
{
    private readonly ContractHost<TextControls> _host = new();

    protected override ContractPage Scope => _host.Page;

    protected override IEditableTextControlObject<ContractPage> Field => new TextInputControl<ContractPage>(_host.Page, _host.Selector("field"));

    protected override IEditableTextControlObject<ContractPage> ReadOnlyField => new TextInputControl<ContractPage>(_host.Page, _host.Selector("readonly"));

    protected override string Placeholder => "Your name";

    public void Dispose() => _host.Dispose();
}

public sealed class HtmlTextAreaContract : EditableTextContract<ContractPage>, IDisposable
{
    private readonly ContractHost<TextControls> _host = new();

    protected override ContractPage Scope => _host.Page;

    protected override IEditableTextControlObject<ContractPage> Field => new TextAreaControl<ContractPage>(_host.Page, _host.Selector("area"));

    protected override IEditableTextControlObject<ContractPage> ReadOnlyField => new TextAreaControl<ContractPage>(_host.Page, _host.Selector("readonly-area"));

    protected override string Placeholder => "Your notes";

    public void Dispose() => _host.Dispose();
}

public sealed class HtmlCheckBoxContract : ToggleContract<ContractPage>, IDisposable
{
    private readonly ContractHost<ToggleControls> _host = new();

    protected override ContractPage Scope => _host.Page;

    protected override IToggleControlObject<ContractPage> Box => new CheckBoxControl<ContractPage>(_host.Page, _host.Selector("box"));

    public void Dispose() => _host.Dispose();
}

public sealed class HtmlRangeInputContract : RangeContract<ContractPage>, IDisposable
{
    private readonly ContractHost<RangeControls> _host = new();

    protected override ContractPage Scope => _host.Page;

    protected override IRangeControlObject<ContractPage> Slider => new RangeInputControl<ContractPage>(_host.Page, _host.Selector("slider"));

    protected override RangeValues Values => new(Minimum: 0, Maximum: 10, Step: 2, Initial: 2, Target: 6);

    public void Dispose() => _host.Dispose();
}

public sealed class HtmlSelectContract : SelectorContract<ContractPage>, IDisposable
{
    private readonly ContractHost<SelectorControls> _host = new();

    protected override ContractPage Scope => _host.Page;

    protected override ISelectorControlObject<ContractPage> List => new SelectControl<ContractPage>(_host.Page, _host.Selector("list"));

    protected override SelectorValues Items => new(["Apple", "Banana", "Cherry"], ["a", "b", "c"], InitialIndex: 1);

    public void Dispose() => _host.Dispose();
}

public sealed class HtmlRadioGroupContract : SelectorContract<ContractPage>, IDisposable
{
    private readonly ContractHost<SelectorControls> _host = new();

    protected override ContractPage Scope => _host.Page;

    protected override ISelectorControlObject<ContractPage> List => new RadioGroupControl<ContractPage>(_host.Page, _host.Selector("radios"));

    protected override SelectorValues Items => new(["Apple", "Banana", "Cherry"], ["a", "b", "c"], InitialIndex: 1);

    public void Dispose() => _host.Dispose();
}
