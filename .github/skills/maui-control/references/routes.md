# Routes: how a control reaches the platform

A route is how one member reads or acts on the app. Choose per member and per platform,
in this order, and record the choice in the member's `<remarks>`.

## 1. A UI Automation pattern, through `IMauiElement`

The element in a Core method already exposes the patterns the platform publishes:

| Need | `IMauiElement` member |
| --- | --- |
| press | `Invoke()` |
| toggle / check | `Toggle()`, `SetChecked(bool)`, `Checked` |
| select | `Select()`, `SelectIndex(i)`, `SelectByText(text)`, `SelectedItemText` |
| text value | `Value`, `IsReadOnly`, `Text`, `AppendText(text)` |
| range | `RangeValue`, `RangeMinimum`, `RangeMaximum`, `RangeSmallChange`, `SetRangeValue(v)` |
| dropdown | `IsDropdownOpen`, `OpenDropdown()`, `CloseDropdown()`, `ReadDropdownItemTexts()` |
| list items | `ReadItemTexts()`, `PositionInSet`, `SizeOfSet` |
| scrolling | `ScrollContent(...)`, `ScrollTo(id)`, `ScrollToIndex(i)`, `ReadScrollPosition()`; `ScrollHelper` for container steps |
| name, state | `Name`, `Visible`, `Enabled`, `GetAttribute(name)` |

A property that answers `null` means the platform does not publish it. Pass the null on;
never substitute a guess.

**A pattern that runs but changes nothing in the app is not a route.** Example: the Scroll
pattern brings the next carousel card on screen, but MAUI's `Position` never updates, so the
bound label and the indicator stay on the old card. Measure the app-side effect (a status
label, a bound value), record the evidence in the member's remarks, and go on to step 2.

## 2. An existing bridge verb

Where UI Automation has no pattern for the action, the app publishes a gesture bridge
(AD-008): verbs declared per element in markup with `GestureAutomation.Verbs` from
`Bruinet.Maui.AppSupport`.

| Need | Member |
| --- | --- |
| a gesture (tap, swipe, pinch, long press) | `element.PerformGesture(MauiGesture.X)` |
| state the tree does not show | `Context.AppElement.TryFindDeclared(id)?.ReadState(property)` |
| date, time | `SetDate(date)`, `SetTime(time)` |
| Shell flyout, toolbar | `OpenFlyout()`, `CloseFlyout()`, `InvokeToolbarItem(id)` |

- Parse state read with `ReadState` using `CultureInfo.InvariantCulture`. A malformed
  value throws, naming the property and the text; it does not return null.
- Name the verbs the app must declare in the class `<remarks>`
  (the app declares `uia:GestureAutomation.Verbs="Tap,GetState"`).
- "The platform does not publish X" means neither the tree nor the bridge answers it. A value
  the tree lacks but the bridge's `GetState` can read is published; add the member.
- **Extending the bridge for an existing verb** - binding it for another view type (a
  `VerbBindings` row) or adding a state property to the `GetState` dispatcher in
  `samples/Bruinet.Maui.AppSupport/Uia/` - is not a new verb, but it is still an app-side
  change. Answer AD-008's three tests for it in the plan or PR (or, when there is none, in the
  control's class remarks), and run tier 2b.

## 3. A new bridge verb, only after AD-008's three tests

Answer each in writing, in the plan or the PR, before adding a verb:

1. **Is it blocked?** If a real pattern (`Invoke`, `Toggle`, `Value`, `SelectionItem`,
   `RangeValue`, `Scroll`, `ExpandCollapse`) already answers it, it stays there.
2. **Is the physical path the thing under test?** A test that asserts typing fires
   `TextChanged` must type; that test runs on the mobile head only.
3. **Does it stay a UI test?** Read what the user can see. If the assertion needs the view
   model, the coverage belongs in `Bruinet.Maui.Tests`.

A verb that fails any test is not added. The member throws instead (below).

Adding a verb also means: classify it (read, element action, app action), add it to the
sample app's markup, and run tier 2b (`--filter "Stage=Background"`).

## Never

- **Coordinates.** No clicking at a point, no offsets from a bounding box.
- **Physical input on MAUI Windows** (AD-005): no pointer movement, typing, clipboard or
  foreground. There is no switch to turn it on.
- **`Thread.Sleep` or `Task.Delay`** to let something happen. Wait for concrete state
  with a `Wait*` member, or with `Confirm` inside a Core method.
- **Reading the view model** to answer a UI question.

## When there is no route

Throw `RouteUnavailableException` naming the route the app would have to offer: the pattern,
the verb, the handler, the sink. It ends the call at once (`Poller.IsFatal`); a plain
`NotSupportedException` is retried until the budget runs out. Do not fall back to something
that works on one machine.

Do not add a member the platform cannot answer at all; say so in a comment where a reader
would look for it:

```csharp
// No SelectionMode or MultiSelectEnabled: neither platform publishes them to automation.
```

## Platform evidence

Routes differ per platform. Every claim in `<remarks>` is measured, not assumed:

- **Windows** (FlaUI, UI Automation): the control type, the patterns, the children, the
  AutomationId. Layouts with no automation peer (`ContentView`, `Border`) publish nothing
  until the app registers the Bruinet automation handlers from
  `samples/Bruinet.Maui.AppSupport`.
- **Android** (Appium, UiAutomator2): the class, `resource-id`, `content-desc`, what touch
  does. Physical input is allowed here, injected by Appium inside the device.

Write it as evidence: "Windows: no ExpandCollapse pattern; the app declares `Tap,GetState`.
Android: touch; no state read, so IsExpanded is null." Date a probe when behaviour depends
on a library version ("probed 2026-09-18, toolkit 10.0.0").
