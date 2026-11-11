# Forbidden APIs in tests

## The list

In test methods, test helpers, the public members of page objects, and app containers and
collections in test projects, do not use:

| Forbidden | Instead |
| --- | --- |
| `Context.Driver`, any `IMauiDriver` member | a control or page member; add one to the control if missing |
| a variable, parameter or return of type `IMauiElement`, or its members (`.Text`, `.Name`, `.Invoke()`, `.ReadState()`, `.PerformGesture()`, `.ReadAlert()`) | the control's `Get*` / `Is*` / action |
| `FindElement`, `FindElements`, `TryFindElement` on a page, container, context or `AppRoot` | a named control property, `Child<T>(id)`, or a collection's `Item` / `Items` / `ItemWhere` |
| `ContainerRoot`, `AppElement`, `TryGetItemRoot`, `ScrollingRoot` | a container, a collection, or an `AppRoot`-scoped control |
| `new Locator(...)` / `Locator.By*` in a test method | a page-object property |
| `Thread.Sleep`, `Task.Delay` | an `Assert*` or `Wait*` on the state you expect |
| a raised `timeoutMs` to hide a flake | fix the control; raise one only for an operation long by nature (scrolling a long list, a search through it), with a comment saying so |
| try/catch around a control call | an `Assert*(false)`, `TryItem`, or `Assert.Throws<...>` |

Inside a ControlObject, `IMauiElement` and `Find*` are the implementation: that is where they
belong. The list applies to everything above the controls.

One structural exception above the controls: an app collection's row constructor
`(collection, IMauiElement itemRoot, int index)` and the collection's item factory pass the
root to `ItemObjectBase` untouched. That is plumbing the base requires; never read or call
the element.

## Why

Raw access skips everything the control model provides:

- **the readiness gate**: the page and the element are ready before the call;
- **the polling**: reads and asserts wait for the state instead of sampling it once;
- **the logging**: entry, exit and duration per call, in the run's artifacts;
- **the failure message**: it names the page, the control and its locator;
- **the platform route**: Windows goes through UI Automation or the gesture bridge, Android
  through Appium; the control chooses per platform.

A test written against the driver works on the platform it was written on and nowhere else,
and its failure says "null reference" instead of "Switch 'TestSwitch' did not turn on".

## When the model has no member for it

That is a gap in the model, not permission to go around it:

- a control lacks a member -> add it with the `maui-control` skill;
- an element has no specific control type -> use the closest control that answers what you
  assert (`Label` for text, a `ViewBase`-derived control for exists/visible), or add one;
- a region has no container -> add one in `Containers/`;
- the platform does not publish it -> the test cannot assert it on that platform. Say so; do not
  read the view model or a coordinate instead.

## The one exception

Tests whose subject *is* the driver or the bridge exercise these APIs on purpose:
`Tests/Background`, `Tests/Gestures/*Bridge*`, `Tests/Diagnostics`. They are framework tests,
not a model for app tests. Some other existing tests and pages also reach past the model
(`StepperTests`, `AlertReadTests`, `ContainerTestPage`, `NavigationDemoPage`,
`AutomationProbePage`, `ShellSamplePage`); they are known debt, not precedent.
