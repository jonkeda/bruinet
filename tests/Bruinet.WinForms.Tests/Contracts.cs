using Bruinet.ControlContracts;

namespace Bruinet.WinForms.Tests;

// The control contracts bound on the fake app (review 08, finding 3): an element that keeps its state, so a contract
// can act and read the effect back, as a UI Automation element does. The desktop suites prove the same on the real
// app; these prove the controls' own logic, with no window.

/// <summary>An element of the fake app that keeps its state and says what happened to it.</summary>
public sealed class StatefulElement(string automationId, ContractApp app) : IWinFormsElement
{
    public string InstanceKey { get; } = Guid.NewGuid().ToString("N");

    public string? AutomationId { get; } = automationId;

    public string? Name { get; set; }

    public string? ClassName { get; set; } = "Fake";

    public string? TagName => ClassName;

    public bool Visible { get; set; } = true;

    public bool Enabled { get; set; } = true;

    public bool Focused { get; set; }

    public bool Selected { get; set; }

    public bool? Checked { get; set; }

    public string? Text { get; set; }

    public string? Value { get; set; }

    public bool? IsReadOnly { get; set; }

    public string? HelpText { get; set; }

    public System.Drawing.Rectangle Rect { get; set; } = new(10, 10, 100, 20);

    public Dictionary<string, string> Attributes { get; } = [];

    /// <summary>The tabs this one is selected among; selecting it unselects the others.</summary>
    public List<StatefulElement>? Tabs { get; set; }

    public string? GetAttribute(string name) => Attributes.TryGetValue(name, out var value) ? value : null;

    public void Click()
    {
        app.LastAction = "click";
        if (Tabs is not null)
        {
            Select();
        }

        // A check box's click turns it over, as the real one's does.
        if (Checked is not null)
        {
            Toggle();
        }
    }

    public void Toggle() => Checked = !(Checked ?? false);

    public void Select()
    {
        foreach (var tab in Tabs ?? [this])
        {
            tab.Selected = ReferenceEquals(tab, this);
        }
    }

    public void SetValue(string value) => Value = value;

    public void SendKeys(string text, TextInputMethod method = TextInputMethod.SetValue) => Value = (Value ?? string.Empty) + text;

    public void Clear() => Value = string.Empty;

    public void Focus() => Focused = true;

    public void PressKey(string key) => app.LastAction = "key " + key;

    public void ClickWithin(int x, int y) => app.LastAction = $"click {x},{y}";

    public void DoubleClick() => app.LastAction = "double-click";

    public void RightClick() => app.LastAction = "right-click";

    public void Hover() => app.LastAction = "hover";

    public void LongPress(int durationMs = 1000) => app.LastAction = "long-press";

    public void ScrollIntoView(int timeoutMs)
    {
    }

    public double? RangeValue { get; set; }

    public double? RangeMinimum { get; set; }

    public double? RangeMaximum { get; set; }

    public double? RangeSmallChange { get; set; }

    public void SetRangeValue(double value) => RangeValue = value;

    public bool? IsExpanded { get; set; }

    public void Expand() => IsExpanded = true;

    public void Collapse() => IsExpanded = false;

    public double? VerticalScrollPercent { get; set; }

    public bool ScrollPage(bool down) => false;

    public void SetVerticalScrollPercent(double percent) => VerticalScrollPercent = percent;

    public IWinFormsElement? TryFindElement(Locator locator) => null;

    public IReadOnlyList<IWinFormsElement> FindElements(Locator locator) => [];
}

/// <summary>The fake app the contracts run on: its elements by AutomationId; one missing.</summary>
public sealed class ContractApp
{
    private readonly FakeApp _app = new();

    public ContractApp()
    {
        Add(nameof(ContractPage), _ => { });
        Add("Shown", e => e.Attributes["HelpText"] = "Shown's help");
        Add("Hidden", e => e.Visible = false);
        Add("Disabled", e => e.Enabled = false);
        Add("Box", e => e.Checked = false);
        Add("Expander", e => e.IsExpanded = false);
        var first = Add("FirstTab", e => (e.Name, e.Selected) = ("General", true));
        var second = Add("SecondTab", e => e.Name = "Advanced");
        first.Tabs = second.Tabs = [first, second];
    }

    /// <summary>What the page saw happen to an element last.</summary>
    public string? LastAction { get; set; }

    public ContractPage Page => new(_app.Context.Object);

    private StatefulElement Add(string automationId, Action<StatefulElement> set)
    {
        var element = new StatefulElement(automationId, this);
        set(element);
        _app.Given(automationId, () => element);
        return element;
    }
}

public sealed class ContractPage(IWinFormsTestContext context) : PageObjectBase<ContractPage>(context)
{
    public Button<ContractPage> Shown => new(this, "Shown");

    public Button<ContractPage> Hidden => new(this, "Hidden");

    public Button<ContractPage> Disabled => new(this, "Disabled");

    public Button<ContractPage> Missing => new(this, "Missing");

    public CheckBox<ContractPage> Box => new(this, "Box");

    public TreeNode<ContractPage> Details => new(this, "Expander");

    public TabPage<ContractPage> FirstTab => new(this, "FirstTab");

    public TabPage<ContractPage> SecondTab => new(this, "SecondTab");
}

public sealed class WinFormsElementContract : ElementContract<ContractPage>
{
    private readonly ContractPage _page = new ContractApp().Page;

    protected override ContractPage Scope => _page;

    protected override IControlObject<ContractPage> Shown => _page.Shown;

    protected override IControlObject<ContractPage> Hidden => _page.Hidden;

    protected override IControlObject<ContractPage> Disabled => _page.Disabled;

    protected override IControlObject<ContractPage> Missing => _page.Missing;

    protected override (string Name, string Value) Attribute => ("HelpText", "Shown's help");
}

public sealed class WinFormsClickableContract : ClickableContract<ContractPage>
{
    private readonly ContractApp _app = new();
    private readonly ContractPage _page;

    public WinFormsClickableContract() => _page = _app.Page;

    protected override ContractPage Scope => _page;

    protected override IClickableControlObject<ContractPage> Button => _page.Shown;

    protected override IClickableControlObject<ContractPage> DisabledButton => _page.Disabled;

    protected override string? LastAction() => _app.LastAction;
}

public sealed class WinFormsToggleContract : ToggleContract<ContractPage>
{
    private readonly ContractPage _page = new ContractApp().Page;

    protected override ContractPage Scope => _page;

    protected override IToggleControlObject<ContractPage> Box => _page.Box;
}

public sealed class WinFormsExpandableContract : ExpandableContract<ContractPage>
{
    private readonly ContractPage _page = new ContractApp().Page;

    protected override ContractPage Scope => _page;

    protected override IExpandableControlObject<ContractPage> Expander => _page.Details;
}

public sealed class WinFormsTabContract : TabContract<ContractPage>
{
    private readonly ContractPage _page = new ContractApp().Page;

    protected override ContractPage Scope => _page;

    protected override ITabControlObject<ContractPage> First => _page.FirstTab;

    protected override ITabControlObject<ContractPage> Second => _page.SecondTab;

    protected override (string First, string Second) Titles => ("General", "Advanced");
}
