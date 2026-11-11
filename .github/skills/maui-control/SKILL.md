---
name: maui-control
description: Create or extend a Bruinet MAUI ControlObject - a simple control, a container, a component with named parts, or a collection with rows - in the .tpl.cs / .gen.cs generator format. Use when asked to add, write, or extend a control object, container, component, collection, or part for a MAUI or third-party (CommunityToolkit, vendor) control, or when a UI test needs a control member that does not exist yet.
---

# Create a MAUI ControlObject

A ControlObject is the only place that touches the platform element. Tests, pages and app
containers talk to it through generated, waiting, logged members. This skill states the
rules; the examples illustrate them. When an existing control disagrees with a rule here,
follow the rule and mention the disagreement.

## Workflow

1. **Choose the kind** with the table below. Read the reference for that kind:
   [simple control](references/simple-control.md), [container](references/container.md),
   [component](references/component.md), [collection](references/collection.md).
2. **Probe the platform** before writing members: what the automation tree shows for the
   element on Windows (control type, patterns, children, AutomationId) and on Android when an
   emulator is up (`adb devices`). The repo has no general probe tool: a short script in your
   scratch directory that walks the raw UI Automation view (FlaUI or
   `System.Windows.Automation`) is fine. Keep it out of the repo and record what it showed in the
   class remarks. Choose a route per member with [routes.md](references/routes.md).
3. **Add the sample**: an element in `samples/Bruinet.Samples.Maui.App` with an
   `AutomationId`, and a status label that shows the visible effect of each action.
4. **Write the template** `Foo.tpl.cs` to the [generator contract](references/generator-contract.md)
   and the common rules below.
5. **Generate and build**: `tools\Scripts\CreateMaui.Bat`, read its errors and warnings, diff
   `Foo.gen.cs` against the API you intended, then
   `dotnet build Bruinet.sln -v:minimal /nr:false`. A plain `.cs` (no Core methods, no
   shortcuts: R5) skips generation; just build.
6. **Prove it**: page-object members and UI tests, written with the `maui-ui-test` skill.
   Rebuild the sample app (the solution build above includes it and the Windows exe the fixture
   launches), then run tier 1 (`--filter "Control=Foo"`), plus tier 2b
   (`--filter "Stage=Background"`) if a bridge verb was added. If you changed a page other tests
   share, also run that area (`--filter "FullyQualifiedName~Tests.<Area>"`).

## Choosing the kind

