---
name: html-control
description: Create or extend a Bruinet Html ControlObject - a simple control, a container, a component with named parts, or a collection with rows - for an HTML or Blazor element, in src/Bruinet.Html or src/Bruinet.Blazor. Use when asked to add, write or extend an Html or Blazor control object, container, collection or row, or when an Html UI test needs a control member that does not exist yet, or a control must work on the bUnit head as well as in a browser.
---

# Write an Html ControlObject

Html controls are generated, as MAUI's are: a template `Foo.tpl.cs` holds the constructors,
`*Core` methods and hand-written members, and `Bruinet.Generator` writes the public API into
`Foo.gen.cs`. The rules are MAUI's (`.docs/contracts/call-model.md`,
`.docs/contracts/control-object.md`), and so is the generator contract
([generator-contract.md](../maui-control/references/generator-contract.md)), with `IHtmlElement` as
the element type. Only the base classes and the platform probe differ.

## Choose the kind

| Kind | Base | Example |
| --- | --- | --- |
| Simple control | `ViewBase<TScope>` or a family base (`ClickableControlBase`, `FocusableControlBase`, `ToggleControlBase`, `RangeControlBase`, `SelectorControlBase`, `ScrollableControlBase`) | `TextInputControl<TScope>` |
| Container or component | `ContainerBase<TParent, TSelf>`; parts are controls declared on it | `TodoStatus<TParent>` in the Todo web tests |
| Frame | `FrameScope<TParent>` | `FramesPage.Inner` |
| Collection with rows | `CollectionObjectBase<TParent, TSelf, TItem>` plus `ItemObjectBase<TSelf>` | `RecyclingListPage.TodoRows` |

## Probe the element

Read it in the browser's DevTools, or with a short Playwright script: the tag, the ARIA role,
the `data-testid`, and the attributes the control reads (`disabled`, `aria-checked`, `value`,
`data-item-key`). Blazor inputs bind on `input`: set them with `Fill`, not `SendKeys`.

## Write the members

A simple control is a template: declare `protected virtual` `*Core` methods, element first, and
generate (from the Bruinet root):

```
tools\Scripts\CreateHtml.Bat
```

It regenerates every `.tpl.cs` under `src\Bruinet.Html\Controls` and
`src\Bruinet.Blazor\Controls`. A control with no `*Core` methods of its own (only
constructors) stays a plain `.cs`.

- **`Get*Core` / `Is*Core` give `GetX`/`WaitX`/`AssertX`**, with `[GenerateComparisons]` for
  `Contains`, `GreaterThan` and the rest; `[AbsenceTolerant]` when "absent" is an answer. An
  `Is*Core` takes `IHtmlElement?`: the generated `IsX()` passes `TryFindElement()`.
- **Every other `*Core` gives an action.** It acts once. When it changes state, confirm it inside
  the Core method with
  `ActAndConfirm("Fill", () => e.Fill(v), () => e.InputValue, x => x == v, $"'{v}'", timeoutMs)`.
  Never loop over the action.
- **Hand-written members** (an alias such as `Check()`, a member spanning parts) call the same
  `Run*` helpers as the generated ones: `RunGetWithElement`, `RunWaitWithElement(expected, ...)`,
  `RunAssertWithElement(expected, ...)`, their `Optional` forms, `RunDoWithElement`,
  `RunSetWithElement`. `Try*` members answer about now.
- **Readiness before acting** goes in `EnsureReadyForActionCore` (enabled, for example), and
  throws `ElementNotReadyException`.
- **Every public member takes `int? timeoutMs = null`.** A wait below the call (a scroll) takes
  `CallRemainingMs`.
- **Containers:** override `ProbeContentReadiness(root)` for a content check (a spinner gone),
  and set `AsksParent => false` for a scope shown over its parent.
- **No catch-alls, no sleeps.** A failure is an answer or a signal (`StaleElementException`,
  `ElementNotReadyException`, `AppUnavailableException`).
- **Read the DOM, so the control works on both heads.** Every control runs in a browser and on
  the bUnit head (`docs/guides/bunit.md`, AD-030). Text, attributes, `InputValue`, `IsChecked`,
  `Enabled`, `Visible` and the actions work on both. `Evaluate`, `GetCssValue`, `Location`/`Size`/`Rect`,
  `Hover`, `ClickAt`, `Swipe` and frames work only in a browser and end the call on bUnit with
  `BrowserOnlyException`. A member that needs one of them is browser-only: say so in its
  `<summary>`, and prefer an attribute the app renders (`aria-*`, `data-*`) when one says the same.

## Prove it

Add a unit test in `tests/Bruinet.Html.Tests` against a mocked `IHtmlElement`. A mocked
element must show the effect of an action (a `Callback` that sets the value), or a confirming
member fails, and that is the point. Then add a UI test on the Blazor sample with the
`html-ui-test` skill, and run it on both heads: `$env:BRUINET_HEAD = "bunit"` first, then the
browser. A control that fails only on bUnit needs a driver member in `src/Bruinet.Html.Bunit`
(with its test in `tests/Bruinet.Html.Bunit.Tests/MemberTests.cs`), or is browser-only and
says so.
