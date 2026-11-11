using System.Reflection;
using Bruinet.Blazor.Pages;
using Bruinet.Core.Interfaces;
using Bruinet.ControlContracts.Html.Pages;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;
using Bruinet.Html.Controls.Range;
using Bruinet.Html.Controls.Selection;
using Bruinet.Html.Controls.Text;
using Bruinet.Html.Controls.Toggle;
using Bruinet.Html.Interfaces;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Bruinet.ControlContracts.Html;

// The control contracts bound to Bruinet.Html's controls (.my/coverage/plan.md, C2 and C3). Each binding
// opens its page on the head THead and hands the contract Bruinet.Html's controls on it; a head's test
// project derives a sealed class per binding, and skips there what its head cannot do, with why.

/// <summary>The page the contracts' controls are on.</summary>
public sealed class ContractPage(IHtmlTestContext context) : BlazorPageObjectBase<ContractPage>(context);

/// <summary>A head the Html contracts run on.</summary>
public interface IContractHead : IAsyncDisposable
{
    /// <summary>Opens the page <paramref name="component"/> is, ready to act on.</summary>
    /// <param name="component">A page from <c>Pages/</c>.</param>
    /// <param name="path">Where it is routed.</param>
    Task<IHtmlTestContext> OpenAsync(Type component, string path);
}

/// <summary>One test's page on a head.</summary>
public sealed class ContractSession<THead>(Type component) : IAsyncDisposable where THead : IContractHead, new()
{
    private readonly THead _head = new();
    private ContractPage? _page;

    public ContractPage Page => _page ?? throw new InvalidOperationException("The contract's page is not open yet.");

    public async Task OpenAsync()
        => _page = new ContractPage(await _head.OpenAsync(component, component.GetCustomAttribute<RouteAttribute>()!.Template));

    public static string Selector(string testId) => $"[data-testid='{testId}']";

    /// <summary>An element's text, null when it shows none.</summary>
    public string? Text(string testId) => new LabelControl<ContractPage>(Page, Selector(testId)).GetText() is { Length: > 0 } text ? text : null;

    public ValueTask DisposeAsync() => _head.DisposeAsync();
}

public abstract class HtmlElementBinding<THead> : ElementContract<ContractPage>, IAsyncLifetime where THead : IContractHead, new()
{
    private readonly ContractSession<THead> _session = new(typeof(ElementControls));

    public Task InitializeAsync() => _session.OpenAsync();

    public Task DisposeAsync() => _session.DisposeAsync().AsTask();

    protected override ContractPage Scope => _session.Page;

    protected override IControlObject<ContractPage> Shown => Button("shown");

    protected override IControlObject<ContractPage> Hidden => Button("hidden");

    protected override IControlObject<ContractPage> Disabled => Button("disabled");

    protected override IControlObject<ContractPage> Missing => Button("missing");

    protected override (string Name, string Value) Attribute => ("title", "Apple pie");

    private ButtonControl<ContractPage> Button(string testId) => new(_session.Page, ContractSession<THead>.Selector(testId));

}

public abstract class HtmlClickableBinding<THead> : ClickableContract<ContractPage>, IAsyncLifetime where THead : IContractHead, new()
{
    private readonly ContractSession<THead> _session = new(typeof(ClickControls));

    public Task InitializeAsync() => _session.OpenAsync();

    public Task DisposeAsync() => _session.DisposeAsync().AsTask();

    protected override ContractPage Scope => _session.Page;

    protected override IClickableControlObject<ContractPage> Button => new ButtonControl<ContractPage>(_session.Page, ContractSession<THead>.Selector("button"));

    protected override IClickableControlObject<ContractPage> DisabledButton => new ButtonControl<ContractPage>(_session.Page, ContractSession<THead>.Selector("disabled"));

    protected override string? LastAction() => _session.Text("last");
}

public abstract class HtmlFocusableBinding<THead> : FocusableContract<ContractPage>, IAsyncLifetime where THead : IContractHead, new()
{
    private readonly ContractSession<THead> _session = new(typeof(TextControls));

    public Task InitializeAsync() => _session.OpenAsync();

    public Task DisposeAsync() => _session.DisposeAsync().AsTask();

    protected override ContractPage Scope => _session.Page;

    protected override IFocusableControlObject<ContractPage> Field => new TextInputControl<ContractPage>(_session.Page, ContractSession<THead>.Selector("field"));
}

public abstract class HtmlTextBinding<THead> : TextContract<ContractPage>, IAsyncLifetime where THead : IContractHead, new()
{
    private readonly ContractSession<THead> _session = new(typeof(TextControls));

    public Task InitializeAsync() => _session.OpenAsync();

    public Task DisposeAsync() => _session.DisposeAsync().AsTask();

