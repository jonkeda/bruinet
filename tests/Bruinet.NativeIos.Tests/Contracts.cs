using Bruinet.ControlContracts;
using Bruinet.Core.Interfaces;
using Bruinet.NativeIos.Controls.Buttons;
using Bruinet.NativeIos.Controls.Toggle;

namespace Bruinet.NativeIos.Tests;

// The control contracts bound on the fake app (review 08, finding 3): an element that keeps its state, so a contract
// can act and read the effect back. The device suites prove the same on a phone; these prove the controls' own
// logic, with no device.

/// <summary>An element of the fake app that keeps its state and says what happened to it.</summary>
public sealed class StatefulElement(string identifier, ContractApp app) : INativeIosElement
{
    public string InstanceKey { get; } = Guid.NewGuid().ToString("N");

    public string? Identifier { get; } = identifier;

    public string? Label { get; set; }

    public string? ElementType { get; set; } = "XCUIElementTypeButton";

    public bool Visible { get; set; } = true;

    public bool Enabled { get; set; } = true;

    public bool Selected { get; set; }

    public bool Focused { get; set; }

    public bool? Checked { get; set; }

    public string? Text { get; set; }

    public string? Placeholder { get; set; }

    public string? Value { get; set; }

    public System.Drawing.Rectangle Rect { get; set; } = new(10, 10, 100, 40);

    public Dictionary<string, string> Attributes { get; } = [];

    public string? GetAttribute(string name) => Attributes.TryGetValue(name, out var value) ? value : null;

    public void Click()
    {
        app.LastAction = "click";

        // A switch's tap turns it over, as the real one's does.
        if (Checked is not null)
        {
            Checked = !Checked;
        }
    }

    public void TapAt(double x, double y) => Click();

    public void DoubleTap() => app.LastAction = "double-click";

    public void LongPress(int durationMs = 1000) => app.LastAction = "long-press";

    public void SendKeys(string text, TextInputMethod method = TextInputMethod.Keys) => Text = (Text ?? string.Empty) + text;

    public void Clear() => Text = string.Empty;

    public void Swipe(GestureDirection direction, double percent = 0.75)
    {
    }

    public bool Scroll(GestureDirection direction, double percent = 0.75) => false;

    public void Adjust(string value) => Value = value;

    public void StepWheel(bool forward)
    {
    }

    public void ScrollIntoView(int timeoutMs)
    {
    }

    public INativeIosElement? TryFindElement(Locator locator) => null;

    public IReadOnlyList<INativeIosElement> FindElements(Locator locator) => [];
}

/// <summary>The fake app the contracts run on: its elements by accessibility identifier; one missing.</summary>
public sealed class ContractApp
{
    private readonly FakeApp _app = new();

    public ContractApp()
    {
        Add(nameof(ContractPage), _ => { });
        Add("Shown", e => e.Attributes["label"] = "Shown's label");
        Add("Hidden", e => e.Visible = false);
        Add("Disabled", e => e.Enabled = false);
        Add("Box", e => (e.ElementType, e.Checked) = ("XCUIElementTypeSwitch", false));
    }

    /// <summary>What the page saw happen to an element last.</summary>
    public string? LastAction { get; set; }

    public ContractPage Page => new(_app.Context.Object);

    private void Add(string id, Action<StatefulElement> set)
    {
        var element = new StatefulElement(id, this);
        set(element);
        _app.Given(id, () => element);
    }
}

public sealed class ContractPage(INativeIosTestContext context) : PageObjectBase<ContractPage>(context)
{
    public Button<ContractPage> Shown => new(this, "Shown");

    public Button<ContractPage> Hidden => new(this, "Hidden");

    public Button<ContractPage> Disabled => new(this, "Disabled");

    public Button<ContractPage> Missing => new(this, "Missing");

    public Switch<ContractPage> Box => new(this, "Box");
}

public sealed class NativeIosElementContract : ElementContract<ContractPage>
{
    private readonly ContractPage _page = new ContractApp().Page;

    protected override ContractPage Scope => _page;

    protected override IControlObject<ContractPage> Shown => _page.Shown;

    protected override IControlObject<ContractPage> Hidden => _page.Hidden;

    protected override IControlObject<ContractPage> Disabled => _page.Disabled;

    protected override IControlObject<ContractPage> Missing => _page.Missing;

    protected override (string Name, string Value) Attribute => ("label", "Shown's label");
}

public sealed class NativeIosClickableContract : ClickableContract<ContractPage>
{
    private readonly ContractApp _app = new();
    private readonly ContractPage _page;

    public NativeIosClickableContract() => _page = _app.Page;

    protected override ContractPage Scope => _page;

    protected override IClickableControlObject<ContractPage> Button => _page.Shown;

    protected override IClickableControlObject<ContractPage> DisabledButton => _page.Disabled;

    protected override string? LastAction() => _app.LastAction;

    [Fact(Skip = "A touch screen has no right button: native iOS's right click is a long press.")]
    public override void RightClick_RightClicksIt()
    {
    }

    [Fact(Skip = "A touch screen has no pointer to hover: native iOS refuses Hover (RouteUnavailableException).")]
    public override void Hover_HoversIt()
    {
    }
}

public sealed class NativeIosToggleContract : ToggleContract<ContractPage>
{
    private readonly ContractPage _page = new ContractApp().Page;

    protected override ContractPage Scope => _page;

    protected override IToggleControlObject<ContractPage> Box => _page.Box;
}
