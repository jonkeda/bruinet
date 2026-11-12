# WinForms Platform Guide

`Bruinet.WinForms` tests WinForms apps through UI Automation, over `Bruinet.FlaUI`. It has the shape of
the MAUI, Html, native Android and WPF stacks: page objects, generated ControlObjects with the same members
and UAT verbs, one call model (act once, confirm, wait for state), scope readiness and collections with
item keys. Every action but setting a date is a UI Automation pattern, so the app does not need the
foreground, and runs behind a locked screen.

## Projects

- `src/Bruinet.WinForms`: interfaces, the FlaUI element and driver, pages, containers, collections, controls.
- `src/Bruinet.FlaUI`: what the FlaUI stacks share (`DesktopApp`, `UiaElements`, `UiaElementBase`).
- `tests/Bruinet.WinForms.Tests`: unit tests over a mocked app (CI runs them).
- `tests/Bruinet.WinForms.UITests`: UI tests and the UAT suite against the sample app (local: they need a desktop).
- `samples/Bruinet.Samples.WinForms.App`: the sample app.

## A session

<!-- sample: tests/Bruinet.Docs.Samples/WinForms/WinFormsGuide.cs#session -->
```csharp
var context = new WinFormsTestContext(new WinFormsTestContextOptions { ExecutablePath = appPath });
var login = new LoginPage(context);
login.AssertLoaded(true);
```

The options launch the app (with `Environment` set on its process only), or attach to a `ProcessId` or
`WindowHandle`. `WinFormsTestFixtureBase` makes the context from `bruinet.winforms.config.json` and the
suite's `GetDefaultAppPath()`.

## Page objects

<!-- sample: tests/Bruinet.Docs.Samples/WinForms/WinFormsGuide.cs#page -->
```csharp
public sealed class LoginPage(IWinFormsTestContext context) : PageObjectBase<LoginPage>(context)
{
    public TextBox<LoginPage> Username => TextBox("txtUsername");
    public NumericUpDown<LoginPage> Port => new(this, "nudPort", minimum: 1, maximum: 65535);
    public ComboBox<LoginPage> Role => ComboBox("cmbRole");

    protected override Locator ReadyLocator => Locator.ByAutomationId("txtUsername");
}
```

A control's AutomationId is its `Name`. The form is the lookup root and not one of its own descendants,
so a page is known by a control it shows (`ReadyLocator`; by default the element whose AutomationId is
the page's name). A page is busy while a control named `BruinetBusy`, or one of its `BusyIndicators`, is
shown.

## Controls

| Control | How |
| --- | --- |
| `Button` | Invoke |
| `CheckBox`, `RadioButton`, `TabPage`, `TreeNode` | Toggle, SelectionItem, ExpandCollapse |
| `TextBox`, `RichTextBox` | Value, confirmed (a rich text box reads back without RichEdit's closing line end) |
| `PasswordBox` | Value, written once: a password field reads "Access denied" |
| `ComboBox` | its item's text written to its Value; expanded and selected when the Value is read-only |
| `ListBox` | the item's SelectionItem |
| `TrackBar` | the number as its Value text (it has no RangeValue); bounds from the page object |
| `NumericUpDown` | the number written to its edit, committed with its Up and Down buttons; bounds from the page object |
| `ProgressBar` | RangeValue |
| `DateTimePicker` | typed field by field with the keyboard, in the culture's short date order (below) |
| `Label` | its name |

Controls are generated from their `.tpl.cs` (`tools/Scripts/CreateWinForms.Bat`, AD-013).

Two things about WinForms' automation:

- A WinForms combo box blocks each UI Automation call about two seconds while it opens its list, so a
  selection is one Value write rather than expand, select and collapse.
- A `NumericUpDown` takes its edit's text only when the focus leaves it; a write through UI Automation
  moves no focus, so the control commits it with its own spinner buttons. Without that, the app keeps
  reading the old number.

## Real input

A date picker ignores a Value write, and its calendar refuses Invoke while the desktop is locked, so
`DateTimePicker.SetDate` types the date with the keyboard. That, a double click, a right click, hover, a
long press and `SendKeys` go through `PhysicalInput`, which `BRUINET_BACKGROUND_MODE=1` refuses (AD-005).
Windows refuses synthetic keys on a locked desktop: the call then fails saying so.

## Run

See [WinForms Run Guide](../run/WinForms.md).
