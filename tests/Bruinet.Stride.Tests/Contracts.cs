using Bruinet.ControlContracts;
using Bruinet.Core.Interfaces;
using Bruinet.Stride.Pages;

namespace Bruinet.Stride.Tests;

// The control contracts bound on the fake game (review 08, finding 3): the game keeps each element's state and the
// actions change it, as the automation server does. The game suite proves the same in a running game.

/// <summary>The fake game the contracts run on: its elements by name; one missing.</summary>
public sealed class ContractGame
{
    private readonly FakeGame _game = new();

    public ContractGame()
    {
        _game.Add(nameof(ContractPage), "StackPanel");
        _game.Add("Shown", "Button");
        _game.Add("Hidden", "Button");
        _game.Element("Hidden").IsVisible = false;
        _game.Add("Disabled", "Button");
        _game.Element("Disabled").IsEnabled = false;
        _game.Add("Box", "ToggleButton", isChecked: false);
    }

    /// <summary>What the game saw happen last: the server's only pointer action is a click.</summary>
    public string? LastAction() => _game.Actions.Count > 0 && _game.Actions[^1].StartsWith("Click ", StringComparison.Ordinal) ? "click" : null;

    public ContractPage Page => new(_game.Context);
}

public sealed class ContractPage(IStrideTestContext context) : PageObjectBase<ContractPage>(context)
{
    public Button<ContractPage> Shown => Button("Shown");

    public Button<ContractPage> Hidden => Button("Hidden");

    public Button<ContractPage> Disabled => Button("Disabled");

    public Button<ContractPage> Missing => Button("Missing");

    public ToggleButton<ContractPage> Box => ToggleButton("Box");
}

public sealed class StrideElementContract : ElementContract<ContractPage>
{
    private readonly ContractPage _page = new ContractGame().Page;

    protected override ContractPage Scope => _page;

    protected override IControlObject<ContractPage> Shown => _page.Shown;

    protected override IControlObject<ContractPage> Hidden => _page.Hidden;

    protected override IControlObject<ContractPage> Disabled => _page.Disabled;

    protected override IControlObject<ContractPage> Missing => _page.Missing;

    protected override (string Name, string Value) Attribute => ("ControlType", "Button");
}

public sealed class StrideClickableContract : ClickableContract<ContractPage>
{
    private const string NoRoute = "The Stride automation server has a click and no other pointer action (RouteUnavailableException).";

    private readonly ContractGame _game = new();
    private readonly ContractPage _page;

    public StrideClickableContract() => _page = _game.Page;

    protected override ContractPage Scope => _page;

    protected override IClickableControlObject<ContractPage> Button => _page.Shown;

    protected override IClickableControlObject<ContractPage> DisabledButton => _page.Disabled;

    protected override string? LastAction() => _game.LastAction();

    [Fact(Skip = NoRoute)]
    public override void DoubleClick_DoubleClicksIt()
    {
    }

    [Fact(Skip = NoRoute)]
    public override void RightClick_RightClicksIt()
    {
    }

    [Fact(Skip = NoRoute)]
    public override void Hover_HoversIt()
    {
    }

    [Fact(Skip = NoRoute)]
    public override void LongPress_PressesIt()
    {
    }
}

public sealed class StrideToggleContract : ToggleContract<ContractPage>
{
    private readonly ContractPage _page = new ContractGame().Page;

    protected override ContractPage Scope => _page;

    protected override IToggleControlObject<ContractPage> Box => _page.Box;
}
