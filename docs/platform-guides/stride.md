# Stride Platform Guide

`Bruinet.Stride` tests the UI of Stride games through an automation server the game embeds
(`Bruinet.Stride.Automation`) and a named pipe. It has the shape of the other stacks: page objects,
generated ControlObjects with the same members and UAT verbs, one call model (act once, confirm, wait
for state), scope readiness and collections with item keys. Everything happens inside the game:
clicks raise the control's Click event and keys go to the game's own input system, so nothing needs
the foreground, and a locked screen does not matter.

## Projects

- `src/Bruinet.Stride`: the client: interfaces, the pipe driver and element, pages, containers, collections, controls.
- `src/Bruinet.Stride.Automation`: the server the game embeds (`AutomationGameSystem`, `StrideUIHandler`).
- `tests/Bruinet.Stride.Tests`: the client over a fake game (CI runs them).
- `tests/Bruinet.Stride.Automation.Tests`: the server over a Stride UI tree built in the test (CI runs them).
- `tests/Bruinet.Stride.UITests`: UI tests and the UAT suite against the sample game.
- `samples/Bruinet.Samples.Stride.App`: the sample game.

## The protocol

Every command is one attempt. An element is known by its identity, Stride's `UIElement.Id`
(`ElementState.InstanceKey`); a target `#key` addresses it, any other target names an element by its
`Name`, and an empty one is the UI root. Queries: `GetState`, `Find` and `FindAll` (inside a scope; from
the root, the root counts), `GetChildren`, `IsGameReady`, `IsBusy`. Actions: `Click`, `Toggle`,
`SetText`, `SetSliderValue`, `SimulateKeyPress`, `SimulateKeyHold`, `TakeScreenshot`. Nothing waits in
the game; the call around a command is what waits.

## Page objects

```csharp
public sealed class MainPage(IStrideTestContext context) : PageObjectBase<MainPage>(context)
{
    public TextBlock<MainPage> CounterDisplay => TextBlock("CounterDisplay");
    public Button<MainPage> IncrementButton => Button("IncrementButton");

    protected override Locator ReadyLocator => Locator.ByAutomationId("MainPanel");
}
```

A page is known by an element it shows (`ReadyLocator`; by default the one named as the page). It is
not ready while the game says it is not, and busy while the game says it is (`IsBusy`) or one of its
`BusyIndicators` is shown. `PressKey` and `HoldKey` feed the game's input. An element the game no
longer has is stale, never another element with the same name.

## Controls

`Button`, `ToggleButton` (a click moves it to its next state, confirmed), `EditText` (its text written
in the game and confirmed: no keys, no selection, so the game's "a field has focus" check stays false),
`TextBlock`, `Slider` (value and bounds from the game, step 1), `ImageElement`. Generated from their
`.tpl.cs` (`tools/Scripts/CreateStride.Bat`, AD-013). A double click, right click, hover or long press
has no route in the server and fails saying so.

## Run

```powershell
dotnet build samples\Bruinet.Samples.Stride.App
dotnet test tests\Bruinet.Stride.UITests
```

The fixture starts the game with `--automation` and a pipe of its own, so runs do not meet an old game.