| The thing on screen... | Kind | Base |
| --- | --- | --- |
| is one element to the test, even when the platform draws parts (Stepper's +/-), and its parts are implementation detail | **simple control** | `ViewBase<TScope>` or a capability base |
| is a region whose content the *app* chooses, which scopes other controls and may read or act on its own root (Border, ScrollView, Popup, StateContainer) | **container** | `ContainerObjectBase<TParent, TSelf>` |
| is one control to the user, made of fixed parts the *control* knows and a test wants to address by name (MediaElement's transport buttons, slider and time labels; AvatarView's initials and image) | **component** | `ComponentObjectBase<TScope, Foo<TScope>>` |
| repeats rows whose children are controls (a row has a label, a checkbox, a delete button) | **collection** + item | `CollectionObjectBase<TParent, TSelf, TItem>` + `ItemObjectBase<TCollection, TSelf>` |

Tie-breakers:

- Options that are only text (Picker) make a **simple** selector (`SelectorControlBase`), not
  a collection.
- If you would expose parts as public properties, it is a component. If you would only use
  them inside Core methods, it is a simple control that resolves its parts privately (Stepper).
- Behaviour on the root does not make a container a component. A container declares Core
  methods on its own root (`Popup.IsOpenCore`, `ScrollView.ScrollForwardCore`) and implements
  capability interfaces (`IRefreshableControlObject`). It is a component when the control names
  parts whose ids or shapes are fixed by the platform or library, not by the app.

## Rules for every kind

### Placement and naming

- Framework controls: `src/Bruinet.Maui/Controls/<Family>/Foo.tpl.cs`. Third-party sources:
  `src/Bruinet.Maui.<Source>/Controls/<Family>/Foo.tpl.cs`. The namespace follows the folder.
- `Foo.gen.cs` is generated. Never edit it; never commit a template change without
  regenerating.
- A `.tpl.cs` class is `partial` (the generator adds the other half); a plain `.cs` class is not.
- Two constructors: `(IMauiScope<TScope> scope, Locator locator)` and
  `(IMauiScope<TScope> scope, string locatorValue)`. Public on concrete classes, protected on
  abstract bases.

### Generator contract

Read [generator-contract.md](references/generator-contract.md) before writing a template: what is
matched, what is emitted, the attributes, shortcuts, and what fails generation. It is the one
statement of the contract; this skill does not repeat it.

### One unit of work per public call

- **R1.** A Core method reads and acts on its own element only. It never calls a public member
  of another control (a part, `Button(id)`, `Child<T>(id)`, an item).
- **R2.** A Core method never calls a `Run*` helper. It waits for its action's effect with
  `Confirm(read, done, timeoutMs)`, which never repeats the action. When the effect is not
  confirmed it throws `confirmation.Failure(Locator, "<action>", lastError => new ...(message,
  lastError))`: a replaced element is reported as `StaleElementException`, and an ignored
  action as the control's own exception with `lastError` as its `InnerException`. A scroll or
  other wait below the call takes `CallRemainingMs`, never a fixed timeout.
- **R3.** A part with behaviour of its own gets its own control class; a component forwards to
  it through `*Shortcut` methods.
- **R4.** A member that needs two parts (or a child and the root) is hand-written as plain calls
  in sequence, with a remark naming the parts.
- **R5.** A class is a `.tpl.cs` if it declares Core methods or shortcuts; otherwise a plain `.cs`
  ([generator-contract.md](references/generator-contract.md#generate)).
- **R6.** Public members take ids and values, never `Locator`s.

### Semantics

- **Null means skip**: `SetX(null)` does nothing, `AssertX(null)` passes.
- **Setters are idempotent**: already in the target state -> no action.
- **Actions confirm their effect**: after acting, wait for the read-back to change, and throw
  naming what did not happen, the before-and-after state, and the `Locator`.
- **Reads return null** when the platform does not publish the value. Never fabricate a value.
- **An action with no route throws** `RouteUnavailableException` naming the route the app would
  have to offer (a pattern, a verb, a handler). A plain `NotSupportedException` is retried until
  the budget runs out.
- **Every error message includes the `Locator`.**
- **No member the platform cannot answer.** Say so in a comment instead.

### Routes (full text: [routes.md](references/routes.md))

1. A UI Automation pattern through an `IMauiElement` member.
2. An existing bridge verb (`PerformGesture`, `ReadState`, `TryFindDeclared`).
3. A new verb only after AD-008's three tests, answered in writing.

No coordinates. No physical input on MAUI Windows (AD-005). No `Thread.Sleep`.

**Inside a control, `IMauiElement` and `Find*` are the implementation.** That is where they
belong: the control exists so nothing above it touches them.

### Documentation

Follow the comment policy in
[.github/instructions/csharp-comments.instructions.md](../../instructions/csharp-comments.instructions.md).
A control's route/evidence `<remarks>` is the one sanctioned `<remarks>` use: it records
measured, per-platform facts that cannot be re-derived from the code. Do not add `<remarks>`
that merely restate what a member does.

- `<summary>` on every public and protected member, Core methods included: the generated member's
  doc comes from it, so write it for that member's caller.
- `<remarks>` records the route per platform and the measured evidence ("Windows: no
  ExpandCollapse pattern; the app declares `Tap,GetState`. Android: touch; no state read, so
  IsExpanded is null").
- The class remarks name every app-side requirement: handler registration, bridge verbs,
  properties the app must set (`ShouldShowPlaybackControls`).

## Report

Finish with: files changed, the public API (from `Foo.gen.cs`; for a plain `.cs`, the declared
and inherited members), hand-written members
and why, the route per member per platform, test commands with exact pass/fail counts, and
anything left undone.
