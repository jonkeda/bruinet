# WPF Platform Guide

`Bruinet.Wpf` tests WPF apps through UI Automation, over `Bruinet.FlaUI`. It has the shape of the MAUI,
Html and native Android stacks: page objects, generated ControlObjects with the same members and UAT
verbs, one call model (act once, confirm, wait for state), scope readiness and collections with item
keys. Every action is a UI Automation pattern, so the app does not need the foreground.

## Projects

- `src/Bruinet.Wpf`: interfaces, the FlaUI element and driver, pages, containers, collections, controls.
- `src/Bruinet.FlaUI`: what the FlaUI stacks share (`DesktopApp`, `UiaElements`, `UiaElementBase`).
- `tests/Bruinet.Wpf.Tests`: unit tests over a mocked app (CI runs them).
- `tests/Bruinet.Wpf.UITests`: UI tests and the UAT suite against the sample app (local: they need a desktop).
- `samples/Bruinet.Samples.Wpf.App`: the sample app.

## A session

```csharp
var context = new WpfTestContext(new WpfTestContextOptions { ExecutablePath = appPath });
var shell = new ShellPage(context);
shell.AssertLoaded(true);
```

The options launch the app (with `Environment` set on its process only), or attach to a `ProcessId` or
`WindowHandle`. A launch hands the foreground back to the person's window and places the app as
`BRUINET_AUT_PLACE` asks. `WpfTestFixtureBase` makes the context from `bruinet.wpf.config.json` and the
suite's `GetDefaultAppPath()`.

## Page objects

```csharp
public sealed class LoginPage(IWpfTestContext context) : PageObjectBase<LoginPage>(context)
{
    public TextBox<LoginPage> Username => TextBox("UsernameTextBox");
    public PasswordBox<LoginPage> Password => PasswordBox("PasswordBox");
    public Button<LoginPage> SignIn => Button("LoginButton");

    protected override IReadOnlyList<Locator> BusyIndicators => [Locator.ByAutomationId("BusyIndicator")];
}
```

A WPF app usually has one window whose content changes, so a page is known by an element it shows:
`ReadyLocator`, by default the element whose AutomationId is the page's name (put
`AutomationProperties.AutomationId="LoginPage"` on the view). A page is busy while an element with
AutomationId `BruinetBusy`, or one of its `BusyIndicators`, is shown.

Two things about WPF's automation tree:

- A panel (`Grid`, `StackPanel`, `Border`) has no automation peer and is not in the tree; give a page a
  control or a `UserControl` to be known by.
- A collapsed element is left out of the tree: it does not exist, and an absent element has no visibility
  to assert. Assert `AssertExists(false)` for something hidden by `Visibility.Collapsed`.

## Controls

| Control | Pattern |
| --- | --- |
| `Button` | Invoke |
| `CheckBox`, `ToggleButton` | Toggle |
| `RadioButton`, `TabItem` | SelectionItem |
| `TextBox` | Value: written and confirmed, no keys typed |
| `PasswordBox` | Value: written once; UI Automation never reads a password back |
| `ComboBox` | ExpandCollapse, then the item's SelectionItem, then collapsed |
| `ListBox` | the item's SelectionItem |
| `Slider`, `ProgressBar` | RangeValue, with the range's own bounds and step |
| `Expander`, `TreeViewItem` | ExpandCollapse |
| `ScrollView` | Scroll |
| `Label` | the element's name (a `TextBlock`'s text) |

A plain string declares a control by AutomationId. Controls are generated from their `.tpl.cs`
(`tools/Scripts/CreateWpf.Bat`, AD-013).

## Real input

A double click, a right click, hover and a long press have no pattern, so they use the pointer, and
`SendKeys` types with the keyboard. These go through `PhysicalInput`, which `BRUINET_BACKGROUND_MODE=1`
refuses (AD-005). Nothing else moves the pointer or takes the foreground.

## Run

See [WPF Run Guide](../run/WPF.md).
