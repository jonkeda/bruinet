# Test Writing

Bruinet tests should read like user workflows. Put mechanics in page objects and
controls.

## Test Shape

<!-- sample: tests/Bruinet.Docs.Samples/Guides/TestWriting.cs#shape -->
```csharp
[Fact]
public void SavingValidForm_ShowsSuccess()
{
    var page = _fixture.OpenFormPage();

    page.FillRequiredFields("Example");
    page.Save();

    Assert.True(page.SuccessMessage.IsVisible());
}
```

## Page Object Rules

- Page objects expose page-level intent.
- Page objects may expose controls when tests need direct assertions.
- Page objects should not contain broad recovery or cleanup loops.
- Navigation methods should wait for the next stable page state.

## Control Rules

- Use Bruinet controls instead of raw driver elements.
- Move repeated interaction behavior into controls.
- Prefer semantic methods over driver-specific APIs.
- Keep app-specific assertions in tests, not controls.

## Never In Tests

Tests, page objects' public members, app containers and test helpers go through page
objects and controls only. Do not use:

| Forbidden | Instead |
| --- | --- |
| `Context.Driver`, any `IMauiDriver` member | a control or page member; add one if missing |
| `IMauiElement` as a variable, parameter or return, or its members (`.Text`, `.Invoke()`, `.ReadState()`, `.PerformGesture()`) | the control's `Get*`/`Is*`/action |
| `FindElement`, `FindElements`, `TryFindElement` on a page, container, context or `AppRoot` | a named control property, `Child<T>(id)`, or a collection's `Item`/`Items`/`ItemWhere` |
| `ContainerRoot`, `AppElement`, `TryGetItemRoot`, `ScrollingRoot` | a container, collection or `AppRoot`-scoped control |
| `new Locator(...)` / `Locator.By*` in a test method | a page-object property |

Raw access skips the readiness gate, the polling, the logging, the failure messages that
name the locator, and the per-platform routes. A test written against the driver works on
the platform it was written on and nowhere else. Inside a ControlObject these APIs are the
implementation, and that is the only place they belong.

The one exception is tests whose subject is the driver or bridge itself (`Tests/Background`,
`Tests/Gestures/*Bridge*`, `Tests/Diagnostics`). The full rationale is in
[forbidden-apis.md](../../.github/skills/maui-ui-test/references/forbidden-apis.md).

## Waiting

Good waits name the state they expect:

- page is loaded;
- control is visible or enabled;
- text equals or contains an expected value;
- collection count changes;
- busy state ends;
- request or navigation completes.

Avoid:

- `Thread.Sleep` as a fix;
- unbounded retries;
- increasing timeouts without diagnostics;
- catching and ignoring platform exceptions.

## Assertions

Use xUnit `Assert`.

<!-- sample: tests/Bruinet.Docs.Samples/Guides/TestWriting.cs#assert -->
```csharp
Assert.True(page.SubmitButton.IsEnabled());
Assert.Equal("Saved", page.Status.GetText());
```

Do not add FluentAssertions to Bruinet test projects.

## Naming

- Test class: `<Feature>Tests`.
- Test method: `<Action>_<Scenario>_<ExpectedResult>`.
- Page object: `<PageName>Page`.
- Fixture: `<PlatformOrApp>Fixture`.

## Artifacts

When a test produces screenshots, logs, UAT output, traces, or videos, route
them through the Bruinet artifact layout under `TestResults/<run-id>/`.