    protected override ContractPage Scope => _session.Page;

    protected override ITextControlObject<ContractPage> Label => new LabelControl<ContractPage>(_session.Page, ContractSession<THead>.Selector("label"));

    protected override ITextControlObject<ContractPage> EmptyLabel => new LabelControl<ContractPage>(_session.Page, ContractSession<THead>.Selector("empty"));

    protected override string Text => "Apple pie";
}

public abstract class HtmlTextInputBinding<THead> : EditableTextContract<ContractPage>, IAsyncLifetime where THead : IContractHead, new()
{
    private readonly ContractSession<THead> _session = new(typeof(TextControls));

    public Task InitializeAsync() => _session.OpenAsync();

    public Task DisposeAsync() => _session.DisposeAsync().AsTask();

    protected override ContractPage Scope => _session.Page;

    protected override IEditableTextControlObject<ContractPage> Field => new TextInputControl<ContractPage>(_session.Page, ContractSession<THead>.Selector("field"));

    protected override IEditableTextControlObject<ContractPage> ReadOnlyField => new TextInputControl<ContractPage>(_session.Page, ContractSession<THead>.Selector("readonly"));

    protected override string Placeholder => "Your name";
}

public abstract class HtmlTextAreaBinding<THead> : EditableTextContract<ContractPage>, IAsyncLifetime where THead : IContractHead, new()
{
    private readonly ContractSession<THead> _session = new(typeof(TextControls));

    public Task InitializeAsync() => _session.OpenAsync();

    public Task DisposeAsync() => _session.DisposeAsync().AsTask();

    protected override ContractPage Scope => _session.Page;

    protected override IEditableTextControlObject<ContractPage> Field => new TextAreaControl<ContractPage>(_session.Page, ContractSession<THead>.Selector("area"));

    protected override IEditableTextControlObject<ContractPage> ReadOnlyField => new TextAreaControl<ContractPage>(_session.Page, ContractSession<THead>.Selector("readonly-area"));

    protected override string Placeholder => "Your notes";
}

public abstract class HtmlCheckBoxBinding<THead> : ToggleContract<ContractPage>, IAsyncLifetime where THead : IContractHead, new()
{
    private readonly ContractSession<THead> _session = new(typeof(ToggleControls));

    public Task InitializeAsync() => _session.OpenAsync();

    public Task DisposeAsync() => _session.DisposeAsync().AsTask();

    protected override ContractPage Scope => _session.Page;

    protected override IToggleControlObject<ContractPage> Box => new CheckBoxControl<ContractPage>(_session.Page, ContractSession<THead>.Selector("box"));
}

public abstract class HtmlRangeInputBinding<THead> : RangeContract<ContractPage>, IAsyncLifetime where THead : IContractHead, new()
{
    private readonly ContractSession<THead> _session = new(typeof(RangeControls));

    public Task InitializeAsync() => _session.OpenAsync();

    public Task DisposeAsync() => _session.DisposeAsync().AsTask();

    protected override ContractPage Scope => _session.Page;

    protected override IRangeControlObject<ContractPage> Slider => new RangeInputControl<ContractPage>(_session.Page, ContractSession<THead>.Selector("slider"));

    protected override RangeValues Values => new(Minimum: 0, Maximum: 10, Step: 2, Initial: 2, Target: 6);
}

public abstract class HtmlSelectBinding<THead> : SelectorContract<ContractPage>, IAsyncLifetime where THead : IContractHead, new()
{
    private readonly ContractSession<THead> _session = new(typeof(SelectorControls));

    public Task InitializeAsync() => _session.OpenAsync();

    public Task DisposeAsync() => _session.DisposeAsync().AsTask();

    protected override ContractPage Scope => _session.Page;

    protected override ISelectorControlObject<ContractPage> List => new SelectControl<ContractPage>(_session.Page, ContractSession<THead>.Selector("list"));

    protected override SelectorValues Items => new(["Apple", "Banana", "Cherry"], ["a", "b", "c"], InitialIndex: 1);
}

public abstract class HtmlRadioGroupBinding<THead> : SelectorContract<ContractPage>, IAsyncLifetime where THead : IContractHead, new()
{
    private readonly ContractSession<THead> _session = new(typeof(SelectorControls));

    public Task InitializeAsync() => _session.OpenAsync();

    public Task DisposeAsync() => _session.DisposeAsync().AsTask();

    protected override ContractPage Scope => _session.Page;

    protected override ISelectorControlObject<ContractPage> List => new RadioGroupControl<ContractPage>(_session.Page, ContractSession<THead>.Selector("radios"));

    protected override SelectorValues Items => new(["Apple", "Banana", "Cherry"], ["a", "b", "c"], InitialIndex: 1);
}
